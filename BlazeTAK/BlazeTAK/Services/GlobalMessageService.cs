namespace BlazeTAK.Services;
using System.Collections.Concurrent;


public class GlobalMessageService : IGlobalMessageService
{
    private ConcurrentDictionary<string, Message> Messages { get; set; } = new ConcurrentDictionary<string, Message>();
    public bool SendMessage(string id, Message message) => Messages.TryAdd(id, message); 
    public List<Message> GetMessages(string id) => Messages.Where(x => x.Key == id).Select(x => x.Value).ToList();
}
public class Message
{
    public Guid SenderId { get; set; }
    public string? CipherText { get; set; } = null;
}