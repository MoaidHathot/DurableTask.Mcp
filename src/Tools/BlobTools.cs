using System.ComponentModel;
using System.Text.Json;
using DurableTasksMcp.Services;
using ModelContextProtocol.Server;

namespace DurableTasksMcp.Tools;

/// <summary>
/// MCP tools for inspecting Durable Task Framework blob storage (large messages).
/// </summary>
[McpServerToolType]
public sealed class BlobTools
{
    private readonly IDurableTaskStorageService _storageService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public BlobTools(IDurableTaskStorageService storageService)
    {
        _storageService = storageService;
    }

    [McpServerTool(Name = "dtfx_list_containers", ReadOnly = true)]
    [Description("Lists all blob containers associated with a task hub (large messages, leases, etc.).")]
    public async Task<string> ListContainers(
        [Description("The name of the task hub")] string taskHubName,
        CancellationToken cancellationToken = default)
    {
        var containers = await _storageService.ListTaskHubContainersAsync(taskHubName, cancellationToken);

        if (containers.Count == 0)
        {
            return $"No blob containers found for task hub '{taskHubName}'.";
        }

        return JsonSerializer.Serialize(containers, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_list_large_messages", ReadOnly = true)]
    [Description("Lists large message blobs stored in the task hub's large messages container. These are payloads that exceeded the queue message size limit (>45KB).")]
    public async Task<string> ListLargeMessages(
        [Description("The name of the task hub")] string taskHubName,
        [Description("Maximum number of blobs to list (default: 100)")] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var blobs = await _storageService.ListLargeMessagesAsync(taskHubName, top, cancellationToken);

        if (blobs.Count == 0)
        {
            return $"No large messages found for task hub '{taskHubName}'.";
        }

        return JsonSerializer.Serialize(new
        {
            TaskHubName = taskHubName,
            Container = $"{taskHubName.ToLowerInvariant()}-largemessages",
            BlobCount = blobs.Count,
            Blobs = blobs
        }, JsonOptions);
    }

    [McpServerTool(Name = "dtfx_get_large_message_content", ReadOnly = true)]
    [Description("Gets the content of a specific large message blob. Useful for inspecting large payloads that couldn't fit in queue messages.")]
    public async Task<string> GetLargeMessageContent(
        [Description("The name of the task hub")] string taskHubName,
        [Description("The name of the blob to retrieve")] string blobName,
        CancellationToken cancellationToken = default)
    {
        var content = await _storageService.GetLargeMessageContentAsync(taskHubName, blobName, cancellationToken);

        if (content == null)
        {
            return $"Large message blob '{blobName}' not found in task hub '{taskHubName}'.";
        }

        // Try to pretty-print if it's JSON
        try
        {
            var jsonDoc = JsonDocument.Parse(content);
            return JsonSerializer.Serialize(new
            {
                BlobName = blobName,
                ContentType = "application/json",
                Content = jsonDoc.RootElement
            }, JsonOptions);
        }
        catch
        {
            // Not JSON, return as-is
            return JsonSerializer.Serialize(new
            {
                BlobName = blobName,
                ContentType = "text/plain",
                Content = content
            }, JsonOptions);
        }
    }
}
