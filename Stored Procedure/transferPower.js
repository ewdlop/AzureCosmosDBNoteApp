function transferPower(fromEmperorId, toEmperorId, date) {
    const collection = getContext().getCollection();
    const response = getContext().getResponse();

    // Perform the transfer in a transaction
    const sproc = function() {
        const fromQuery = `SELECT * FROM c WHERE c.id = "${fromEmperorId}"`;
        const toQuery = `SELECT * FROM c WHERE c.id = "${toEmperorId}"`;

        collection.queryDocuments(
            collection.getSelfLink(),
            fromQuery,
            function(err, predecessors) {
                if (err) throw err;
                if (!predecessors || predecessors.length === 0) {
                    throw new Error("Predecessor emperor not found");
                }

                collection.queryDocuments(
                    collection.getSelfLink(),
                    toQuery,
                    function(err, successors) {
                        if (err) throw err;
                        if (!successors || successors.length === 0) {
                            throw new Error("Successor emperor not found");
                        }

                        const predecessor = predecessors[0];
                        const successor = successors[0];

                        // Update predecessor's end date
                        predecessor.reign.period.end = date;

                        // Update successor's start date
                        successor.reign.period.start = date;

                        // Update in transaction
                        collection.replaceDocument(
                            predecessor._self,
                            predecessor,
                            function(err) {
                                if (err) throw err;
                                collection.replaceDocument(
                                    successor._self,
                                    successor,
                                    function(err) {
                                        if (err) throw err;
                                        response.setBody("Power transfer completed");
                                    }
                                );
                            }
                        );
                    }
                );
            }
        );
    };

    const isAccepted = collection.execute(sproc);
    if (!isAccepted) throw new Error("The transaction was not accepted by the server.");
}

// 3. Join Queries Examples
