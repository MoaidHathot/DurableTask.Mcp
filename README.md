# DurableTasksMcp

[![NuGet](https://img.shields.io/nuget/v/DurableTasksMcp.svg)](https://www.nuget.org/packages/DurableTasksMcp/)
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
dotnet tool install --global DurableTasksMcp
```

Once installed, run the tool using:

```bash
dtfx-mcp --storage-account <your-storage-account>
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

## Available Tools

### Task Hub Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_task_hubs` | Lists all DTFx task hubs in the storage account |
| `dtfx_get_task_hub_details` | Gets detailed information about a specific task hub |
| `dtfx_get_orchestration_summary` | Gets summary statistics by runtime status |

### Orchestration Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_orchestrations` | Lists orchestration instances with filtering |
| `dtfx_get_orchestration` | Gets details of a specific orchestration |
| `dtfx_search_orchestrations` | Searches orchestrations by instance ID prefix |
| `dtfx_get_failed_orchestrations` | Gets failed orchestrations with error messages |
| `dtfx_list_running_orchestrations` | Lists currently running orchestrations |
| `dtfx_list_pending_orchestrations` | Lists orchestrations waiting to start |

### History Tools

| Tool | Description |
|------|-------------|
| `dtfx_get_orchestration_history` | Gets complete execution history |
| `dtfx_get_activity_history` | Gets activity-related events only |
| `dtfx_get_sub_orchestration_history` | Gets sub-orchestration events |
| `dtfx_get_timer_history` | Gets timer events |
| `dtfx_get_external_events` | Gets external events raised to orchestration |
| `dtfx_get_failed_activities` | Gets all failed activity executions |
| `dtfx_get_history_summary` | Gets execution history summary |

### Queue Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_queues` | Lists queues for a task hub with message counts |
| `dtfx_peek_queue_messages` | Peeks at messages in a queue |
| `dtfx_get_queue_depth` | Gets approximate message count for a queue |
| `dtfx_get_all_queue_depths` | Gets message counts for all queues |

### Blob Tools

| Tool | Description |
|------|-------------|
| `dtfx_list_containers` | Lists blob containers for a task hub |
| `dtfx_list_large_messages` | Lists large message blobs |
| `dtfx_get_large_message_content` | Gets content of a large message blob |

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
