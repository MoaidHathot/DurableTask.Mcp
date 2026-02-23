using System.Text.Json;
using DurableTasksMcp.Models;
using DurableTasksMcp.Services;
using DurableTasksMcp.Tools;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DurableTasksMcp.Tests.Tools;

public class QueueToolsTests
{
    private readonly IDurableTaskStorageService _storageService;
    private readonly QueueTools _sut;

    public QueueToolsTests()
    {
        _storageService = Substitute.For<IDurableTaskStorageService>();
        _sut = new QueueTools(_storageService);
    }

    #region ListQueues Tests

    [Fact]
    public async Task ListQueues_WhenNoQueues_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListTaskHubQueuesAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListQueues("MyHub");

        // Assert
        result.Should().Contain("No queues found");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task ListQueues_ReturnsQueuesWithMessageCounts()
    {
        // Arrange
        var queues = new List<string> { "myhub-control-00", "myhub-workitems" };
        _storageService.ListTaskHubQueuesAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(queues);
        _storageService.GetQueueMessageCountAsync("myhub-control-00", Arg.Any<CancellationToken>())
            .Returns(5);
        _storageService.GetQueueMessageCountAsync("myhub-workitems", Arg.Any<CancellationToken>())
            .Returns(10);

        // Act
        var result = await _sut.ListQueues("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
        
        var controlQueue = json.RootElement[0];
        controlQueue.GetProperty("Name").GetString().Should().Be("myhub-control-00");
        controlQueue.GetProperty("ApproximateMessageCount").GetInt32().Should().Be(5);
        controlQueue.GetProperty("Type").GetString().Should().Be("Control");
        
        var workItemQueue = json.RootElement[1];
        workItemQueue.GetProperty("Name").GetString().Should().Be("myhub-workitems");
        workItemQueue.GetProperty("ApproximateMessageCount").GetInt32().Should().Be(10);
        workItemQueue.GetProperty("Type").GetString().Should().Be("WorkItem");
    }

    [Fact]
    public async Task ListQueues_IdentifiesQueueTypes()
    {
        // Arrange
        var queues = new List<string> { "myhub-control-00", "myhub-control-01", "myhub-workitems", "myhub-other" };
        _storageService.ListTaskHubQueuesAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(queues);
        _storageService.GetQueueMessageCountAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _sut.ListQueues("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        var queueList = json.RootElement.EnumerateArray().ToList();
        
        queueList[0].GetProperty("Type").GetString().Should().Be("Control");
        queueList[1].GetProperty("Type").GetString().Should().Be("Control");
        queueList[2].GetProperty("Type").GetString().Should().Be("WorkItem");
        queueList[3].GetProperty("Type").GetString().Should().Be("Other");
    }

    #endregion

    #region PeekQueueMessages Tests

    [Fact]
    public async Task PeekQueueMessages_WhenNoMessages_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.PeekQueueMessagesAsync("myhub-workitems", 32, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.PeekQueueMessages("myhub-workitems");

        // Assert
        result.Should().Contain("No messages found");
        result.Should().Contain("myhub-workitems");
    }

    [Fact]
    public async Task PeekQueueMessages_ReturnsMessages()
    {
        // Arrange
        var messages = new List<QueueMessageInfo>
        {
            new() { MessageId = "msg-1", QueueName = "myhub-workitems", MessageText = "{\"action\": \"process\"}" },
            new() { MessageId = "msg-2", QueueName = "myhub-workitems", MessageText = "{\"action\": \"complete\"}" }
        };
        _storageService.PeekQueueMessagesAsync("myhub-workitems", 32, Arg.Any<CancellationToken>())
            .Returns(messages);

        // Act
        var result = await _sut.PeekQueueMessages("myhub-workitems");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task PeekQueueMessages_LimitsMaxMessagesTo32()
    {
        // Arrange
        _storageService.PeekQueueMessagesAsync("myhub-workitems", 32, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.PeekQueueMessages("myhub-workitems", maxMessages: 100);

        // Assert
        await _storageService.Received(1).PeekQueueMessagesAsync("myhub-workitems", 32, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PeekQueueMessages_PassesCustomMaxMessages()
    {
        // Arrange
        _storageService.PeekQueueMessagesAsync("myhub-workitems", 10, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.PeekQueueMessages("myhub-workitems", maxMessages: 10);

        // Assert
        await _storageService.Received(1).PeekQueueMessagesAsync("myhub-workitems", 10, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetQueueDepth Tests

    [Fact]
    public async Task GetQueueDepth_ReturnsQueueNameAndCount()
    {
        // Arrange
        _storageService.GetQueueMessageCountAsync("myhub-workitems", Arg.Any<CancellationToken>())
            .Returns(42);

        // Act
        var result = await _sut.GetQueueDepth("myhub-workitems");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("QueueName").GetString().Should().Be("myhub-workitems");
        json.RootElement.GetProperty("ApproximateMessageCount").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task GetQueueDepth_WhenQueueNotFound_ReturnsZero()
    {
        // Arrange
        _storageService.GetQueueMessageCountAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _sut.GetQueueDepth("nonexistent");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("ApproximateMessageCount").GetInt32().Should().Be(0);
    }

    #endregion

    #region GetAllQueueDepths Tests

    [Fact]
    public async Task GetAllQueueDepths_WhenNoQueues_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListTaskHubQueuesAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.GetAllQueueDepths("MyHub");

        // Assert
        result.Should().Contain("No queues found");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task GetAllQueueDepths_ReturnsTotalAndPerQueueCounts()
    {
        // Arrange
        var queues = new List<string> { "myhub-control-00", "myhub-control-01", "myhub-workitems" };
        _storageService.ListTaskHubQueuesAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(queues);
        _storageService.GetQueueMessageCountAsync("myhub-control-00", Arg.Any<CancellationToken>())
            .Returns(5);
        _storageService.GetQueueMessageCountAsync("myhub-control-01", Arg.Any<CancellationToken>())
            .Returns(3);
        _storageService.GetQueueMessageCountAsync("myhub-workitems", Arg.Any<CancellationToken>())
            .Returns(10);

        // Act
        var result = await _sut.GetAllQueueDepths("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("TaskHubName").GetString().Should().Be("MyHub");
        json.RootElement.GetProperty("TotalMessageCount").GetInt32().Should().Be(18);
        json.RootElement.GetProperty("Queues").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task GetAllQueueDepths_IncludesQueueTypes()
    {
        // Arrange
        var queues = new List<string> { "myhub-control-00", "myhub-workitems" };
        _storageService.ListTaskHubQueuesAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(queues);
        _storageService.GetQueueMessageCountAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _sut.GetAllQueueDepths("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        var queueList = json.RootElement.GetProperty("Queues").EnumerateArray().ToList();
        
        queueList[0].GetProperty("Type").GetString().Should().Be("Control");
        queueList[1].GetProperty("Type").GetString().Should().Be("WorkItem");
    }

    #endregion
}
