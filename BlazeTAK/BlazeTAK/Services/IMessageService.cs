using BlazeTAK.Client.Models;

namespace BlazeTAK.Services;

//in-memory chat service. Chat data (membership, messages) lives only in memory;
//only identity/user data (callsigns, membership) is fetched via EF.
//
//MODEL
//  - identity: a VirtUser entity a user can act on behalf of: himself, his unit, groups he
//    leads / is member of. The client picks one identity; it filters which chats are shown
//    (only chats the identity is a member of) and sets the sender of outgoing messages.
//    Several clients may act through the same shared identity (unit/group) - messages
//    appear as one VirtUser on the server and for everyone.
//  - receive = strict per-identity: only chats where the selected identity is a member.
public interface IMessageService
{
    //------- identities -------

    //self + unit + groups (leader/member); CanSend flags for the switcher
    Task<List<ChannelDto>> GetChannelsAsync(Guid virtUserID, CancellationToken cancellationToken = default);

    //------- chats -------

    //all chats where ANY identity of the user is a member (for hub subscriptions)
    Task<List<ChatSummaryDto>> GetChatsAsync(Guid virtUserID, CancellationToken cancellationToken = default);

    //only chats where the given identity is directly a member of
    Task<List<ChatSummaryDto>> GetChatsForIdentityAsync(Guid virtUserID, Guid identityID, CancellationToken cancellationToken = default);

    //chat with history; AllowedIdentityIDs = identities that may send in this chat
    Task<ChatDto> GetChatAsync(Guid chatID, Guid requesterVirtUserID, CancellationToken cancellationToken = default);

    //------- messaging -------

    //send on behalf of senderIdentityID (self/unit/group); sender callsign = identity callsign
    Task<MessageChatDto> SendMessageAsync(Guid senderVirtUserID, Guid chatID, Guid senderIdentityID, string message, CancellationToken cancellationToken = default);

    //sender-side deletion: acting through an identity whose id equals the message sender
    //(or an admin) removes all messages with that content in the chat
    Task DeleteMessageAsync(Guid requesterVirtUserID, Guid chatID, MessageChatDto message, CancellationToken cancellationToken = default);

    //------- chat management -------

    //members = any VirtUser entities (users/units/groups, mixed); creator is added as himself
    Task<Guid> CreateChatAsync(Guid creatorVirtUserID, string name, IEnumerable<Guid> memberIDs, CancellationToken cancellationToken = default);

    //creator/admin only
    Task AddVirtUserToChatAsync(Guid actorVirtUserID, Guid chatID, Guid memberEntityID, CancellationToken cancellationToken = default);

    //creator/admin only (leaving is done via LeaveChatAsync)
    Task RemoveVirtUserFromChatAsync(Guid actorVirtUserID, Guid chatID, Guid memberEntityID, CancellationToken cancellationToken = default);

    //self removal from a chat
    Task LeaveChatAsync(Guid virtUserID, Guid chatID, CancellationToken cancellationToken = default);

    //creator/admin only
    Task DeleteChatAsync(Guid actorVirtUserID, Guid chatID, CancellationToken cancellationToken = default);

    //creator/admin only
    Task RenameChatAsync(Guid actorVirtUserID, Guid chatID, string newName, CancellationToken cancellationToken = default);

    //------- helpers for hub events -------

    //member entities (users/units/groups) resolved to concrete user virtuser ids
    Task<List<Guid>> GetChatMemberVirtUserIDsAsync(Guid chatID, CancellationToken cancellationToken = default);

    //role overrides, called by the hub/admin seed
    void SetAdmin(Guid virtUserID);
    void UnsetAdmin(Guid virtUserID);
}
