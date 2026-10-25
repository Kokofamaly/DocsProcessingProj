using AspireApp.AuditService;
using Microsoft.AspNetCore.Builder;
using CommonData;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<AuditProcessor>();
builder.Services.AddHostedService<Worker>();
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);
builder.AddServiceDefaults();


builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);

var host = builder.Build();

host.Run();
