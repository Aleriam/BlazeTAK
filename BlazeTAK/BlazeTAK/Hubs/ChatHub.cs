using System.Collections.Concurrent;
using BlazeTAK.Client.Models;
using BlazeTAK.Data;
using BlazeTAK.Data.Models;
using BlazeTAK.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Hubs;

//all chat communication goes through this hub.
//user is identified by his VirtUser.ID (profile); chat data lives only in the
//MessageService (in memory), only callsigns/directories are fetched via EF.
//
//SERVER -> CLIENT events:
//  - MessageReceived(chatMessage)      new chat message
//  - MessagesDeleted(chatMessage)      a message was deleted (content match)
//  - ChatsUpdated()                    chat list/membership changed - client should reload
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.GroupLeader},{AppRoles.User}")]
public class ChatHub : Hub
{
    //virtUserID -> active connection ids (for ChatsUpdated broadcasts)
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> Connections = new();

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserInfoService _userInfoService;
    private readonly IMessageService _messageService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IUserInfoService userInfoService,
        IMessageService messageService,
        ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _userInfoService = userInfoService;
        _messageService = messageService;
        _logger = logger;
    }

    //------------- connection lifecycle -------------

    public override async Task OnConnectedAsync()
    {
        var profile = await GetCurrentUserProfileAsync();
        if (profile == null)
        {
            Context.Abort();
            return;
        }

        if (Context.User?.IsInRole(AppRoles.Admin) == true)
            _messageService.SetAdmin(profile.ID);

        //subscribe the connection to every chat the user is a member of
        var chatIDs = await _messageService.GetChatsAsync(profile.ID);
        foreach (var chatID in chatIDs.Select(c => c.ID))
            await Groups.AddToGroupAsync(Context.ConnectionId, FormatChatGroup(chatID));

        //connection map for events
        Connections.AddOrUpdate(profile.ID, _ => new HashSet<string> { Context.ConnectionId },
            (_, set) => { lock (set) set.Add(Context.ConnectionId); return set; });

        _logger.LogInformation("Chat: user {User} connected, subscribed to {Count} chats.",
            profile.ID, chatIDs.Count);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var profile = await GetCurrentUserProfileAsync();
        if (profile != null)
        {
            _messageService.UnsetAdmin(profile.ID);
            if (Connections.TryGetValue(profile.ID, out var set))
            {
                lock (set) set.Remove(Context.ConnectionId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    //------------- hub methods (called by Blazor WASM client) -------------

    //identities (self/unit/groups) - view switcher and sender selection
    public Task<List<ChannelDto>> GetMyIdentities()
        => CallWithProfileAsync(async profile => await _messageService.GetChannelsAsync(profile.ID));

    //chats where the selected identity is a member
    public Task<List<ChatSummaryDto>> GetMyChats(Guid identityID)
        => CallWithProfileAsync(async profile => await _messageService.GetChatsForIdentityAsync(profile.ID, identityID));

    //chat with history and the identities allowed to send in it
    public Task<ChatDto> GetChat(Guid chatID)
        => CallWithProfileAsync(async profile => await _messageService.GetChatAsync(chatID, profile.ID));

    //directory for the "new chat" member picker: all users, units and groups (all VirtUsers)
    public async Task<List<DirectoryEntryDto>> GetDirectory()
    {
        var result = new List<DirectoryEntryDto>();

        var users = await _dbContext.VirtUsers.OfType<User>().AsNoTracking().ToListAsync();
        result.AddRange(users.Select(v => new DirectoryEntryDto
        {
            ID = v.ID, Name = v.Name, CallSign = v.CallSign,
            CallSignNumber = v.CallSignNumber, Kind = ChannelKind.Personal
        }));

        var units = await _dbContext.VirtUsers.OfType<Unit>().AsNoTracking().ToListAsync();
        result.AddRange(units.Select(v => new DirectoryEntryDto
        {
            ID = v.ID, Name = v.Name, CallSign = v.CallSign,
            CallSignNumber = v.CallSignNumber, Kind = ChannelKind.Unit
        }));

        var groups = await _dbContext.VirtUsers.OfType<Group>().AsNoTracking().ToListAsync();
        result.AddRange(groups.Select(v => new DirectoryEntryDto
        {
            ID = v.ID, Name = v.Name, CallSign = v.CallSign,
            CallSignNumber = v.CallSignNumber, Kind = ChannelKind.Group
        }));

        return result.OrderBy(e => e.CallSign).ToList();
    }

    //send a message on behalf of an identity; sender callsign resolved server-side
    public async Task<MessageChatDto> SendMessage(Guid chatID, Guid senderIdentityID, string message)
    {
        var profile = await GetCurrentUserProfileAsync()
            ?? throw new HubException("No profile for current user.");
        var dto = await _messageService.SendMessageAsync(profile.ID, chatID, senderIdentityID, message);
        await Clients.Group(FormatChatGroup(chatID)).SendAsync("MessageReceived", dto);
        return dto;
    }

    //delete a message; acting through the sender identity (or an admin) only
    public async Task DeleteMessage(Guid chatID, MessageChatDto message)
    {
        var profile = await GetCurrentUserProfileAsync()
            ?? throw new HubException("No profile for current user.");
        await _messageService.DeleteMessageAsync(profile.ID, chatID, message);
        await Clients.Group(FormatChatGroup(chatID)).SendAsync("MessagesDeleted", message);
    }

    //create a new chat; members = virtUsers, the creator is added automatically
    public async Task<Guid> CreateChat(string name, List<Guid> memberIDs)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        var chatID = await _messageService.CreateChatAsync(profile.ID, name, memberIDs ?? new List<Guid>());

        //put the creator connection into the chat group right away + notify everyone affected
        await Groups.AddToGroupAsync(Context.ConnectionId, FormatChatGroup(chatID));
        await BroadcastChatsUpdatedAsync(chatID);
        return chatID;
    }

    //membership can change later
    public async Task AddChatMember(Guid chatID, Guid virtUserID)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        await _messageService.AddVirtUserToChatAsync(profile.ID, chatID, virtUserID);
        await BroadcastChatsUpdatedAsync(chatID);
    }

    public async Task RemoveChatMember(Guid chatID, Guid virtUserID)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        await _messageService.RemoveVirtUserFromChatAsync(profile.ID, chatID, virtUserID);
        await BroadcastChatsUpdatedAsync(chatID);
    }

    //creator/admin only
    public async Task DeleteChat(Guid chatID)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        await _messageService.DeleteChatAsync(profile.ID, chatID);
        await Clients.Group(FormatChatGroup(chatID)).SendAsync("ChatDeleted", chatID);
        await BroadcastChatsUpdatedAsync(chatID);
    }

    //creator/admin only
    public async Task RenameChat(Guid chatID, string newName)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        await _messageService.RenameChatAsync(profile.ID, chatID, newName);
        await BroadcastChatsUpdatedAsync(chatID);
    }

    //client calls this after ChatsUpdated for each new chat (or after reconnect) so that
    //its current connection is placed into the SignalR chat group
    public async Task SubscribeToChat(Guid chatID)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        _ = await _messageService.GetChatAsync(chatID, profile.ID); //throws when no access
        await Groups.AddToGroupAsync(Context.ConnectionId, FormatChatGroup(chatID));
    }

    public async Task LeaveChat(Guid chatID)
    {
        var profile = await GetCurrentUserProfileAsync() ?? throw new HubException("No profile for current user.");
        await _messageService.LeaveChatAsync(profile.ID, chatID);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FormatChatGroup(chatID));
        await BroadcastChatsUpdatedAsync(chatID);
    }

    //------------- helpers -------------

    private async Task<User?> GetCurrentUserProfileAsync()
    {
        var appUser = await _userManager.GetUserAsync(Context.User!);
        return appUser == null ? null : await _userInfoService.GetCurrentUserAsync(appUser, _dbContext);
    }

    private async Task<T> CallWithProfileAsync<T>(Func<User, Task<T>> action)
    {
        var profile = await GetCurrentUserProfileAsync()
            ?? throw new HubException("No profile for current user.");
        return await action(profile);
    }

    private static string FormatChatGroup(Guid chatID) => $"chat-{chatID}";

    //broadcast ChatsUpdated to every (connected) user-connection involved in the chat
    private async Task BroadcastChatsUpdatedAsync(Guid chatID)
    {
        var memberUsers = await _messageService.GetChatMemberVirtUserIDsAsync(chatID);
        var connectionIDs = memberUsers
            .SelectMany(id => Connections.TryGetValue(id, out var set) ? set : (IEnumerable<string>)Array.Empty<string>())
            .ToList();
        if (connectionIDs.Count > 0)
            await Clients.Clients(connectionIDs).SendAsync("ChatsUpdated");
        _logger.LogDebug("ChatsUpdated broadcast for chat {Chat} to {Conns} connections.", chatID, connectionIDs.Count);
    }
}
