using DurableTask.Mcp.Models;

namespace DurableTask.Mcp.Services;

/// <summary>
/// Interface for accessing Durable Task Framework data stored in Azure Storage.
/// </summary>
public interface IDurableTaskStorageService
{
    /// <summary>
    /// Discovers all task hubs in the storage account by analyzing tables, queues, and containers.
    /// </summary>
    Task<List<TaskHub>> ListTaskHubsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific task hub.
    /// </summary>
    Task<TaskHub> GetTaskHubDetailsAsync(string taskHubName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists orchestration instances from the Instances table with optional filtering.
    /// </summary>
    Task<List<OrchestrationInstance>> ListOrchestrationInstancesAsync(
        string taskHubName,
        string? runtimeStatus = null,
        string? nameFilter = null,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific orchestration instance by its ID.
    /// </summary>
    Task<OrchestrationInstance?> GetOrchestrationInstanceAsync(
        string taskHubName,
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the execution history for an orchestration instance.
    /// </summary>
    Task<List<HistoryEvent>> GetOrchestrationHistoryAsync(
        string taskHubName,
        string instanceId,
        int? top = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for orchestration instances by a partial instance ID match.
    /// </summary>
    Task<List<OrchestrationInstance>> SearchOrchestrationsByInstanceIdAsync(
        string taskHubName,
        string instanceIdPrefix,
        int? top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets summary statistics for orchestrations in a task hub.
    /// </summary>
    Task<OrchestrationSummary> GetOrchestrationSummaryAsync(
        string taskHubName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists queues associated with a task hub.
    /// </summary>
    Task<List<string>> ListTaskHubQueuesAsync(
        string taskHubName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks at messages in a queue without removing them.
    /// </summary>
    Task<List<QueueMessageInfo>> PeekQueueMessagesAsync(
        string queueName,
        int maxMessages = 32,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approximate message count for a queue.
    /// </summary>
    Task<int> GetQueueMessageCountAsync(
        string queueName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists blob containers associated with a task hub.
    /// </summary>
    Task<List<string>> ListTaskHubContainersAsync(
        string taskHubName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists large message blobs for a task hub.
    /// </summary>
    Task<List<string>> ListLargeMessagesAsync(
        string taskHubName,
        int? top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a large message blob.
    /// </summary>
    Task<string?> GetLargeMessageContentAsync(
        string taskHubName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed orchestrations (status = Failed) with their error details from history.
    /// </summary>
    Task<List<(OrchestrationInstance Instance, string? ErrorMessage)>> GetFailedOrchestrationsAsync(
        string taskHubName,
        int? top = 50,
        CancellationToken cancellationToken = default);
}
