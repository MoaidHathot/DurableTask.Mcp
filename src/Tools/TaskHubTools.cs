using System.ComponentModel;
using System.Text.Json;
using DurableTasksMcp.Services;
using ModelContextProtocol.Server;

namespace DurableTasksMcp.Tools;

/// <summary>
/// MCP tools for discovering and inspecting Durable Task Framework task hubs.
/// </summary>
[McpServerToolType]
public sealed class TaskHubTools
{
    private readonly IDurableTaskStorageService _storageService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TaskHubTools(IDurableTaskStorageService storageService)
    {
        _storageService = storageService;
    }

    [McpServerTool(Name = "dtfx_list_task_hubs", ReadOnly = true)]
    [Description("Lists all Durable Task Framework task hubs discovered in the configured Azure Storage account. Returns information about each hub's resources (tables, queues, containers).")]
    public async Task<string> ListTaskHubs(CancellationToken cancellationToken = default)
    {
        var hubs = await _storageService.ListTaskHubsAsync(cancellationToken);
        
        if (hubs.Count == 0)
        {
            return "No task hubs found in the storage account.";
        }

        return JsonSerializer.Serialize(hubs, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_task_hub_details", ReadOnly = true)]
    [Description("Gets detailed information about a specific task hub, including its tables, queues, and blob containers.")]
    public async Task<string> GetTaskHubDetails(
        [Description("The name of the task hub to inspect")] string taskHubName,
        CancellationToken cancellationToken = default)
    {
        var hub = await _storageService.GetTaskHubDetailsAsync(taskHubName, cancellationToken);
        return JsonSerializer.Serialize(hub, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_orchestration_summary", ReadOnly = true)]
    [Description("Gets summary statistics for orchestrations in a task hub, including counts by runtime status (Running, Completed, Failed, etc.).")]
    public async Task<string> GetOrchestrationSummary(
        [Description("The name of the task hub")] string taskHubName,
        CancellationToken cancellationToken = default)
    {
        var summary = await _storageService.GetOrchestrationSummaryAsync(taskHubName, cancellationToken);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}
