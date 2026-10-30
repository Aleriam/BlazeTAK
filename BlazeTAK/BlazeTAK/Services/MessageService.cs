using System.Collections.Concurrent;
using BlazeTAK.Client.Models;
using BlazeTAK.Data;
using BlazeTAK.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Services;

//singleton - everything (chats, membership, messages) lives in memory only, nothing is persisted.
//
//MODEL
//  - chat: an ad-hoc conversation with mixed member entities: users, units and groups
//    (no "unit chat"/"group chat" typing - a chat is just a chat, members decide).
//  - identity: a VirtUser entity a user can act on behalf of: himself, his unit, groups he
//    leads / is member of. The client picks one identity; it filters which chats are shown
//    (only chats the identity is a member of) and sets the sender of outgoing messages.
//  - shared identities: many clients may act through the same unit/group identity -
//    messages appear as one VirtUser on the server and for everyone.
//  - receive = strict per-identity: only chats where the selected identity is a member.
//  - management: creator and admins manage members/chat; any member can leave.
public class MessageService : IMessageService
{
    private sealed class Chat
    {
        public required Guid ID { get; init; }
        public required string Name { get; set; }
        //participant entity ids: user VirtUser ids, unit ids, group ids
        public readonly HashSet<Guid> Members = new();
        public readonly object Lock = new();
        public readonly List<MessageChatDto> Messages = new();
        public Guid CreatorID { get; set; }
    }

    private const int MaxMessagesPerChat = 500;

    private readonly ConcurrentDictionary<Guid, Chat> _chats = new();
    private readonly ConcurrentDictionary<Guid, byte> _admins = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageService> _logger;

    public MessageService(IServiceScopeFactory scopeFactory, ILogger<MessageService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    //------------- identities (view switcher / sender selection) -------------

    //identities a user may act on behalf of: himself, his unit, groups he leads/is member of
    public async Task<List<ChannelDto>> GetChannelsAsync(Guid virtUserID, CancellationToken cancellationToken = default)
    {
        var topology = await LoadUserTopologyAsync(virtUserID, cancellationToken)
            ?? throw new UnauthorizedAccessException($"No profile for user {virtUserID}.");
        var isAdmin = await IsAdminAsync(virtUserID, cancellationToken);

        var result = new List<ChannelDto>
        {
            new() //personal identity = himself
            {
                ID = topology.Profile.ID,
                Name = topology.Profile.Name,
                CallSign = topology.Profile.CallSign,
                CallSignNumber = topology.Profile.CallSignNumber,
                Kind = ChannelKind.Personal,
                CanSend = true
            }
        };

        if (topology.Unit != null) //unit identity - membership based
        {
            result.Add(new ChannelDto
            {
                ID = topology.Unit.ID,
                Name = topology.Unit.Name,
                CallSign = topology.Unit.CallSign,
                CallSignNumber = topology.Unit.CallSignNumber,
                Kind = ChannelKind.Unit,
                CanSend = true
            });
        }

        foreach (var group in topology.Groups) //group identity - only leaders/admin can send as group
        {
            var isLeader = group.LeaderId == topology.Profile.ID;
            result.Add(new ChannelDto
            {
                ID = group.ID,
                Name = group.Name,
                CallSign = group.CallSign,
                CallSignNumber = group.CallSignNumber,
                Kind = ChannelKind.Group,
                CanSend = isLeader || isAdmin
            });
        }

        return result;
    }

    //------------- chats -------------

    //all chats where ANY identity of the user is a member (union - used for hub subscriptions)
    public async Task<List<ChatSummaryDto>> GetChatsAsync(Guid virtUserID, CancellationToken cancellationToken = default)
    {
        var topology = await LoadUserTopologyAsync(virtUserID, cancellationToken)
            ?? throw new UnauthorizedAccessException($"No profile for user {virtUserID}.");

        var chats = _chats.Values
            .Where(chat => topology.Ids.Any(chat.Members.Contains))
            .Select(ToSummary)
            .OrderBy(c => c.Name)
            .ToList();

        //chats shared with the user's entities the topology resolvers may miss (defensive)
        foreach (var chat in _chats.Values)
            if (chat.Members.Contains(virtUserID) && !chats.Any(c => c.ID == chat.ID))
                chats.Add(ToSummary(chat));

        return chats;
    }

    //chats filtered by ONE identity: only chats where this identity is directly a member
    public async Task<List<ChatSummaryDto>> GetChatsForIdentityAsync(Guid virtUserID, Guid identityID, CancellationToken ct = default)
    {
        var topology = await LoadUserTopologyAsync(virtUserID, ct)
            ?? throw new UnauthorizedAccessException("No profile for user.");
        if (!topology.Ids.Contains(identityID))
            throw new UnauthorizedAccessException("This identity does not belong to the user.");

        return _chats.Values
            .Where(chat => chat.Members.Contains(identityID))
            .Select(ToSummary)
            .OrderBy(c => c.Name)
            .ToList();
    }

    public async Task<ChatDto> GetChatAsync(Guid chatID, Guid requesterVirtUserID, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);

        var topology = await LoadUserTopologyAsync(requesterVirtUserID, cancellationToken)
            ?? throw new UnauthorizedAccessException("No profile for requester.");
        var isAdmin = await IsAdminAsync(requesterVirtUserID, cancellationToken);

        //any identity of the requester being a member gives access
        if (!topology.Ids.Any(chat.Members.Contains) && !isAdmin)
            throw new UnauthorizedAccessException("Not allowed to access this chat.");

        //identities the requester may send on behalf of in this chat
        var allowed = new List<Guid>();
        if (chat.Members.Contains(topology.Profile.ID) || isAdmin)
            allowed.Add(topology.Profile.ID);
        if (topology.Unit != null && chat.Members.Contains(topology.Unit.ID))
            allowed.Add(topology.Unit.ID);
        foreach (var group in topology.Groups)
        {
            if (chat.Members.Contains(group.ID) && (group.LeaderId == topology.Profile.ID || isAdmin))
                allowed.Add(group.ID);
        }

        lock (chat.Lock)
        {
            return new ChatDto
            {
                ID = chat.ID,
                Name = chat.Name,
                MemberIDs = chat.Members.ToList(),
                AllowedIdentityIDs = allowed,
                Messages = chat.Messages.OrderBy(m => m.SentUtc).ToList()
            };
        }
    }

