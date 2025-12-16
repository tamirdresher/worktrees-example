# Aspire Integration Testing - NoteTaker.Tests

This test project demonstrates the powerful integration testing capabilities of .NET Aspire, showcasing how to test complex distributed applications with real infrastructure.

## 🎯 Key Benefits of Aspire Testing

### 1. **Real Infrastructure Testing**
Unlike traditional unit tests that use mocks, Aspire tests run against **actual containers**:
- ✅ Real PostgreSQL database
- ✅ Real Redis cache
- ✅ Real RabbitMQ message broker
- ✅ Real application services

**Why this matters:** Catch integration issues that mocks can't detect, such as:
- Database schema mismatches
- Connection string problems
- Message serialization issues
- Caching behavior edge cases

### 2. **Automatic Resource Management**
```csharp
public async Task InitializeAsync()
{
    var appHost = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.NoteTaker_AppHost>();
    
    _app = await appHost.BuildAsync();
    await _app.StartAsync(); // Spins up all containers automatically
}
```

Aspire handles:
- 🚀 Starting containers before tests
- 🧹 Cleaning up containers after tests
- 🔄 Resource isolation between test classes
- ⚡ Parallel test execution

### 3. **Service Discovery & Dynamic Ports**
```csharp
// No hardcoded URLs! Aspire discovers the service endpoint dynamically
_httpClient = _app.CreateHttpClient("backend");
```

Benefits:
- Tests work on any machine without configuration
- No port conflicts in CI/CD pipelines
- Services can run on random ports
- True environment parity

### 4. **Complete Application Stack Testing**
Each test spins up the entire distributed application:

```
┌─────────────────────────────────────────────┐
│         Aspire Test Application             │
├─────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │ Backend  │  │ Frontend │  │AI Service│ │
│  │  (.NET)  │  │ (Node.js)│  │ (Python) │ │
│  └────┬─────┘  └──────────┘  └────┬─────┘ │
│       │                            │       │
│  ┌────▼──────┐  ┌──────────┐ ┌────▼─────┐ │
│  │PostgreSQL │  │  Redis   │ │ RabbitMQ │ │
│  │Container  │  │Container │ │Container │ │
│  └───────────┘  └──────────┘ └──────────┘ │
└─────────────────────────────────────────────┘
```

### 5. **Fast Feedback with Parallel Execution**
```bash
# Run all tests in parallel with xUnit
dotnet test --logger "console;verbosity=detailed"
```

- Each test class gets isolated infrastructure
- Tests run concurrently
- Container reuse between tests in same class
- Typical test execution: 5-15 seconds including container startup

## 📊 Test Coverage

### Backend API Tests (`IntegrationTests.cs`)
- ✅ `Backend_HealthEndpoint_ReturnsHealthy` - Verifies service health
- ✅ `CreateTask_StoresInDatabase_AndReturnsCreatedTask` - PostgreSQL integration
- ✅ `GetTasks_AfterCreation_ReturnsTaskFromDatabase` - Data retrieval
- ✅ `GetTasks_SecondRequest_ReturnsFromRedisCache` - Redis caching validation
- ✅ `UpdateTask_InvalidatesCache_AndReturnsUpdatedData` - Cache invalidation
- ✅ `DeleteTask_RemovesFromDatabase_AndInvalidatesCache` - CRUD operations

### Service Integration Tests
- ✅ `CreateTask_TriggersAIAnalysis_ViaRabbitMQ` - **Full end-to-end test**
  - Creates task via REST API
  - Verifies RabbitMQ message publishing
  - Confirms AI service processes message
  - Validates database updates with AI analysis

### Concurrency Tests
- ✅ `MultipleParallelRequests_HandleConcurrency_Correctly` - Load testing

### Frontend UI Tests with Playwright (`PlaywrightIntegrationTests.cs`)
- ✅ `Frontend_CreateTask_AddsItemToList` - **Full end-to-end UI test**
  - Creates task via the frontend UI
  - Verifies task appears in the list
  - Waits for AI analysis to complete
  - Tests the entire user workflow
- ✅ `Frontend_DeleteTask_RemovesItemFromList` - Delete functionality
- ✅ `Frontend_RefreshButton_ReloadsTaskList` - Cache indicator validation
- ✅ `Frontend_FormValidation_RequiresFields` - Form validation

## 🎭 Playwright Integration Testing

The `PlaywrightIntegrationTests.cs` file demonstrates how to combine Aspire distributed testing with Playwright for complete end-to-end UI testing:

```csharp
[Fact]
public async Task Frontend_CreateTask_AddsItemToList()
{
    // Aspire spins up the entire distributed app
    var frontendUrl = _app!.GetEndpoint("frontend").ToString();
    
    // Playwright automates the browser
    var page = await _browser!.NewPageAsync();
    await page.GotoAsync(frontendUrl);
    
    // Fill form and submit
    await page.FillAsync("#title", "Test Task");
    await page.FillAsync("#description", "Test Description");
    await page.ClickAsync("button[type='submit']");
    
    // Verify task appears in the list
    await page.WaitForSelectorAsync(".task-item");
    var taskTitle = await page.Locator(".task-title").TextContentAsync();
    taskTitle.Should().Be("Test Task");
}
```

