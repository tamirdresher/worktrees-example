#:package ModelContextProtocol@0.4.1-preview.1
#:package Microsoft.Extensions.Hosting@10.0.0
#:package Microsoft.Extensions.Logging@10.0.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Initialize paths
var settingsDir = Environment.GetEnvironmentVariable("ASPIRE_MCP_SETTINGS_DIR") 
    ?? Path.Combine(Directory.GetCurrentDirectory(), "scripts");
Directory.CreateDirectory(settingsDir);

var settingsPath = Path.Combine(settingsDir, "settings.json");
var cachePath = Path.Combine(settingsDir, "tools-cache.json");

await Console.Error.WriteLineAsync($"[AspireMcpProxy] Settings: {settingsPath}");
await Console.Error.WriteLineAsync($"[AspireMcpProxy] Cache: {cachePath}");

// Load settings
var settings = await LoadSettingsAsync(settingsPath);
await SaveSettingsAsync(settingsPath, settings);

await Console.Error.WriteLineAsync($"[AspireMcpProxy] Proxying to http://localhost:{settings.Port}/mcp");

// Client factory
async Task<McpClient> CreateClientAsync()
{
    var current = await LoadSettingsAsync(settingsPath);
    var transport = new HttpClientTransport(new()
    {
        Endpoint = new Uri($"http://localhost:{current.Port}/mcp"),
        AdditionalHeaders = new Dictionary<string, string>
        {
            ["x-mcp-api-key"] = current.ApiKey!,
            ["Accept"] = "application/json, text/event-stream"
        }
    });
    return await McpClient.CreateAsync(transport);
}

// Initialize cache
var cache = new ToolCache(cachePath);
IEnumerable<ProxyTool> tools;

try
{
    var client = await CreateClientAsync();
    await cache.RefreshAsync(client);
    // Online mode: use live tools
    tools = cache.GetTools().Select(t => new ProxyTool(CreateClientAsync, t));
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"[AspireMcpProxy] Connection failed: {ex.Message}");
    await Console.Error.WriteLineAsync("[AspireMcpProxy] Using cached tools");
    
    var cachedTools = await cache.LoadAsync();
    // Offline mode: create tools from cached metadata
    tools = cachedTools.Select(t => new ProxyTool(CreateClientAsync, t));
}

// Register MCP server with tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools(tools);

await builder.Build().RunAsync();
return 0;

// Helper functions
async Task<Settings> LoadSettingsAsync(string path)
{
    Settings? file = null;
    if (File.Exists(path))
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            file = JsonSerializer.Deserialize(json, SourceGenContext.Default.Settings);
        }
        catch { }
    }

    var port = Environment.GetEnvironmentVariable("ASPIRE_MCP_PORT")
        ?? Environment.GetEnvironmentVariable("ASPIRE_MCP_PORT_DEFAULT")
        ?? file?.Port;

    var apiKey = Environment.GetEnvironmentVariable("ASPIRE_MCP_API_KEY")
        ?? Environment.GetEnvironmentVariable("ASPIRE_MCP_API_KEY_DEFAULT")
        ?? file?.ApiKey;

    return new Settings { Port = port, ApiKey = apiKey, LastUpdated = DateTime.UtcNow };
}

async Task SaveSettingsAsync(string path, Settings settings)
{
    try
    {
        var json = JsonSerializer.Serialize(settings, SourceGenContext.Default.Settings);
        await File.WriteAllTextAsync(path, json);
    }
    catch { }
}

// Models
record Settings
{
    [JsonPropertyName("port")] public string? Port { get; set; }
    [JsonPropertyName("apiKey")] public string? ApiKey { get; set; }
    [JsonPropertyName("lastUpdated")] public DateTime LastUpdated { get; set; }
}

record CachedTool
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
}

record CacheFile
{
    [JsonPropertyName("tools")] public List<CachedTool> Tools { get; set; } = [];
}

// Source generation context
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(CacheFile))]
internal partial class SourceGenContext : JsonSerializerContext { }

// Tool cache
class ToolCache(string path)
{
    private readonly Dictionary<string, McpClientTool> _tools = new();

    public async Task RefreshAsync(McpClient client)
    {
        var tools = await client.ListToolsAsync();
        
        // Build new cache
        var newCache = new CacheFile
        {
            Tools = tools.Select(t => new CachedTool
            {
                Name = t.Name,
                Description = t.Description ?? ""
            }).ToList()
        };
        
        // Only save if tools changed
        var existingJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        var newJson = JsonSerializer.Serialize(newCache, SourceGenContext.Default.CacheFile);
        
        if (existingJson != newJson)
        {
            await File.WriteAllTextAsync(path, newJson);
            await Console.Error.WriteLineAsync($"[ToolCache] Updated cache ({newCache.Tools.Count} tools)");
        }
        else
        {
            await Console.Error.WriteLineAsync($"[ToolCache] Cache unchanged ({newCache.Tools.Count} tools)");
        }
        
        _tools.Clear();
        foreach (var tool in tools) _tools[tool.Name] = tool;
    }

    public async Task<List<CachedTool>> LoadAsync()
    {
        if (!File.Exists(path)) return [];
        
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var cache = JsonSerializer.Deserialize(json, SourceGenContext.Default.CacheFile);
            if (cache != null)
            {
                await Console.Error.WriteLineAsync($"[ToolCache] Loaded metadata ({cache.Tools.Count} tools)");
                return cache.Tools;
            }
        }
        catch { }
        
        return [];
    }

    public IEnumerable<McpClientTool> GetTools() => _tools.Values;
}

// Proxy tool
sealed class ProxyTool : McpServerTool
{
    private static readonly JsonDocument _emptySchema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}");
    private readonly Func<Task<McpClient>> _clientFactory;
    private readonly Tool _tool;

    // Online constructor: use live McpClientTool
    public ProxyTool(Func<Task<McpClient>> clientFactory, McpClientTool downstream)
    {
        _clientFactory = clientFactory;
        _tool = CloneTool(downstream.ProtocolTool);
    }

    // Offline constructor: use cached metadata
    public ProxyTool(Func<Task<McpClient>> clientFactory, CachedTool cached)
    {
        _clientFactory = clientFactory;
        _tool = new Tool
        {
            Name = cached.Name,
            Description = cached.Description,
            InputSchema = _emptySchema.RootElement.Clone()
        };
    }

    public override Tool ProtocolTool => _tool;
    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken ct = default)
    {
        var args = request.Params?.Arguments?
            .ToDictionary(kv => kv.Key, kv => (object?)kv.Value) 
            ?? new Dictionary<string, object?>();

        await Console.Error.WriteLineAsync($"[ProxyTool] Calling {_tool.Name}");

        try
        {
            var client = await _clientFactory();
            var result = await client.CallToolAsync(_tool.Name, args, null);
            await Console.Error.WriteLineAsync($"[ProxyTool] {_tool.Name} completed (error: {result.IsError ?? false})");
            return result;
        }
        catch (HttpRequestException ex)
        {
            return Error($"Connection failed: {ex.Message}\n\nVerify Aspire is running.");
        }
        catch (Exception ex)
        {
            return Error($"Error: {ex.Message}");
        }
    }

    private static Tool CloneTool(Tool src) => new()
    {
        Name = src.Name,
        Description = src.Description,
        Title = src.Title,
        Icons = src.Icons,
        Annotations = src.Annotations,
        Meta = src.Meta,
        InputSchema = src.InputSchema.Clone(),
        OutputSchema = src.OutputSchema?.Clone()
    };

    private static CallToolResult Error(string message) => new()
    {
        Content = [new TextContentBlock { Text = message }],
        IsError = true
    };
}