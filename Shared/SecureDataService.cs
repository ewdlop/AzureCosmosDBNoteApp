using Microsoft.Azure.Cosmos;
using Azure.Core;
using Azure.Identity;
using System.Security.Cryptography;
using System.Text.Json;

public interface ISecureDataOperation<T> where T : class
{
    Task<T> StoreAsync(T data);
    Task<T> RetrieveAsync(string id, string partitionKey);
    Task<IEnumerable<T>> QueryAsync(string sqlQuery);
}

public interface IKeyRotationOperation
{
    Task RotateKeyAsync(string oldKeyId, string newKeyId);
}

public interface IEncryptedEntity
{
    string Id { get; set; }
    string PartitionKey { get; set; }
    DateTime Timestamp { get; set; }
}

public class SensitiveData : IEncryptedEntity
{
    public string Id { get; set; }
    public string PartitionKey { get; set; }
    
    [Encrypted]
    public string SecretInformation { get; set; }
    
    [Encrypted]
    public Dictionary<string, string> EncryptedMetadata { get; set; }
    
    public DateTime Timestamp { get; set; }
}

public abstract class CosmosOperationBase : IAsyncDisposable
{
    protected readonly CosmosClient _cosmosClient;
    protected readonly Container _container;
    protected readonly string _databaseId;
    protected readonly string _containerId;
    protected readonly TokenCredential _credential;

    protected CosmosOperationBase(string endpoint, string databaseId, string containerId)
    {
        _databaseId = databaseId;
        _containerId = containerId;
        _credential = new DefaultAzureCredential();

        var encryptionKeyWrapProvider = CreateKeyWrapProvider();
        var clientOptions = ConfigureClientOptions(encryptionKeyWrapProvider);

        _cosmosClient = new CosmosClient(endpoint, _credential, clientOptions);
        _container = _cosmosClient.GetContainer(_databaseId, _containerId);
    }

    protected virtual IKeyWrapProvider CreateKeyWrapProvider()
    {
        return new AzureKeyVaultKeyWrapProvider(_credential);
    }

    protected virtual CosmosClientOptions ConfigureClientOptions(IKeyWrapProvider keyWrapProvider)
    {
        return new CosmosClientOptions
        {
            EncryptionKeyWrapProvider = keyWrapProvider,
            ConnectionMode = ConnectionMode.Direct
        };
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_cosmosClient != null)
        {
            await _cosmosClient.DisposeAsync();
        }
    }
}

public class SecureDataService : CosmosOperationBase, 
    ISecureDataOperation<SensitiveData>,
    IKeyRotationOperation
{
    public SecureDataService(string endpoint, string databaseId, string containerId)
        : base(endpoint, databaseId, containerId)
    {
    }

    protected virtual EncryptionItemRequestOptions CreateEncryptionOptions()
    {
        return new EncryptionItemRequestOptions();
    }

    protected virtual async Task ValidateDataAsync(SensitiveData data)
    {
        if (string.IsNullOrEmpty(data.SecretInformation))
        {
            throw new ArgumentException("Secret information cannot be empty");
        }

        // Add additional validation logic
    }

    public async Task<SensitiveData> StoreAsync(SensitiveData data)
    {
        await ValidateDataAsync(data);

        try
        {
            ItemResponse<SensitiveData> response = await _container.CreateItemAsync(
                data,
                new PartitionKey(data.PartitionKey),
                new ItemRequestOptions { EncryptionPolicy = CreateEncryptionOptions() }
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("Data with this ID already exists", ex);
        }
    }

    public async Task<SensitiveData> RetrieveAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<SensitiveData> response = await _container.ReadItemAsync<SensitiveData>(
                id,
                new PartitionKey(partitionKey),
                new ItemRequestOptions { EncryptionPolicy = CreateEncryptionOptions() }
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<SensitiveData>> QueryAsync(string sqlQuery)
    {
        var queryDefinition = new QueryDefinition(sqlQuery);
        var queryRequestOptions = new QueryRequestOptions
        {
            EncryptionPolicy = CreateEncryptionOptions()
        };

        var results = new List<SensitiveData>();
        var queryIterator = _container.GetItemQueryIterator<SensitiveData>(
            queryDefinition,
            requestOptions: queryRequestOptions
        );

        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task RotateKeyAsync(string oldKeyId, string newKeyId)
    {
        var options = new DatabaseRequestOptions
        {
            EncryptionKeyWrapProvider = new AzureKeyVaultKeyWrapProvider(
                _credential,
                keyEncryptionKeyUrl: newKeyId
            )
        };

        await _cosmosClient.GetDatabase(_databaseId)
            .ReplaceDatabaseEncryptionKeyAsync(options);
    }
}

// Custom implementation example
public class EnhancedSecureDataService : SecureDataService
{
    public EnhancedSecureDataService(string endpoint, string databaseId, string containerId)
        : base(endpoint, databaseId, containerId)
    {
    }

    protected override async Task ValidateDataAsync(SensitiveData data)
    {
        await base.ValidateDataAsync(data);
        
        // Additional validation
        if (data.EncryptedMetadata == null || !data.EncryptedMetadata.Any())
        {
            throw new ArgumentException("Metadata is required");
        }
    }

    protected override IKeyWrapProvider CreateKeyWrapProvider()
    {
        // Custom key provider implementation
        return new CustomKeyWrapProvider(_credential);
    }
}

public class Program
{
    public static async Task Main()
    {
        var endpoint = "your_cosmos_endpoint";
        var databaseId = "secure_database";
        var containerId = "encrypted_container";

        // Using the enhanced service
        ISecureDataOperation<SensitiveData> secureService = 
            new EnhancedSecureDataService(endpoint, databaseId, containerId);

        var metadata = new Dictionary<string, string>
        {
            { "category", "sensitive" },
            { "department", "finance" }
        };

        var sensitiveData = new SensitiveData
        {
            Id = Guid.NewGuid().ToString(),
            PartitionKey = DateTime.UtcNow.ToString("yyyyMM"),
            SecretInformation = "Secret data",
            EncryptedMetadata = metadata,
            Timestamp = DateTime.UtcNow
        };

        // Store data
        var storedData = await secureService.StoreAsync(sensitiveData);

        // Query data
        var query = "SELECT * FROM c WHERE c.EncryptedMetadata.category = 'sensitive'";
        var results = await secureService.QueryAsync(query);

        await ((IAsyncDisposable)secureService).DisposeAsync();
    }
}