**Key Features:**
- Tests the actual frontend running in Node.js
- Validates real user interactions and UI behavior
- Verifies the complete flow: UI → Backend → Database → AI Service
- Runs headless Chromium for fast, reliable testing

## 🚀 Running the Tests

### Prerequisites
- .NET 10 SDK
- Docker Desktop running
- Python 3.x (for AI service)
- Playwright browsers installed (automatic on first run)

### First-Time Setup
```bash
cd src/NoteTaker.Tests
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

### Run All Tests
```bash
cd src
dotnet test
```

### Run Specific Test Class
```bash
# Backend API tests only
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Playwright UI tests only
dotnet test --filter "FullyQualifiedName~PlaywrightIntegrationTests"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~CreateTask_TriggersAIAnalysis"
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Watch Mode (Continuous Testing)
```bash
dotnet watch test
```

## 📈 Comparison: Aspire vs. Traditional Testing

| Aspect | Traditional Mocks | Aspire Testing |
|--------|------------------|----------------|
| **Infrastructure** | Fake/Mock objects | Real containers |
| **Setup Complexity** | Manual mock configuration | Automatic via AppHost |
| **Confidence Level** | Medium (mocks may not match reality) | High (tests real behavior) |
| **Debugging** | Limited (can't inspect real DB) | Full (can connect to containers) |
| **CI/CD Integration** | Easy but less reliable | Easy and highly reliable |
| **Test Speed** | Very fast (in-memory) | Fast (container startup ~2-5s) |
| **Maintenance** | Update mocks when APIs change | Self-updating via AppHost |
| **Realistic Scenarios** | Limited | Complete end-to-end flows |

## 🔍 Example: End-to-End Test Walkthrough

```csharp
[Fact]
public async Task CreateTask_TriggersAIAnalysis_ViaRabbitMQ()
{
    // 1. Create a task via REST API
    var createResponse = await _httpClient!.PostAsJsonAsync("/api/tasks", newTask);
    var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>();
    
    // 2. Wait for asynchronous processing
    //    - Backend publishes message to RabbitMQ
    //    - AI Service consumes message
    //    - AI Service analyzes sentiment and category
    //    - AI Service updates PostgreSQL
    await Task.Delay(TimeSpan.FromSeconds(3));
    
    // 3. Verify AI analysis was performed
    var getResponse = await _httpClient!.GetAsync($"/api/tasks/{createdTask!.Id}");
    var analyzedTask = await getResponse.Content.ReadFromJsonAsync<TaskResponse>();
    
    // 4. Assert AI data exists
    analyzedTask!.AiCategory.Should().NotBeNullOrEmpty();
    analyzedTask.AiSentiment.Should().NotBeNullOrEmpty();
}
```

**What makes this powerful:**
- Tests the **actual** RabbitMQ message flow
- Uses **real** Python AI service
- Verifies **actual** database updates
- Catches issues mocks would miss (serialization, timing, connection errors)

## 🎓 Best Practices

### 1. Use `IAsyncLifetime` for Setup/Cleanup
```csharp
public class IntegrationTests : IAsyncLifetime
{
    public async Task InitializeAsync() { /* Start app */ }
    public async Task DisposeAsync() { /* Cleanup */ }
}
```

### 2. Leverage FluentAssertions for Readability
```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
```

### 3. Test End-to-End Scenarios
Don't just test individual services - test the **entire flow** across multiple services.

### 4. Handle Async Operations
```csharp
// For message-based systems, allow time for processing
await Task.Delay(TimeSpan.FromSeconds(3));
```

### 5. Use Meaningful Test Names
```csharp
CreateTask_TriggersAIAnalysis_ViaRabbitMQ() // Clear what's being tested
Frontend_CreateTask_AddsItemToList() // UI test
```

## 🐛 Debugging Tips

### View Container Logs
```bash
docker logs <container-name>
```

### Connect to Test Database
While tests are running (with breakpoint):
```bash
docker ps  # Find PostgreSQL container
docker exec -it <container-id> psql -U postgres -d notetakerdb
```

### Inspect RabbitMQ Management UI
```bash
# RabbitMQ management console typically at:
http://localhost:15672
# Credentials: guest/guest
```

### Debug Playwright Tests
Enable headed mode for visual debugging:
```csharp
_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,  // Shows the browser
    SlowMo = 1000      // Slows down actions by 1 second
});
```

### Enable Detailed Logging
Add to test project:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## 📚 Additional Resources

- [Aspire Testing Documentation](https://aspire.dev/testing/overview/)
- [Write Your First Aspire Test](https://aspire.dev/testing/write-your-first-test/?testing-framework=mstest)
- [Managing App Host in Tests](https://aspire.dev/testing/manage-app-host/?testing-framework=mstest)
- [Accessing Resources in Tests](https://aspire.dev/testing/accessing-resources/)
- [Playwright .NET Documentation](https://playwright.dev/dotnet/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)

## 🎉 Summary

Aspire testing with Playwright provides:
- ✅ **Confidence** - Test against real infrastructure and real browsers
- ✅ **Simplicity** - Automatic resource management
- ✅ **Completeness** - Full stack testing from UI to database
- ✅ **Speed** - Fast feedback with parallel execution
- ✅ **Reliability** - Catch integration issues early
- ✅ **Maintainability** - Self-updating tests via AppHost

**Result:** High-quality, maintainable integration tests that give you confidence your distributed application works correctly from frontend to backend!