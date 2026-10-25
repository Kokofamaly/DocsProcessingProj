using Azure.Messaging.ServiceBus;
using CommonData;
using DocsProcessingProj.Api.DTOs;
using DocsProcessingProj.Api.Services;
using System.Security.Claims;

namespace DocsProcessingProj.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/documents");

        group.MapGet("/", GetDocuments);
        group.MapPost("/", UploadDocument).DisableAntiforgery();
        group.MapPost("/{documentId:guid}/reprocess", Reprocess);

        app.MapGet("/dlq", GetDlq).RequireAuthorization("AdminOnly");

        return app;
    }

    private static async Task<IResult> GetDocuments(DocumentService docService, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();

        if (!userId.HasValue)
            return Results.Unauthorized();

        return Results.Ok(await docService.GetDocumentsAsync(userId.Value));
    }

    private static async Task<IResult> UploadDocument(
        DocumentService docService, IFormFile file, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();

        if (!userId.HasValue)
            return Results.Unauthorized();

        try
        {
            await docService.PostDocumentAsync(userId.Value, file);
            return Results.Created();
        }
        catch (UserDoesNotExistException)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> Reprocess(
        Guid documentId, DocumentService docService, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();

        if (!userId.HasValue)
            return Results.Unauthorized();

        try
        {
            var doc = await docService.ReprocessFileAsync(userId.Value, documentId);
            if (doc == null) return Results.NotFound();

            return Results.Ok(new DocumentResponseDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                Status = doc.Status,
                UploadedAt = doc.UploadedAt
            });
        }
        catch (DocumentAccessDeniedException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> GetDlq(ServiceBusClient serviceBusClient)
    {
        await using var receiver = serviceBusClient.CreateReceiver(
            AspireResourceNames.ServiceBusQueue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(10);

        return Results.Ok(messages
            .Select(m => new { dlqMessageId = m.MessageId, dlqReason = m.DeadLetterReason })
            .ToList());
    }
}