var gitFolderName = GitFolderResolver.GetGitFolderName();
var dashboardAppName = string.IsNullOrEmpty(gitFolderName) ? "NoteTaker" : $"NoteTaker-{gitFolderName}";

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




public static class GitFolderResolver
{
    /// <summary>
    /// Gets the current Git folder name (supports both regular repos and worktrees)
    /// </summary>
    /// <param name="workingDirectory">The working directory to resolve from. If null, uses current directory.</param>
    /// <returns>The Git folder name, or "default" if not in a Git repository</returns>
    public static string GetGitFolderName(string? workingDirectory = null)
    {
        try
        {
            var directory = workingDirectory ?? Directory.GetCurrentDirectory();
            
            // Find .git directory or file (worktrees use a .git file)
            var gitPath = FindGitPath(directory);
            if (gitPath == null)
            {
                return "default";
            }

            // If .git is a directory (regular repo), return parent folder name
            if (Directory.Exists(gitPath))
            {
                var repoDirectory = Path.GetDirectoryName(gitPath);
                return Path.GetFileName(repoDirectory) ?? "default";
            }

            // If .git is a file (worktree), parse it to get worktree name
            if (File.Exists(gitPath))
            {
                var gitFileContent = File.ReadAllText(gitPath);
                // Format: "gitdir: /path/to/repo/.git/worktrees/worktree-name"
                if (gitFileContent.StartsWith("gitdir:"))
                {
                    var gitDirPath = gitFileContent.Substring(7).Trim();
                    
                    // Extract worktree name from path
                    // Path format: /path/to/repo/.git/worktrees/worktree-name
                    var worktreeSegments = gitDirPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Find the worktrees segment and get the next one
                    for (int i = 0; i < worktreeSegments.Length - 1; i++)
                    {
                        if (worktreeSegments[i] == "worktrees")
                        {
                            return worktreeSegments[i + 1];
                        }
                    }
                    
                    // Fallback: use current directory name
                    return Path.GetFileName(directory) ?? "default";
                }
            }

            return "default";
        }
        catch
        {
            // If anything goes wrong, return default
            return "default";
        }
    }

    private static string? FindGitPath(string directory)
    {
        var currentDir = directory;
        
        while (!string.IsNullOrEmpty(currentDir))
        {
            var gitPath = Path.Combine(currentDir, ".git");
            
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return gitPath;
            }
            
            var parentDir = Path.GetDirectoryName(currentDir);
            if (parentDir == currentDir) // Reached root
            {
                break;
            }
            
            currentDir = parentDir;
        }
        
        return null;
    }
}