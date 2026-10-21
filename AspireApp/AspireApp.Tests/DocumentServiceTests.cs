
using System.Text.Json;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommonData;
using DocsProcessingProj.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace DocsProcessingProj.Tests;

public class DocumentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ServiceBusClient> _busClient = new();
    private readonly Mock<ServiceBusSender> _sender = new();
    private readonly Mock<BlobServiceClient> _blobClient = new();
    private readonly Mock<BlobContainerClient> _container = new();

    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        _busClient
            .Setup(x => x.CreateSender(It.IsAny<string>()))
            .Returns(_sender.Object);

        _sender
            .Setup(x => x.SendMessageAsync(
                It.IsAny<ServiceBusMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _blobClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_container.Object);

        _container
            .Setup(x => x.UploadBlobAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        _service = new DocumentService(
            _db,
            _busClient.Object,
            _blobClient.Object);
    }

    [Fact]
    public async Task GetDocumentsAsync_ReturnsDocuments()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            Status = DocumentStatus.Pending,
            BlobName = "test-blob"
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        var result = await _service.GetDocumentsAsync();

        var item = Assert.Single(result);
        Assert.Equal(document.Id, item.Id);
        Assert.Equal("test.pdf", item.FileName);
        Assert.Equal(DocumentStatus.Pending, item.Status);
    }


    [Fact]
    public async Task GetDocumentsAsync_WhenEmpty_ReturnsEmptyList()
    {
        var result = await _service.GetDocumentsAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }


    [Fact]
    public async Task ReprocessFileAsync_WhenNotFound_ReturnsNull()
    {
        var id = Guid.NewGuid().ToString();

        var result = await _service.ReprocessFileAsync(id);

        Assert.Null(result);

        _sender.Verify(
            x => x.SendMessageAsync(
                It.IsAny<ServiceBusMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReprocessFileAsync_WhenFound_ResetsStatusAndSendsMessage()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            Status = DocumentStatus.Pending,
            BlobName = "test-blob"
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        document.Status = (DocumentStatus)1;
        await _db.SaveChangesAsync();

        var result = await _service.ReprocessFileAsync(
            document.Id.ToString());

        Assert.NotNull(result);
        Assert.Equal(DocumentStatus.Pending, result.Status);

        var saved = await _db.Documents.FindAsync(document.Id);
        Assert.NotNull(saved);
        Assert.Equal(DocumentStatus.Pending, saved.Status);

        _sender.Verify(
            x => x.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    JsonDocument.Parse(m.Body.ToString())
                        .RootElement.GetProperty("documentId")
                        .GetGuid() == document.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [Fact]
    public async Task PostDocumentAsync_ValidFile_UploadsSavesAndSendsMessage()
    {
        const string fileName = "test.pdf";
        var bytes = System.Text.Encoding.UTF8.GetBytes("test file content");

        using var stream = new MemoryStream(bytes);

        IFormFile file = new FormFile(
            stream,
            0,
            stream.Length,
            "file",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await _service.PostDocumentAsync(file);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(fileName, result.FileName);
        Assert.Equal(DocumentStatus.Pending, result.Status);
        Assert.Equal(result.Id.ToString(), result.BlobName);

        var saved = await _db.Documents.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal(fileName, saved.FileName);

        _blobClient.Verify(
            x => x.GetBlobContainerClient(It.IsAny<string>()),
            Times.Once);

        _container.Verify(
            x => x.UploadBlobAsync(
                result.BlobName,
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _sender.Verify(
            x => x.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    JsonDocument.Parse(m.Body.ToString())
                        .RootElement.GetProperty("documentId")
                        .GetGuid() == result.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
