function updateEmperorAchievement(emperorId, newAchievement) {
    const collection = getContext().getCollection();
    const response = getContext().getResponse();

    // Query to find the emperor document
    const query = `SELECT * FROM c WHERE c.id = "${emperorId}"`;

    // Execute the query
    const isAccepted = collection.queryDocuments(
        collection.getSelfLink(),
        query,
        function(err, documents) {
            if (err) throw err;
            if (!documents || documents.length === 0) {
                response.setBody("Emperor not found");
                return;
            }

            const emperor = documents[0];
            emperor.culturalAchievements.majorProjects.push(newAchievement);

            // Replace the document
            collection.replaceDocument(
                documents[0]._self,
                emperor,
                function(err, replacedDoc) {
                    if (err) throw err;
                    response.setBody("Achievement added successfully");
                }
            );
        }
    );

    if (!isAccepted) throw new Error("The query was not accepted by the server.");
}
