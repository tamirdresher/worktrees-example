# Coding Standards & Best Practices

## General Principles

### SOLID Principles
- **Single Responsibility**: Each class/endpoint should have one reason to change
- **Open/Closed**: Open for extension, closed for modification
- **Liskov Substitution**: Derived classes must be substitutable for base classes
- **Interface Segregation**: Many specific interfaces better than one general
- **Dependency Inversion**: Depend on abstractions, not concretions

### Clean Code Practices
- Use meaningful, descriptive names for variables, methods, and classes
- Keep methods short and focused (ideally < 20 lines)
- Follow the Boy Scout Rule: Leave code cleaner than you found it
- Write self-documenting code; comments explain "why", not "what"
- Avoid magic numbers and strings - use constants or enums

## C# Specific Guidelines

### Naming Conventions
- **Classes**: PascalCase (e.g., `TaskItem`)
- **Interfaces**: PascalCase with 'I' prefix (e.g., `ITaskRepository`)
- **Methods**: PascalCase (e.g., `GetTasksAsync`)
- **Properties**: PascalCase (e.g., `CreatedAt`)
- **Private Fields**: camelCase with underscore prefix (e.g., `_logger`)
- **Local Variables**: camelCase (e.g., `taskId`)
- **Constants**: PascalCase (e.g., `CacheKeys.AllTasks`)
- **Enums**: PascalCase for type and values (e.g., `TaskStatus.Pending`)

### Modern C# Features
- Use **nullable reference types** (`string?` for nullable strings)
- Use **pattern matching** for type checks and null checks
- Leverage **async/await** for all I/O operations
- Use **LINQ** for collection operations
- Prefer **expression-bodied members** for simple properties/methods
- Use **init** accessors for immutable properties

### Error Handling
- Use specific exception types
- Provide meaningful error messages
- Include context in exceptions
- Log exceptions with appropriate severity levels

## Project-Specific Patterns

### Minimal API Pattern
```csharp
app.MapGet("/api/tasks", async (AppDbContext db, RedisCacheService cache) =>
{
    // Implementation
})
.WithName("GetAllTasks")
.WithOpenApi();
```

### EF Core Entity Pattern
```csharp
public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Service Pattern
```csharp
public class RedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        // Implementation
    }
}
```

## Dependency Injection

### Registration Patterns
```csharp
// Scoped (per request)
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

// Singleton (shared across application)
builder.Services.AddSingleton<RedisCacheService>();

// Transient (new instance each time)
builder.Services.AddTransient<TaskValidator>();
```

### Constructor Injection
```csharp
public class TaskService
{
    private readonly ITaskRepository _repository;
    private readonly ILogger<TaskService> _logger;
    
    public TaskService(
        ITaskRepository repository,
        ILogger<TaskService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}
```

## Testing Standards

### Required Testing Coverage
- All code changes MUST include comprehensive unit tests
- All code MUST build without errors or warnings
- All unit tests MUST pass
- Unit tests MUST verify:
  - Core functionality and business logic
  - Edge cases and error conditions
  - Integration points with external systems
  - Error handling and recovery paths
  - Async/await behavior

### Build Verification Requirements
- Code MUST compile without errors
- Code MUST compile without warnings
- No technical debt warnings allowed

### Test Coverage Guidelines
- Cover all public methods and classes
- Test both successful and failure paths
- Verify boundary conditions
- Mock external dependencies
- Test async behavior

### Unit Test Structure
```csharp
public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _mockRepo;
    private readonly TaskService _sut;
    
    public TaskServiceTests()
    {
        _mockRepo = new Mock<ITaskRepository>();
        _sut = new TaskService(_mockRepo.Object);
    }

    [Fact]
    public async Task CreateTaskAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var task = new TaskItem { Title = "Test Task" };
        
        // Act
        var result = await _sut.CreateTaskAsync(task);
        
        // Assert
        result.Should().NotBeNull();
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Once);
    }
}
```

### Assertion Libraries
- Use **FluentAssertions** for readable assertions
- Examples:
  ```csharp
  result.Should().NotBeNull();
  result.IsSuccess.Should().BeTrue();
  ```

## Async/Await Guidelines

### Always Use CancellationToken
```csharp
public async Task<List<TaskItem>> GetTasksAsync(CancellationToken ct = default)
{
    return await _db.Tasks.ToListAsync(ct);
}
```

### Avoid Async Void
```csharp
// ❌ Bad
public async void ProcessData() { }

// ✅ Good
public async Task ProcessDataAsync() { }
```

## Logging Best Practices

### Structured Logging
```csharp
// ✅ Good - Structured logging
_logger.LogInformation("Processing task {TaskId}", taskId);

// ❌ Bad - String interpolation
_logger.LogInformation($"Processing task {taskId}");
```

### Log Levels
- **Trace**: Very detailed logs for debugging
- **Debug**: Detailed information for development
- **Information**: General information about application flow
- **Warning**: Unexpected events that don't prevent operation
- **Error**: Errors and exceptions
- **Critical**: Critical errors that require immediate attention

## API Design Guidelines

### RESTful Endpoints
- Use proper HTTP verbs (GET, POST, PUT, DELETE)
- Use plural nouns for collections (`/api/tasks`, not `/api/task`)
- Return appropriate status codes (200, 201, 204, 400, 404, 500)

### Request/Response DTOs
```csharp
public record CreateTaskRequest(string Title, string Description);

public record TaskResponse(Guid Id, string Title, string Status, DateTime CreatedAt);
```

## Documentation Standards

### XML Documentation
```csharp
/// <summary>
/// Retrieves a task by its unique identifier.
/// </summary>
/// <param name="id">The task ID.</param>
/// <returns>The task if found, otherwise null.</returns>
public async Task<TaskItem?> GetTaskAsync(Guid id)
{
    // Implementation
}