    //------------- messaging -------------

    //sends message on behalf of senderIdentityID (self/unit/group); callsign resolved from identity.
    //several clients may send through the same shared identity (unit/group).
    public async Task<MessageChatDto> SendMessageAsync(Guid senderVirtUserID, Guid chatID, Guid senderIdentityID, string message, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is empty.", nameof(message));

        var topology = await LoadUserTopologyAsync(senderVirtUserID, cancellationToken)
            ?? throw new UnauthorizedAccessException("No profile for sender.");
        var isAdmin = await IsAdminAsync(senderVirtUserID, cancellationToken);
        if (IsAllowedIdentity(topology, chat, senderIdentityID, isAdmin) is null)
            throw new UnauthorizedAccessException("This identity cannot send in this chat.");

        if (!chat.Members.Contains(senderIdentityID) && !isAdmin)
            throw new UnauthorizedAccessException("This identity is not a member of this chat.");

        var dto = new MessageChatDto
        {
            ChannelID = chatID,
            SenderID = senderIdentityID, //identity on whose behalf the message is sent
            SenderCallSign = await ResolveCallsignAsync(senderIdentityID, cancellationToken),
            Message = message.Trim(),
            SentUtc = DateTime.UtcNow
        };

        lock (chat.Lock)
        {
            chat.Messages.Add(dto);
            if (chat.Messages.Count > MaxMessagesPerChat)
                chat.Messages.RemoveRange(0, chat.Messages.Count - MaxMessagesPerChat);
        }

        _logger.LogDebug("Identity {Identity} sent message to chat {Chat} on behalf of user {User}.",
            senderIdentityID, chatID, senderVirtUserID);
        return dto;
    }

    //sender-side deletion: acting through an identity whose id equals the message sender
    //(or an admin) removes all messages with that content in the chat
    public async Task DeleteMessageAsync(Guid requesterVirtUserID, Guid chatID, MessageChatDto message, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);

        var topology = await LoadUserTopologyAsync(requesterVirtUserID, cancellationToken)
            ?? throw new UnauthorizedAccessException("No profile for requester.");
        var isAdmin = await IsAdminAsync(requesterVirtUserID, cancellationToken);

        if (IsAllowedIdentity(topology, chat, message.SenderID, isAdmin) is null)
            throw new UnauthorizedAccessException("Not allowed - only the sender identity (or an admin) may delete.");

