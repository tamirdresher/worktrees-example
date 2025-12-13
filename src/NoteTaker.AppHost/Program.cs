var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var db = builder.AddPostgres("db")
    .AddDatabase("notetakerdb");

var messaging = builder.AddRabbitMQ("messaging");

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReference(cache)
    .WithReference(db)
    .WithReference(messaging);

var aiService = builder.AddUvicornApp("ai-service", "../ai-service", "main:app")
    .WithReference(db)
    .WithReference(messaging)
    .WithExternalHttpEndpoints();

builder.AddJavaScriptApp("frontend", "../frontend")
    .WithRunScript("start")
    .WithReference(backend)
    .WithReference(aiService)
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
