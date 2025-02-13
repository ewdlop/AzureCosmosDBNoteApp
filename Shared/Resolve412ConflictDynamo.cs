using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

class Program
{
    private static readonly string EndpointUri = "https://your-cosmos-account.documents.azure.com:443/";
    private static readonly string PrimaryKey = "your-primary-key";
    private static readonly string DatabaseId = "your-database";
    private static readonly string ContainerId = "your-container";
    private static readonly string PartitionKey = "your-partition-key";
    private static readonly string ItemId = "your-item-id";

    private static CosmosClient cosmosClient;
    private static Container container;

    static async Task Main(string[] args)
    {
        cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
        container = cosmosClient.GetContainer(DatabaseId, ContainerId);

        // Define partial update operation
        List<PatchOperation> patchOperations = new()
        {
            PatchOperation.Increment("/counter", 1),  // Increment counter field
            PatchOperation.Replace("/status", "updated")  // Replace status field
        };
#if false
        await PatchWithRetriesAsync(ItemId, PartitionKey, patchOperations);
#endif
    }

#if false
    static async Task PatchWithRetriesAsync(string itemId, string partitionKey, List<PatchOperation> patchOperations, int maxRetries = 5)
    {
        int attempt = 0;
        while (attempt < maxRetries)
        {
            try
            {
                // Step 1: Fetch latest ETag before applying patch
                ItemResponse<dynamic> currentItem = await container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
                string etag = currentItem.ETag;  // Latest ETag

                // Step 2: Apply Patch with ETag validation
                ItemResponse<dynamic> patchedItem = await container.PatchItemAsync<dynamic>(
                    itemId,
                    new PartitionKey(partitionKey),
                    patchOperations,
                    new PatchItemRequestOptions { IfMatchEtag = etag }  // Enforce ETag check
                );

                Console.WriteLine($"PATCH succeeded on attempt {attempt + 1}");
                return;  // Success, exit loop
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                Console.WriteLine($"412 Precondition Failed - Conflict detected. Retrying... (Attempt {attempt + 1})");

                // Step 3: Fetch latest version of the document
                ItemResponse<dynamic> latestItem = await container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
                string latestEtag = latestItem.ETag;  // Updated ETag

                // Step 4: Compare fields to check if conflicting updates exist
                bool fieldsAreSame = ComparePatchedFields(latestItem.Resource, patchOperations);

                if (fieldsAreSame)
                {
                    Console.WriteLine("Fields are unchanged, retrying with latest ETag...");
                }
                else
                {
                    Console.WriteLine("Conflict detected, resolving using tie-breaker...");

                    // Step 5: Resolve conflict using a tie-breaker strategy
                    ResolveConflict(latestItem.Resource, patchOperations);
                }
                
                attempt++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                break;
            }
        }

        Console.WriteLine("PATCH operation failed after max retries.");
    }
#endif

    static async Task PatchWithRetriesAsync<T>(string itemId, string partitionKey, List<PatchOperation<T>> patchOperations, int maxRetries = 5)
    {
        int attempt = 0;
        while (attempt < maxRetries)
        {
            try
            {
                // Step 1: Fetch latest ETag before applying patch
                ItemResponse<dynamic> currentItem = await container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
                string etag = currentItem.ETag;  // Latest ETag

                // Step 2: Apply Patch with ETag validation
                ItemResponse<dynamic> patchedItem = await container.PatchItemAsync<dynamic>(
                    itemId,
                    new PartitionKey(partitionKey),
                    patchOperations,
                    new PatchItemRequestOptions { IfMatchEtag = etag }  // Enforce ETag check
                );

                Console.WriteLine($"PATCH succeeded on attempt {attempt + 1}");
                return;  // Success, exit loop
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                Console.WriteLine($"412 Precondition Failed - Conflict detected. Retrying... (Attempt {attempt + 1})");

                // Step 3: Fetch latest version of the document
                ItemResponse<dynamic> latestItem = await container.ReadItemAsync<dynamic>(itemId, new PartitionKey(partitionKey));
                string latestEtag = latestItem.ETag;  // Updated ETag

                // Step 4: Compare fields to check if conflicting updates exist
                bool fieldsAreSame = ComparePatchedFields(latestItem.Resource, patchOperations);

                if (fieldsAreSame)
                {
                    Console.WriteLine("Fields are unchanged, retrying with latest ETag...");
                }
                else
                {
                    Console.WriteLine("Conflict detected, resolving using tie-breaker...");

                    // Step 5: Resolve conflict using a tie-breaker strategy
                    ResolveConflict(latestItem.Resource, patchOperations);
                }

                attempt++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                break;
            }
        }

        Console.WriteLine("PATCH operation failed after max retries.");
    }

#if false

