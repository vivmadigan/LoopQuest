// The AppHost is the Aspire orchestrator. It is the project you run (F5 / `dotnet run`) during
// development: it starts the supporting containers, launches each service, injects connection
// strings between them, and opens the Aspire dashboard.

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL runs in a container that Aspire manages for you.
//   .WithDataVolume() persists the data across restarts, so your loops/activities survive an F5.
//   Add .WithPgAdmin() (or .WithPgWeb()) here if you want a database admin UI container too.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var loopQuestDb = postgres.AddDatabase("loopquestdb");

// The Web API project.
//   .WithReference(db) injects the "loopquestdb" connection string into the API's configuration.
//   .WaitFor(db) delays the API until Postgres reports healthy, avoiding startup races.
builder.AddProject<Projects.LoopQuest_Api>("api")
    .WithReference(loopQuestDb)
    .WaitFor(loopQuestDb);

builder.Build().Run();
