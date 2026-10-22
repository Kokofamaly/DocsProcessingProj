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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(.)
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
    var login = logRequest.login;
    var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login);

    if(user == null) return Results.Unauthorized();

    var passwordVerification = hasher.VerifyHashedPassword(user, user.HashedPassword, logRequest.password);
    
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

app.MapGet("/documents", async (DocumentService docService) =>
{
    var docs = await docService.GetDocumentsAsync();

    return Results.Ok(docs);
});

app.MapPost(
    "/documents", 
    async (DocumentService docService, IFormFile file) =>
    {
        await docService.PostDocumentAsync(file);
        return Results.Created();
    }).DisableAntiforgery();

app.MapPost(
    "/documents/{id}/reprocess", 
    async (string id, DocumentService docService) =>
{
    var doc = await docService.ReprocessFileAsync(id);

    return doc is null ? Results.NotFound() : Results.Ok();
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

public record LoginRequest(string login, string password);
public record RegisterRequest(string login, string password);
