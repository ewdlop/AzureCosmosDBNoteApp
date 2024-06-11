using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Shared;
using System.Net;

namespace AzureCosomosDBNoteTestProject
{
    public class Tests
    {
        private Container? container;
        [SetUp]
        public void Setup()
        {
            string azureKeyVaultEndpoint = "https://不太可能.vault.azure.net/";
            string connectionStringSecret = "connectionStringSecret";
            string databaseIdSecret = "databaseIdSecret";
            SecretClient secretClient = new SecretClient(new Uri(azureKeyVaultEndpoint), new DefaultAzureCredential(includeInteractiveCredentials: true));
            Azure.Response<KeyVaultSecret> connectionStringSecretResponse = secretClient.GetSecret(connectionStringSecret);
            Azure.Response<KeyVaultSecret> databaseIdSecretResponse = secretClient.GetSecret(databaseIdSecret);

            //place holders
            //should fetch the key from the Azure Key Vault
            string connectionString = connectionStringSecretResponse.Value.Value;
            string databaseId = databaseIdSecretResponse.Value.Value;

            CosmosClient client = new(connectionString);
            Database database = client.GetDatabase(databaseId);
            string partitionKeyPath = "/myPartitionKey";
            ContainerResponse containerResponse = database.CreateContainerIfNotExistsAsync("myTestingContainer", partitionKeyPath).GetAwaiter().GetResult();
            if (containerResponse.StatusCode == HttpStatusCode.Created || containerResponse.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine(containerResponse.StatusCode == HttpStatusCode.Created ? "Container created" : "Container already exists");
                container = containerResponse.Container;

            }
        }

        public record PlaceHolder(string Id, string MyProperty);
        public record PlaceHolder2(string Id, string MyProperty, string MyProperty2) : PlaceHolder(Id, MyProperty);

        [Test]
        public async Task Test1()
        {
            //if (container is null) Assert.Fail("Container is null");
            Assert.IsNotNull(container);
            Assert.Warn("This test has not been fully implemented yet");
            ItemResponse<dynamic> dynamicItemResponse = await container.ReadItemAsync<dynamic>("myId", new PartitionKey("myPartitionKey"));
            Analyzer.AnalyzeResponse(dynamicItemResponse);
            ItemResponse<PlaceHolder> placeHolderItemResponse = await container.ReadItemAsync<PlaceHolder>("myId", new PartitionKey("myPartitionKey"));
            Analyzer.AnalyzeResponse(placeHolderItemResponse);
            ItemResponse<PlaceHolder2> placeHolder2ItemResponse = await container.ReadItemAsync<PlaceHolder2>("myId", new PartitionKey("myPartitionKey"));
            Analyzer.AnalyzeResponse(placeHolder2ItemResponse);
            ItemResponse<(string id, string myproperty)> tupleItemResponse = await container.ReadItemAsync<(string id, string myproperty)>("myId", new PartitionKey("myPartitionKey"));
            Analyzer.AnalyzeResponse(tupleItemResponse);
        }
    }
}