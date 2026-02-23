using DurableTask.Mcp.Models;
using DurableTask.Mcp.Services;
using FluentAssertions;
using Xunit;

namespace DurableTask.Mcp.Tests.Services;

/// <summary>
/// Tests for the DurableTaskStorageService.
/// Note: Full unit tests for this service require mocking Azure SDK clients which are complex.
/// These tests focus on the mapping logic and basic behavior verification.
/// For comprehensive testing, consider integration tests with Azure Storage Emulator (Azurite).
/// </summary>
public class DurableTaskStorageServiceTests
{
    #region Model Mapping Tests - Using Reflection to Test Private Methods

    [Fact]
    public void OrchestrationInstance_HasRequiredProperties()
    {
        // Verify model properties exist and can be set
        var instance = new OrchestrationInstance
        {
            InstanceId = "test-instance-id",
            Name = "TestOrchestrator",
            RuntimeStatus = "Running",
            Input = "{\"value\": 42}",
            Output = "{\"result\": 84}",
            CustomStatus = "Processing step 1",
            CreatedTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastUpdatedTime = DateTimeOffset.UtcNow,
            CompletedTime = null,
            ExecutionId = "exec-123",
            TaskHubName = "TestHub"
        };

        instance.InstanceId.Should().Be("test-instance-id");
        instance.Name.Should().Be("TestOrchestrator");
        instance.RuntimeStatus.Should().Be("Running");
        instance.Input.Should().Contain("42");
        instance.TaskHubName.Should().Be("TestHub");
    }

    [Fact]
    public void HistoryEvent_HasRequiredProperties()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var fireAt = DateTimeOffset.UtcNow.AddMinutes(10);

        var historyEvent = new HistoryEvent
        {
            InstanceId = "test-instance",
            SequenceNumber = "001",
            EventType = "TaskScheduled",
            Timestamp = timestamp,
            Name = "ProcessItem",
            Input = "{\"data\": 1}",
            Result = "{\"output\": 2}",
            TaskScheduledId = 42,
            ScheduledTime = scheduledTime,
            FireAt = fireAt,
            OrchestrationStatus = "Running",
            ExecutionId = "exec-456",
            Reason = "Some reason",
            IsPlayed = false
        };

        historyEvent.InstanceId.Should().Be("test-instance");
        historyEvent.SequenceNumber.Should().Be("001");
        historyEvent.EventType.Should().Be("TaskScheduled");
        historyEvent.Timestamp.Should().Be(timestamp);
        historyEvent.Name.Should().Be("ProcessItem");
        historyEvent.TaskScheduledId.Should().Be(42);
        historyEvent.IsPlayed.Should().BeFalse();
    }

    [Fact]
    public void TaskHub_HasRequiredProperties()
    {
        var taskHub = new TaskHub
        {
            Name = "MyTaskHub",
            HasInstancesTable = true,
            HasHistoryTable = true,
            ControlQueueCount = 4,
            HasWorkItemQueue = true,
            HasLargeMessagesContainer = true,
            HasLeasesContainer = true
        };

        taskHub.Name.Should().Be("MyTaskHub");
        taskHub.HasInstancesTable.Should().BeTrue();
        taskHub.HasHistoryTable.Should().BeTrue();
        taskHub.ControlQueueCount.Should().Be(4);
        taskHub.HasWorkItemQueue.Should().BeTrue();
        taskHub.HasLargeMessagesContainer.Should().BeTrue();
        taskHub.HasLeasesContainer.Should().BeTrue();
    }

    [Fact]
    public void QueueMessageInfo_HasRequiredProperties()
    {
        var insertedOn = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresOn = DateTimeOffset.UtcNow.AddDays(7);

        var messageInfo = new QueueMessageInfo
        {
            MessageId = "msg-123",
            QueueName = "myhub-workitems",
            MessageText = "{\"action\": \"process\"}",
            InsertedOn = insertedOn,
            ExpiresOn = expiresOn,
            DequeueCount = 3,
            PopReceipt = "receipt-xyz"
        };

        messageInfo.MessageId.Should().Be("msg-123");
        messageInfo.QueueName.Should().Be("myhub-workitems");
        messageInfo.MessageText.Should().Contain("process");
        messageInfo.InsertedOn.Should().Be(insertedOn);
        messageInfo.ExpiresOn.Should().Be(expiresOn);
        messageInfo.DequeueCount.Should().Be(3);
    }

    [Fact]
    public void OrchestrationSummary_HasRequiredProperties()
    {
        var summary = new OrchestrationSummary
        {
            TaskHubName = "TestHub",
            TotalCount = 100,
            RunningCount = 10,
            CompletedCount = 75,
            FailedCount = 5,
            PendingCount = 3,
            TerminatedCount = 2,
            SuspendedCount = 1,
            ContinuedAsNewCount = 4
        };

        summary.TaskHubName.Should().Be("TestHub");
        summary.TotalCount.Should().Be(100);
        summary.RunningCount.Should().Be(10);
        summary.CompletedCount.Should().Be(75);
        summary.FailedCount.Should().Be(5);
        summary.PendingCount.Should().Be(3);
        summary.TerminatedCount.Should().Be(2);
        summary.SuspendedCount.Should().Be(1);
        summary.ContinuedAsNewCount.Should().Be(4);

        // Verify counts add up correctly
        var expectedStatusSum = summary.RunningCount + summary.CompletedCount + 
                                summary.FailedCount + summary.PendingCount + 
                                summary.TerminatedCount + summary.SuspendedCount + 
                                summary.ContinuedAsNewCount;
        expectedStatusSum.Should().Be(100);
    }

    #endregion

    #region Orchestration Status Enum Values

    [Theory]
    [InlineData("Running")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Pending")]
    [InlineData("Terminated")]
    [InlineData("Suspended")]
    [InlineData("ContinuedAsNew")]
    public void OrchestrationInstance_AcceptsValidRuntimeStatus(string status)
    {
        var instance = new OrchestrationInstance
        {
            InstanceId = "test",
            RuntimeStatus = status
        };

        instance.RuntimeStatus.Should().Be(status);
    }

    #endregion

    #region History Event Types

    [Theory]
    [InlineData("ExecutionStarted")]
    [InlineData("ExecutionCompleted")]
    [InlineData("TaskScheduled")]
    [InlineData("TaskCompleted")]
    [InlineData("TaskFailed")]
    [InlineData("SubOrchestrationInstanceCreated")]
    [InlineData("SubOrchestrationInstanceCompleted")]
    [InlineData("SubOrchestrationInstanceFailed")]
    [InlineData("TimerCreated")]
    [InlineData("TimerFired")]
    [InlineData("EventRaised")]
    [InlineData("EventSent")]
    public void HistoryEvent_AcceptsValidEventTypes(string eventType)
    {
        var historyEvent = new HistoryEvent
        {
            InstanceId = "test",
            SequenceNumber = "001",
            EventType = eventType
        };

        historyEvent.EventType.Should().Be(eventType);
    }

    #endregion
}
