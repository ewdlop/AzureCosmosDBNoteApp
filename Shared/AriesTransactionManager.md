# AriesTransactionManager

```markdown
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public class AriesTransactionManager
{
    private readonly Container _container;
    private readonly string _partitionKey;

    public AriesTransactionManager(CosmosClient client, string databaseId, string containerId, string partitionKey)
    {
        _container = client.GetDatabase(databaseId).GetContainer(containerId);
        _partitionKey = partitionKey;
    }

    public async Task<TransactionResult> ExecuteTransactionAsync(List<TransactionOperation> operations)
    {
        var transactionLog = new TransactionLog
        {
            Id = Guid.NewGuid().ToString(),
            Status = TransactionStatus.Pending,
            Operations = operations,
            Timestamp = DateTime.UtcNow,
            PartitionKey = _partitionKey
        };

        try
        {
            // Phase 1: Write-Ahead Logging
            await _container.CreateItemAsync(transactionLog, new PartitionKey(_partitionKey));

            // Phase 2: Execute Operations
            for (int i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                var compensatingOperation = CreateCompensatingOperation(operation);

                // Update checkpoint
                transactionLog.LastCheckpoint = i;
                await _container.ReplaceItemAsync(
                    transactionLog,
                    transactionLog.Id,
                    new PartitionKey(_partitionKey)
                );

                try
                {
                    await ExecuteOperation(operation);
                    transactionLog.CompensatingOperations.Insert(0, compensatingOperation);
                }
                catch (Exception ex)
                {
                    await RollbackTransaction(transactionLog);
                    return new TransactionResult
                    {
                        Success = false,
                        TransactionId = transactionLog.Id,
                        Error = ex.Message
                    };
                }
            }

            // Phase 3: Mark as Committed
            transactionLog.Status = TransactionStatus.Committed;
            transactionLog.CompletedAt = DateTime.UtcNow;
            await _container.ReplaceItemAsync(
                transactionLog,
                transactionLog.Id,
                new PartitionKey(_partitionKey)
            );

            return new TransactionResult
            {
                Success = true,
                TransactionId = transactionLog.Id
            };
        }
        catch (Exception ex)
        {
            await RollbackTransaction(transactionLog);
            return new TransactionResult
            {
                Success = false,
                TransactionId = transactionLog.Id,
                Error = ex.Message
            };
        }
    }

    private async Task ExecuteOperation(TransactionOperation operation)
    {
        switch (operation.Type)
        {
            case OperationType.Create:
                await _container.CreateItemAsync(
                    operation.Document,
                    new PartitionKey(operation.Document.PartitionKey)
                );
                break;

            case OperationType.Update:
                await _container.ReplaceItemAsync(
                    operation.Document,
                    operation.Document.Id,
                    new PartitionKey(operation.Document.PartitionKey)
                );
                break;

            case OperationType.Delete:
                await _container.DeleteItemAsync<dynamic>(
                    operation.Document.Id,
                    new PartitionKey(operation.Document.PartitionKey)
                );
                break;

            default:
                throw new ArgumentException($"Unknown operation type: {operation.Type}");
        }
    }

    private TransactionOperation CreateCompensatingOperation(TransactionOperation operation)
    {
        switch (operation.Type)
        {
            case OperationType.Create:
                return new TransactionOperation
                {
                    Type = OperationType.Delete,
                    Document = operation.Document
                };

            case OperationType.Update:
                return new TransactionOperation
                {
                    Type = OperationType.Update,
                    Document = operation.OriginalDocument
                };

            case OperationType.Delete:
                return new TransactionOperation
                {
                    Type = OperationType.Create,
                    Document = operation.Document
                };

            default:
                throw new ArgumentException($"Unknown operation type: {operation.Type}");
        }
    }

    private async Task RollbackTransaction(TransactionLog log)
    {
        log.Status = TransactionStatus.RollingBack;
        await _container.ReplaceItemAsync(
            log,
            log.Id,
            new PartitionKey(_partitionKey)
        );

        foreach (var compensatingOp in log.CompensatingOperations)
        {
            try
            {
                await ExecuteOperation(compensatingOp);
            }
            catch (Exception ex)
            {
                // Log rollback failure but continue with other compensations
                log.RollbackErrors = log.RollbackErrors ?? new List<RollbackError>();
                log.RollbackErrors.Add(new RollbackError
                {
                    Operation = compensatingOp,
                    Error = ex.Message
                });
            }
        }

        log.Status = TransactionStatus.RolledBack;
        await _container.ReplaceItemAsync(
            log,
            log.Id,
            new PartitionKey(_partitionKey)
        );
    }

    public async Task<List<TransactionLog>> RecoverIncompleteTransactions()
    {
        var incompleteTransactions = new List<TransactionLog>();

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = 'transaction_log' AND c.status IN ('PENDING', 'ROLLING_BACK')"
        );

        using (var iterator = _container.GetItemQueryIterator<TransactionLog>(query))
        {
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var log in response)
                {
                    await RecoverTransaction(log);
                    incompleteTransactions.Add(log);
                }
            }
        }

        return incompleteTransactions;
    }

    private async Task RecoverTransaction(TransactionLog log)
    {
        if (log.Status == TransactionStatus.Pending)
        {
            // Resume from last checkpoint
            var remainingOps = log.Operations.Skip(log.LastCheckpoint + 1).ToList();
            
            try
            {
                foreach (var operation in remainingOps)
                {
                    await ExecuteOperation(operation);
                }

                log.Status = TransactionStatus.Committed;
                log.CompletedAt = DateTime.UtcNow;
            }
            catch
            {
                await RollbackTransaction(log);
            }
        }
        else if (log.Status == TransactionStatus.RollingBack)
        {
            await RollbackTransaction(log);
        }

        await _container.ReplaceItemAsync(
            log,
            log.Id,
            new PartitionKey(_partitionKey)
        );
    }
}

public class TransactionLog
{
    public string Id { get; set; }
    public string PartitionKey { get; set; }
    public TransactionStatus Status { get; set; }
    public List<TransactionOperation> Operations { get; set; } = new List<TransactionOperation>();
    public List<TransactionOperation> CompensatingOperations { get; set; } = new List<TransactionOperation>();
    public DateTime Timestamp { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int LastCheckpoint { get; set; }
    public List<RollbackError> RollbackErrors { get; set; }
}

public enum TransactionStatus
{
    Pending,
    Committed,
    RollingBack,
    RolledBack
}

public class TransactionOperation
{
    public OperationType Type { get; set; }
    public dynamic Document { get; set; }
    public dynamic OriginalDocument { get; set; }
}

public enum OperationType
{
    Create,
    Update,
    Delete
}

public class RollbackError
{
    public TransactionOperation Operation { get; set; }
    public string Error { get; set; }
}

public class TransactionResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; }
    public string Error { get; set; }
}
```
