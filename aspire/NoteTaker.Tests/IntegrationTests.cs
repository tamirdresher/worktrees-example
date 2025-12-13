using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace NoteTaker.Tests;

/// <summary>
/// Integration tests demonstrating Aspire's testing capabilities with real infrastructure.
/// These tests spin up actual PostgreSQL, Redis, and RabbitMQ containers.
/// </summary>
[Collection("Sequential")]
public class IntegrationTests : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        // Create a test application builder - this spins up the entire distributed app
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NoteTaker_AppHost>();

        // Build and start the application with all its dependencies
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get HTTP client for the backend service
        _httpClient = _app.CreateHttpClient("backend");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task Backend_HealthEndpoint_ReturnsHealthy()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        health.Should().NotBeNull();
        health!.Status.Should().Be("healthy");
        health.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateTask_StoresInDatabase_AndReturnsCreatedTask()
    {
        // Arrange
        var newTask = new
        {
            title = "Test Integration Task",
            description = "This task tests the full integration with PostgreSQL"
        };

        // Act
        var response = await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdTask = await response.Content.ReadFromJsonAsync<TaskResponse>();
        createdTask.Should().NotBeNull();
        createdTask!.Id.Should().NotBeEmpty();
        createdTask.Title.Should().Be(newTask.title);
        createdTask.Description.Should().Be(newTask.description);
        createdTask.Status.Should().Be("pending");
        createdTask.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetTasks_AfterCreation_ReturnsTaskFromDatabase()
    {
        // Arrange - Create a task first
        var newTask = new
        {
            title = "Retrieve Test Task",
            description = "Testing task retrieval from PostgreSQL"
        };
        
        var createResponse = await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);
        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>();

        // Act - Retrieve all tasks
        var getResponse = await _httpClient!.GetAsync("/api/tasks");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await getResponse.Content.ReadFromJsonAsync<TaskListResponse>();
        result.Should().NotBeNull();
        result!.Source.Should().Be("database"); // First request comes from database
        result.Tasks.Should().Contain(t => t.Id == createdTask!.Id);
    }

    [Fact]
    public async Task GetTasks_SecondRequest_ReturnsFromRedisCache()
    {
        // Arrange - Create a task and fetch it once to populate cache
        var newTask = new
        {
            title = "Cache Test Task",
            description = "Testing Redis caching functionality"
        };
        
        await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);
        await _httpClient!.GetAsync("/api/tasks"); // First request - populates cache

        // Act - Second request should come from cache
        var response = await _httpClient!.GetAsync("/api/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<TaskListResponse>();
        result.Should().NotBeNull();
        result!.Source.Should().Be("cache"); // Second request comes from Redis cache
    }

    [Fact]
    public async Task CreateTask_TriggersAIAnalysis_ViaRabbitMQ()
    {
        // Arrange
        var newTask = new
        {
            title = "AI Analysis Test",
            description = "This is an amazing and wonderful task that should be analyzed positively"
        };

        // Act
        var createResponse = await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);
        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>();
        
        // Wait for AI service to process the message from RabbitMQ
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Fetch the task again to see AI analysis results
        var getResponse = await _httpClient!.GetAsync($"/api/tasks/{createdTask!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var analyzedTask = await getResponse.Content.ReadFromJsonAsync<TaskResponse>();
        analyzedTask.Should().NotBeNull();
        analyzedTask!.AiCategory.Should().NotBeNullOrEmpty();
        analyzedTask.AiSentiment.Should().NotBeNullOrEmpty();
        analyzedTask.AiAnalyzedAt.Should().NotBeNull();
        analyzedTask.AiAnalyzedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdateTask_InvalidatesCache_AndReturnsUpdatedData()
    {
        // Arrange - Create task and populate cache
        var newTask = new
        {
            title = "Update Test Task",
            description = "Original description"
        };
        
        var createResponse = await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);
        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>();
        await _httpClient!.GetAsync("/api/tasks"); // Populate cache

        // Act - Update the task
        var updatedTask = new
        {
            title = "Updated Title",
            description = "Updated description",
            status = "completed"
        };
        
        var updateResponse = await _httpClient!.PutAsJsonAsync($"/api/tasks/{createdTask!.Id}", updatedTask);

        // Get tasks again - should come from database since cache was invalidated
        var getResponse = await _httpClient!.GetAsync("/api/tasks");

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await getResponse.Content.ReadFromJsonAsync<TaskListResponse>();
        result!.Source.Should().Be("database"); // Cache was invalidated
        result.Tasks.Should().Contain(t => 
            t.Id == createdTask.Id && 
            t.Title == updatedTask.title &&
            t.Status == updatedTask.status);
    }

    [Fact]
    public async Task DeleteTask_RemovesFromDatabase_AndInvalidatesCache()
    {
        // Arrange
        var newTask = new
        {
            title = "Delete Test Task",
            description = "This task will be deleted"
        };
        
        var createResponse = await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);
        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>();

        // Act
        var deleteResponse = await _httpClient!.DeleteAsync($"/api/tasks/{createdTask!.Id}");

        // Get the specific task - should return NotFound
        var getResponse = await _httpClient!.GetAsync($"/api/tasks/{createdTask.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleParallelRequests_HandleConcurrency_Correctly()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 5).Select(i => new
        {
            title = $"Concurrent Task {i}",
            description = $"Testing concurrent operations {i}"
        }).ToList();

        // Act - Create multiple tasks in parallel
        var createTasks = tasks.Select(t => 
            _httpClient!.PostAsJsonAsync("/api/tasks", t)).ToList();
        
        var responses = await Task.WhenAll(createTasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));

        // Verify all tasks are in the database
        var getResponse = await _httpClient!.GetAsync("/api/tasks");
        var result = await getResponse.Content.ReadFromJsonAsync<TaskListResponse>();
        
        result!.Tasks.Count.Should().BeGreaterOrEqualTo(5);
    }
}

// Response models for deserialization
public record HealthResponse(string Status, DateTime Timestamp);

public record TaskResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? AiCategory,
    string? AiSentiment,
    DateTime? AiAnalyzedAt);

public record TaskListResponse(string Source, List<TaskResponse> Tasks);