using System.ComponentModel;
using System.Text.Json;
using DurableTask.Mcp.Services;
using ModelContextProtocol.Server;

namespace DurableTask.Mcp.Tools;

/// <summary>
/// MCP tools for inspecting Durable Task Framework orchestration instances.
/// </summary>
[McpServerToolType]
public sealed class OrchestrationTools
{
    private readonly IDurableTaskStorageService _storageService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public OrchestrationTools(IDurableTaskStorageService storageService)
    {
        _storageService = storageService;
    }

    [McpServerTool(Name = "dtfx_list_orchestrations", ReadOnly = true)]
    [Description("Lists orchestration instances from a task hub with optional filtering by status, name, and creation time.")]
    public async Task<string> ListOrchestrations(
        [Description("The name of the task hub")] string taskHubName,
        [Description("Filter by runtime status: Running, Completed, Failed, Pending, Terminated, Suspended, ContinuedAsNew")] string? runtimeStatus = null,
        [Description("Filter by orchestration name (exact match)")] string? nameFilter = null,
        [Description("Filter for orchestrations created after this date (ISO 8601 format)")] string? createdAfter = null,
        [Description("Filter for orchestrations created before this date (ISO 8601 format)")] string? createdBefore = null,
        [Description("Maximum number of results to return (default: 100)")] int top = 100,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? createdAfterDate = null;
        DateTimeOffset? createdBeforeDate = null;

        if (!string.IsNullOrEmpty(createdAfter) && DateTimeOffset.TryParse(createdAfter, out var afterDate))
        {
            createdAfterDate = afterDate;
        }

        if (!string.IsNullOrEmpty(createdBefore) && DateTimeOffset.TryParse(createdBefore, out var beforeDate))
        {
            createdBeforeDate = beforeDate;
        }

        var instances = await _storageService.ListOrchestrationInstancesAsync(
            taskHubName, 
            runtimeStatus, 
            nameFilter, 
            createdAfterDate, 
            createdBeforeDate, 
            top, 
            cancellationToken);

        if (instances.Count == 0)
        {
            return "No orchestration instances found matching the criteria.";
        }

        return JsonSerializer.Serialize(instances, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_orchestration", ReadOnly = true)]
    [Description("Gets detailed information about a specific orchestration instance by its instance ID.")]
    public async Task<string> GetOrchestration(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await _storageService.GetOrchestrationInstanceAsync(taskHubName, instanceId, cancellationToken);

        if (instance == null)
        {
            return $"Orchestration instance '{instanceId}' not found in task hub '{taskHubName}'.";
        }

        return JsonSerializer.Serialize(instance, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_search_orchestrations", ReadOnly = true)]
    [Description("Searches for orchestration instances by instance ID prefix. Useful for finding related orchestrations or sub-orchestrations.")]
    public async Task<string> SearchOrchestrations(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID prefix to search for")] string instanceIdPrefix,
        [Description("Maximum number of results to return (default: 100)")] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var instances = await _storageService.SearchOrchestrationsByInstanceIdAsync(taskHubName, instanceIdPrefix, top, cancellationToken);

        if (instances.Count == 0)
        {
            return $"No orchestration instances found with prefix '{instanceIdPrefix}'.";
        }

        return JsonSerializer.Serialize(instances, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_failed_orchestrations", ReadOnly = true)]
    [Description("Gets orchestrations with Failed status along with their error messages from history. Useful for troubleshooting.")]
    public async Task<string> GetFailedOrchestrations(
        [Description("The name of the task hub")] string taskHubName,
        [Description("Maximum number of results to return (default: 50)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        var failed = await _storageService.GetFailedOrchestrationsAsync(taskHubName, top, cancellationToken);

        if (failed.Count == 0)
        {
            return "No failed orchestrations found.";
        }

        var results = failed.Select(f => new
        {
            f.Instance.InstanceId,
            f.Instance.Name,
            f.Instance.CreatedTime,
            f.Instance.LastUpdatedTime,
            f.Instance.ExecutionId,
            ErrorMessage = f.ErrorMessage
        });

        return JsonSerializer.Serialize(results, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_list_running_orchestrations", ReadOnly = true)]
    [Description("Lists all currently running orchestrations. Useful for monitoring active work.")]
    public async Task<string> ListRunningOrchestrations(
        [Description("The name of the task hub")] string taskHubName,
        [Description("Maximum number of results to return (default: 100)")] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var instances = await _storageService.ListOrchestrationInstancesAsync(
            taskHubName, 
            runtimeStatus: "Running", 
            top: top, 
            cancellationToken: cancellationToken);

        if (instances.Count == 0)
        {
            return "No running orchestrations found.";
        }

        return JsonSerializer.Serialize(instances, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_list_pending_orchestrations", ReadOnly = true)]
    [Description("Lists orchestrations in Pending status (scheduled but not yet started). Useful for identifying backlog.")]
    public async Task<string> ListPendingOrchestrations(
        [Description("The name of the task hub")] string taskHubName,
        [Description("Maximum number of results to return (default: 100)")] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var instances = await _storageService.ListOrchestrationInstancesAsync(
            taskHubName, 
            runtimeStatus: "Pending", 
            top: top, 
            cancellationToken: cancellationToken);

        if (instances.Count == 0)
        {
            return "No pending orchestrations found.";
        }

        return JsonSerializer.Serialize(instances, JsonOptions);
    }
}
