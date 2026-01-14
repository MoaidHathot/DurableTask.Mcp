using System.ComponentModel;
using System.Text.Json;
using DurableTasksMcp.Services;
using ModelContextProtocol.Server;

namespace DurableTasksMcp.Tools;

/// <summary>
/// MCP tools for inspecting Durable Task Framework orchestration history and events.
/// </summary>
[McpServerToolType]
public sealed class HistoryTools
{
    private readonly DurableTaskStorageService _storageService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public HistoryTools(DurableTaskStorageService storageService)
    {
        _storageService = storageService;
    }

    [McpServerTool(Name = "dtfx_get_orchestration_history", ReadOnly = true)]
    [Description("Gets the complete execution history for an orchestration instance. Shows all events including activity executions, timers, sub-orchestrations, and external events.")]
    public async Task<string> GetOrchestrationHistory(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        [Description("Maximum number of history events to return")] int? top = null,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, top, cancellationToken);

        if (history.Count == 0)
        {
            return $"No history found for orchestration '{instanceId}' in task hub '{taskHubName}'.";
        }

        return JsonSerializer.Serialize(history, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_activity_history", ReadOnly = true)]
    [Description("Gets only the activity-related history events (TaskScheduled, TaskCompleted, TaskFailed) for an orchestration. Useful for understanding the activity execution flow.")]
    public async Task<string> GetActivityHistory(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, cancellationToken: cancellationToken);

        var activityEvents = history.Where(h => 
            h.EventType == "TaskScheduled" || 
            h.EventType == "TaskCompleted" || 
            h.EventType == "TaskFailed")
            .ToList();

        if (activityEvents.Count == 0)
        {
            return $"No activity events found for orchestration '{instanceId}'.";
        }

        return JsonSerializer.Serialize(activityEvents, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_sub_orchestration_history", ReadOnly = true)]
    [Description("Gets sub-orchestration related history events for an orchestration. Shows when sub-orchestrations were created and their completion status.")]
    public async Task<string> GetSubOrchestrationHistory(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, cancellationToken: cancellationToken);

        var subOrchEvents = history.Where(h => 
            h.EventType == "SubOrchestrationInstanceCreated" || 
            h.EventType == "SubOrchestrationInstanceCompleted" || 
            h.EventType == "SubOrchestrationInstanceFailed")
            .ToList();

        if (subOrchEvents.Count == 0)
        {
            return $"No sub-orchestration events found for orchestration '{instanceId}'.";
        }

        return JsonSerializer.Serialize(subOrchEvents, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_timer_history", ReadOnly = true)]
    [Description("Gets timer-related history events for an orchestration. Shows durable timers that were created and when they fired.")]
    public async Task<string> GetTimerHistory(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, cancellationToken: cancellationToken);

        var timerEvents = history.Where(h => 
            h.EventType == "TimerCreated" || 
            h.EventType == "TimerFired")
            .ToList();

        if (timerEvents.Count == 0)
        {
            return $"No timer events found for orchestration '{instanceId}'.";
        }

        return JsonSerializer.Serialize(timerEvents, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_external_events", ReadOnly = true)]
    [Description("Gets external event history for an orchestration. Shows events that were raised from outside the orchestration (e.g., via RaiseEventAsync).")]
    public async Task<string> GetExternalEvents(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, cancellationToken: cancellationToken);

        var externalEvents = history.Where(h => 
            h.EventType == "EventRaised" || 
            h.EventType == "EventSent")
            .ToList();

        if (externalEvents.Count == 0)
        {
            return $"No external events found for orchestration '{instanceId}'.";
        }

        return JsonSerializer.Serialize(externalEvents, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_failed_activities", ReadOnly = true)]
    [Description("Gets all failed activity executions for an orchestration. Shows the activity name and failure reason.")]
    public async Task<string> GetFailedActivities(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, cancellationToken: cancellationToken);

        var failedActivities = history.Where(h => h.EventType == "TaskFailed").ToList();

        if (failedActivities.Count == 0)
        {
            return $"No failed activities found for orchestration '{instanceId}'.";
        }

        // Correlate with scheduled events to get activity names
        var scheduledEvents = history.Where(h => h.EventType == "TaskScheduled").ToDictionary(h => h.TaskScheduledId ?? 0);
        
        var results = failedActivities.Select(f => new
        {
            f.SequenceNumber,
            f.Timestamp,
            ActivityName = scheduledEvents.TryGetValue(f.TaskScheduledId ?? 0, out var scheduled) ? scheduled.Name : "Unknown",
            f.TaskScheduledId,
            FailureReason = f.Reason ?? f.Result
        });

        return JsonSerializer.Serialize(results, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_history_summary", ReadOnly = true)]
    [Description("Gets a summary of the orchestration history showing counts of each event type and key milestones.")]
    public async Task<string> GetHistorySummary(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The instance ID of the orchestration")] string instanceId,
        CancellationToken cancellationToken = default)
    {
        var history = await _storageService.GetOrchestrationHistoryAsync(taskHubName, instanceId, cancellationToken: cancellationToken);

        if (history.Count == 0)
        {
            return $"No history found for orchestration '{instanceId}'.";
        }

        var eventCounts = history.GroupBy(h => h.EventType)
            .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());

        var executionStarted = history.FirstOrDefault(h => h.EventType == "ExecutionStarted");
        var executionCompleted = history.FirstOrDefault(h => h.EventType == "ExecutionCompleted");

        var summary = new
        {
            TotalEvents = history.Count,
            EventCounts = eventCounts,
            StartTime = executionStarted?.Timestamp,
            EndTime = executionCompleted?.Timestamp,
            Duration = executionStarted?.Timestamp != null && executionCompleted?.Timestamp != null
                ? (executionCompleted.Timestamp.Value - executionStarted.Timestamp.Value).ToString()
                : null,
            FinalStatus = executionCompleted?.OrchestrationStatus,
            TotalActivitiesScheduled = eventCounts.GetValueOrDefault("TaskScheduled", 0),
            TotalActivitiesCompleted = eventCounts.GetValueOrDefault("TaskCompleted", 0),
            TotalActivitiesFailed = eventCounts.GetValueOrDefault("TaskFailed", 0),
            TotalTimersCreated = eventCounts.GetValueOrDefault("TimerCreated", 0),
            TotalExternalEvents = eventCounts.GetValueOrDefault("EventRaised", 0),
            TotalSubOrchestrations = eventCounts.GetValueOrDefault("SubOrchestrationInstanceCreated", 0)
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}
