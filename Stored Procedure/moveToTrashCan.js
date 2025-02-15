function moveToTrashCan(trashCanContainerId, batchSize, queryCondition) {
    var context = getContext();
    var collection = context.getCollection();
    var trashCollection = context.getCollection('/dbs/' + collection.getDatabaseId() + '/colls/' + trashCanContainerId);
    var response = context.getResponse();
    var collectionLink = collection.getSelfLink();
    var trashCollectionLink = trashCollection.getSelfLink();

    // Response object
    var responseBody = {
        movedCount: 0,
        continuation: true,
        errorMessage: null,
        lastProcessedId: null
    };

    // Validate input
    if (!trashCanContainerId) {
        responseBody.errorMessage = "Trash can container ID is required";
        responseBody.continuation = false;
        response.setBody(responseBody);
        return;
    }

    // Set default batch size
    batchSize = batchSize || 100;

    // Build query
    var query = 'SELECT * FROM c';
    if (queryCondition) {
        query += ' WHERE ' + queryCondition;
    }
    query += ' OFFSET ' + responseBody.movedCount + ' LIMIT ' + batchSize;

    // Read documents to move
    var acceptedDocuments = collection.queryDocuments(
        collectionLink,
        query,
        {},
        function(err, documents, responseOptions) {
            if (err) {
                responseBody.errorMessage = "Error querying source: " + err.message;
                responseBody.continuation = false;
                response.setBody(responseBody);
                return;
            }

            if (documents.length === 0) {
                responseBody.continuation = false;
                response.setBody(responseBody);
                return;
            }

            processBatch(documents);
        }
    );

    if (!acceptedDocuments) {
        responseBody.errorMessage = "Failed to read documents";
        responseBody.continuation = false;
        response.setBody(responseBody);
        return;
    }

    function processBatch(documents) {
        var pendingDocuments = documents.length;
        var failedDocuments = [];

        documents.forEach(function(doc) {
            // Add trash can metadata
            var trashDoc = JSON.parse(JSON.stringify(doc)); // Deep copy
            trashDoc._ts_deleted = new Date().toISOString();
            trashDoc._originalId = doc.id;
            trashDoc._originalPartitionKey = doc[collection.getPartitionKey().paths[0]];
            trashDoc._sourceContainer = collection.getId();
            trashDoc._deletedBy = context.getUserContext().userToken || 'system';
            
            // Create in trash can
            var isAccepted = trashCollection.createDocument(
                trashCollectionLink,
                trashDoc,
                function(err, createdDoc) {
                    if (err) {
                        pendingDocuments--;
                        failedDocuments.push({
                            id: doc.id,
                            error: "Failed to create in trash: " + err.message
                        });
                        checkCompletion();
                        return;
                    }

                    // Delete from source after successful move to trash
                    var isDeleteAccepted = collection.deleteDocument(
                        doc._self,
                        {},
                        function(deleteErr) {
                            pendingDocuments--;
                            
                            if (deleteErr) {
                                failedDocuments.push({
                                    id: doc.id,
                                    error: "Moved to trash but failed to delete source: " + deleteErr.message
                                });
                            } else {
                                responseBody.movedCount++;
                                responseBody.lastProcessedId = doc.id;
                            }
                            
                            checkCompletion();
                        }
                    );

                    if (!isDeleteAccepted) {
                        pendingDocuments--;
                        failedDocuments.push({
                            id: doc.id,
                            error: "Delete operation not accepted"
                        });
                        checkCompletion();
                    }
                }
            );

            if (!isAccepted) {
                pendingDocuments--;
                failedDocuments.push({
                    id: doc.id,
                    error: "Create in trash not accepted"
                });
                checkCompletion();
            }
        });
    }

    function checkCompletion() {
        if (pendingDocuments === 0) {
            finalizeBatch(failedDocuments);
        }
    }

    function finalizeBatch(failedDocuments) {
        if (failedDocuments.length > 0) {
            responseBody.failedDocuments = failedDocuments;
        }

        responseBody.continuation = responseBody.movedCount === batchSize;
        response.setBody(responseBody);
    }
}
