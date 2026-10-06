var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var blobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");
var serviceBus = builder.AddAzureServiceBus("messaging").RunAsEmulator();
var queue = serviceBus.AddServiceBusQueue("");
var topic = serviceBus.AddServiceBusTopic("");
var subscription = topic.AddServiceBusSubscription("notification");

var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(queue)
    .WaitFor(queue)
    .WithReference(topic)
    .WithReference(subscription)
    .WithHttpHealthCheck("/health");

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
