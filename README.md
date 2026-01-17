# DurableTask.Mcp

[![NuGet](https://img.shields.io/nuget/v/DurableTask.Mcp.svg)](https://www.nuget.org/packages/DurableTask.Mcp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A Model Context Protocol (MCP) server for debugging, monitoring, and troubleshooting [Durable Task Framework (DTFx)](https://github.com/Azure/durabletask) orchestrations stored in Azure Storage.

> **Note:** This project is currently a work in progress. More storage providers coming soon!

## Features

| Feature | Description |
|---------|-------------|
| **Task Hub Discovery** | Automatically discovers all DTFx task hubs in your Azure Storage account |
| **Orchestration Inspection** | List, search, and inspect orchestration instances |
| **History Analysis** | Deep dive into orchestration execution history with specialized views |
| **Queue Monitoring** | Inspect control queues and work-item queues for pending messages |
| **Large Message Access** | Retrieve large payloads stored in blob storage |

## Installation

### As a .NET Tool (Recommended)

```bash
dotnet tool install --global DurableTask.Mcp
```

Once installed, run the tool using:

```bash
dtfx-mcp --storage-account <your-storage-account>
```

### Using dnx (MCP Runner)

If you have [dnx](https://github.com/AzureMCP/dnx) installed, you can run the tool directly without installing it globally:

```bash
dnx DurableTask.Mcp -- --storage-account <your-storage-account>
```

Or with logging enabled:

```bash
dnx DurableTask.Mcp -- --storage-account <your-storage-account> --log-file ./dtfx-mcp.log
```

### From Source

```bash
git clone https://github.com/MoaidHathot/DurableTasksMcp.git
cd DurableTasksMcp/src
dotnet build
dotnet run -- --storage-account <your-storage-account>
```

## Prerequisites

- .NET 10.0 SDK or later
- Azure Storage account with DTFx data
- Azure credentials configured (Azure CLI, environment variables, or managed identity)

## Configuration

### Storage Account (Required)

Provide the Azure Storage account name via command line or environment variable:

```bash
# Command line
dtfx-mcp --storage-account mystorageaccount
dtfx-mcp -s mystorageaccount

# Environment variable
export DTFX_STORAGE_ACCOUNT=mystorageaccount
dtfx-mcp
```

### Logging (Optional)

Enable file logging for debugging:

```bash
# Command line
dtfx-mcp -s mystorageaccount --log-file ./dtfx-mcp.log
dtfx-mcp -s mystorageaccount -l ./dtfx-mcp.log

# Environment variable
export DTFX_LOG_FILE=./dtfx-mcp.log
dtfx-mcp -s mystorageaccount
```

### Authentication

The server uses `DefaultAzureCredential` which supports:

- Azure CLI (`az login`)
- Environment variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`)
- Managed Identity (when running in Azure)
- Visual Studio / VS Code credentials

### Required Azure RBAC Permissions

Your identity needs these roles on the storage account:

| Role | Purpose |
|------|---------|
| **Storage Table Data Reader** | Reading orchestration instances and history |
| **Storage Queue Data Reader** | Peeking at queue messages |
| **Storage Blob Data Reader** | Reading large message blobs |

## MCP Client Configuration

### Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "durable-tasks": {
      "command": "dtfx-mcp",
      "args": ["-s", "mystorageaccount"]
    }
  }
}
```

With logging enabled:

```json
{
  "mcpServers": {
    "durable-tasks": {
      "command": "dtfx-mcp",
      "args": ["-s", "mystorageaccount", "-l", "/path/to/dtfx-mcp.log"]
    }
  }
}
```

### Using dnx

You can also configure Claude Desktop to use `dnx` to run the tool:

```json
{
  "mcpServers": {
    "durable-tasks": {
      "command": "dnx",
      "args": ["DurableTask.Mcp", "--", "-s", "mystorageaccount"]
    }
  }
}
```

## Available Tools

### Task Hub Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_task_hubs` | Lists all Durable Task Framework task hubs discovered in the configured Azure Storage account. Returns information about each hub's resources (tables, queues, containers). |
| `dtfx_get_task_hub_details` | Gets detailed information about a specific task hub, including its tables, queues, and blob containers. |
| `dtfx_get_orchestration_summary` | Gets summary statistics for orchestrations in a task hub, including counts by runtime status (Running, Completed, Failed, etc.). |

### Orchestration Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_orchestrations` | Lists orchestration instances from a task hub with optional filtering by status, name, and creation time. |
| `dtfx_get_orchestration` | Gets detailed information about a specific orchestration instance by its instance ID. |
| `dtfx_search_orchestrations` | Searches for orchestration instances by instance ID prefix. Useful for finding related orchestrations or sub-orchestrations. |
| `dtfx_get_failed_orchestrations` | Gets orchestrations with Failed status along with their error messages from history. Useful for troubleshooting. |
| `dtfx_list_running_orchestrations` | Lists all currently running orchestrations. Useful for monitoring active work. |
| `dtfx_list_pending_orchestrations` | Lists orchestrations in Pending status (scheduled but not yet started). Useful for identifying backlog. |

### History Tools

| Tool | Description |
|------|-------------|
| `dtfx_get_orchestration_history` | Gets the complete execution history for an orchestration instance. Shows all events including activity executions, timers, sub-orchestrations, and external events. |
| `dtfx_get_activity_history` | Gets only the activity-related history events (TaskScheduled, TaskCompleted, TaskFailed) for an orchestration. Useful for understanding the activity execution flow. |
| `dtfx_get_sub_orchestration_history` | Gets sub-orchestration related history events for an orchestration. Shows when sub-orchestrations were created and their completion status. |
| `dtfx_get_timer_history` | Gets timer-related history events for an orchestration. Shows durable timers that were created and when they fired. |
| `dtfx_get_external_events` | Gets external event history for an orchestration. Shows events that were raised from outside the orchestration (e.g., via RaiseEventAsync). |
| `dtfx_get_failed_activities` | Gets all failed activity executions for an orchestration. Shows the activity name and failure reason. |
| `dtfx_get_history_summary` | Gets a summary of the orchestration history showing counts of each event type and key milestones. |

### Queue Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_queues` | Lists all queues associated with a task hub, including control queues (for orchestrators) and work-item queue (for activities). |
| `dtfx_peek_queue_messages` | Peeks at messages in a queue without removing them. Useful for debugging stuck orchestrations or activities. |
| `dtfx_get_queue_depth` | Gets the approximate message count for a specific queue. Useful for monitoring backlog. |
| `dtfx_get_all_queue_depths` | Gets the approximate message counts for all queues in a task hub. Useful for monitoring overall system health. |

### Blob Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_containers` | Lists all blob containers associated with a task hub (large messages, leases, etc.). |
| `dtfx_list_large_messages` | Lists large message blobs stored in the task hub's large messages container. These are payloads that exceeded the queue message size limit (>45KB). |
| `dtfx_get_large_message_content` | Gets the content of a specific large message blob. Useful for inspecting large payloads that couldn't fit in queue messages. |

## Example Prompts

Once connected via an MCP client, try these:

- *"List all task hubs in my storage account"*
- *"Show me all failed orchestrations in MyTaskHub"*
- *"Get the execution history for orchestration instance abc-123"*
- *"What activities failed in this orchestration?"*
- *"Show me the queue depths for all control queues"*
- *"Search for orchestrations starting with 'order-'"*

## DTFx Storage Structure

The MCP server reads from these Azure Storage resources:

### Tables
| Table | Description |
|-------|-------------|
| `{TaskHubName}Instances` | Current state of all orchestration instances |
| `{TaskHubName}History` | Event-sourced history for all orchestrations |

### Queues
| Queue | Description |
|-------|-------------|
| `{taskhubname}-workitems` | Activity function messages |
| `{taskhubname}-control-00` to `-{N}` | Orchestrator messages |

### Blob Containers
| Container | Description |
|-----------|-------------|
| `{taskhubname}-largemessages` | Payloads exceeding 45KB |
| `{taskhubname}-leases` | Worker partition leases |

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Author

**Moaid Hathot**

- GitHub: [@MoaidHathot](https://github.com/MoaidHathot)
