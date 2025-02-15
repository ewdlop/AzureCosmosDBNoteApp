# CosmosTransactionManager

```markdown
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Cosmos.Linq;

public class CosmosTransactionManager
{
    private readonly Container _container;
    private readonly CosmosClient _client;
    private readonly string _databaseId;

    public CosmosTransactionManager(string connectionString, string databaseId)
    {
        _client = new CosmosClient(connectionString);
        _databaseId = databaseId;
        _container = _client.GetDatabase(databaseId).GetContainer("orders");
    }

    // Single-partition transaction example
    public async Task<bool> ProcessOrderTransaction(string orderId, string customerId)
    {
        // Define a batch of operations
        TransactionalBatch batch = _container.CreateTransactionalBatch(
            new PartitionKey(customerId));

        try
        {
            // Read the order
            var order = await _container.ReadItemAsync<Order>(
                orderId, 
                new PartitionKey(customerId)
            );

            // Create payment record
            var payment = new Payment
            {
                Id = Guid.NewGuid().ToString(),
                OrderId = orderId,
                CustomerId = customerId,
                Amount = order.Resource.TotalAmount,
                Status = "Processing",
                Timestamp = DateTime.UtcNow
            };

            // Update order status
            order.Resource.Status = "Processing";
            order.Resource.LastUpdated = DateTime.UtcNow;

            // Add operations to batch
            batch.ReplaceItem(orderId, order.Resource);
            batch.CreateItem(payment);

            // Execute the batch
            using TransactionalBatchResponse response = await batch.ExecuteAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // Handle specific error cases
            if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                throw new Exception("Concurrent modification detected");
            }

            return false;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new Exception("Order not found");
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Transaction failed: {ex.Message}");
            throw;
        }
    }

    // Cross-partition transaction example
    public async Task<bool> TransferFunds(
        string sourceAccountId, 
        string targetAccountId, 
        decimal amount)
    {
        var accountContainer = _client.GetDatabase(_databaseId)
                                    .GetContainer("accounts");

        // Start transaction batch operations
        List<Task<TransactionalBatchResponse>> operations = 
            new List<Task<TransactionalBatchResponse>>();

        try
        {
            // Read source account
            var sourceAccount = await accountContainer.ReadItemAsync<Account>(
                sourceAccountId, 
                new PartitionKey(sourceAccountId)
            );

            // Read target account
            var targetAccount = await accountContainer.ReadItemAsync<Account>(
                targetAccountId, 
                new PartitionKey(targetAccountId)
            );

            // Validate balances
            if (sourceAccount.Resource.Balance < amount)
            {
                throw new Exception("Insufficient funds");
            }

            // Update source account
            var sourceBatch = accountContainer.CreateTransactionalBatch(
                new PartitionKey(sourceAccountId));
            sourceAccount.Resource.Balance -= amount;
            sourceBatch.ReplaceItem(sourceAccountId, sourceAccount.Resource);

            // Update target account
            var targetBatch = accountContainer.CreateTransactionalBatch(
                new PartitionKey(targetAccountId));
            targetAccount.Resource.Balance += amount;
            targetBatch.ReplaceItem(targetAccountId, targetAccount.Resource);

            // Execute batches
            operations.Add(sourceBatch.ExecuteAsync());
            operations.Add(targetBatch.ExecuteAsync());

            // Wait for all operations to complete
            await Task.WhenAll(operations);

            // Verify all operations succeeded
            bool success = operations.All(op => op.Result.IsSuccessStatusCode);
            
            if (!success)
            {
                // Handle rollback if needed
                await RollbackTransfer(sourceAccountId, targetAccountId, amount);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Log error and attempt rollback
            Console.WriteLine($"Transfer failed: {ex.Message}");
            await RollbackTransfer(sourceAccountId, targetAccountId, amount);
            throw;
        }
    }

    // Bulk transaction example
    public async Task<BulkOperationResponse> ProcessBulkTransactions(
        List<Order> orders)
    {
        var response = new BulkOperationResponse
        {
            SuccessCount = 0,
            FailedItems = new List<FailedOperation>()
        };

        List<Task> operations = new List<Task>();
        
        foreach (var orderGroup in orders.GroupBy(o => o.CustomerId))
        {
            var batch = _container.CreateTransactionalBatch(
                new PartitionKey(orderGroup.Key));

            foreach (var order in orderGroup)
            {
                batch.CreateItem(order);
            }

            operations.Add(ProcessBatchWithRetry(batch, orderGroup.ToList(), response));
        }

        await Task.WhenAll(operations);
        return response;
    }

    private async Task ProcessBatchWithRetry(
        TransactionalBatch batch, 
        List<Order> orders,
        BulkOperationResponse response,
        int maxRetries = 3)
    {
        int attempts = 0;
        bool success = false;

        while (!success && attempts < maxRetries)
        {
            try
            {
                using var batchResponse = await batch.ExecuteAsync();
                
                if (batchResponse.IsSuccessStatusCode)
                {
                    response.SuccessCount += orders.Count;
                    success = true;
                }
                else
                {
                    attempts++;
                    await Task.Delay(attempts * 1000); // Exponential backoff
                }
            }
            catch (Exception ex)
            {
                attempts++;
                if (attempts >= maxRetries)
                {
                    orders.ForEach(o => response.FailedItems.Add(
                        new FailedOperation 
                        { 
                            ItemId = o.Id,
                            Error = ex.Message 
                        }));
                }
                await Task.Delay(attempts * 1000);
            }
        }
    }

    private async Task RollbackTransfer(
        string sourceAccountId, 
        string targetAccountId, 
        decimal amount)
    {
        try
        {
            var accountContainer = _client.GetDatabase(_databaseId)
                                        .GetContainer("accounts");

            // Attempt to restore original balances
            var sourceAccount = await accountContainer.ReadItemAsync<Account>(
                sourceAccountId, 
                new PartitionKey(sourceAccountId)
            );
            var targetAccount = await accountContainer.ReadItemAsync<Account>(
                targetAccountId, 
                new PartitionKey(targetAccountId)
            );

            var sourceBatch = accountContainer.CreateTransactionalBatch(
                new PartitionKey(sourceAccountId));
            sourceAccount.Resource.Balance += amount;
            sourceBatch.ReplaceItem(sourceAccountId, sourceAccount.Resource);

            var targetBatch = accountContainer.CreateTransactionalBatch(
                new PartitionKey(targetAccountId));
            targetAccount.Resource.Balance -= amount;
            targetBatch.ReplaceItem(targetAccountId, targetAccount.Resource);

            await Task.WhenAll(
                sourceBatch.ExecuteAsync(),
                targetBatch.ExecuteAsync()
            );
        }
        catch (Exception ex)
        {
            // Log rollback failure
            Console.WriteLine($"Rollback failed: {ex.Message}");
            throw;
        }
    }
}

// Supporting classes
public class Order
{
    public string Id { get; set; }
    public string CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class Payment
{
    public string Id { get; set; }
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public DateTime Timestamp { get; set; }
}

public class Account
{
    public string Id { get; set; }
    public decimal Balance { get; set; }
}

public class BulkOperationResponse
{
    public int SuccessCount { get; set; }
    public List<FailedOperation> FailedItems { get; set; }
}

public class FailedOperation
{
    public string ItemId { get; set; }
    public string Error { get; set; }
}
```
