using System.Text.Json;
using AspireApp.AuditService;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;

public class AuditProcessorTests
{
    private readonly AuditProcessor _processor = new(NullLogger<AuditProcessor>.Instance);

    private static ServiceBusReceivedMessage CreateMessage(string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body));

    private static ServiceBusReceivedMessage CreateMessage(Guid documentId, string eventType) =>
        CreateMessage(JsonSerializer.Serialize(new { documentId, eventType }));

    [Fact]
    public void Process_ValidMessage_AuditLogContainsDocumentId()
    {
        var id = Guid.NewGuid();

        var log = _processor.Process(CreateMessage(id, "DocumentProcessed"));

        using var json = JsonDocument.Parse(log);
        Assert.Equal(id, json.RootElement.GetProperty("documentId").GetGuid());
    }

    [Fact]
    public void Process_ValidMessage_AuditLogContainsEventType()
    {
        var log = _processor.Process(CreateMessage(Guid.NewGuid(), "DocumentProcessed"));

        using var json = JsonDocument.Parse(log);
        Assert.Equal("DocumentProcessed", json.RootElement.GetProperty("eventType").GetString());
    }

    [Fact]
    public void Process_ValidMessage_AuditLogHasRecentTimestamp()
    {
        var log = _processor.Process(CreateMessage(Guid.NewGuid(), "DocumentProcessed"));

        using var json = JsonDocument.Parse(log);
        var timestamp = json.RootElement.GetProperty("timeStamp").GetDateTimeOffset();
        Assert.True(DateTimeOffset.UtcNow - timestamp < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Process_PropertyNamesInPascalCase_AreStillParsed()
    {
        var id = Guid.NewGuid();
        var body = $$"""{"DocumentId":"{{id}}","EventType":"X"}""";

        var log = _processor.Process(CreateMessage(body));

        using var json = JsonDocument.Parse(log);
        Assert.Equal(id, json.RootElement.GetProperty("documentId").GetGuid());
    }

    [Fact]
    public void Process_NullBody_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => _processor.Process(CreateMessage("null")));
    }

    [Fact]
    public void Process_BrokenJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => _processor.Process(CreateMessage("not a json")));
    }
}