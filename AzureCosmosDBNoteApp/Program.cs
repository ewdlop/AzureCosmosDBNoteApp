using Microsoft.Azure.Cosmos;
using System.Net;

//place holders for Cosmos DB account, key and database
string connectionString = "AccountEndpoint=https://mycosmosaccount.documents.azure.com:443/;AccountKey=myAccountKey;";
string databaseId = "myTestingDatabase";
string containerName = "myTestingContainer";

//TODO : test the code
try
{
    CosmosClient client = new(connectionString);
    Database database = client.GetDatabase(databaseId);
    string partitionKeyPath = "/myPartitionKey";
    ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
    if (containerResponse.StatusCode == HttpStatusCode.Created || containerResponse.StatusCode == HttpStatusCode.OK)
    {
        Console.WriteLine(containerResponse.StatusCode == HttpStatusCode.Created ? "Container created" : "Container already exists");
        Container container = containerResponse.Container;
        string id = Guid.NewGuid().ToString();
        dynamic item = new
        {
            id,
            myProperty = "myValue"
        };
        ItemResponse<dynamic> itemResponse = await container.CreateItemAsync(item, new PartitionKey(item.myPartitionKey));
        if (itemResponse.StatusCode == HttpStatusCode.Created)
        {
            Console.WriteLine("Item created");
        }
        AnalyzeResponse(itemResponse);
    }
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound ||
                                    ex.StatusCode == HttpStatusCode.BadRequest ||
                                    ex.StatusCode == HttpStatusCode.PreconditionFailed ||
                                    ex.StatusCode == HttpStatusCode.FailedDependency ||
                                    ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
{
    AnalyzeDiDiagnostics(ex.Diagnostics);
    Console.WriteLine($"CosmosException: {ex}");
}
catch (CosmosException ex)
{
    AnalyzeDiDiagnostics(ex.Diagnostics);
    Console.WriteLine($"Other CosmosException: {ex}");
}
catch (Exception ex)
{
    Console.WriteLine($"Exception: {ex}");
}