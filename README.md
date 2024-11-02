# AzureCosmosDBNoteApp


To Build the sln locally, please clone the following repository to your local machine.

(not submoduled)
Project Reference: https://github.com/ewdlop/CosmosDBPartialUpdateTypeConverter

# Session-State-and-Caching-Provider
https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/session-state-and-caching-provider

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
