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
using DocsProcessingProj.Api.Endpoints;

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
builder.Services.AddScoped<DocumentService>();

builder.AddAzureBlobServiceClient(AspireResourceNames.Blobs);
builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);

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

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapDocumentEndpoints();

app.Run();