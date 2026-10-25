namespace BlazeTAK.Services;

public interface IGlobalMessageService
{
    bool SendMessage(string id, Message message);

    List<Message> GetMessages(string id);
}
