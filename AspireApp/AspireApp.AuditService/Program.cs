using AspireApp.AuditService;
using Microsoft.AspNetCore.Builder;
using CommonData;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);

var host = builder.Build();

host.Run();
