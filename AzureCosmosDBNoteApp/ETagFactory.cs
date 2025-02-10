using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public class ETagFactory
{
    private static readonly string DatabaseId = "YourDatabase";
    private static readonly string ETagContainerId = "ETagTracking";
    private static CosmosClient _cosmosClient;
    private static Container _etagContainer;

    public ETagFactory(CosmosClient client)
    {
        _cosmosClient = client;
        _etagContainer = _cosmosClient.GetContainer(DatabaseId, ETagContainerId);
    }

    public static string GenerateETag<T>(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item), "Item cannot be null");
        }

        string json = JsonSerializer.Serialize(item);
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(hashBytes);
        }
    }

    public async Task SaveETagAsync(string itemId, string etag)
    {
        var etagRecord = new { id = itemId, etag };
        await _etagContainer.UpsertItemAsync(etagRecord, new PartitionKey(itemId));
    }

    public async Task<string> GetETagAsync(string itemId)
    {
        try
        {
            var response = await _etagContainer.ReadItemAsync<dynamic>(itemId, new PartitionKey(itemId));
            return response.Resource.etag;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
