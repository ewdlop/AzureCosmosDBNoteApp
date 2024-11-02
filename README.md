# AzureCosmosDBNoteApp


To Build the sln locally, please clone the following repository to your local machine.

(not submoduled)
Project Reference: https://github.com/ewdlop/CosmosDBPartialUpdateTypeConverter


# 08/14/2024 Upate
https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-migrate-from-bulk-executor-library
```
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
