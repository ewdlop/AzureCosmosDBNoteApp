using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class CosmosDbWithCustomETag
{
    public class ETagFactory
    {
        public static string GenerateETag<T>(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "Item cannot be null");
            }
    
            // Convert item to JSON
            string json = JsonSerializer.Serialize(item);
    
            // Compute SHA-256 hash
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }

    
    private static readonly string EndpointUri = "https://your-cosmosdb.documents.azure.com:443/";
    private static readonly string PrimaryKey = "your-primary-key";
    private static readonly string DatabaseId = "YourDatabase";
    private static readonly string ContainerId = "YourContainer";
    private static CosmosClient _cosmosClient;
    private static Container _container;

    public static async Task Main(string[] args)
    {
        _cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
        _container = _cosmosClient.GetContainer(DatabaseId, ContainerId);

        string itemId = "item123";
        string partitionKey = "partitionKeyValue";

        // **Step 1: Read item normally**
        var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
        var item = response.Resource;

        // Generate custom ETag
        string customEtag = ETagFactory.GenerateETag(item);
        Console.WriteLine($"Generated ETag: {customEtag}");

        // **Step 2: Read again using If-None-Match with custom ETag**
        try
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                IfNoneMatchEtag = customEtag
            };

            var newResponse = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey), requestOptions);
            Console.WriteLine($"Item retrieved again, new ETag: {ETagFactory.GenerateETag(newResponse.Resource)}");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotModified)
        {
            Console.WriteLine("Item has not changed, skipping redundant fetch.");
        }

        // **Step 3: Modify and update using If-Match with custom ETag**
        item["modifiedField"] = "Updated Value";

        try
        {
            string updatedEtag = ETagFactory.GenerateETag(item);
            ItemRequestOptions updateOptions = new ItemRequestOptions
            {
                IfMatchEtag = updatedEtag
            };

            var updateResponse = await _container.ReplaceItemAsync(item, itemId, new PartitionKey(partitionKey), updateOptions);
            Console.WriteLine($"Item updated successfully. New ETag: {ETagFactory.GenerateETag(updateResponse.Resource)}");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Update failed due to ETag mismatch. Someone else modified the data.");
        }
    }
}
