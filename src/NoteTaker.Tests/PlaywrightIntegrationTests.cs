using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

namespace NoteTaker.Tests;

/// <summary>
/// Integration tests using Playwright to test the frontend UI end-to-end.
/// Tests spin up the entire distributed application including frontend, backend, and all dependencies.
/// </summary>
[Collection("Sequential")]
public class PlaywrightIntegrationTests : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        // Create and start the distributed application with all its services
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NoteTaker_AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for the frontend service to be healthy
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("frontend");

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
        
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Frontend_CreateTask_AddsItemToList()
    {
        // Arrange - Get the frontend URL and create a new browser context
        var frontendUrl = _app!.GetEndpoint("frontend").ToString().TrimEnd('/');
        var context = await _browser!.NewContextAsync();
        context.SetDefaultNavigationTimeout(60000);
        var page = await context.NewPageAsync();

        try
        {
            // Navigate to the frontend
            await page.GotoAsync(frontendUrl);

            // Wait for the page to load
            await page.WaitForSelectorAsync("h1");
            var title = await page.Locator("h1").TextContentAsync();
            title.Should().Contain("Task Management System");

            // Wait for tasks to load (initial state might be empty)
            await page.WaitForSelectorAsync("#taskList");

            // Get initial task count
            var initialTaskElements = await page.Locator(".task-item").CountAsync();

            // Act - Fill in the task form
            var taskTitle = $"Test Task from Playwright {Guid.NewGuid()}";
            var taskDescription = "This task was created by an automated Playwright test";

            await page.FillAsync("#title", taskTitle);
            await page.FillAsync("#description", taskDescription);

            // Submit the form
            await page.ClickAsync("button[type='submit']");

            // Wait for the alert and dismiss it
            page.Dialog += async (_, dialog) => await dialog.DismissAsync();

            // Wait a bit for the task to be created and the list to refresh
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert - Verify the task appears in the list
            await page.WaitForSelectorAsync(".task-item", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            // Check that we have at least one more task
            var newTaskElements = await page.Locator(".task-item").CountAsync();
            newTaskElements.Should().BeGreaterThan(initialTaskElements);

            // Verify the task with our title is in the list
            var taskWithTitle = page.Locator(".task-item").Filter(new LocatorFilterOptions
            {
                HasText = taskTitle
            });

            await taskWithTitle.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

            var taskTitleElement = taskWithTitle.Locator(".task-title");
            var displayedTitle = await taskTitleElement.TextContentAsync();
            displayedTitle.Should().Be(taskTitle);

            var taskDescriptionElement = taskWithTitle.Locator(".task-description");
            var displayedDescription = await taskDescriptionElement.TextContentAsync();
            displayedDescription.Should().Be(taskDescription);

            // Verify the task has a status badge
            var statusBadge = taskWithTitle.Locator(".badge-status");
            await statusBadge.WaitForAsync();
            var status = await statusBadge.TextContentAsync();
            status.Should().Be("pending");

            // Wait for AI analysis (with timeout)
            var maxWaitTime = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                await page.ReloadAsync();
                await page.WaitForSelectorAsync(".task-item");

                var aiAnalysisElements = await taskWithTitle.Locator(".badge-category").CountAsync();
                if (aiAnalysisElements > 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            // Verify AI analysis was completed (or at least shows pending)
            var aiIndicator = await taskWithTitle.Locator(".badge-category, .badge-pending").CountAsync();
            aiIndicator.Should().BeGreaterThan(0, "Task should have either AI analysis or pending indicator");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Frontend_DeleteTask_RemovesItemFromList()
    {
        // Arrange - Get the frontend URL and create a new browser context
        var frontendUrl = _app!.GetEndpoint("frontend").ToString().TrimEnd('/');
        var context = await _browser!.NewContextAsync();
        context.SetDefaultNavigationTimeout(60000);
        var page = await context.NewPageAsync();

        try
        {
            // Navigate to the frontend
            await page.GotoAsync(frontendUrl);
            await page.WaitForSelectorAsync("h1");

            // Create a task first
            var taskTitle = $"Task to Delete {Guid.NewGuid()}";
            var taskDescription = "This task will be deleted by the test";

            await page.FillAsync("#title", taskTitle);
            await page.FillAsync("#description", taskDescription);
            // Handle alert
            EventHandler<IDialog> alertHandler = async (_, dialog) => await dialog.DismissAsync();
            page.Dialog += alertHandler;

            await page.ClickAsync("button[type='submit']");

            // Wait for task to appear
            await Task.Delay(TimeSpan.FromSeconds(2));
            await page.WaitForSelectorAsync(".task-item");

            // Remove the alert handler so it doesn't interfere with the delete confirmation
            page.Dialog -= alertHandler;

            var taskWithTitle = page.Locator(".task-item").Filter(new LocatorFilterOptions
            {
                HasText = taskTitle
            });

            await taskWithTitle.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

            // Act - Delete the task
            var deleteButton = taskWithTitle.Locator(".btn-delete");

            // Handle confirmation dialog
            page.Dialog += async (_, dialog) =>
            {
                dialog.Type.Should().Be(DialogType.Confirm);
                await dialog.AcceptAsync();
            };

            await deleteButton.ClickAsync();

            // Wait for the task to be removed
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert - Verify the task is no longer in the list
            await taskWithTitle.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Detached,
                Timeout = 10000
            });

            var taskCount = await page.Locator(".task-item").Filter(new LocatorFilterOptions
            {
                HasText = taskTitle
            }).CountAsync();

            taskCount.Should().Be(0, "Task should be removed from the list");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Frontend_RefreshButton_ReloadsTaskList()
    {
        // Arrange - Get the frontend URL and create a new browser context
        var frontendUrl = _app!.GetEndpoint("frontend").ToString().TrimEnd('/');
        var context = await _browser!.NewContextAsync();
        context.SetDefaultNavigationTimeout(60000);
        var page = await context.NewPageAsync();

        try
        {
            // Navigate to the frontend
            await page.GotoAsync(frontendUrl);
            await page.WaitForSelectorAsync("h1");

            // Wait for initial load
            await page.WaitForSelectorAsync("#taskList");

            // Act - Click the refresh button
            var refreshButton = page.Locator("button.refresh-btn");
            await refreshButton.ClickAsync();

            // Wait for the cache indicator to update
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Assert - Verify the cache indicator is present
            var cacheIndicator = page.Locator("#cacheIndicator .cache-indicator");
            await cacheIndicator.WaitForAsync();

            var cacheText = await cacheIndicator.TextContentAsync();
            cacheText.Should().Match(text => 
                text!.Contains("From Cache") || text.Contains("From Database"));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Frontend_FormValidation_RequiresFields()
    {
        // Arrange - Get the frontend URL and create a new browser context
        var frontendUrl = _app!.GetEndpoint("frontend").ToString().TrimEnd('/');
        var context = await _browser!.NewContextAsync();
        context.SetDefaultNavigationTimeout(60000);
        var page = await context.NewPageAsync();

        try
        {
            // Navigate to the frontend
            await page.GotoAsync(frontendUrl);
            await page.WaitForSelectorAsync("h1");

            // Act - Try to submit the form without filling fields
            var submitButton = page.Locator("button[type='submit']");
            await submitButton.ClickAsync();

            // Assert - Form should not submit due to HTML5 validation
            // The title field should have the "required" attribute
            var titleInput = page.Locator("#title");
            var isRequired = await titleInput.GetAttributeAsync("required");
            isRequired.Should().NotBeNull("Title field should be required");

            var descriptionInput = page.Locator("#description");
            var isDescRequired = await descriptionInput.GetAttributeAsync("required");
            isDescRequired.Should().NotBeNull("Description field should be required");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
