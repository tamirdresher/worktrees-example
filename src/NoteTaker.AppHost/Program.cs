var builder = DistributedApplication.CreateBuilder(args);

Console.WriteLine("Aspire AppHost Configuration:");
Console.WriteLine($"  ASPNETCORE_URLS: {builder.Configuration["ASPNETCORE_URLS"] ?? "(using defaults)"}");
Console.WriteLine($"  ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL: {builder.Configuration["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"] ?? "(not set)"}");
Console.WriteLine($"  ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL: {builder.Configuration["ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL"] ?? "(not set)"}");
Console.WriteLine($"  ASPIRE_DASHBOARD_MCP_ENDPOINT_URL: {builder.Configuration["ASPIRE_DASHBOARD_MCP_ENDPOINT_URL"] ?? "(not set)"}");
Console.WriteLine($"  ASPIRE_ALLOW_UNSECURED_TRANSPORT: {builder.Configuration["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] ?? "(not set)"}");

var cache = builder.AddRedis("cache");

var db = builder.AddPostgres("db")
    .AddDatabase("notetakerdb");

var messaging = builder.AddRabbitMQ("messaging");

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReference(cache)
    .WithReference(db)
    .WithReference(messaging)
    .WithHttpEndpoint(name: "http")
    .WithExternalHttpEndpoints();

var aiService = builder.AddUvicornApp("ai-service", "../ai-service", "main:app")
    .WithReference(db)
    .WithReference(messaging)
    .WithExternalHttpEndpoints();

builder.AddJavaScriptApp("frontend", "../frontend")
    .WithRunScript("start")
    .WithReference(backend)
    .WithReference(aiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
