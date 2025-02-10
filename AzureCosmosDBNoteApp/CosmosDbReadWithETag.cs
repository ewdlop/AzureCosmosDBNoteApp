using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public class CosmosDbReadWithETag
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

        // Read item initially
        var response = await _container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
        string etag = response.ETag;

        Console.WriteLine($"First read ETag: {etag}");

        // Attempt to read again with If-None-Match
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
            Console.WriteLine("Item has not changed, skipping read.");
        }
    }
}
