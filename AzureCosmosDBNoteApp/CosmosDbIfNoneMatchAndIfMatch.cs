using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public class CosmosDbIfNoneMatchAndIfMatch
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

        // **Step 1: Read item and get ETag**
        var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
        string etag = response.ETag;
        dynamic item = response.Resource;

        Console.WriteLine($"First Read: ETag = {etag}");

        // **Step 2: Read again using If-None-Match**
        try
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                IfNoneMatchEtag = etag
            };

            var newResponse = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey), requestOptions);
            Console.WriteLine($"Item retrieved again, new ETag: {newResponse.ETag}");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotModified)
        {
            Console.WriteLine("Item has not changed, skipping redundant fetch.");
        }

        // **Step 3: Modify the item and attempt an update using If-Match**
        item["modifiedField"] = "Updated Value";

        try
        {
            ItemRequestOptions updateOptions = new ItemRequestOptions
            {
                IfMatchEtag = etag // Ensure update only if item wasn't modified by another process
            };

            var updateResponse = await _container.ReplaceItemAsync(item, itemId, new PartitionKey(partitionKey), updateOptions);
            Console.WriteLine($"Item updated successfully. New ETag: {updateResponse.ETag}");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Update failed due to ETag mismatch. Someone else modified the data.");
        }
    }
}
