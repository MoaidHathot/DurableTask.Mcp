using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace DurableTask.Mcp.Services;

/// <summary>
/// Service for accessing Durable Task Framework data stored in Azure Storage.
/// </summary>
public sealed class DurableTaskStorageService : IDurableTaskStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<DurableTaskStorageService> _logger;

    public DurableTaskStorageService(
        TableServiceClient tableServiceClient,
        QueueServiceClient queueServiceClient,
        BlobServiceClient blobServiceClient,
        ILogger<DurableTaskStorageService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _queueServiceClient = queueServiceClient;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all task hubs in the storage account by analyzing tables, queues, and containers.
    /// </summary>
    public async Task<List<TaskHub>> ListTaskHubsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Discovering task hubs in storage account");
        
        var taskHubs = new Dictionary<string, TaskHub>(StringComparer.OrdinalIgnoreCase);

        // Discover from tables (look for *Instances and *History tables)
        await foreach (var table in _tableServiceClient.QueryAsync(cancellationToken: cancellationToken))
        {
            if (table.Name.EndsWith("Instances", StringComparison.OrdinalIgnoreCase))
            {
                var hubName = table.Name[..^"Instances".Length];
                if (!string.IsNullOrEmpty(hubName))
                {
                    taskHubs.TryAdd(hubName, new TaskHub { Name = hubName });
                }
            }
            else if (table.Name.EndsWith("History", StringComparison.OrdinalIgnoreCase))
            {
                var hubName = table.Name[..^"History".Length];
                if (!string.IsNullOrEmpty(hubName))
                {
                    taskHubs.TryAdd(hubName, new TaskHub { Name = hubName });
                }
            }
        }

        // Now verify what resources exist for each hub
        var result = new List<TaskHub>();
        foreach (var hubName in taskHubs.Keys)
        {
            var hub = await GetTaskHubDetailsAsync(hubName, cancellationToken);
            result.Add(hub);
        }

        _logger.LogDebug("Discovered {Count} task hubs", result.Count);
        return result;
    }

    /// <summary>
    /// Gets detailed information about a specific task hub.
    /// </summary>
    public async Task<TaskHub> GetTaskHubDetailsAsync(string taskHubName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting details for task hub: {TaskHubName}", taskHubName);
        
        var hasInstancesTable = false;
        var hasHistoryTable = false;
        var controlQueueCount = 0;
        var hasWorkItemQueue = false;
        var hasLargeMessagesContainer = false;
        var hasLeasesContainer = false;

        // Check tables
        try
        {
            var instancesTable = _tableServiceClient.GetTableClient($"{taskHubName}Instances");
            await instancesTable.GetAccessPoliciesAsync(cancellationToken);
            hasInstancesTable = true;
        }
        catch { /* Table doesn't exist or no access */ }

        try
        {
            var historyTable = _tableServiceClient.GetTableClient($"{taskHubName}History");
            await historyTable.GetAccessPoliciesAsync(cancellationToken);
            hasHistoryTable = true;
        }
        catch { /* Table doesn't exist or no access */ }

        // Check queues (lowercase for DTFx convention)
        var queuePrefix = taskHubName.ToLowerInvariant();
        await foreach (var queue in _queueServiceClient.GetQueuesAsync(prefix: queuePrefix, cancellationToken: cancellationToken))
        {
            if (queue.Name.StartsWith($"{queuePrefix}-control-", StringComparison.OrdinalIgnoreCase))
            {
                controlQueueCount++;
            }
            else if (queue.Name.Equals($"{queuePrefix}-workitems", StringComparison.OrdinalIgnoreCase))
            {
                hasWorkItemQueue = true;
            }
        }

        // Check blob containers
        await foreach (var container in _blobServiceClient.GetBlobContainersAsync(prefix: queuePrefix, cancellationToken: cancellationToken))
        {
            if (container.Name.Equals($"{queuePrefix}-largemessages", StringComparison.OrdinalIgnoreCase))
            {
                hasLargeMessagesContainer = true;
            }
            else if (container.Name.Equals($"{queuePrefix}-leases", StringComparison.OrdinalIgnoreCase))
            {
                hasLeasesContainer = true;
            }
        }

        return new TaskHub
        {
            Name = taskHubName,
            HasInstancesTable = hasInstancesTable,
            HasHistoryTable = hasHistoryTable,
            ControlQueueCount = controlQueueCount,
            HasWorkItemQueue = hasWorkItemQueue,
            HasLargeMessagesContainer = hasLargeMessagesContainer,
            HasLeasesContainer = hasLeasesContainer
        };
    }

    /// <summary>
    /// Lists orchestration instances from the Instances table with optional filtering.
    /// </summary>
    public async Task<List<OrchestrationInstance>> ListOrchestrationInstancesAsync(
        string taskHubName,
        string? runtimeStatus = null,
        string? nameFilter = null,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? top = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing orchestration instances for hub: {TaskHubName}", taskHubName);
        
        var tableClient = _tableServiceClient.GetTableClient($"{taskHubName}Instances");
        var instances = new List<OrchestrationInstance>();

        var filters = new List<string>();
        
        if (!string.IsNullOrEmpty(runtimeStatus))
        {
            filters.Add($"RuntimeStatus eq '{runtimeStatus}'");
        }
        
        if (!string.IsNullOrEmpty(nameFilter))
        {
            filters.Add($"Name eq '{nameFilter}'");
        }
        
        if (createdAfter.HasValue)
        {
            filters.Add($"CreatedTime ge datetime'{createdAfter.Value:O}'");
        }
        
        if (createdBefore.HasValue)
        {
            filters.Add($"CreatedTime le datetime'{createdBefore.Value:O}'");
        }

        var filterString = filters.Count > 0 ? string.Join(" and ", filters) : null;
        
        var query = tableClient.QueryAsync<TableEntity>(filter: filterString, maxPerPage: top, cancellationToken: cancellationToken);
        
        var count = 0;
        await foreach (var entity in query)
        {
            if (top.HasValue && count >= top.Value)
                break;
                
            instances.Add(MapToOrchestrationInstance(entity, taskHubName));
            count++;
        }

        _logger.LogDebug("Retrieved {Count} orchestration instances", instances.Count);
        return instances;
    }

    /// <summary>
    /// Gets a specific orchestration instance by its ID.
    /// </summary>
    public async Task<OrchestrationInstance?> GetOrchestrationInstanceAsync(
        string taskHubName,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting orchestration instance: {InstanceId} from hub: {TaskHubName}", instanceId, taskHubName);
        
        var tableClient = _tableServiceClient.GetTableClient($"{taskHubName}Instances");
        
        try
        {
            // In DTFx, PartitionKey is the instanceId and RowKey is empty string
            var response = await tableClient.GetEntityAsync<TableEntity>(instanceId, "", cancellationToken: cancellationToken);
            return MapToOrchestrationInstance(response.Value, taskHubName);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Orchestration instance not found: {InstanceId}", instanceId);
            return null;
        }
    }

    /// <summary>
    /// Gets the execution history for an orchestration instance.
    /// </summary>
    public async Task<List<HistoryEvent>> GetOrchestrationHistoryAsync(
        string taskHubName,
        string instanceId,
        int? top = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting history for instance: {InstanceId} from hub: {TaskHubName}", instanceId, taskHubName);
        
        var tableClient = _tableServiceClient.GetTableClient($"{taskHubName}History");
        var history = new List<HistoryEvent>();

        // History entries are partitioned by instanceId
        var filter = $"PartitionKey eq '{instanceId}'";
        var query = tableClient.QueryAsync<TableEntity>(filter: filter, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in query)
        {
            if (top.HasValue && count >= top.Value)
                break;
                
            history.Add(MapToHistoryEvent(entity));
            count++;
        }

        // Sort by sequence number
        history = history.OrderBy(h => h.SequenceNumber).ToList();

        _logger.LogDebug("Retrieved {Count} history events for instance: {InstanceId}", history.Count, instanceId);
        return history;
    }

    /// <summary>
    /// Searches for orchestration instances by a partial instance ID match.
    /// </summary>
    public async Task<List<OrchestrationInstance>> SearchOrchestrationsByInstanceIdAsync(
        string taskHubName,
        string instanceIdPrefix,
        int? top = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching orchestrations by prefix: {Prefix} in hub: {TaskHubName}", instanceIdPrefix, taskHubName);
        
        var tableClient = _tableServiceClient.GetTableClient($"{taskHubName}Instances");
        var instances = new List<OrchestrationInstance>();

        // Use range query for prefix matching
        var endPrefix = instanceIdPrefix + "\uffff";
        var filter = $"PartitionKey ge '{instanceIdPrefix}' and PartitionKey lt '{endPrefix}'";
        
        var query = tableClient.QueryAsync<TableEntity>(filter: filter, maxPerPage: top, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in query)
        {
            if (top.HasValue && count >= top.Value)
                break;
                
            instances.Add(MapToOrchestrationInstance(entity, taskHubName));
            count++;
        }

        return instances;
    }

    /// <summary>
    /// Gets summary statistics for orchestrations in a task hub.
    /// </summary>
    public async Task<OrchestrationSummary> GetOrchestrationSummaryAsync(
        string taskHubName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting orchestration summary for hub: {TaskHubName}", taskHubName);
        
        var tableClient = _tableServiceClient.GetTableClient($"{taskHubName}Instances");
        
        var summary = new OrchestrationSummary
        {
            TaskHubName = taskHubName
        };
        
        var statusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Running"] = 0,
            ["Completed"] = 0,
            ["Failed"] = 0,
            ["Pending"] = 0,
            ["Terminated"] = 0,
            ["Suspended"] = 0,
            ["ContinuedAsNew"] = 0
        };
        
        var totalCount = 0;
        
        // We need to scan all entities to get counts - this can be expensive for large hubs
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(select: ["RuntimeStatus"], cancellationToken: cancellationToken))
        {
            totalCount++;
            var status = entity.GetString("RuntimeStatus");
            if (!string.IsNullOrEmpty(status) && statusCounts.ContainsKey(status))
            {
                statusCounts[status]++;
            }
        }

        return new OrchestrationSummary
        {
            TaskHubName = taskHubName,
            TotalCount = totalCount,
            RunningCount = statusCounts["Running"],
            CompletedCount = statusCounts["Completed"],
            FailedCount = statusCounts["Failed"],
            PendingCount = statusCounts["Pending"],
            TerminatedCount = statusCounts["Terminated"],
            SuspendedCount = statusCounts["Suspended"],
            ContinuedAsNewCount = statusCounts["ContinuedAsNew"]
        };
    }

    /// <summary>
    /// Lists queues associated with a task hub.
    /// </summary>
    public async Task<List<string>> ListTaskHubQueuesAsync(
        string taskHubName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing queues for hub: {TaskHubName}", taskHubName);
        
        var queues = new List<string>();
        var queuePrefix = taskHubName.ToLowerInvariant();

        await foreach (var queue in _queueServiceClient.GetQueuesAsync(prefix: queuePrefix, cancellationToken: cancellationToken))
        {
            queues.Add(queue.Name);
        }

        return queues;
    }

    /// <summary>
    /// Peeks at messages in a queue without removing them.
    /// </summary>
    public async Task<List<QueueMessageInfo>> PeekQueueMessagesAsync(
        string queueName,
        int maxMessages = 32,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Peeking at messages in queue: {QueueName}", queueName);
        
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        var messages = new List<QueueMessageInfo>();

        try
        {
            var response = await queueClient.PeekMessagesAsync(maxMessages, cancellationToken);
            
            foreach (var msg in response.Value)
            {
                messages.Add(new QueueMessageInfo
                {
                    MessageId = msg.MessageId,
                    QueueName = queueName,
                    MessageText = msg.MessageText,
                    InsertedOn = msg.InsertedOn,
                    ExpiresOn = msg.ExpiresOn,
                    DequeueCount = msg.DequeueCount
                });
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Queue not found: {QueueName}", queueName);
        }

        return messages;
    }

    /// <summary>
    /// Gets approximate message count for a queue.
    /// </summary>
    public async Task<int> GetQueueMessageCountAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting message count for queue: {QueueName}", queueName);
        
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        
        try
        {
            var properties = await queueClient.GetPropertiesAsync(cancellationToken);
            return properties.Value.ApproximateMessagesCount;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    /// <summary>
    /// Lists blob containers associated with a task hub.
    /// </summary>
    public async Task<List<string>> ListTaskHubContainersAsync(
        string taskHubName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing blob containers for hub: {TaskHubName}", taskHubName);
        
        var containers = new List<string>();
        var containerPrefix = taskHubName.ToLowerInvariant();

        await foreach (var container in _blobServiceClient.GetBlobContainersAsync(prefix: containerPrefix, cancellationToken: cancellationToken))
        {
            containers.Add(container.Name);
        }

        return containers;
    }

    /// <summary>
    /// Lists large message blobs for a task hub.
    /// </summary>
    public async Task<List<string>> ListLargeMessagesAsync(
        string taskHubName,
        int? top = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing large messages for hub: {TaskHubName}", taskHubName);
        
        var containerName = $"{taskHubName.ToLowerInvariant()}-largemessages";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobs = new List<string>();

        try
        {
            var count = 0;
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                if (top.HasValue && count >= top.Value)
                    break;
                    
                blobs.Add(blob.Name);
                count++;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Large messages container not found for hub: {TaskHubName}", taskHubName);
        }

        return blobs;
    }

    /// <summary>
    /// Gets the content of a large message blob.
    /// </summary>
    public async Task<string?> GetLargeMessageContentAsync(
        string taskHubName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting large message content: {BlobName} from hub: {TaskHubName}", blobName, taskHubName);
        
        var containerName = $"{taskHubName.ToLowerInvariant()}-largemessages";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        try
        {
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Large message blob not found: {BlobName}", blobName);
            return null;
        }
    }

    /// <summary>
    /// Gets failed orchestrations (status = Failed) with their error details from history.
    /// </summary>
    public async Task<List<(OrchestrationInstance Instance, string? ErrorMessage)>> GetFailedOrchestrationsAsync(
        string taskHubName,
        int? top = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting failed orchestrations from hub: {TaskHubName}", taskHubName);
        
        var instances = await ListOrchestrationInstancesAsync(taskHubName, runtimeStatus: "Failed", top: top, cancellationToken: cancellationToken);
        var results = new List<(OrchestrationInstance, string?)>();

        foreach (var instance in instances)
        {
            // Get the last history event to find the error
            var history = await GetOrchestrationHistoryAsync(taskHubName, instance.InstanceId, cancellationToken: cancellationToken);
            var failedEvent = history.LastOrDefault(h => h.EventType == "TaskFailed" || h.EventType == "SubOrchestrationInstanceFailed" || h.EventType == "ExecutionCompleted");
            var errorMessage = failedEvent?.Reason ?? failedEvent?.Result;
            results.Add((instance, errorMessage));
        }

        return results;
    }

    private static OrchestrationInstance MapToOrchestrationInstance(TableEntity entity, string taskHubName)
    {
        return new OrchestrationInstance
        {
            InstanceId = entity.PartitionKey,
            Name = entity.GetString("Name"),
            RuntimeStatus = entity.GetString("RuntimeStatus"),
            Input = entity.GetString("Input"),
            Output = entity.GetString("Output"),
            CustomStatus = entity.GetString("CustomStatus"),
            CreatedTime = entity.GetDateTimeOffset("CreatedTime"),
            LastUpdatedTime = entity.GetDateTimeOffset("LastUpdatedTime"),
            CompletedTime = entity.GetDateTimeOffset("CompletedTime"),
            ExecutionId = entity.GetString("ExecutionId"),
            TaskHubName = taskHubName
        };
    }

    private static HistoryEvent MapToHistoryEvent(TableEntity entity)
    {
        return new HistoryEvent
        {
            InstanceId = entity.PartitionKey,
            SequenceNumber = entity.RowKey,
            EventType = entity.GetString("EventType"),
            Timestamp = entity.GetDateTimeOffset("Timestamp") ?? entity.Timestamp,
            Name = entity.GetString("Name"),
            Input = entity.GetString("Input"),
            Result = entity.GetString("Result"),
            TaskScheduledId = entity.GetInt32("TaskScheduledId"),
            ScheduledTime = entity.GetDateTimeOffset("ScheduledTime"),
            FireAt = entity.GetDateTimeOffset("FireAt"),
            OrchestrationStatus = entity.GetString("OrchestrationStatus"),
            ExecutionId = entity.GetString("ExecutionId"),
            Reason = entity.GetString("Reason"),
            IsPlayed = entity.GetBoolean("IsPlayed")
        };
    }
}
