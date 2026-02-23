using System.Text.Json;
using DurableTasksMcp.Models;
using DurableTasksMcp.Services;
using DurableTasksMcp.Tools;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DurableTasksMcp.Tests.Tools;

public class OrchestrationToolsTests
{
    private readonly IDurableTaskStorageService _storageService;
    private readonly OrchestrationTools _sut;

    public OrchestrationToolsTests()
    {
        _storageService = Substitute.For<IDurableTaskStorageService>();
        _sut = new OrchestrationTools(_storageService);
    }

    #region ListOrchestrations Tests

    [Fact]
    public async Task ListOrchestrations_WhenNoInstances_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", null, null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListOrchestrations("MyHub");

        // Assert
        result.Should().Contain("No orchestration instances found");
    }

    [Fact]
    public async Task ListOrchestrations_WhenInstancesExist_ReturnsJsonArray()
    {
        // Arrange
        var instances = new List<OrchestrationInstance>
        {
            new() { InstanceId = "instance-1", Name = "MyOrchestrator", RuntimeStatus = "Running" },
            new() { InstanceId = "instance-2", Name = "MyOrchestrator", RuntimeStatus = "Completed" }
        };
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", null, null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns(instances);

        // Act
        var result = await _sut.ListOrchestrations("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ListOrchestrations_PassesStatusFilter()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", "Running", null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.ListOrchestrations("MyHub", runtimeStatus: "Running");

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", "Running", null, null, null, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrchestrations_PassesNameFilter()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", null, "ProcessOrder", null, null, 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.ListOrchestrations("MyHub", nameFilter: "ProcessOrder");

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", null, "ProcessOrder", null, null, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrchestrations_ParsesDateFilters()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", null, null, Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.ListOrchestrations("MyHub", 
            createdAfter: "2026-01-01T00:00:00Z", 
            createdBefore: "2026-01-31T23:59:59Z");

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", null, null, 
            Arg.Is<DateTimeOffset?>(d => d.HasValue && d.Value.Year == 2026 && d.Value.Month == 1 && d.Value.Day == 1),
            Arg.Is<DateTimeOffset?>(d => d.HasValue && d.Value.Year == 2026 && d.Value.Month == 1 && d.Value.Day == 31),
            100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrchestrations_InvalidDateFormat_IgnoresFilter()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", null, null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.ListOrchestrations("MyHub", createdAfter: "invalid-date");

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", null, null, null, null, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrchestrations_PassesTopParameter()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", null, null, null, null, 50, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.ListOrchestrations("MyHub", top: 50);

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", null, null, null, null, 50, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetOrchestration Tests

    [Fact]
    public async Task GetOrchestration_WhenInstanceNotFound_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.GetOrchestrationInstanceAsync("MyHub", "nonexistent", Arg.Any<CancellationToken>())
            .Returns((OrchestrationInstance?)null);

        // Act
        var result = await _sut.GetOrchestration("MyHub", "nonexistent");

        // Assert
        result.Should().Contain("not found");
        result.Should().Contain("nonexistent");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task GetOrchestration_WhenInstanceExists_ReturnsJson()
    {
        // Arrange
        var instance = new OrchestrationInstance
        {
            InstanceId = "instance-123",
            Name = "ProcessOrder",
            RuntimeStatus = "Completed",
            Input = "{\"orderId\": 42}",
            Output = "{\"result\": \"success\"}"
        };
        _storageService.GetOrchestrationInstanceAsync("MyHub", "instance-123", Arg.Any<CancellationToken>())
            .Returns(instance);

        // Act
        var result = await _sut.GetOrchestration("MyHub", "instance-123");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("InstanceId").GetString().Should().Be("instance-123");
        json.RootElement.GetProperty("Name").GetString().Should().Be("ProcessOrder");
        json.RootElement.GetProperty("RuntimeStatus").GetString().Should().Be("Completed");
    }

    #endregion

    #region SearchOrchestrations Tests

    [Fact]
    public async Task SearchOrchestrations_WhenNoMatches_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.SearchOrchestrationsByInstanceIdAsync("MyHub", "order-", 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.SearchOrchestrations("MyHub", "order-");

        // Assert
        result.Should().Contain("No orchestration instances found");
        result.Should().Contain("order-");
    }

    [Fact]
    public async Task SearchOrchestrations_WhenMatchesFound_ReturnsJsonArray()
    {
        // Arrange
        var instances = new List<OrchestrationInstance>
        {
            new() { InstanceId = "order-001", Name = "ProcessOrder" },
            new() { InstanceId = "order-002", Name = "ProcessOrder" }
        };
        _storageService.SearchOrchestrationsByInstanceIdAsync("MyHub", "order-", 100, Arg.Any<CancellationToken>())
            .Returns(instances);

        // Act
        var result = await _sut.SearchOrchestrations("MyHub", "order-");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task SearchOrchestrations_PassesTopParameter()
    {
        // Arrange
        _storageService.SearchOrchestrationsByInstanceIdAsync("MyHub", "order-", 25, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.SearchOrchestrations("MyHub", "order-", top: 25);

        // Assert
        await _storageService.Received(1).SearchOrchestrationsByInstanceIdAsync("MyHub", "order-", 25, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetFailedOrchestrations Tests

    [Fact]
    public async Task GetFailedOrchestrations_WhenNoFailures_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.GetFailedOrchestrationsAsync("MyHub", 50, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.GetFailedOrchestrations("MyHub");

        // Assert
        result.Should().Contain("No failed orchestrations found");
    }

    [Fact]
    public async Task GetFailedOrchestrations_ReturnsFormattedResults()
    {
        // Arrange
        var failed = new List<(OrchestrationInstance Instance, string? ErrorMessage)>
        {
            (new OrchestrationInstance 
            { 
                InstanceId = "failed-1", 
                Name = "ProcessOrder",
                CreatedTime = DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
                LastUpdatedTime = DateTimeOffset.Parse("2026-01-01T10:05:00Z"),
                ExecutionId = "exec-1"
            }, "Database connection failed"),
            (new OrchestrationInstance 
            { 
                InstanceId = "failed-2", 
                Name = "SendEmail",
                CreatedTime = DateTimeOffset.Parse("2026-01-01T11:00:00Z"),
                LastUpdatedTime = DateTimeOffset.Parse("2026-01-01T11:01:00Z"),
                ExecutionId = "exec-2"
            }, "SMTP server unavailable")
        };
        _storageService.GetFailedOrchestrationsAsync("MyHub", 50, Arg.Any<CancellationToken>())
            .Returns(failed);

        // Act
        var result = await _sut.GetFailedOrchestrations("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
        
        var first = json.RootElement[0];
        first.GetProperty("InstanceId").GetString().Should().Be("failed-1");
        first.GetProperty("Name").GetString().Should().Be("ProcessOrder");
        first.GetProperty("ErrorMessage").GetString().Should().Be("Database connection failed");
    }

    [Fact]
    public async Task GetFailedOrchestrations_PassesTopParameter()
    {
        // Arrange
        _storageService.GetFailedOrchestrationsAsync("MyHub", 20, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.GetFailedOrchestrations("MyHub", top: 20);

        // Assert
        await _storageService.Received(1).GetFailedOrchestrationsAsync("MyHub", 20, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ListRunningOrchestrations Tests

    [Fact]
    public async Task ListRunningOrchestrations_WhenNoRunning_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", "Running", null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListRunningOrchestrations("MyHub");

        // Assert
        result.Should().Contain("No running orchestrations found");
    }

    [Fact]
    public async Task ListRunningOrchestrations_FiltersToRunningStatus()
    {
        // Arrange
        var instances = new List<OrchestrationInstance>
        {
            new() { InstanceId = "running-1", RuntimeStatus = "Running" }
        };
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", "Running", null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns(instances);

        // Act
        var result = await _sut.ListRunningOrchestrations("MyHub");

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", "Running", null, null, null, 100, Arg.Any<CancellationToken>());
        
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(1);
    }

    #endregion

    #region ListPendingOrchestrations Tests

    [Fact]
    public async Task ListPendingOrchestrations_WhenNoPending_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", "Pending", null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListPendingOrchestrations("MyHub");

        // Assert
        result.Should().Contain("No pending orchestrations found");
    }

    [Fact]
    public async Task ListPendingOrchestrations_FiltersToPendingStatus()
    {
        // Arrange
        var instances = new List<OrchestrationInstance>
        {
            new() { InstanceId = "pending-1", RuntimeStatus = "Pending" }
        };
        _storageService.ListOrchestrationInstancesAsync(
            "MyHub", "Pending", null, null, null, 100, Arg.Any<CancellationToken>())
            .Returns(instances);

        // Act
        var result = await _sut.ListPendingOrchestrations("MyHub");

        // Assert
        await _storageService.Received(1).ListOrchestrationInstancesAsync(
            "MyHub", "Pending", null, null, null, 100, Arg.Any<CancellationToken>());
        
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(1);
    }

    #endregion
}
