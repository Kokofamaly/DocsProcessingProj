using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using CommonData;
using Microsoft.Extensions.Logging;

public class ServiceBusQueueTrigger(DocumentProcessor processor)
{
    [Function("ProcessDocumentTrigger")]
    public async Task Run([ServiceBusTrigger(AspireResourceNames.ServiceBusQueue, Connection = AspireResourceNames.ServiceBus)] ServiceBusReceivedMessage message, FunctionContext context)
    {
        await processor.ProcessAsync(message);
    }
}

