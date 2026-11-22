using CommonData;
using AspireApp.OutboxPublisher;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);
builder.AddServiceDefaults();

var host = builder.Build();
host.Run();
