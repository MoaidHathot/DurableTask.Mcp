using System.Text.Json;
using DurableTask.Mcp.Services;
using DurableTask.Mcp.Tools;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DurableTask.Mcp.Tests.Tools;

public class BlobToolsTests
{
    private readonly IDurableTaskStorageService _storageService;
    private readonly BlobTools _sut;

    public BlobToolsTests()
    {
        _storageService = Substitute.For<IDurableTaskStorageService>();
        _sut = new BlobTools(_storageService);
    }

    #region ListContainers Tests

    [Fact]
    public async Task ListContainers_WhenNoContainers_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListTaskHubContainersAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListContainers("MyHub");

        // Assert
        result.Should().Contain("No blob containers found");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task ListContainers_ReturnsContainerList()
    {
        // Arrange
        var containers = new List<string> { "myhub-largemessages", "myhub-leases" };
        _storageService.ListTaskHubContainersAsync("MyHub", Arg.Any<CancellationToken>())
            .Returns(containers);

        // Act
        var result = await _sut.ListContainers("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetArrayLength().Should().Be(2);
        json.RootElement[0].GetString().Should().Be("myhub-largemessages");
        json.RootElement[1].GetString().Should().Be("myhub-leases");
    }

    #endregion

    #region ListLargeMessages Tests

    [Fact]
    public async Task ListLargeMessages_WhenNoBlobs_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.ListLargeMessagesAsync("MyHub", 100, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.ListLargeMessages("MyHub");

        // Assert
        result.Should().Contain("No large messages found");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task ListLargeMessages_ReturnsFormattedResult()
    {
        // Arrange
        var blobs = new List<string> { "msg-001.json", "msg-002.json", "msg-003.json" };
        _storageService.ListLargeMessagesAsync("MyHub", 100, Arg.Any<CancellationToken>())
            .Returns(blobs);

        // Act
        var result = await _sut.ListLargeMessages("MyHub");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("TaskHubName").GetString().Should().Be("MyHub");
        json.RootElement.GetProperty("Container").GetString().Should().Be("myhub-largemessages");
        json.RootElement.GetProperty("BlobCount").GetInt32().Should().Be(3);
        json.RootElement.GetProperty("Blobs").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task ListLargeMessages_PassesTopParameter()
    {
        // Arrange
        _storageService.ListLargeMessagesAsync("MyHub", 50, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.ListLargeMessages("MyHub", top: 50);

        // Assert
        await _storageService.Received(1).ListLargeMessagesAsync("MyHub", 50, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetLargeMessageContent Tests

    [Fact]
    public async Task GetLargeMessageContent_WhenBlobNotFound_ReturnsNotFoundMessage()
    {
        // Arrange
        _storageService.GetLargeMessageContentAsync("MyHub", "nonexistent.json", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _sut.GetLargeMessageContent("MyHub", "nonexistent.json");

        // Assert
        result.Should().Contain("not found");
        result.Should().Contain("nonexistent.json");
        result.Should().Contain("MyHub");
    }

    [Fact]
    public async Task GetLargeMessageContent_WhenJsonContent_ReturnsPrettyPrinted()
    {
        // Arrange
        var jsonContent = "{\"orderId\":42,\"status\":\"pending\"}";
        _storageService.GetLargeMessageContentAsync("MyHub", "msg-001.json", Arg.Any<CancellationToken>())
            .Returns(jsonContent);

        // Act
        var result = await _sut.GetLargeMessageContent("MyHub", "msg-001.json");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("BlobName").GetString().Should().Be("msg-001.json");
        json.RootElement.GetProperty("ContentType").GetString().Should().Be("application/json");
        
        var content = json.RootElement.GetProperty("Content");
        content.GetProperty("orderId").GetInt32().Should().Be(42);
        content.GetProperty("status").GetString().Should().Be("pending");
    }

    [Fact]
    public async Task GetLargeMessageContent_WhenPlainTextContent_ReturnsAsIs()
    {
        // Arrange
        var plainContent = "This is not valid JSON content";
        _storageService.GetLargeMessageContentAsync("MyHub", "msg-001.txt", Arg.Any<CancellationToken>())
            .Returns(plainContent);

        // Act
        var result = await _sut.GetLargeMessageContent("MyHub", "msg-001.txt");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("BlobName").GetString().Should().Be("msg-001.txt");
        json.RootElement.GetProperty("ContentType").GetString().Should().Be("text/plain");
        json.RootElement.GetProperty("Content").GetString().Should().Be(plainContent);
    }

    [Fact]
    public async Task GetLargeMessageContent_WhenComplexJsonContent_ParsesCorrectly()
    {
        // Arrange
        var complexJson = """
        {
            "items": [
                {"id": 1, "name": "Item 1"},
                {"id": 2, "name": "Item 2"}
            ],
            "metadata": {
                "total": 2,
                "timestamp": "2026-01-01T10:00:00Z"
            }
        }
        """;
        _storageService.GetLargeMessageContentAsync("MyHub", "complex.json", Arg.Any<CancellationToken>())
            .Returns(complexJson);

        // Act
        var result = await _sut.GetLargeMessageContent("MyHub", "complex.json");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("ContentType").GetString().Should().Be("application/json");
        
        var content = json.RootElement.GetProperty("Content");
        content.GetProperty("items").GetArrayLength().Should().Be(2);
        content.GetProperty("metadata").GetProperty("total").GetInt32().Should().Be(2);
    }

    #endregion
}
