using BlazeTAK.Data.Models;

namespace BlazeTAK.Services;

public interface IGlobalKeyService
{
    Dictionary<string, byte[]>? GetGroupKeyList(string guid);

    bool RegisterKey(string groupId, string userId, byte[] key);
}
