using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Text.Json;
using DocsProcessingProj.Api.DTOs;
using CommonData;
using DocsProcessingProj.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using DocsProcessingProj.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder()
.SetFallbackPolicy(
    new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build()).AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.AddAzureBlobServiceClient(AspireResourceNames.Blobs);
builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);
builder.Services.AddScoped<DocumentService>();
builder.Services.AddAntiforgery();

var frontendUrl = builder.Configuration["FrontendUrl"]
    ?? throw new InvalidOperationException("FrontendUrl is not configured.");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

var container = app.Services.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(AspireResourceNames.BlobContainerClient);

await container.CreateIfNotExistsAsync();

app.MapPost("/register", async (RegisterRequest regRequest, HttpContext context, AppDbContext db, IPasswordHasher<User> hasher) =>
{
    if(string.IsNullOrEmpty(regRequest.login) || string.IsNullOrEmpty(regRequest.password)) return Results.BadRequest();

    var exists = db.Users.Any(u => u.Login == regRequest.login);
    if(exists) return Results.Conflict("Login is taken");

    var user = new User{ Login = regRequest.login };
    user.HashedPassword = hasher.HashPassword(user, regRequest.password);

    await db.Users.AddAsync(user);
    await db.SaveChangesAsync();

    var identity = new ClaimsIdentity(
        [ 
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
            new Claim(ClaimTypes.Name, user.Login), 
            new Claim(ClaimTypes.Role, user.Role)
        ], CookieAuthenticationDefaults.AuthenticationScheme
    );

    await context.SignInAsync(new ClaimsPrincipal(identity));
    
    return Results.Created();
}).AllowAnonymous();

app.MapPost("/login", async (LoginRequest logRequest, HttpContext context, AppDbContext db, IPasswordHasher<User> hasher) =>
{
    if(string.IsNullOrEmpty(logRequest.login) || string.IsNullOrEmpty(logRequest.password)) return Results.BadRequest();

    var login = logRequest.login;
    var password = logRequest.password;
    var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login);

    if(user == null) return Results.Unauthorized();

    var passwordVerification = hasher.VerifyHashedPassword(user, user.HashedPassword, password);
    
    if(passwordVerification == PasswordVerificationResult.Failed) return Results.Unauthorized();

    var identity = new ClaimsIdentity(
        [ 
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
            new Claim(ClaimTypes.Name, user.Login), 
            new Claim(ClaimTypes.Role, user.Role)
        ], CookieAuthenticationDefaults.AuthenticationScheme
    );
    
    await context.SignInAsync(new ClaimsPrincipal(identity));
    return Results.Ok();
}).AllowAnonymous();

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.Ok();
});

app.MapGet("/me", async (ClaimsPrincipal user) => Results.Ok(new {login = user.Identity!.Name, role = user.FindFirstValue(ClaimTypes.Role)}));

app.MapGet("/documents", async (DocumentService docService, ClaimsPrincipal user) =>
{
    if(!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        return Results.Unauthorized();

    var docs = await docService.GetDocumentsAsync(id);

    return Results.Ok(docs);
});

app.MapPost(
    "/documents", 
    async (DocumentService docService, IFormFile file, ClaimsPrincipal user) =>
    {
        if(!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Results.Unauthorized();
        
        try
        {
            await docService.PostDocumentAsync(id, file);
            return Results.Created();
        }
        catch(UserDoesNotExistException)
        {
            return Results.Unauthorized();
        }
    }).DisableAntiforgery();

app.MapPost(
    "/documents/{documentId:guid}/reprocess", 
    async (Guid documentId, DocumentService docService, ClaimsPrincipal user) =>
    {   
        if(!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Results.Unauthorized();
        try
        {
            var doc = await docService.ReprocessFileAsync(userId, documentId);

            if(doc == null) return Results.NotFound();

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
        
    });

app.MapGet("/dlq", async (ServiceBusClient serviceBusClient) =>
{
        await using var rec = serviceBusClient.CreateReceiver(
        AspireResourceNames.ServiceBusQueue,
        new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });

        var dlqMessages = await rec.PeekMessagesAsync(10);


        if(dlqMessages is null) return Results.Ok(new { message = "DLQ IS NULL"});

        var result = dlqMessages.Select(dlq => new { dlqMessageId = dlq.MessageId, dlqReason = dlq.DeadLetterReason}).ToList();
        return Results.Ok(result);

}).RequireAuthorization("AdminOnly");

app.MapDefaultEndpoints();

app.Run();


