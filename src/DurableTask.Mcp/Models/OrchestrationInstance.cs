namespace DurableTask.Mcp.Models;

/// <summary>
/// Represents an orchestration instance from the DTFx Instances table.
/// </summary>
public sealed class OrchestrationInstance
{
    public required string InstanceId { get; init; }
    public string? Name { get; init; }
    public string? RuntimeStatus { get; init; }
    public string? Input { get; init; }
    public string? Output { get; init; }
    public string? CustomStatus { get; init; }
    public DateTimeOffset? CreatedTime { get; init; }
    public DateTimeOffset? LastUpdatedTime { get; init; }
    public DateTimeOffset? CompletedTime { get; init; }
    public string? ExecutionId { get; init; }
    public string? TaskHubName { get; init; }
}

/// <summary>
/// Represents a history event from the DTFx History table.
/// </summary>
public sealed class HistoryEvent
{
    public required string InstanceId { get; init; }
    public required string SequenceNumber { get; init; }
    public string? EventType { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? Name { get; init; }
    public string? Input { get; init; }
    public string? Result { get; init; }
    public int? TaskScheduledId { get; init; }
    public DateTimeOffset? ScheduledTime { get; init; }
    public DateTimeOffset? FireAt { get; init; }
    public string? OrchestrationStatus { get; init; }
    public string? ExecutionId { get; init; }
    public string? Reason { get; init; }
    public bool? IsPlayed { get; init; }
}

/// <summary>
/// Represents a task hub discovered from Azure Storage.
/// </summary>
public sealed class TaskHub
{
    public required string Name { get; init; }
    public bool HasInstancesTable { get; init; }
    public bool HasHistoryTable { get; init; }
    public int ControlQueueCount { get; init; }
    public bool HasWorkItemQueue { get; init; }
    public bool HasLargeMessagesContainer { get; init; }
    public bool HasLeasesContainer { get; init; }
}

/// <summary>
/// Represents a queue message from DTFx control or work-item queues.
/// </summary>
public sealed class QueueMessageInfo
{
    public required string MessageId { get; init; }
    public required string QueueName { get; init; }
    public string? MessageText { get; init; }
    public DateTimeOffset? InsertedOn { get; init; }
    public DateTimeOffset? ExpiresOn { get; init; }
    public long DequeueCount { get; init; }
    public string? PopReceipt { get; init; }
}

/// <summary>
/// Summary statistics for orchestration instances.
/// </summary>
public sealed class OrchestrationSummary
{
    public required string TaskHubName { get; init; }
    public int TotalCount { get; init; }
    public int RunningCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }
    public int TerminatedCount { get; init; }
    public int SuspendedCount { get; init; }
    public int ContinuedAsNewCount { get; init; }
}
