// 1. Stored Procedure to Update Emperor's Achievement
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

// 2. Stored Procedure to Transfer Power Between Emperors
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
// These would be executed from your application code:

// Join emperors with their construction projects
const joinConstructionQuery = `
    SELECT e.personalInfo.name.regналName as Emperor,
           c.name as Construction,
           c.date,
           c.type
    FROM EmperorsContainer e
    JOIN c IN e.administration.construction
    WHERE e.reign.dynasty = 'Julio-Claudian'
`;

// Join emperors with their military campaigns
const joinMilitaryQuery = `
    SELECT e.personalInfo.name.regналName as Emperor,
           m.name as Campaign,
           m.period,
           m.outcome,
           b as Battle
    FROM EmperorsContainer e
    JOIN m IN e.military.campaigns
    JOIN b IN m.majorBattles
    WHERE e.reign.period.start >= '0 AD'
`;

// Join emperors with their provinces
const joinProvincesQuery = `
    SELECT e.personalInfo.name.regналName as Emperor,
           p.name as Province,
           p.yearAcquired,
           p.method
    FROM EmperorsContainer e
    JOIN p IN e.territorialExpansion.provincesAdded
    WHERE p.method = 'Conquest'
`;

// Example of using these queries in your application:
const { CosmosClient } = require("@azure/cosmos");

async function executeJoinQueries(client) {
    const database = client.database("RomanEmpireDB");
    const container = database.container("EmperorsContainer");

    // Execute construction projects query
    const constructionResults = await container.items
        .query(joinConstructionQuery)
        .fetchAll();
    
    // Execute military campaigns query
    const militaryResults = await container.items
        .query(joinMilitaryQuery)
        .fetchAll();
    
    // Execute provinces query
    const provinceResults = await container.items
        .query(joinProvincesQuery)
        .fetchAll();

    return {
        constructions: constructionResults.resources,
        military: militaryResults.resources,
        provinces: provinceResults.resources
    };
}

// Function to register stored procedures
async function registerStoredProcedures(client) {
    const database = client.database("RomanEmpireDB");
    const container = database.container("EmperorsContainer");

    // Register updateEmperorAchievement
    await container.scripts.storedProcedures.create({
        id: "updateEmperorAchievement",
        body: updateEmperorAchievement.toString()
    });

    // Register transferPower
    await container.scripts.storedProcedures.create({
        id: "transferPower",
        body: transferPower.toString()
    });
}

// Example usage:
async function example() {
    const client = new CosmosClient({
        endpoint: "YOUR_COSMOS_DB_ENDPOINT",
        key: "YOUR_COSMOS_DB_KEY"
    });

    // Register stored procedures
    await registerStoredProcedures(client);

    // Execute stored procedure to add achievement
    const container = client.database("RomanEmpireDB").container("EmperorsContainer");
    await container.scripts.storedProcedure("updateEmperorAchievement")
        .execute("augustus", ["Built the Pantheon"]);

    // Execute stored procedure to transfer power
    await container.scripts.storedProcedure("transferPower")
        .execute("augustus", "tiberius", "14 AD");

    // Execute join queries
    const queryResults = await executeJoinQueries(client);
    console.log(queryResults);
}
