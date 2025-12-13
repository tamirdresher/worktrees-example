var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var db = builder.AddPostgres("db")
    .AddDatabase("notetakerdb");

var messaging = builder.AddRabbitMQ("messaging");

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReference(cache)
    .WithReference(db)
    .WithReference(messaging);

var aiService = builder.AddPythonApp("ai-service", "../ai-service", "main.py")
    .WithReference(db)
    .WithReference(messaging)
    .WithHttpEndpoint(env: "PORT", port: 8000)
    .WithExternalHttpEndpoints();

builder.AddNpmApp("frontend", "../frontend")
    .WithReference(backend)
    .WithReference(aiService)
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
