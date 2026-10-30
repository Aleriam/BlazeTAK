namespace BlazeTAK.Client.Models;

//sender identity kind
public enum ChannelKind
{
    Personal,
    Unit,
    Group
}

//an identity the user may act on behalf of: himself, his unit, a group he leads/is member of.
//the client picks one; it filters the chat list and defines the sender ID of outgoing messages.
public class ChannelDto
{
    public Guid ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public int CallSignNumber { get; set; }
    public ChannelKind Kind { get; set; }
    public bool CanSend { get; set; } //false = receive only (e.g. group member)
}

//full chat: membership, allowed senders, history
public class ChatDto
{
    public Guid ID { get; set; }
    public string Name { get; set; } = string.Empty;
    //member entity ids: users, units, groups (mixed virtUser ids)
    public List<Guid> MemberIDs { get; set; } = new();
    //identity ids the requester may send on behalf of in this chat
    public List<Guid> AllowedIdentityIDs { get; set; } = new();
    public List<MessageChatDto> Messages { get; set; } = new();
}

//light chat entry for lists
public class ChatSummaryDto
{
    public Guid ID { get; set; }
    public string Name { get; set; } = string.Empty;
}

//virtUser entry for the "new chat" member picker (users/units/groups)
public class DirectoryEntryDto
{
    public Guid ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public int CallSignNumber { get; set; }
    public ChannelKind Kind { get; set; }
    //UI only: checkbox state in the member picker (not sent over the wire)
    public bool Selected { get; set; }
}

public class MessageChatDto
{
    //chat the message was sent to
    public Guid ChannelID { get; set; }
    //identity virtUser id on whose behalf the message was sent
    public Guid SenderID { get; set; }
    //callsign of the sender identity, resolved server-side when sent
    public string SenderCallSign { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentUtc { get; set; }
}
