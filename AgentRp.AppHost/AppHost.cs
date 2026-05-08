var builder = DistributedApplication.CreateBuilder(args);

var tinifyApiKey = builder.AddParameter("tinify-api-key", secret: true);
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithDataVolume());
var blobs = storage.AddBlobs("blobs");

var app = builder.AddProject<Projects.AgentRp>("app")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Tinify__ApiKey", tinifyApiKey);

var database = builder.AddAzureSqlServer("sql")
    .RunAsContainer(c => c
        .WithContainerName("azure-sql-edge")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume()
        .WithContainerRuntimeArgs("-e", "ACCEPT_EULA=1", "--cap-add", "SYS_PTRACE")
        .WithImage("azure-sql-edge"))
    .AddDatabase("db", "agentrp2");

app.WaitFor(database)
    .WithReference(database)
    .WaitFor(blobs)
    .WithReference(blobs);

builder.Build().Run();
