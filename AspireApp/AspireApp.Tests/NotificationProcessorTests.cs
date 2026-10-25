using System.Text.Json;
using AspireApp.NotificationService;
using Azure.Messaging.ServiceBus;
using CommonData;
using Microsoft.EntityFrameworkCore;

public class NotificationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ServiceBusReceivedMessage CreateMessage(string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body));

    private static async Task<Document> AddDocumentAsync(AppDbContext db, string fileName)
    {
        var doc = new Document { Id = Guid.NewGuid(), FileName = fileName, BlobName = Guid.NewGuid().ToString() };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task ProcessAsync_ValidMessage_SavesNotificationWithFileName()
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db, "report.pdf");
        var message = CreateMessage(JsonSerializer.Serialize(new { documentId = doc.Id }));

        await new NotificationProcessor(db).ProcessAsync(message);

        var saved = await db.Notifications.SingleAsync();
        Assert.Equal("report.pdf was processed.", saved.NotificationMessage);
    }

    [Fact]
    public async Task ProcessAsync_DocumentNotFound_ThrowsAndSavesNothing()
    {
        await using var db = CreateDb();
        var message = CreateMessage(JsonSerializer.Serialize(new { documentId = Guid.NewGuid() }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new NotificationProcessor(db).ProcessAsync(message));

        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task ProcessAsync_NullBody_Throws()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new NotificationProcessor(db).ProcessAsync(CreateMessage("null")));
    }

}