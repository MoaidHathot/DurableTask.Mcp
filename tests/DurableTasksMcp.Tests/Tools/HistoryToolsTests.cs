using System.Text.Json;
using DurableTasksMcp.Models;
using DurableTasksMcp.Services;
using DurableTasksMcp.Tools;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DurableTasksMcp.Tests.Tools;

public class HistoryToolsTests
{
    private readonly IDurableTaskStorageService _storageService;
    private readonly HistoryTools _sut;

    public HistoryToolsTests()
    {
        _storageService = Substitute.For<IDurableTaskStorageService>();
        _sut = new HistoryTools(_storageService);
    }

    #region GetOrchestrationHistory Tests

    [Fact]
    public async Task GetOrchestrationHistory_WhenNoHistory_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.GetOrchestrationHistoryAsync("MyHub", "instance-123", null, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.GetOrchestrationHistory("MyHub", "instance-123");

        // Assert
        result.Should().Contain("No history found");
        result.Should().Contain("instance-123");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task GetOrchestrationHistory_WhenHistoryExists_ReturnsJsonArray()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "instance-123", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "instance-123", SequenceNumber = "002", EventType = "TaskScheduled" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "instance-123", null, Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetOrchestrationHistory("MyHub", "instance-123");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetOrchestrationHistory_PassesTopParameter()
    {
        // Arrange
        _storageService.GetOrchestrationHistoryAsync("MyHub", "instance-123", 50, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.GetOrchestrationHistory("MyHub", "instance-123", top: 50);

        // Assert
        await _storageService.Received(1).GetOrchestrationHistoryAsync("MyHub", "instance-123", 50, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetActivityHistory Tests

    [Fact]
    public async Task GetActivityHistory_WhenNoActivityEvents_ReturnsNotFoundMessage()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "instance-123", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "instance-123", SequenceNumber = "002", EventType = "TimerCreated" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "instance-123", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetActivityHistory("MyHub", "instance-123");

        // Assert
        result.Should().Contain("No activity events found");
    }

    [Fact]
    public async Task GetActivityHistory_FiltersOnlyActivityEvents()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled", Name = "ProcessItem" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskCompleted", Name = "ProcessItem" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "TimerCreated" },
            new() { InstanceId = "i1", SequenceNumber = "005", EventType = "TaskFailed", Name = "FailingTask" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetActivityHistory("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(3);
        
        var events = json.RootElement.EnumerateArray().ToList();
        events.Should().AllSatisfy(e =>
        {
            var eventType = e.GetProperty("EventType").GetString();
            eventType.Should().BeOneOf("TaskScheduled", "TaskCompleted", "TaskFailed");
        });
    }

    #endregion

    #region GetSubOrchestrationHistory Tests

    [Fact]
    public async Task GetSubOrchestrationHistory_WhenNoSubOrchEvents_ReturnsNotFoundMessage()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetSubOrchestrationHistory("MyHub", "i1");

        // Assert
        result.Should().Contain("No sub-orchestration events found");
    }

    [Fact]
    public async Task GetSubOrchestrationHistory_FiltersOnlySubOrchestrationEvents()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "SubOrchestrationInstanceCreated", Name = "ChildOrchestrator" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "SubOrchestrationInstanceCompleted" },
            new() { InstanceId = "i1", SequenceNumber = "005", EventType = "SubOrchestrationInstanceFailed" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetSubOrchestrationHistory("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(3);
        
        var events = json.RootElement.EnumerateArray().ToList();
        events.Should().AllSatisfy(e =>
        {
            var eventType = e.GetProperty("EventType").GetString();
            eventType.Should().BeOneOf("SubOrchestrationInstanceCreated", "SubOrchestrationInstanceCompleted", "SubOrchestrationInstanceFailed");
        });
    }

    #endregion

    #region GetTimerHistory Tests

    [Fact]
    public async Task GetTimerHistory_WhenNoTimerEvents_ReturnsNotFoundMessage()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetTimerHistory("MyHub", "i1");

        // Assert
        result.Should().Contain("No timer events found");
    }

    [Fact]
    public async Task GetTimerHistory_FiltersOnlyTimerEvents()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TimerCreated", FireAt = DateTimeOffset.Parse("2026-01-01T10:05:00Z") },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "TimerFired" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetTimerHistory("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
        
        var events = json.RootElement.EnumerateArray().ToList();
        events.Should().AllSatisfy(e =>
        {
            var eventType = e.GetProperty("EventType").GetString();
            eventType.Should().BeOneOf("TimerCreated", "TimerFired");
        });
    }

    #endregion

    #region GetExternalEvents Tests

    [Fact]
    public async Task GetExternalEvents_WhenNoExternalEvents_ReturnsNotFoundMessage()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetExternalEvents("MyHub", "i1");

