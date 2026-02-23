using System.Text.Json;
using DurableTasksMcp.Models;
using DurableTasksMcp.Services;
using DurableTasksMcp.Tools;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DurableTasksMcp.Tests.Tools;

public class TaskHubToolsTests
{
    private readonly IDurableTaskStorageService _storageService;
    private readonly TaskHubTools _sut;

    public TaskHubToolsTests()
    {
        _storageService = Substitute.For<IDurableTaskStorageService>();
        _sut = new TaskHubTools(_storageService);
    }

    #region ListTaskHubs Tests

    [Fact]
    public async Task ListTaskHubs_WhenNoHubs_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListTaskHubsAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListTaskHubs();

        // Assert
        result.Should().Contain("No task hubs found");
    }

    [Fact]
    public async Task ListTaskHubs_WhenHubsExist_ReturnsJsonArray()
    {
        // Arrange
        var hubs = new List<TaskHub>
        {
            new() 
            { 
                Name = "Hub1", 
                HasInstancesTable = true, 
                HasHistoryTable = true,
                ControlQueueCount = 4,
                HasWorkItemQueue = true,
                HasLargeMessagesContainer = true,
                HasLeasesContainer = true
            },
            new() 
            { 
                Name = "Hub2", 
                HasInstancesTable = true, 
                HasHistoryTable = false,
                ControlQueueCount = 2,
                HasWorkItemQueue = true,
                HasLargeMessagesContainer = false,
                HasLeasesContainer = false
            }
        };
        _storageService.ListTaskHubsAsync(Arg.Any<CancellationToken>())
            .Returns(hubs);

        // Act
        var result = await _sut.ListTaskHubs();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetArrayLength().Should().Be(2);
        
        var hub1 = json.RootElement[0];
        hub1.GetProperty("Name").GetString().Should().Be("Hub1");
        hub1.GetProperty("HasInstancesTable").GetBoolean().Should().BeTrue();
        hub1.GetProperty("ControlQueueCount").GetInt32().Should().Be(4);
    }

    #endregion

    #region GetTaskHubDetails Tests

    [Fact]
    public async Task GetTaskHubDetails_ReturnsHubDetails()
    {
        // Arrange
        var hub = new TaskHub
        {
            Name = "MyHub",
            HasInstancesTable = true,
            HasHistoryTable = true,
            ControlQueueCount = 4,
            HasWorkItemQueue = true,
            HasLargeMessagesContainer = true,
            HasLeasesContainer = true
        };
        _storageService.GetTaskHubDetailsAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(hub);

        // Act
        var result = await _sut.GetTaskHubDetails("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("Name").GetString().Should().Be("MyHub");
        json.RootElement.GetProperty("HasInstancesTable").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("HasHistoryTable").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("ControlQueueCount").GetInt32().Should().Be(4);
        json.RootElement.GetProperty("HasWorkItemQueue").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("HasLargeMessagesContainer").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("HasLeasesContainer").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetTaskHubDetails_WhenPartialResources_ReturnsCorrectState()
    {
        // Arrange
        var hub = new TaskHub
        {
            Name = "PartialHub",
            HasInstancesTable = true,
            HasHistoryTable = false,
            ControlQueueCount = 0,
            HasWorkItemQueue = false,
            HasLargeMessagesContainer = false,
            HasLeasesContainer = false
        };
        _storageService.GetTaskHubDetailsAsync("PartialHub", Arg.Any<CancellationToken>())
            .Returns(hub);

        // Act
        var result = await _sut.GetTaskHubDetails("PartialHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("HasInstancesTable").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("HasHistoryTable").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("ControlQueueCount").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("HasWorkItemQueue").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region GetOrchestrationSummary Tests

    [Fact]
    public async Task GetOrchestrationSummary_ReturnsSummaryStatistics()
    {
        // Arrange
        var summary = new OrchestrationSummary
        {
            TaskHubName = "MyHub",
            TotalCount = 100,
            RunningCount = 10,
            CompletedCount = 75,
            FailedCount = 5,
            PendingCount = 3,
            TerminatedCount = 2,
            SuspendedCount = 1,
            ContinuedAsNewCount = 4
        };
        _storageService.GetOrchestrationSummaryAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(summary);

        // Act
        var result = await _sut.GetOrchestrationSummary("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("TaskHubName").GetString().Should().Be("MyHub");
        json.RootElement.GetProperty("TotalCount").GetInt32().Should().Be(100);
        json.RootElement.GetProperty("RunningCount").GetInt32().Should().Be(10);
        json.RootElement.GetProperty("CompletedCount").GetInt32().Should().Be(75);
        json.RootElement.GetProperty("FailedCount").GetInt32().Should().Be(5);
        json.RootElement.GetProperty("PendingCount").GetInt32().Should().Be(3);
        json.RootElement.GetProperty("TerminatedCount").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("SuspendedCount").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("ContinuedAsNewCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task GetOrchestrationSummary_WhenEmpty_ReturnsZeroCounts()
    {
        // Arrange
        var summary = new OrchestrationSummary
        {
            TaskHubName = "EmptyHub",
            TotalCount = 0,
            RunningCount = 0,
            CompletedCount = 0,
            FailedCount = 0,
            PendingCount = 0,
            TerminatedCount = 0,
            SuspendedCount = 0,
            ContinuedAsNewCount = 0
        };
        _storageService.GetOrchestrationSummaryAsync("EmptyHub", Arg.Any<CancellationToken>())
            .Returns(summary);

        // Act
        var result = await _sut.GetOrchestrationSummary("EmptyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("TotalCount").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("RunningCount").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("CompletedCount").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("FailedCount").GetInt32().Should().Be(0);
    }

    #endregion
}
