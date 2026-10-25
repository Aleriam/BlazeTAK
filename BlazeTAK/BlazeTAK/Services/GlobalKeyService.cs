using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;

namespace BlazeTAK.Services;
using BlazeTAK.Data.Models;

public class GlobalKeyService : IGlobalKeyService
{
    private ConcurrentDictionary<string, UserKeyData> GroupKeys { get; set; } = new ConcurrentDictionary<string, UserKeyData>();
    
    public Dictionary<string, byte[]>? GetGroupKeyList(string guid)
    {   
        if (GroupKeys.TryGetValue(guid, out UserKeyData? groupData))
        {
            return groupData.UserKeys.ToDictionary();
        }
        return null;
    }

    public bool RegisterKey(string groupId, string userId, byte[] key)
    { 
        return GroupKeys.TryAdd(groupId, new UserKeyData(userId, key));
    }
    
}
public class UserKeyData
{
    //string = guid
    public ConcurrentDictionary<string, byte[]> UserKeys { get; set; } = new ConcurrentDictionary<string, byte[]>();
    
    public UserKeyData(string userId, byte[] key)
    {
        UserKeys.TryAdd(userId, key);
    }
}