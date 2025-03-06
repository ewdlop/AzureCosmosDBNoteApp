# AzureCosmosDBNoteApp

```markdown
To Build the sln locally, please clone the following repository to your local machine.

(not submoduled)
Project Reference: https://github.com/ewdlop/CosmosDBPartialUpdateTypeConverter

#Client sideeeeeeeee
https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-always-encrypted?tabs=dotnet
https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver16#step-1-generating-the-initialization-vector-iv

https://www.ezpassnj.com/en/home/index.shtml <---shtml!?
https://en.wikipedia.org/wiki/Server_Side_Includes

https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver16#step-1-generating-the-initialization-vector-iv
```

## Performance?
```markdown
https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/materialized-views?tabs=azure-portal#Previewing

```
## 08/14/2024 Upate
```markdown
https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-migrate-from-bulk-executor-library
```cs
BulkOperations<MyItem> bulkOperations = new BulkOperations<MyItem>(documentsToWorkWith.Count);
foreach (MyItem document in documentsToWorkWith)
{
    document.operationCounter++;
    bulkOperations.Tasks.Add(CaptureOperationResponse(container.ReplaceItemAsync(document, document.id, new PartitionKey(document.pk)), document));
}
```
```
private static async Task<OperationResponse<T>> CaptureOperationResponse<T>(Task<ItemResponse<T>> task, T item)
{
    try
    {
        ItemResponse<T> response = await task;
        return new OperationResponse<T>()
        {
            Item = item,
            IsSuccessful = true,
            RequestUnitsConsumed = task.Result.RequestCharge
        };
    }
    catch (Exception ex)
    {
        if (ex is CosmosException cosmosException)
        {
            return new OperationResponse<T>()
            {
                Item = item,
                RequestUnitsConsumed = cosmosException.RequestCharge,
                IsSuccessful = false,
                CosmosException = cosmosException
            };
        }

        return new OperationResponse<T>()
        {
            Item = item,
            IsSuccessful = false,
            CosmosException = ex
        };
    }
}
```
```cs
BulkOperations<MyItem> bulkOperations = new BulkOperations<MyItem>(documentsToWorkWith.Count);
foreach (MyItem document in documentsToWorkWith)
{
    document.operationCounter++;
    bulkOperations.Tasks.Add(CaptureOperationResponse(container.DeleteItemAsync<MyItem>(document.id, new PartitionKey(document.pk)), document));
}
```

# Session-State-and-Caching-Provider
https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/session-state-and-caching-provider

```cs
public class MyBusinessClass
{
    private readonly IDistributedCache cache;

    public MyBusinessClass(IDistributedCache cache)
    {
        this.cache = cache;
    }
    
    public async Task SomeOperationAsync()
    {
        string someCachedValue = await this.cache.GetStringAsync("someKey");
        /* Use the cache */
    }
}
```

```cs
void captureDiagnostics(CosmosDiagnostics diagnostics)
{
    if (diagnostics.GetClientElapsedTime() > SomePredefinedThresholdTime)
    {
        Console.WriteLine(diagnostics.ToString());
    }
}

services.AddCosmosCache((CosmosCacheOptions cacheOptions) =>
{
    cacheOptions.DiagnosticsHandler = captureDiagnostics;
    /* other options */
});
```

## Also NOSQL 

https://www.youtube.com/watch?v=D5xU7_98jWc
![image](https://github.com/user-attachments/assets/982d6f6e-286b-4c94-88e4-d3e71330af9e)


## geo-redundancy

<https://www.infobip.com/glossary/geo-redundancy>

<https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.management.cosmosdb.fluent.models.location?view=azure-dotnet-legacy>

<https://learn.microsoft.com/en-us/java/api/com.microsoft.azure.management.cosmosdb.location?view=azure-java-legacy>

## pusedo-Reference

> microsoft docs, course lectures, books, videos, by professionals

## Azure Cosmsdb Emulator is not free.

## EntityFramework Core Typebuilderextensions

[EntityFramework Core Typebuilderextensions - https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.cosmosentitytypebuilderextensions?view=efcore-9.0](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.cosmosentitytypebuilderextensions?view=efcore-9.0)
