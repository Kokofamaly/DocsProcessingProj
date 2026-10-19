using CommonData;

var builder = DistributedApplication.CreateBuilder(args);

// var cache = builder.AddRedis("cache");


var postgres = builder.AddPostgres("postgres");
var postgresDb = postgres.AddDatabase(AspireResourceNames.Database);

var blobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs(AspireResourceNames.Blobs);
var serviceBus = builder.AddAzureServiceBus(AspireResourceNames.ServiceBus).RunAsEmulator();
var queue = serviceBus.AddServiceBusQueue(AspireResourceNames.ServiceBusQueue);
var topic = serviceBus.AddServiceBusTopic(AspireResourceNames.ServicBusTopic);
var notificationSubscription = topic.AddServiceBusSubscription(AspireResourceNames.ServicBusTopicNotificationSubscription);
var auditSubscription = topic.AddServiceBusSubscription(AspireResourceNames.ServicBusTopicAuditSubscription);


var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithHttpHealthCheck("/health");



var function = builder.AddAzureFunctionsProject<Projects.AspireApp_Functions>("function")
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WaitFor(apiService)
    .WithReference(postgresDb);

var notificationService = builder.AddProject<Projects.AspireApp_NotificationService>("notification-service").WithReference(serviceBus).WithReference(postgresDb).WaitFor(postgresDb).WaitFor(apiService).WithExternalHttpEndpoints();
var auditService = builder.AddProject<Projects.AspireApp_AuditService>("audit-service").WithReference(serviceBus);

// builder.AddProject<Projects.AspireApp_Web>("webfrontend")
//     .WithExternalHttpEndpoints()
//     .WithHttpHealthCheck("/health")
//     .WithReference(cache)
//     .WaitFor(cache)
//     .WithReference(apiService)
//     .WaitFor(apiService);

builder.AddViteApp("frontend", "../frontend")
.WithHttpEndpoint(env: "PORT")
.WithReference(apiService)
.WithEnvironment("VITE_API_URL", apiService.GetEndpoint("http"));

builder.Build().Run();
