# DurableTasksMcp

A Model Context Protocol (MCP) server for debugging, monitoring, and troubleshooting Durable Task Framework (DTFx) orchestrations stored in Azure Storage.

## Features

- **Task Hub Discovery**: Automatically discovers all DTFx task hubs in your Azure Storage account
- **Orchestration Inspection**: List, search, and inspect orchestration instances
- **History Analysis**: Deep dive into orchestration execution history with specialized views for activities, timers, sub-orchestrations, and external events
- **Queue Monitoring**: Inspect control queues and work-item queues for pending messages
- **Large Message Access**: Retrieve large payloads stored in blob storage

## Prerequisites

- .NET 10.0 SDK
- Azure Storage account with DTFx data
- Azure credentials configured (Azure CLI, environment variables, or managed identity)

## Installation

```bash
# Clone the repository
git clone <repository-url>
cd DurableTasksMcp

# Build the project
dotnet build

# Or publish for distribution
dotnet publish -c Release -o ./publish
```

## Configuration

### Storage Account (Required)

The MCP server requires an Azure Storage account name. You can provide it via:

1. **Command line argument**:
   ```bash
   dotnet run -- --storage-account mystorageaccount
   # or
   dotnet run -- -s mystorageaccount
   ```

2. **Environment variable**:
   ```bash
   export DTFX_STORAGE_ACCOUNT=mystorageaccount
   dotnet run
   ```

### Logging (Optional)

By default, warnings and errors are logged to stderr. To enable file logging for debugging:

1. **Command line argument**:
   ```bash
   dotnet run -- -s mystorageaccount --log-file ./dtfx-mcp.log
   # or
   dotnet run -- -s mystorageaccount -l ./dtfx-mcp.log
   ```

2. **Environment variable**:
   ```bash
   export DTFX_LOG_FILE=./dtfx-mcp.log
   dotnet run -- -s mystorageaccount
   ```

When file logging is enabled:
- The log file is created or cleared on startup
- All log levels (Debug and above) are written to the file
- Log format: `[timestamp] [level] [category] message`

### Authentication

The server uses `DefaultAzureCredential` which supports multiple authentication methods:

- Azure CLI (`az login`)
- Environment variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`)
- Managed Identity (when running in Azure)
- Visual Studio / VS Code credentials

### Required Azure RBAC Permissions

The identity used must have the following permissions on the storage account:

- **Storage Table Data Reader** - for reading orchestration instances and history
- **Storage Queue Data Reader** - for peeking at queue messages
- **Storage Blob Data Reader** - for reading large message blobs

## MCP Client Configuration

### Claude Desktop (claude_desktop_config.json)

```json
{
  "mcpServers": {
    "durable-tasks": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/DurableTasksMcp", "--", "-s", "mystorageaccount", "-l", "/path/to/dtfx-mcp.log"]
    }
  }
}
```

Or using a published executable:

```json
{
  "mcpServers": {
    "durable-tasks": {
      "command": "/path/to/DurableTasksMcp",
      "args": ["-s", "mystorageaccount", "-l", "/path/to/dtfx-mcp.log"]
    }
  }
}
```

## Available Tools

### Task Hub Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_task_hubs` | Lists all DTFx task hubs discovered in the storage account |
| `dtfx_get_task_hub_details` | Gets detailed information about a specific task hub |
| `dtfx_get_orchestration_summary` | Gets summary statistics by runtime status |

### Orchestration Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_orchestrations` | Lists orchestration instances with filtering options |
| `dtfx_get_orchestration` | Gets details of a specific orchestration by instance ID |
| `dtfx_search_orchestrations` | Searches orchestrations by instance ID prefix |
| `dtfx_get_failed_orchestrations` | Gets failed orchestrations with error messages |
| `dtfx_list_running_orchestrations` | Lists all currently running orchestrations |
| `dtfx_list_pending_orchestrations` | Lists orchestrations waiting to start |

### History Tools

| Tool | Description |
|------|-------------|
| `dtfx_get_orchestration_history` | Gets complete execution history for an orchestration |
| `dtfx_get_activity_history` | Gets only activity-related events |
| `dtfx_get_sub_orchestration_history` | Gets sub-orchestration events |
| `dtfx_get_timer_history` | Gets timer events |
| `dtfx_get_external_events` | Gets external events raised to the orchestration |
| `dtfx_get_failed_activities` | Gets all failed activity executions |
| `dtfx_get_history_summary` | Gets a summary of the execution history |

### Queue Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_queues` | Lists all queues for a task hub with message counts |
| `dtfx_peek_queue_messages` | Peeks at messages in a queue |
| `dtfx_get_queue_depth` | Gets approximate message count for a queue |
| `dtfx_get_all_queue_depths` | Gets message counts for all queues in a hub |

### Blob Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_containers` | Lists blob containers for a task hub |
| `dtfx_list_large_messages` | Lists large message blobs |
| `dtfx_get_large_message_content` | Gets content of a large message blob |

## Example Usage

Once connected via an MCP client, you can ask questions like:

- "List all task hubs in my storage account"
- "Show me all failed orchestrations in the MyTaskHub"
- "Get the execution history for orchestration instance abc-123"
- "What activities failed in this orchestration?"
- "Show me the queue depths for all control queues"
- "Search for orchestrations starting with 'order-'"

## DTFx Storage Structure

The MCP server reads from these Azure Storage resources:

### Tables
- `{TaskHubName}Instances` - Current state of all orchestration instances
- `{TaskHubName}History` - Event-sourced history for all orchestrations

### Queues
- `{taskhubname}-workitems` - Activity function messages
- `{taskhubname}-control-00` to `{taskhubname}-control-{N}` - Orchestrator messages

### Blob Containers
- `{taskhubname}-largemessages` - Payloads exceeding 45KB
- `{taskhubname}-leases` - Worker partition leases

## License

MIT
