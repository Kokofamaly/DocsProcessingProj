using AspireApp.AuditService;
using Microsoft.AspNetCore.Builder;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.AddAzureServiceBusClient("messaging");

var host = builder.Build();

host.Run();
