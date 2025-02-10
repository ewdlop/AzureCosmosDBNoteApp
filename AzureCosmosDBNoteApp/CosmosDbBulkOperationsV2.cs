using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class CosmosDbBulkOperationsV2
{
    /**using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;**/
    
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
    
        // Generate a Custom ETag (SHA-256 of Item Content)
        public static string GenerateCustomETag<T>(T item)
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
    
        // Save Both Custom & CosmosDB ETags
        public async Task SaveETagAsync(string itemId, string cosmosEtag, string customEtag)
        {
            var etagRecord = new
            {
                id = itemId,
                cosmosEtag = cosmosEtag,
                customEtag = customEtag,
                timestamp = DateTime.UtcNow
            };
    
            await _etagContainer.UpsertItemAsync(etagRecord, new PartitionKey(itemId));
        }
    
        // Retrieve Both ETags
        public async Task<(string CosmosEtag, string CustomEtag)> GetETagsAsync(string itemId)
        {
            try
            {
                var response = await _etagContainer.ReadItemAsync<dynamic>(itemId, new PartitionKey(itemId));
                return (response.Resource.cosmosEtag.ToString(), response.Resource.customEtag.ToString());
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (null, null);
            }
        }
    
        // Cleanup Old ETags (Older than 7 Days)
        public async Task CleanupOldETagsAsync()
        {
            QueryDefinition query = new QueryDefinition("SELECT c.id FROM c WHERE c.timestamp < @threshold")
                .WithParameter("@threshold", DateTime.UtcNow.AddDays(-7));
    
            using FeedIterator<dynamic> iterator = _etagContainer.GetItemQueryIterator<dynamic>(query);
            List<Task> deleteTasks = new();
            while (iterator.HasMoreResults)
            {
                FeedResponse<dynamic> response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    deleteTasks.Add(_etagContainer.DeleteItemAsync<dynamic>(item.id.ToString(), new PartitionKey(item.id.ToString())));
                }
            }
            await Task.WhenAll(deleteTasks);
        }
    }


    private static readonly string EndpointUri = "https://your-cosmosdb.documents.azure.com:443/";
    private static readonly string PrimaryKey = "your-primary-key";
    private static readonly string DatabaseId = "YourDatabase";
    private static readonly string ContainerId = "YourContainer";
    private static CosmosClient _cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions { AllowBulkExecution = true });
    private static Container _container = _cosmosClient.GetContainer(DatabaseId, ContainerId);
    private static ETagFactory _etagFactory = new(_cosmosClient);
    private static ConcurrentDictionary<string, HttpStatusCode> responseCodes = new();

    public static async Task Main(string[] args)
    {
        List<string> itemIds = new() { "item1", "item2", "item3" };

        // **BULK READ WITH IF-NONE-MATCH (Avoid Redundant Reads)**
        await Parallel.ForEachAsync(itemIds, async (itemId, _) =>
        {
            (string cosmosEtag, string customEtag) = await _etagFactory.GetETagsAsync(itemId);

            try
            {
                ItemRequestOptions requestOptions = cosmosEtag != null ? new ItemRequestOptions { IfNoneMatchEtag = cosmosEtag } : null;
                var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(itemId), requestOptions);
                responseCodes[itemId] = response.StatusCode;

                string newCustomEtag = ETagFactory.GenerateCustomETag(response.Resource);
                await _etagFactory.SaveETagAsync(itemId, response.ETag, newCustomEtag);
                Console.WriteLine($"Item {itemId} retrieved. New ETags stored.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotModified)
            {
                responseCodes[itemId] = HttpStatusCode.NotModified;
                Console.WriteLine($"Item {itemId} not modified. Skipped.");
            }
        });

        // **BULK UPDATE WITH IF-MATCH (Prevents Concurrent Writes)**
        await Parallel.ForEachAsync(itemIds, async (itemId, _) =>
        {
            (string storedCosmosEtag, string storedCustomEtag) = await _etagFactory.GetETagsAsync(itemId);
            var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(itemId));
            dynamic item = response.Resource;
            item["modifiedField"] = "Updated in bulk";

            try
            {
                ItemRequestOptions updateOptions = new() { IfMatchEtag = storedCosmosEtag };
                var updateResponse = await _container.ReplaceItemAsync(item, itemId, new PartitionKey(itemId), updateOptions);
                responseCodes[itemId] = updateResponse.StatusCode;

                string newCustomEtag = ETagFactory.GenerateCustomETag(updateResponse.Resource);
                await _etagFactory.SaveETagAsync(itemId, updateResponse.ETag, newCustomEtag);
                Console.WriteLine($"Item {itemId} updated. New ETags stored.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                responseCodes[itemId] = HttpStatusCode.PreconditionFailed;
                Console.WriteLine($"Item {itemId} update failed due to concurrent modification.");
            }
        });

        // **Print All Response Codes**
        Console.WriteLine("Final Response Codes:");
        foreach (var entry in responseCodes)
        {
            Console.WriteLine($"Item {entry.Key}: {entry.Value}");
        }
    }
}
