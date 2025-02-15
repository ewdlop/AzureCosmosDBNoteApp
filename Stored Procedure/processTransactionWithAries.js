function processTransactionWithAries(operations) {
    var context = getContext();
    var container = context.getCollection();
    var response = context.getResponse();

    // Validate input
    if (!operations || !Array.isArray(operations)) {
        throw new Error("Operations array is required");
    }

    // Transaction Log structure
    var transactionLog = {
        id: new Date().toISOString() + "_" + context.getRequest().getUserContext().userId,
        type: "transaction_log",
        status: "PENDING",
        operations: operations,
        timestamp: new Date().toISOString(),
        lastCheckpoint: null,
        compensatingOperations: []
    };

    // Phase 1: Write Ahead Logging
    var isAccepted = container.createDocument(
        container.getSelfLink(),
        transactionLog,
        function(err, createdLog) {
            if (err) throw new Error("Failed to create transaction log: " + err.message);

            // Phase 2: Execute Operations
            executeOperations(createdLog, operations, 0);
        }
    );

    if (!isAccepted) throw new Error("Failed to accept log creation");

    function executeOperations(log, ops, index) {
        if (index >= ops.length) {
            // All operations completed successfully
            finalizeTransaction(log);
            return;
        }

        var currentOp = ops[index];
        var compensatingOp = createCompensatingOperation(currentOp);
        
        // Update checkpoint before operation
        log.lastCheckpoint = index;
        var isUpdateAccepted = container.replaceDocument(
            log._self,
            log,
            function(err) {
                if (err) {
                    rollbackTransaction(log);
                    return;
                }

                // Execute the actual operation
                executeOperation(currentOp, function(err) {
                    if (err) {
                        rollbackTransaction(log);
                        return;
                    }

                    // Add compensating operation to log
                    log.compensatingOperations.unshift(compensatingOp);
                    
                    // Move to next operation
                    executeOperations(log, ops, index + 1);
                });
            }
        );

        if (!isUpdateAccepted) rollbackTransaction(log);
    }

    function executeOperation(op, callback) {
        switch(op.type) {
            case "CREATE":
                container.createDocument(
                    container.getSelfLink(),
                    op.document,
                    callback
                );
                break;
                
            case "UPDATE":
                container.replaceDocument(
                    op.documentLink,
                    op.document,
                    callback
                );
                break;
                
            case "DELETE":
                container.deleteDocument(
                    op.documentLink,
                    callback
                );
                break;
                
            default:
                callback(new Error("Unknown operation type"));
        }
    }

    function createCompensatingOperation(op) {
        switch(op.type) {
            case "CREATE":
                return {
                    type: "DELETE",
                    documentLink: op.document._self
                };
                
            case "UPDATE":
                return {
                    type: "UPDATE",
                    documentLink: op.documentLink,
                    document: op.originalDocument
                };
                
            case "DELETE":
                return {
                    type: "CREATE",
                    document: op.originalDocument
                };
                
            default:
                throw new Error("Unknown operation type");
        }
    }

    function rollbackTransaction(log) {
        log.status = "ROLLING_BACK";
        container.replaceDocument(
            log._self,
            log,
            function(err) {
                if (err) throw new Error("Failed to update log status for rollback");

                // Execute compensating operations
                executeCompensatingOperations(log, 0);
            }
        );
    }

    function executeCompensatingOperations(log, index) {
        if (index >= log.compensatingOperations.length) {
            // Rollback completed
            log.status = "ROLLED_BACK";
            container.replaceDocument(log._self, log);
            return;
        }

        var compensatingOp = log.compensatingOperations[index];
        executeOperation(compensatingOp, function(err) {
            if (err) {
                // Log rollback failure but continue with other compensations
                log.rollbackErrors = log.rollbackErrors || [];
                log.rollbackErrors.push({
                    operation: compensatingOp,
                    error: err.message
                });
            }
            executeCompensatingOperations(log, index + 1);
        });
    }

    function finalizeTransaction(log) {
        log.status = "COMMITTED";
        log.completedAt = new Date().toISOString();
        
        container.replaceDocument(
            log._self,
            log,
            function(err) {
                if (err) throw new Error("Failed to finalize transaction");
                
                response.setBody({
                    status: "success",
                    transactionId: log.id
                });
            }
        );
    }
}
