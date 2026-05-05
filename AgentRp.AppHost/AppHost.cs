var builder = DistributedApplication.CreateBuilder(args);

var app = builder.AddProject<Projects.AgentRp>("app")
    .WithExternalHttpEndpoints();

var database = builder.AddAzureSqlServer("sql")
    .RunAsContainer(c => c
        .WithContainerName("azure-sql-edge")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume()
        .WithContainerRuntimeArgs("-e", "ACCEPT_EULA=1", "--cap-add", "SYS_PTRACE")
        .WithImage("azure-sql-edge"))
    .AddDatabase("db", "agentrp2");

app.WaitFor(database).WithReference(database);

builder.Build().Run();
