using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class ETagFactory
{
    private static readonly ConcurrentDictionary<string, string> ETagStore = new();

    public static string GenerateETag<T>(T item, string itemId)
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
            string etag = Convert.ToBase64String(hashBytes);

            // Store ETag with itemId
            ETagStore[itemId] = etag;
            return etag;
        }
    }

    public static string GetETag(string itemId)
    {
        return ETagStore.TryGetValue(itemId, out string etag) ? etag : null;
    }

    public static void RemoveETag(string itemId)
    {
        ETagStore.TryRemove(itemId, out _);
    }

    public static void PrintTrackedETags()
    {
        Console.WriteLine("Tracked ETags:");
        foreach (var entry in ETagStore)
        {
            Console.WriteLine($"Item ID: {entry.Key}, ETag: {entry.Value}");
        }
    }
}

//using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public class CosmosDbWithTrackedETags
{
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

        // **Step 1: Read item normally & Track ETag**
        var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
        var item = response.Resource;

        string generatedEtag = ETagFactory.GenerateETag(item, itemId);
        Console.WriteLine($"Generated & Tracked ETag: {generatedEtag}");

        // **Step 2: Read again using If-None-Match with tracked ETag**
        string cachedEtag = ETagFactory.GetETag(itemId);
        if (cachedEtag != null)
        {
            try
            {
                ItemRequestOptions requestOptions = new ItemRequestOptions
                {
                    IfNoneMatchEtag = cachedEtag
                };

                var newResponse = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey), requestOptions);
                Console.WriteLine($"Item retrieved again, new ETag: {ETagFactory.GenerateETag(newResponse.Resource, itemId)}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotModified)
            {
                Console.WriteLine("Item has not changed, skipping redundant fetch.");
            }
        }

        // **Step 3: Modify and attempt to update using If-Match with tracked ETag**
        item["modifiedField"] = "Updated Value";

        try
        {
            string updatedEtag = ETagFactory.GenerateETag(item, itemId);
            ItemRequestOptions updateOptions = new ItemRequestOptions
            {
                IfMatchEtag = cachedEtag // Prevents overwriting if someone else modified the item
            };

            var updateResponse = await _container.ReplaceItemAsync(item, itemId, new PartitionKey(partitionKey), updateOptions);
            string newEtag = ETagFactory.GenerateETag(updateResponse.Resource, itemId);
            Console.WriteLine($"Item updated successfully. New ETag: {newEtag}");

            // Update the tracked ETag
            ETagFactory.GenerateETag(updateResponse.Resource, itemId);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Update failed due to ETag mismatch. Someone else modified the data.");
        }

        // **Print all tracked ETags**
        ETagFactory.PrintTrackedETags();
    }
}