        var removed = chat.Messages.RemoveAll(m => MessageMatches(m, message));
        if (removed == 0)
            throw new KeyNotFoundException("Message not found in this chat.");

        _logger.LogDebug("User {User} deleted {Count} message(s) from chat {Chat}.", requesterVirtUserID, removed, chatID);
    }

    private static bool MessageMatches(MessageChatDto a, MessageChatDto b)
        => a.SenderID == b.SenderID
           && a.Message == b.Message
           && a.SentUtc == b.SentUtc;

    //checks the identity belongs to the user's identity set AND the user may send through it
    //in this chat (member, group leader/admin); returns the identity else null
    private static Guid? IsAllowedIdentity((User Profile, HashSet<Guid> Ids, Unit? Unit, List<Group> Groups) topology,
        Chat chat, Guid identityID, bool isAdmin)
    {
        if (!topology.Ids.Contains(identityID)) return null;
        if (!chat.Members.Contains(identityID) && !isAdmin) return null;

        if (identityID == topology.Profile.ID) return identityID;
        if (topology.Unit != null && identityID == topology.Unit.ID) return identityID;

        var group = topology.Groups.FirstOrDefault(g => g.ID == identityID);
        if (group != null && (group.LeaderId == topology.Profile.ID || isAdmin)) return identityID;
        return null;
    }

    //------------- chat management -------------

    //creates an ad-hoc chat; members can be ANY VirtUser entities (users/units/groups), mixed;
    //creator is always added as himself; creator and admins manage the chat
    public Task<Guid> CreateChatAsync(Guid creatorVirtUserID, string name, IEnumerable<Guid> memberIDs, CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            ID = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? "Chat" : name.Trim(),
            CreatorID = creatorVirtUserID
        };
        lock (chat.Lock)
        {
            chat.Members.Add(creatorVirtUserID); //creator participates personally
            foreach (var member in memberIDs) chat.Members.Add(member);
        }
        _chats[chat.ID] = chat;
        _logger.LogInformation("Chat '{Name}' ({Chat}) created by {User} with {Count} member entities.",
            chat.Name, chat.ID, creatorVirtUserID, chat.Members.Count);
        return Task.FromResult(chat.ID);
    }

    public async Task AddVirtUserToChatAsync(Guid actorVirtUserID, Guid chatID, Guid memberEntityID, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);
        await AssertManagerAsync(actorVirtUserID, chat, cancellationToken, "add people to");
        lock (chat.Lock) chat.Members.Add(memberEntityID);
    }

    public async Task RemoveVirtUserFromChatAsync(Guid actorVirtUserID, Guid chatID, Guid memberEntityID, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);
        await AssertManagerAsync(actorVirtUserID, chat, cancellationToken, "remove people from");
        lock (chat.Lock) chat.Members.Remove(memberEntityID);
    }

    public Task LeaveChatAsync(Guid virtUserID, Guid chatID, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);
        lock (chat.Lock) chat.Members.Remove(virtUserID);
        _logger.LogInformation("User {User} left chat {Chat}.", virtUserID, chatID);
        return Task.CompletedTask;
    }

    public async Task DeleteChatAsync(Guid actorVirtUserID, Guid chatID, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);
        await AssertManagerAsync(actorVirtUserID, chat, cancellationToken, "delete");
        _chats.TryRemove(chatID, out _);
        _logger.LogInformation("Chat {Chat} deleted by {User}.", chatID, actorVirtUserID);
    }

    public async Task RenameChatAsync(Guid actorVirtUserID, Guid chatID, string newName, CancellationToken cancellationToken = default)
    {
        var chat = GetChatRequired(chatID);
        await AssertManagerAsync(actorVirtUserID, chat, cancellationToken, "rename");
        if (!string.IsNullOrWhiteSpace(newName))
            lock (chat.Lock) chat.Name = newName.Trim();
    }

    //------------- helpers for hub events -------------

    //resolve member entities to concrete user virtuser ids (for hub event broadcast)
    public async Task<List<Guid>> GetChatMemberVirtUserIDsAsync(Guid chatID, CancellationToken cancellationToken = default)
    {
        var chat = _chats.GetValueOrDefault(chatID);
        if (chat == null) return new List<Guid>();

        var entityIDs = chat.Members.ToList();
        if (entityIDs.Count == 0) return new List<Guid>();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var result = new HashSet<Guid>();

        //direct user members
        var directUsers = await dbContext.Profiles.AsNoTracking()
            .Where(p => entityIDs.Contains(p.ID))
            .Select(p => p.ID).ToListAsync(cancellationToken);
        foreach (var id in directUsers) result.Add(id);

        //members via units
        var usersInUnits = await dbContext.Profiles.AsNoTracking()
            .Where(u => u.UnitId != null && entityIDs.Contains(u.UnitId.Value))
            .Select(u => u.ID).ToListAsync(cancellationToken);
        foreach (var id in usersInUnits) result.Add(id);

        //members via groups (direct or via their units)
        var usersInGroups = await dbContext.Profiles.AsNoTracking()
            .Where(u => (u.GroupId != null && entityIDs.Contains(u.GroupId.Value))
                        || (u.UnitId != null && u.Unit!.GroupId != null && entityIDs.Contains(u.Unit!.GroupId.Value)))
            .Select(u => u.ID).ToListAsync(cancellationToken);
        foreach (var id in usersInGroups) result.Add(id);

        return result.ToList();
    }

    public void SetAdmin(Guid virtUserID) => _admins[virtUserID] = 1;
    public void UnsetAdmin(Guid virtUserID) => _admins.TryRemove(virtUserID, out _);

    //------------- internals -------------

    private Chat GetChatRequired(Guid chatID)
        => _chats.GetValueOrDefault(chatID) ?? throw new KeyNotFoundException("Chat not found.");

    private static ChatSummaryDto ToSummary(Chat chat) => new() { ID = chat.ID, Name = chat.Name };

    //profile + all identity ids of the user (self, unit, groups - via membership or leadership)
    private async Task<(User Profile, HashSet<Guid> Ids, Unit? Unit, List<Group> Groups)?> LoadUserTopologyAsync(Guid virtUserID, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var profile = await dbContext.Profiles.AsNoTracking()
            .Include(p => p.Unit).ThenInclude(u => u!.Group)
            .Include(p => p.Group) //direct group membership (member without a unit)
            .FirstOrDefaultAsync(p => p.ID == virtUserID, ct);
        if (profile == null) return null;

        var ids = new HashSet<Guid> { profile.ID };

        var unit = profile.Unit;
        if (unit != null) ids.Add(unit.ID);

        var groups = new List<Group>();
        var viaProfileGroup = profile.Group ?? unit?.Group;
        if (viaProfileGroup != null) groups.Add(viaProfileGroup);

        var ledGroups = await dbContext.Groups.AsNoTracking()
            .Where(g => g.LeaderId == profile.ID)
            .ToListAsync(ct);
        groups.AddRange(ledGroups.Where(g => groups.All(x => x.ID != g.ID)));

        foreach (var group in groups) ids.Add(group.ID);

        return (profile, ids, unit, groups);
    }

    private async Task<string> ResolveCallsignAsync(Guid identityID, CancellationToken ct)
    {
        //identities (users, units, groups) are all VirtUsers - one lookup covers everything
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = await dbContext.VirtUsers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.ID == identityID, ct);

        return entity == null ? "UNKNOWN" : FormatCallSign(entity.CallSign, entity.CallSignNumber);
    }

    private static string FormatCallSign(string callSign, int number)
        => string.IsNullOrWhiteSpace(callSign) ? $"#{number}" : $"{callSign}-{number}";

    //creator/admin can manage the chat
    private async Task AssertManagerAsync(Guid actorVirtUserID, Chat chat, CancellationToken ct, string action)
    {
        if (chat.CreatorID == actorVirtUserID || await IsAdminAsync(actorVirtUserID, ct)) return;
        throw new UnauthorizedAccessException($"Only the chat creator or an admin can {action} this chat.");
    }

    private async Task<bool> IsAdminAsync(Guid virtUserID, CancellationToken ct)
    {
        if (_admins.ContainsKey(virtUserID)) return true;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await dbContext.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.ID == virtUserID, ct);
        if (profile == null) return false;

        var appUser = await dbContext.Users.FindAsync([profile.ApplicationUserId], ct);
        if (appUser == null) return false;

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.IsInRoleAsync(appUser, AppRoles.Admin))
        {
            SetAdmin(virtUserID);
            return true;
        }
        return false;
    }
}
