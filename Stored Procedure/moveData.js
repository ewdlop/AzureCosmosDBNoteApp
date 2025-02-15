function moveData(targetContainerId, batchSize, queryCondition) {
    var context = getContext();
    var collection = context.getCollection();
    var targetCollection = context.getCollection('/dbs/' + collection.getDatabaseId() + '/colls/' + targetContainerId);
    var response = context.getResponse();
    var collectionLink = collection.getSelfLink();
    var targetCollectionLink = targetCollection.getSelfLink();

    // Initialize response object
    var responseBody = {
        movedCount: 0,
        continuation: true,
        errorMessage: null,
        lastProcessedId: null
    };

    // Validate input parameters
    if (!targetContainerId) {
        responseBody.errorMessage = "Target container ID is required";
        responseBody.continuation = false;
        response.setBody(responseBody);
        return;
    }

    // Set default batch size if not provided
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
                responseBody.errorMessage = "Error querying source container: " + err.message;
                responseBody.continuation = false;
                response.setBody(responseBody);
                return;
            }

            // Check if we have documents to process
            if (documents.length === 0) {
                responseBody.continuation = false;
                response.setBody(responseBody);
                return;
            }

            // Process documents in batch
            processBatch(documents);
        }
    );

    // Return if query was not accepted
    if (!acceptedDocuments) {
        responseBody.errorMessage = "Failed to read documents from source container";
        responseBody.continuation = false;
        response.setBody(responseBody);
        return;
    }

    function processBatch(documents) {
        var pendingDocuments = documents.length;
        var failedDocuments = [];

        // Process each document
        documents.forEach(function(doc) {
            // Create document in target container
            var isAccepted = targetCollection.createDocument(
                targetCollectionLink,
                doc,
                function(err, createdDoc) {
                    pendingDocuments--;

                    if (err) {
                        failedDocuments.push({
                            id: doc.id,
                            error: err.message
                        });
                    } else {
                        responseBody.movedCount++;
                        responseBody.lastProcessedId = doc.id;
                    }

                    // Check if all documents are processed
                    if (pendingDocuments === 0) {
                        finalizeBatch(failedDocuments);
                    }
                }
            );

            // Handle if create operation was not accepted
            if (!isAccepted) {
                pendingDocuments--;
                failedDocuments.push({
                    id: doc.id,
                    error: "Create operation not accepted"
                });

                // Check if all documents are processed
                if (pendingDocuments === 0) {
                    finalizeBatch(failedDocuments);
                }
            }
        });
    }

    function finalizeBatch(failedDocuments) {
        // Add failed documents to response
        if (failedDocuments.length > 0) {
            responseBody.failedDocuments = failedDocuments;
        }

        // Set continuation flag based on processed documents
        responseBody.continuation = responseBody.movedCount === batchSize;

        // Set response body
        response.setBody(responseBody);
    }
}
