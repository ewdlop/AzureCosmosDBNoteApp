using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public class CosmosDbBulkOperations
{
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

        // **BULK READ WITH IF-NONE-MATCH**
        await Parallel.ForEachAsync(itemIds, async (itemId, _) =>
        {
            string etag = await _etagFactory.GetETagAsync(itemId);

            try
            {
                ItemRequestOptions requestOptions = etag != null ? new ItemRequestOptions { IfNoneMatchEtag = etag } : null;
                var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(itemId), requestOptions);
                responseCodes[itemId] = response.StatusCode;

                string newEtag = ETagFactory.GenerateETag(response.Resource);
                await _etagFactory.SaveETagAsync(itemId, newEtag);
                Console.WriteLine($"Item {itemId} retrieved. Status: {response.StatusCode}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotModified)
            {
                responseCodes[itemId] = HttpStatusCode.NotModified;
                Console.WriteLine($"Item {itemId} not modified. Skipped.");
            }
        });

        // **BULK UPDATE WITH IF-MATCH**
        await Parallel.ForEachAsync(itemIds, async (itemId, _) =>
        {
            string storedEtag = await _etagFactory.GetETagAsync(itemId);
            var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(itemId));
            dynamic item = response.Resource;
            item["modifiedField"] = "Updated in bulk";

            try
            {
                ItemRequestOptions updateOptions = new() { IfMatchEtag = storedEtag };
                var updateResponse = await _container.ReplaceItemAsync(item, itemId, new PartitionKey(itemId), updateOptions);
                responseCodes[itemId] = updateResponse.StatusCode;

                string newEtag = ETagFactory.GenerateETag(updateResponse.Resource);
                await _etagFactory.SaveETagAsync(itemId, newEtag);
                Console.WriteLine($"Item {itemId} updated. Status: {updateResponse.StatusCode}");
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
