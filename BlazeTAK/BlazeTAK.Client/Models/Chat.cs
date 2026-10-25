namespace BlazeTAK.Client.Models;

using System.Security.Cryptography;

public class Chat
{
    
}

public class CryptData
{
    private readonly byte[] _privateKey;
    public byte[] PublicKey { get; private set; }
    
    
    public CryptData()
    {
        using MLKem new_key = MLKem.GenerateKey(MLKemAlgorithm.MLKem768);
        _privateKey = new_key.ExportPrivateSeed();
        PublicKey = new_key.ExportEncapsulationKey();
    }

    public async Task<bool> SendSymetricKey()
    {
        
        
        return true;
    }
}

public class Agent
{
    public Guid Id { get; set; }
    public byte[] PublicKey { get; set; }
}