var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var blobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");
var serviceBus = builder.AddAzureServiceBus("messaging").RunAsEmulator();
var queue = serviceBus.AddServiceBusQueue("documents-queue");
var topic = serviceBus.AddServiceBusTopic("documents-topic");
var notificationSubscription = topic.AddServiceBusSubscription("notification");
var auditSubscription = topic.AddServiceBusSubscription("audit");


var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithHttpHealthCheck("/health");

var function = builder.AddAzureFunctionsProject<Projects.AspireApp_Functions>("function")
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WaitFor(apiService);


// builder.AddProject<Projects.AspireApp_Web>("webfrontend")
//     .WithExternalHttpEndpoints()
//     .WithHttpHealthCheck("/health")
//     .WithReference(cache)
//     .WaitFor(cache)
//     .WithReference(apiService)
//     .WaitFor(apiService);

builder.AddViteApp("frontend", "../frontend")
.WithHttpEndpoint(env: "PORT")
.WithReference(apiService);

builder.Build().Run();
