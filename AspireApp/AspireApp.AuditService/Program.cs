using AspireApp.AuditService;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.AddAzureServiceBusClient("messaging");

var app = builder.Build();

app.MapGet("/notifications", async () =>
{
    
});

app.Run();