    // Function to compare patched fields with the latest document state
    static bool ComparePatchedFields(dynamic latestDoc, List<PatchOperation> patchOperations)
    {
        foreach (var op in patchOperations)
        {
            string path = op.Path.TrimStart('/');
            dynamic newValue = op.Value;
            dynamic currentValue = latestDoc[path];

            if (!Equals(currentValue, newValue))
            {
                return false; // Conflict detected
            }
        }
        return true; // No changes, safe to retry
    }
#endif

    // Function to compare patched fields with the latest document state
    static bool ComparePatchedFields<T>(dynamic latestDoc, List<PatchOperation<T>> patchOperations)
    {
        foreach (var op in patchOperations)
        {
            string path = op.Path.TrimStart('/');
            T newValue = op.Value;
            T currentValue = latestDoc[path];

            if (!Equals(currentValue, newValue))
            {
                return false; // Conflict detected
            }
        }
        return true; // No changes, safe to retry
    }

#if false
    // Function to resolve conflicts using a tie-breaker strategy
    static void ResolveConflict(dynamic latestDoc, List<PatchOperation> patchOperations)
    {
        foreach (var op in patchOperations)
        {
            string path = op.Path.TrimStart('/');
            dynamic newValue = op.Value;
            dynamic currentValue = latestDoc[path];

            // Example: Tie-breaker using LastUpdated timestamp
            if (latestDoc.ContainsKey("lastUpdated"))
            {
                DateTime latestTimestamp = DateTime.Parse(latestDoc["lastUpdated"]);
                DateTime incomingTimestamp = DateTime.UtcNow; // Assume incoming update is "now"

                if (incomingTimestamp > latestTimestamp)
                {
                    Console.WriteLine($"Applying new value for {path} based on latest timestamp.");
                    latestDoc[path] = newValue;
                }
                else
                {
                    Console.WriteLine($"Skipping update for {path} as existing data is newer.");
                }
            }
            else
            {
                Console.WriteLine($"No timestamp available, defaulting to latest value for {path}.");
                latestDoc[path] = newValue; // Default resolution
            }
        }
    }
#endif

    static void ResolveConflict<T>(dynamic latestDoc, List<PatchOperation<T>> patchOperations)
    {
        foreach (var op in patchOperations)
        {
            string path = op.Path.TrimStart('/');
            T newValue = op.Value;

            // Example: Tie-breaker using LastUpdated timestamp
            if (latestDoc.ContainsKey("lastUpdated"))
            {
                DateTime latestTimestamp = DateTime.Parse(latestDoc["lastUpdated"]);
                DateTime incomingTimestamp = DateTime.UtcNow; // Assume incoming update is "now"

                if (incomingTimestamp > latestTimestamp)
                {
                    Console.WriteLine($"Applying new value for {path} based on latest timestamp.");
                    latestDoc[path] = newValue;
                }
                else
                {
                    Console.WriteLine($"Skipping update for {path} as existing data is newer.");
                }
            }
            else
            {
                Console.WriteLine($"No timestamp available, defaulting to latest value for {path}.");
                latestDoc[path] = newValue; // Default resolution
            }
        }
    }

#if false
    // Function to remove fields from patchOperations if their values are unchanged
    static List<PatchOperation> FilterUnchangedFields(dynamic latestDoc, List<PatchOperation> patchOperations)
    {
        List<PatchOperation> filteredOperations = new();

        foreach (var op in patchOperations)
        {
            string path = op.Path.TrimStart('/');
            dynamic newValue = op.Value;
            dynamic currentValue = latestDoc[path];

            if (!Equals(currentValue, newValue))
            {
                filteredOperations.Add(op);  // Keep only changed fields
            }
            else
            {
                Console.WriteLine($"Skipping unchanged field: {path}");
            }
        }

        return filteredOperations;
    }

#endif
    // Function to remove fields from patchOperations if their values are unchanged
    static List<PatchOperation<T>> FilterUnchangedFields<T>(dynamic latestDoc, List<PatchOperation<T>> patchOperations)
    {
        List<PatchOperation<T>> filteredOperations = new();

        foreach (var op in patchOperations)
        {
            string path = op.Path.TrimStart('/');
            T newValue = op.Value;
            T currentValue = latestDoc[path];

            if (!Equals(currentValue, newValue))
            {
                filteredOperations.Add(op);  // Keep only changed fields
            }
            else
            {
                Console.WriteLine($"Skipping unchanged field: {path}");
            }
        }
        return filteredOperations;
    }
}