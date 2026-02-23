using System.ComponentModel;
using System.Text.Json;
using DurableTasksMcp.Services;
using ModelContextProtocol.Server;

namespace DurableTasksMcp.Tools;

/// <summary>
/// MCP tools for inspecting Durable Task Framework queues and messages.
/// </summary>
[McpServerToolType]
public sealed class QueueTools
{
    private readonly IDurableTaskStorageService _storageService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public QueueTools(IDurableTaskStorageService storageService)
    {
        _storageService = storageService;
    }

    [McpServerTool(Name = "dtfx_list_queues", ReadOnly = true)]
    [Description("Lists all queues associated with a task hub, including control queues (for orchestrators) and work-item queue (for activities).")]
    public async Task<string> ListQueues(
        [Description("The name of the task hub")] string taskHubName,
        CancellationToken cancellationToken = default)
    {
        var queues = await _storageService.ListTaskHubQueuesAsync(taskHubName, cancellationToken);

        if (queues.Count == 0)
        {
            return $"No queues found for task hub '{taskHubName}'.";
        }

        // Get message counts for each queue
        var queueInfo = new List<object>();
        foreach (var queueName in queues)
        {
            var messageCount = await _storageService.GetQueueMessageCountAsync(queueName, cancellationToken);
            queueInfo.Add(new
            {
                Name = queueName,
                ApproximateMessageCount = messageCount,
                Type = queueName.Contains("-control-") ? "Control" : queueName.EndsWith("-workitems") ? "WorkItem" : "Other"
            });
        }

        return JsonSerializer.Serialize(queueInfo, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_peek_queue_messages", ReadOnly = true)]
    [Description("Peeks at messages in a queue without removing them. Useful for debugging stuck orchestrations or activities.")]
    public async Task<string> PeekQueueMessages(
        [Description("The full name of the queue (e.g., 'mytaskhub-control-00' or 'mytaskhub-workitems')")] string queueName,
        [Description("Maximum number of messages to peek (max 32)")] int maxMessages = 32,
        CancellationToken cancellationToken = default)
    {
        if (maxMessages > 32)
        {
            maxMessages = 32; // Azure Storage limit
        }

        var messages = await _storageService.PeekQueueMessagesAsync(queueName, maxMessages, cancellationToken);

        if (messages.Count == 0)
        {
            return $"No messages found in queue '{queueName}'.";
        }

        return JsonSerializer.Serialize(messages, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_queue_depth", ReadOnly = true)]
    [Description("Gets the approximate message count for a specific queue. Useful for monitoring backlog.")]
    public async Task<string> GetQueueDepth(
        [Description("The full name of the queue")] string queueName,
        CancellationToken cancellationToken = default)
    {
        var count = await _storageService.GetQueueMessageCountAsync(queueName, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            QueueName = queueName,
            ApproximateMessageCount = count
        }, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_all_queue_depths", ReadOnly = true)]
    [Description("Gets the approximate message counts for all queues in a task hub. Useful for monitoring overall system health.")]
    public async Task<string> GetAllQueueDepths(
        [Description("The name of the task hub")] string taskHubName,
        CancellationToken cancellationToken = default)
    {
        var queues = await _storageService.ListTaskHubQueuesAsync(taskHubName, cancellationToken);

        if (queues.Count == 0)
        {
            return $"No queues found for task hub '{taskHubName}'.";
        }

        var depths = new List<object>();
        var totalMessages = 0;
        
        foreach (var queueName in queues)
        {
            var count = await _storageService.GetQueueMessageCountAsync(queueName, cancellationToken);
            totalMessages += count;
            depths.Add(new
            {
                QueueName = queueName,
                ApproximateMessageCount = count,
                Type = queueName.Contains("-control-") ? "Control" : queueName.EndsWith("-workitems") ? "WorkItem" : "Other"
            });
        }

        return JsonSerializer.Serialize(new
        {
            TaskHubName = taskHubName,
            TotalMessageCount = totalMessages,
            Queues = depths
        }, JsonOptions);
    }
}
