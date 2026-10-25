using CommonData;

var builder = DistributedApplication.CreateBuilder(args);


var postgres = builder.AddPostgres("postgres").WithDataVolume().WithPgAdmin();
var postgresDb = postgres.AddDatabase(AspireResourceNames.Database);


var blobs = 
    builder.AddAzureStorage("storage")
    .RunAsEmulator( emulator => emulator.WithDataVolume() )
    .AddBlobs(AspireResourceNames.Blobs);


var serviceBus = builder.AddAzureServiceBus(AspireResourceNames.ServiceBus).RunAsEmulator();
var queue = serviceBus.AddServiceBusQueue(AspireResourceNames.ServiceBusQueue).WithProperties(q =>
{
    q.MaxDeliveryCount = 3;
});
var topic = serviceBus.AddServiceBusTopic(AspireResourceNames.ServicBusTopic);
var notificationSubscription = topic.AddServiceBusSubscription(AspireResourceNames.ServicBusTopicNotificationSubscription);
var auditSubscription = topic.AddServiceBusSubscription(AspireResourceNames.ServicBusTopicAuditSubscription);


var apiService = 
    builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithHttpHealthCheck("/health");


var function = 
    builder.AddAzureFunctionsProject<Projects.AspireApp_Functions>("function")
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WaitFor(apiService)
    .WithReference(postgresDb);


var notificationService = 
    builder.AddProject<Projects.AspireApp_NotificationService>("notification-service")
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WaitFor(apiService)
    .WithExternalHttpEndpoints();


var auditService = 
    builder.AddProject<Projects.AspireApp_AuditService>("audit-service")
    .WithReference(serviceBus)
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WaitFor(apiService)
    .WaitFor(serviceBus);

var frontend = 
    builder.AddViteApp("frontend", "../frontend")
    .WithHttpEndpoint(port: 5000, env: "PORT")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("VITE_API_URL", apiService.GetEndpoint("http"));


builder.Build().Run();
