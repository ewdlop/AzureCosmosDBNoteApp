const { CosmosClient } = require("@azure/cosmos");

// Configuration
const config = {
    endpoint: "YOUR_COSMOS_DB_ENDPOINT",
    key: "YOUR_COSMOS_DB_KEY",
    databaseId: "RomanEmpireDB",
    containerId: "EmperorsContainer"
};

async function createDatabase() {
    const client = new CosmosClient({
        endpoint: config.endpoint,
        key: config.key
    });

    const { database } = await client.databases.createIfNotExists({
        id: config.databaseId
    });
    console.log(`Database ${config.databaseId} created or already exists`);
    return database;
}

async function createContainer(database) {
    const containerDefinition = {
        id: config.containerId,
        partitionKey: {
            paths: ["/dynasty"]
        },
        indexingPolicy: {
            includedPaths: [
                {
                    path: "/*"
                }
            ],
            excludedPaths: [
                {
                    path: '/"_etag"/?'
                }
            ]
        }
    };

    const { container } = await database.containers.createIfNotExists(containerDefinition);
    console.log(`Container ${config.containerId} created or already exists`);
    return container;
}

// Enhanced sample data structure for an emperor
const sampleEmperor = {
    id: "augustus",
    personalInfo: {
        name: {
            birthName: "Gaius Octavius",
            regналName: "Augustus",
            commonNames: ["Octavian", "Augustus Caesar"],
            posthumousName: "Divus Augustus"
        },
        birth: {
            date: "23 September 63 BC",
            place: "Rome, Roman Republic",
            family: {
                father: "Gaius Octavius",
                mother: "Atia Balba Caesonia",
                adoptiveFather: "Julius Caesar"
            }
        },
        death: {
            date: "19 August 14 AD",
            place: "Nola, Italia",
            age: 75,
            cause: "Natural causes",
            burial: "Mausoleum of Augustus"
        },
        marriages: [
            {
                spouse: "Clodia Pulchra",
                period: "43-40 BC",
                reasonForEnd: "Divorce",
                children: []
            },
            {
                spouse: "Scribonia",
                period: "40-38 BC",
                reasonForEnd: "Divorce",
                children: ["Julia the Elder"]
            },
            {
                spouse: "Livia Drusilla",
                period: "38 BC - 14 AD",
                reasonForEnd: "Death of Augustus",
                children: []
            }
        ]
    },
    reign: {
        dynasty: "Julio-Claudian",
        period: {
            start: "27 BC",
            end: "14 AD",
            duration: "40 years",
            predecessors: ["Julius Caesar", "Roman Republic"],
            successor: "Tiberius"
        },
        titles: [
            {
                title: "Princeps Civitatis",
                meaning: "First Citizen",
                dateGranted: "27 BC"
            },
            {
                title: "Pontifex Maximus",
                meaning: "Chief Priest",
                dateGranted: "12 BC"
            },
            {
                title: "Augustus",
                meaning: "The Revered One",
                dateGranted: "27 BC"
            }
        ],
        capitals: [
            {
                city: "Rome",
                period: "Entire reign",
                significance: "Primary capital"
            }
        ]
    },
    military: {
        campaigns: [
            {
                name: "Civil War against Mark Antony",
                period: "32-30 BC",
                outcome: "Victory",
                majorBattles: ["Battle of Actium"]
            }
        ],
        reforms: [
            {
                type: "Military",
                description: "Created the Praetorian Guard",
                date: "27 BC"
            },
            {
                type: "Naval",
                description: "Established permanent naval bases",
                locations: ["Misenum", "Ravenna"]
            }
        ]
    },
    administration: {
        politicalReforms: [
            {
                name: "Constitutional Settlement",
                date: "27 BC",
                description: "Established the Principate"
            }
        ],
        economicReforms: [
            {
                type: "Currency",
                description: "Established imperial mint",
                date: "15 BC"
            },
            {
                type: "Taxation",
                description: "Reformed tax collection system",
                date: "6 AD"
            }
        ],
        construction: [
            {
                name: "Temple of Apollo",
                location: "Palatine Hill",
                date: "28 BC",
                type: "Religious"
            },
            {
                name: "Forum of Augustus",
                location: "Rome",
                date: "2 BC",
                type: "Civic"
            }
        ]
    },
    territorialExpansion: {
        provincesAdded: [
            {
                name: "Egypt",
                yearAcquired: "30 BC",
                method: "Conquest",
                previousRuler: "Ptolemaic Dynasty"
            },
            {
                name: "Pannonia",
                yearAcquired: "10 AD",
                method: "Conquest",
                strategicValue: "Danube frontier defense"
            }
        ],
        totalProvinces: 40,
        majorTerritories: ["Italia", "Gaul", "Hispania", "Egypt", "Asia Minor"]
    },
    culturalAchievements: {
        literature: {
            patronage: ["Virgil", "Horace", "Livy"],
            majorWorks: ["Aeneid", "Res Gestae Divi Augusti"]
        },
        architecture: {
            motto: "Found Rome a city of brick and left it a city of marble",
            majorProjects: [
                "Temple of Apollo Palatinus",
                "Forum of Augustus",
                "Ara Pacis"
            ]
        },
        religion: {
            reforms: [
                "Revival of traditional Roman religion",
                "Establishment of imperial cult"
            ],
            temples: ["Temple of Apollo", "Temple of Mars Ultor"]
        }
    },
    legacy: {
        immediateSuccession: {
            heir: "Tiberius",
            relationship: "Stepson",
            preparationForPower: ["Military commands", "Tribune power"]
        },
        historicalAssessment: {
            positives: [
                "Established lasting imperial system",
                "Brought peace after civil wars",
                "Cultural golden age"
            ],
            negatives: [
                "End of Republican freedom",
                "Dynasty problems",
                "Exile of daughter Julia"
            ]
        },
        longTermImpact: [
            "Title 'Augustus' used by all subsequent emperors",
            "Created model for imperial administration",
            "Established Pax Romana"
        ]
    },
    sources: {
        primary: [
            "Res Gestae Divi Augusti",
            "Suetonius's Lives of the Twelve Caesars",
            "Tacitus's Annals"
        ],
        archaeological: [
            "Ara Pacis",
            "Forum of Augustus",
            "Prima Porta statue"
        ],
        numismatic: [
            "Aureus coins",
            "Denarii with portraits"
        ]
    },
    metadata: {
        lastUpdated: new Date().toISOString(),
        version: "1.0",
        categories: ["Emperor", "Julio-Claudian", "First Emperor"],
        tags: ["Founder", "Military Leader", "Reformer"]
    }
};

async function initializeCosmosDB() {
    try {
        const database = await createDatabase();
        const container = await createContainer(database);
        
        // Create sample document
        const { resource } = await container.items.create(sampleEmperor);
        console.log(`Created sample emperor document with id: ${resource.id}`);
        
        return container;
    } catch (error) {
        console.error("Error initializing Cosmos DB:", error);
        throw error;
    }
}

// Execute the setup
initializeCosmosDB().catch(error => {
    console.error("Setup failed:", error);
    process.exit(1);
});
