using BlazeTAK.Client.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazeTAK.Client.Services;

//client side chat service - wraps one SignalR hub connection to /hubs/chat,
//used by the blazor wasm gui (Chat.razor). All chat communication goes through the hub.
//stable by design:
//  - hub methods can be called from any thread; StartAsync is idempotent and auto-resumes
//  - events fire on the SignalR worker thread - the gui must InvokeAsync/StateHasChanged itself
//  - re-subscription after reconnect is the page's responsibility (OnReconnected event)
public class ChatService : IAsyncDisposable
{
    //hub connection, used from blazor page gui
    public HubConnection Connection { get; }

    //server -> client events
    public event Action<MessageChatDto>? MessageReceived;
    public event Action<MessageChatDto>? MessagesDeleted;   //a message was deleted (content match)
    public event Action? ChatsUpdated;                     //chat list/membership changed
    public event Action<Guid>? ChatDeleted;                //a chat was deleted (creator/admin)
    public event Action? Reconnected;                      //connection restored - resubscribe chat groups

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private bool _started;

    public ChatService(NavigationManager navigation)
    {
        Connection = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri("/hubs/chat")) //cookie auth is sent with handshake
            .WithAutomaticReconnect() //default policy then stops - we also listen to Reconnected
            .Build();

        Connection.On<MessageChatDto>("MessageReceived", dto => MessageReceived?.Invoke(dto));
        Connection.On<MessageChatDto>("MessagesDeleted", dto => MessagesDeleted?.Invoke(dto));
        Connection.On<Guid>("ChatDeleted", chatID => ChatDeleted?.Invoke(chatID));
        Connection.On("ChatsUpdated", () => ChatsUpdated?.Invoke());
        Connection.Reconnected += _ => { Reconnected?.Invoke(); return Task.CompletedTask; };
    }

    public HubConnectionState State => Connection.State;

    //idempotent: concurrent callers are serialized; safe to call repeatedly
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            switch (Connection.State)
            {
                case HubConnectionState.Connected:
                case HubConnectionState.Connecting:
                    _started = true;
                    return;
                case HubConnectionState.Reconnecting:
                    _started = true;
                    return; //automatic reconnect in progress - nothing to do
            }

            if (_started && Connection.State == HubConnectionState.Disconnected)
            {
                //default reconnect policy exhausted - manual restart
                await Connection.StartAsync(cancellationToken);
                return;
            }

            await Connection.StartAsync(cancellationToken);
            _started = true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    //------- hub invokes -------

    //identities (self/unit/groups) for the view switcher and sender selection
    public Task<List<ChannelDto>> GetMyIdentitiesAsync(CancellationToken cancellationToken = default)
        => Connection.InvokeAsync<List<ChannelDto>>("GetMyIdentities", cancellationToken);

    //chats where the selected identity is a member
    public Task<List<ChatSummaryDto>> GetMyChatsAsync(Guid identityID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync<List<ChatSummaryDto>>("GetMyChats", identityID, cancellationToken);

    //chat with history; AllowedIdentityIDs = identities allowed to send there
    public Task<ChatDto> GetChatAsync(Guid chatID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync<ChatDto>("GetChat", chatID, cancellationToken);

    public Task SendAsync(Guid chatID, Guid senderIdentityID, string message, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync<MessageChatDto>("SendMessage", chatID, senderIdentityID, message, cancellationToken);

    //delete a message; acting through the sender identity (or an admin) only
    public Task DeleteMessageAsync(Guid chatID, MessageChatDto message, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("DeleteMessage", chatID, message, cancellationToken);

    //put the current connection into the chat's signalr group (after ChatsUpdated/create/reconnect)
    public Task SubscribeToChatAsync(Guid chatID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("SubscribeToChat", chatID, cancellationToken);

    //subscribe to only the chats not yet subscribed to this session
    public async Task SubscribeToNewChatsAsync(IEnumerable<Guid> chatIDs)
    {
        foreach (var chatID in chatIDs)
        {
            try
            {
                await SubscribeToChatAsync(chatID); //server-side idempotent, validates access
            }
            catch (Exception)
            {
                //no access (anymore) - ignore
            }
        }
    }

    //all users/units/groups for the "new chat" member picker
    public Task<List<DirectoryEntryDto>> GetDirectoryAsync(CancellationToken cancellationToken = default)
        => Connection.InvokeAsync<List<DirectoryEntryDto>>("GetDirectory", cancellationToken);

    //create a new chat; members = any VirtUser entities (users/units/groups, mixed)
    public Task<Guid> CreateChatAsync(string name, List<Guid> memberIDs, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync<Guid>("CreateChat", name, memberIDs, cancellationToken);

    //creator/admin only
    public Task AddChatMemberAsync(Guid chatID, Guid virtUserID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("AddChatMember", chatID, virtUserID, cancellationToken);

    //creator/admin only
    public Task RemoveChatMemberAsync(Guid chatID, Guid virtUserID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("RemoveChatMember", chatID, virtUserID, cancellationToken);

    //creator/admin only
    public Task DeleteChatAsync(Guid chatID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("DeleteChat", chatID, cancellationToken);

    //creator/admin only
    public Task RenameChatAsync(Guid chatID, string newName, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("RenameChat", chatID, newName, cancellationToken);

    public Task LeaveChatAsync(Guid chatID, CancellationToken cancellationToken = default)
        => Connection.InvokeAsync("LeaveChat", chatID, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _startLock.Dispose();
        await Connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
