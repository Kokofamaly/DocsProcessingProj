using System.Text.Json;
using AspireApp.AuditService;
using Azure.Messaging.ServiceBus;
using CommonData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

public class AuditProcessorTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AuditProcessor CreateProcessor(AppDbContext db) =>
        new(NullLogger<AuditProcessor>.Instance, db);

    private static ServiceBusReceivedMessage CreateMessage(string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body));

    private static ServiceBusReceivedMessage CreateMessage(Guid documentId, string eventType) =>
        CreateMessage(JsonSerializer.Serialize(new { documentId, eventType }));

    [Fact]
    public async Task Process_ValidMessage_SavesDocumentId()
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();

        await CreateProcessor(db).Process(CreateMessage(id, "DocumentProcessed"));

        var saved = await db.AuditLogs.SingleAsync();
        Assert.Equal(id, saved.DocumentId);
    }

    [Fact]
    public async Task Process_ValidMessage_SavesEventType()
    {
        await using var db = CreateDb();

        await CreateProcessor(db).Process(CreateMessage(Guid.NewGuid(), "DocumentProcessed"));

        var saved = await db.AuditLogs.SingleAsync();
        Assert.Equal("DocumentProcessed", saved.EventType);
    }

    [Fact]
    public async Task Process_ValidMessage_SavesRecentTimestamp()
    {
        await using var db = CreateDb();

        await CreateProcessor(db).Process(CreateMessage(Guid.NewGuid(), "DocumentProcessed"));

        var saved = await db.AuditLogs.SingleAsync();
        Assert.True(DateTimeOffset.UtcNow - saved.Timestamp < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Process_PropertyNamesInPascalCase_AreStillParsed()
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();
        var body = $$"""{"DocumentId":"{{id}}","EventType":"X"}""";

        await CreateProcessor(db).Process(CreateMessage(body));

        var saved = await db.AuditLogs.SingleAsync();
        Assert.Equal(id, saved.DocumentId);
        Assert.Equal("X", saved.EventType);
    }

    [Fact]
    public async Task Process_NullBody_ThrowsAndSavesNothing()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateProcessor(db).Process(CreateMessage("null")));

        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Process_BrokenJson_ThrowsJsonExceptionAndSavesNothing()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAnyAsync<JsonException>(
            () => CreateProcessor(db).Process(CreateMessage("not a json")));

        Assert.Empty(db.AuditLogs);
    }
}