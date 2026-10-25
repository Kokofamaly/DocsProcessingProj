using System.Text.Json;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommonData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class DocumentProcessorTests
{
    private readonly Mock<ServiceBusSender> _sender = new();
    private readonly Mock<ServiceBusClient> _serviceBus = new();
    private readonly Mock<BlobClient> _blob = new();
    private readonly Mock<BlobContainerClient> _container = new();
    private readonly Mock<BlobServiceClient> _blobService = new();

    public DocumentProcessorTests()
    {
        _serviceBus.Setup(x => x.CreateSender(It.IsAny<string>())).Returns(_sender.Object);
        _blobService.Setup(x => x.GetBlobContainerClient(It.IsAny<string>())).Returns(_container.Object);
        _container.Setup(x => x.GetBlobClient(It.IsAny<string>())).Returns(_blob.Object);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private DocumentProcessor CreateProcessor(AppDbContext db) =>
        new(_blobService.Object, _serviceBus.Object, db, NullLogger<DocumentProcessor>.Instance);

    private static async Task<Document> AddDocumentAsync(AppDbContext db, string fileName = "report.pdf")
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            BlobName = Guid.NewGuid().ToString(),
            Status = DocumentStatus.Pending
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    private static ServiceBusReceivedMessage CreateMessage(Document doc, int deliveryCount = 1) =>
        CreateRawMessage(
            JsonSerializer.Serialize(new { documentId = doc.Id, blobName = doc.BlobName }),
            deliveryCount);

    private static ServiceBusReceivedMessage CreateRawMessage(string body, int deliveryCount = 1) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            deliveryCount: deliveryCount);

    private void SetupBlobContent(byte[] content)
    {
        var result = BlobsModelFactory.BlobDownloadResult(content: BinaryData.FromBytes(content));
        _blob.Setup(b => b.DownloadContentAsync())
            .ReturnsAsync(Response.FromValue(result, Mock.Of<Response>()));
    }

    [Fact]
    public async Task ProcessAsync_ValidMessage_SetsStatusCompleted()
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db);
        SetupBlobContent([1, 2, 3, 4, 5]);

        await CreateProcessor(db).ProcessAsync(CreateMessage(doc));

        Assert.Equal(DocumentStatus.Completed, (await db.Documents.FindAsync(doc.Id))!.Status);
    }

    [Fact]
    public async Task ProcessAsync_ValidMessage_SavesProcessedDataWithFileSize()
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db, "report.pdf");
        SetupBlobContent([1, 2, 3, 4, 5]);

        await CreateProcessor(db).ProcessAsync(CreateMessage(doc));

        var processed = await db.ProcessedDocumentDatas.SingleAsync();
        Assert.Equal(5, processed.FileSize);
        Assert.Equal(doc.Id, processed.DocumentId);
        Assert.Equal("report.pdf", processed.FileName);
    }

    [Fact]
    public async Task ProcessAsync_ValidMessage_PublishesEventToTopic()
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db);
        SetupBlobContent([1, 2, 3]);

        await CreateProcessor(db).ProcessAsync(CreateMessage(doc));

        _serviceBus.Verify(x => x.CreateSender(AspireResourceNames.ServicBusTopic), Times.Once);
        _sender.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_NullBody_ThrowsInvalidOperation()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateProcessor(db).ProcessAsync(CreateRawMessage("null")));
    }

    [Fact]
    public async Task ProcessAsync_DocumentNotFound_ThrowsAndSendsNothing()
    {
        await using var db = CreateDb();
        var ghost = new Document { Id = Guid.NewGuid(), BlobName = "x" }; // в БД его нет

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateProcessor(db).ProcessAsync(CreateMessage(ghost)));

        _sender.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ProcessAsync_FailureBeforeLastAttempt_RethrowsAndDoesNotMarkFailed(int deliveryCount)
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db, "error-file.pdf"); 

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateProcessor(db).ProcessAsync(CreateMessage(doc, deliveryCount)));

        var status = (await db.Documents.FindAsync(doc.Id))!.Status;
        Assert.NotEqual(DocumentStatus.Failed, status);
    }

    [Fact]
    public async Task ProcessAsync_FailureOnThirdAttempt_MarksDocumentFailed()
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db, "error-file.pdf");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateProcessor(db).ProcessAsync(CreateMessage(doc, deliveryCount: 3)));

        Assert.Equal(DocumentStatus.Failed, (await db.Documents.FindAsync(doc.Id))!.Status);
    }

    [Fact]
    public async Task ProcessAsync_BlobDownloadFails_MarksFailedOnThirdAttemptAndSendsNoEvent()
    {
        await using var db = CreateDb();
        var doc = await AddDocumentAsync(db);
        _blob.Setup(b => b.DownloadContentAsync())
            .ThrowsAsync(new RequestFailedException("blob is gone"));

        await Assert.ThrowsAsync<RequestFailedException>(
            () => CreateProcessor(db).ProcessAsync(CreateMessage(doc, deliveryCount: 3)));

        Assert.Equal(DocumentStatus.Failed, (await db.Documents.FindAsync(doc.Id))!.Status);
        Assert.Empty(db.ProcessedDocumentDatas);
        _sender.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}