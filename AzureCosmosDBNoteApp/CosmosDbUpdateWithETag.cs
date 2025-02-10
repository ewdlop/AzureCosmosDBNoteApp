using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public class CosmosDbUpdateWithETag
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

        // Read item first
        var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
        string etag = response.ETag;
        dynamic item = response.Resource;

        // Modify the item
        item["modifiedField"] = "Updated Value";

        try
        {
            // Try updating only if ETag matches
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                IfMatchEtag = etag
            };

            var updatedResponse = await _container.ReplaceItemAsync(item, itemId, new PartitionKey(partitionKey), requestOptions);
            Console.WriteLine($"Item updated successfully. New ETag: {updatedResponse.ETag}");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Update failed due to ETag mismatch. Data was modified by another process.");
        }
    }
}
