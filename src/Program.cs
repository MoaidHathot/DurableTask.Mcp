using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTasksMcp.Services;
using DurableTasksMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Parse command line arguments for the storage account and log file
var storageAccountName = GetStorageAccountName(args);
var logFilePath = GetLogFilePath(args);

if (string.IsNullOrEmpty(storageAccountName))
{
    Console.Error.WriteLine("Error: Azure Storage account name is required.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage: DurableTasksMcp --storage-account <account-name> [--log-file <path>]");
    Console.Error.WriteLine("   or: DurableTasksMcp -s <account-name> [-l <path>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Environment variables:");
    Console.Error.WriteLine("  DTFX_STORAGE_ACCOUNT  - Azure Storage account name");
    Console.Error.WriteLine("  DTFX_LOG_FILE         - Log file path");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Example:");
    Console.Error.WriteLine("  DurableTasksMcp --storage-account mystorageaccount --log-file ./dtfx-mcp.log");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Authentication:");
    Console.Error.WriteLine("  Uses DefaultAzureCredential which supports:");
    Console.Error.WriteLine("  - Azure CLI (az login)");
    Console.Error.WriteLine("  - Environment variables (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET)");
    Console.Error.WriteLine("  - Managed Identity");
    Console.Error.WriteLine("  - Visual Studio / VS Code credentials");
    return 1;
}

// Initialize log file (create or clear)
StreamWriter? logFileWriter = null;
if (!string.IsNullOrEmpty(logFilePath))
{
    try
    {
        // Create directory if it doesn't exist
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        
        // Create or clear the log file
        logFileWriter = new StreamWriter(logFilePath, append: false) { AutoFlush = true };
        logFileWriter.WriteLine($"[{DateTime.UtcNow:O}] DurableTasksMcp starting...");
        logFileWriter.WriteLine($"[{DateTime.UtcNow:O}] Storage account: {storageAccountName}");
        logFileWriter.WriteLine($"[{DateTime.UtcNow:O}] Log file: {logFilePath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to initialize log file '{logFilePath}': {ex.Message}");
        logFileWriter?.Dispose();
        logFileWriter = null;
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();

// Add file logging if configured
if (logFileWriter != null)
{
    builder.Logging.AddProvider(new FileLoggerProvider(logFileWriter));
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    // Fall back to console logging to stderr (MCP uses stdout for communication)
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
}

// Configure Azure credentials
var credential = new DefaultAzureCredential();

// Build service URIs
var tableServiceUri = new Uri($"https://{storageAccountName}.table.core.windows.net");
var queueServiceUri = new Uri($"https://{storageAccountName}.queue.core.windows.net");
var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");

// Register Azure Storage clients
builder.Services.AddSingleton(new TableServiceClient(tableServiceUri, credential));
builder.Services.AddSingleton(new QueueServiceClient(queueServiceUri, credential));
builder.Services.AddSingleton(new BlobServiceClient(blobServiceUri, credential));

// Register our storage service
builder.Services.AddSingleton<IDurableTaskStorageService, DurableTaskStorageService>();
builder.Services.AddSingleton<DurableTaskStorageService>();

// Configure MCP server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "DurableTask.Mcp",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

try
{
    var appTask = app.RunAsync();
	var dtss = app.Services.GetRequiredService<DurableTaskStorageService>();
	var hubTool = new TaskHubTools(dtss);
	
	var hubs = await hubTool.ListTaskHubs();

	await appTask;
}
finally
{
    logFileWriter?.WriteLine($"[{DateTime.UtcNow:O}] DurableTasksMcp shutting down...");
    logFileWriter?.Dispose();
}

return 0;

static string? GetStorageAccountName(string[] args)
{
    // Check command line arguments
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--storage-account" || args[i] == "-s")
        {
            return args[i + 1];
        }
    }

    // Check environment variable
    return Environment.GetEnvironmentVariable("DTFX_STORAGE_ACCOUNT");
}

static string? GetLogFilePath(string[] args)
{
    // Check command line arguments
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--log-file" || args[i] == "-l")
        {
            return args[i + 1];
        }
    }

	return "log_file.txt";
    // Check environment variable
    // return Environment.GetEnvironmentVariable("DTFX_LOG_FILE");
}

/// <summary>
/// Simple file logger provider that writes to a StreamWriter.
/// </summary>
sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;

    public FileLoggerProvider(StreamWriter writer)
    {
        _writer = writer;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_writer, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Simple file logger that writes formatted log messages to a file.
/// </summary>
sealed class FileLogger : ILogger
{
    private readonly StreamWriter _writer;
    private readonly string _categoryName;

    public FileLogger(StreamWriter writer, string categoryName)
    {
        _writer = writer;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logLevelShort = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        lock (_writer)
        {
            _writer.WriteLine($"[{DateTime.UtcNow:O}] [{logLevelShort}] [{_categoryName}] {message}");
            if (exception != null)
            {
                _writer.WriteLine($"[{DateTime.UtcNow:O}] [{logLevelShort}] [{_categoryName}] Exception: {exception}");
            }
        }
    }
}