        // Assert
        result.Should().Contain("No external events found");
    }

    [Fact]
    public async Task GetExternalEvents_FiltersOnlyExternalEventTypes()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "EventRaised", Name = "ApprovalReceived" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "EventSent", Name = "Notification" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetExternalEvents("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
        
        var events = json.RootElement.EnumerateArray().ToList();
        events.Should().AllSatisfy(e =>
        {
            var eventType = e.GetProperty("EventType").GetString();
            eventType.Should().BeOneOf("EventRaised", "EventSent");
        });
    }

    #endregion

    #region GetFailedActivities Tests

    [Fact]
    public async Task GetFailedActivities_WhenNoFailedActivities_ReturnsNotFoundMessage()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskCompleted" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetFailedActivities("MyHub", "i1");

        // Assert
        result.Should().Contain("No failed activities found");
    }

    [Fact]
    public async Task GetFailedActivities_CorrelatesWithScheduledEvents()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "TaskScheduled", Name = "ProcessOrder", TaskScheduledId = 1 },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled", Name = "SendEmail", TaskScheduledId = 2 },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskFailed", TaskScheduledId = 1, Reason = "Database connection failed" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "TaskFailed", TaskScheduledId = 2, Result = "SMTP error" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetFailedActivities("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
        
        var failures = json.RootElement.EnumerateArray().ToList();
        failures[0].GetProperty("ActivityName").GetString().Should().Be("ProcessOrder");
        failures[0].GetProperty("FailureReason").GetString().Should().Be("Database connection failed");
        failures[1].GetProperty("ActivityName").GetString().Should().Be("SendEmail");
        failures[1].GetProperty("FailureReason").GetString().Should().Be("SMTP error");
    }

    [Fact]
    public async Task GetFailedActivities_WhenScheduledEventNotFound_ReturnsUnknownActivityName()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "TaskFailed", TaskScheduledId = 999, Reason = "Some error" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetFailedActivities("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        var failure = json.RootElement.EnumerateArray().First();
        failure.GetProperty("ActivityName").GetString().Should().Be("Unknown");
    }

    #endregion

    #region GetHistorySummary Tests

    [Fact]
    public async Task GetHistorySummary_WhenNoHistory_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.GetHistorySummary("MyHub", "i1");

        // Assert
        result.Should().Contain("No history found");
    }

    [Fact]
    public async Task GetHistorySummary_ReturnsTotalEventCount()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskCompleted" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetHistorySummary("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("TotalEvents").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetHistorySummary_ReturnsEventCounts()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "TaskCompleted" },
            new() { InstanceId = "i1", SequenceNumber = "005", EventType = "TaskCompleted" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetHistorySummary("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        var eventCounts = json.RootElement.GetProperty("EventCounts");
        eventCounts.GetProperty("ExecutionStarted").GetInt32().Should().Be(1);
        eventCounts.GetProperty("TaskScheduled").GetInt32().Should().Be(2);
        eventCounts.GetProperty("TaskCompleted").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetHistorySummary_ReturnsStartAndEndTimes()
    {
        // Arrange
        var startTime = DateTimeOffset.Parse("2026-01-01T10:00:00Z");
        var endTime = DateTimeOffset.Parse("2026-01-01T10:05:00Z");
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "ExecutionStarted", Timestamp = startTime },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "ExecutionCompleted", Timestamp = endTime, OrchestrationStatus = "Completed" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetHistorySummary("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("FinalStatus").GetString().Should().Be("Completed");
        json.RootElement.TryGetProperty("Duration", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetHistorySummary_ReturnsActivityStatistics()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { InstanceId = "i1", SequenceNumber = "001", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "002", EventType = "TaskScheduled" },
            new() { InstanceId = "i1", SequenceNumber = "003", EventType = "TaskCompleted" },
            new() { InstanceId = "i1", SequenceNumber = "004", EventType = "TaskFailed" },
            new() { InstanceId = "i1", SequenceNumber = "005", EventType = "TimerCreated" },
            new() { InstanceId = "i1", SequenceNumber = "006", EventType = "EventRaised" },
            new() { InstanceId = "i1", SequenceNumber = "007", EventType = "SubOrchestrationInstanceCreated" }
        };
        _storageService.GetOrchestrationHistoryAsync("MyHub", "i1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(history);

        // Act
        var result = await _sut.GetHistorySummary("MyHub", "i1");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("TotalActivitiesScheduled").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("TotalActivitiesCompleted").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("TotalActivitiesFailed").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("TotalTimersCreated").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("TotalExternalEvents").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("TotalSubOrchestrations").GetInt32().Should().Be(1);
    }

    #endregion
}
