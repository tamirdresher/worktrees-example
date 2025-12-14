using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using NoteTaker.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, etc.)
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddOpenApi();

// Configure PostgreSQL with Aspire (falls back to manual config for Docker)
if (builder.Configuration.GetConnectionString("notetakerdb") != null)
{
    // Aspire mode - use Aspire's PostgreSQL integration
    builder.AddNpgsqlDbContext<AppDbContext>("notetakerdb");
}
else
{
    // Docker mode - use manual configuration
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=postgres;Database=notetakerdb;Username=postgres;Password=postgres";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Configure Redis with Aspire (falls back to manual config for Docker)
if (builder.Configuration.GetConnectionString("cache") != null)
{
    // Aspire mode - use Aspire's Redis integration
    builder.AddRedisClient("cache");
    builder.Services.AddSingleton<RedisCacheService>();
}
else
{
    // Docker mode - use manual configuration
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddSingleton<RedisCacheService>();
}

// Configure RabbitMQ with Aspire (falls back to manual config for Docker)
if (builder.Configuration.GetConnectionString("messaging") != null)
{
    // Aspire mode - use Aspire's RabbitMQ integration
    builder.AddRabbitMQClient("messaging");
    // RabbitMQService will use the injected IConnection from Aspire
    builder.Services.AddSingleton<RabbitMQService>(sp =>
    {
        var connection = sp.GetRequiredService<RabbitMQ.Client.IConnection>();
        var logger = sp.GetRequiredService<ILogger<RabbitMQService>>();
        return new RabbitMQService(connection, logger);
    });
}
else
{
    // Docker mode - use manual configuration
    // RabbitMQService will create its own connection using IConfiguration
    builder.Services.AddSingleton<RabbitMQService>();
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

// Ensure database is created (with retry logic for container startup)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    var maxRetries = 30;
    var delay = TimeSpan.FromSeconds(2);
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            logger.LogInformation("Attempting to connect to database (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);
            db.Database.EnsureCreated();
            logger.LogInformation("Database connection established successfully");
            break;
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            logger.LogWarning(ex, "Failed to connect to database. Retrying in {Delay} seconds...", delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

// Get all tasks (with caching)
app.MapGet("/api/tasks", async (AppDbContext db, RedisCacheService cache) =>
{
    const string cacheKey = "all_tasks";
    
    // Try to get from cache
    var cachedTasks = await cache.GetAsync<List<TaskItem>>(cacheKey);
    if (cachedTasks != null)
    {
        return Results.Ok(new { source = "cache", tasks = cachedTasks });
    }
    
    // Get from database
    var tasks = await db.Tasks.OrderByDescending(t => t.CreatedAt).ToListAsync();
    
    // Cache the results
    await cache.SetAsync(cacheKey, tasks, TimeSpan.FromSeconds(60));
    
    return Results.Ok(new { source = "database", tasks });
})
.WithName("GetAllTasks");

// Get single task
app.MapGet("/api/tasks/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    return task is not null ? Results.Ok(task) : Results.NotFound();
})
.WithName("GetTask");

// Create new task
app.MapPost("/api/tasks", async (TaskItem task, AppDbContext db, RedisCacheService cache, RabbitMQService rabbitMQ, ILogger<Program> logger) =>
{
    task.Id = Guid.NewGuid();
    task.CreatedAt = DateTime.UtcNow;
    task.UpdatedAt = DateTime.UtcNow;
    task.Status = "pending";
    if (task.Description.Contains("fail"))
    {
        logger.LogError("The word 'fail' appeared in the task description, was i supposed to fail?, i think so");
        throw new ArgumentException("why did you sent this value?");
    }
    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    
    // Invalidate cache
    await cache.RemoveAsync("all_tasks");
    
    // Publish to RabbitMQ for AI processing
    rabbitMQ.PublishTaskCreated(task.Id, task.Title, task.Description);
    
    return Results.Created($"/api/tasks/{task.Id}", task);
})
.WithName("CreateTask");

// Update task
app.MapPut("/api/tasks/{id:guid}", async (Guid id, TaskItem updatedTask, AppDbContext db, RedisCacheService cache) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    
    task.Title = updatedTask.Title;
    task.Description = updatedTask.Description;
    task.Status = updatedTask.Status;
    task.UpdatedAt = DateTime.UtcNow;
    
    await db.SaveChangesAsync();
    
    // Invalidate cache
    await cache.RemoveAsync("all_tasks");
    
    return Results.Ok(task);
})
.WithName("UpdateTask");

// Delete task
app.MapDelete("/api/tasks/{id:guid}", async (Guid id, AppDbContext db, RedisCacheService cache) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    
    db.Tasks.Remove(task);
    await db.SaveChangesAsync();
    
    // Invalidate cache
    await cache.RemoveAsync("all_tasks");
    
    return Results.NoContent();
})
.WithName("DeleteTask");

app.MapDefaultEndpoints();

app.Run();
