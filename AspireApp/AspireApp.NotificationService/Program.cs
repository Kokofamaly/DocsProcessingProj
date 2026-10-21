using AspireApp.NotificationService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CommonData;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<NotificationProcessor>();
builder.Services.AddHostedService<Worker>();

builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);

var app = builder.Build();

app.MapGet("/notifications", async (AppDbContext db) =>
{
    var notifications = await db.Notifications.AsNoTracking().ToListAsync();
    return Results.Ok(notifications);
});

app.MapGet("/notifications/{page:int}", async (AppDbContext db, int page, int pageSize = 10) =>
{
    if(page < 1) return Results.NotFound();
    var notifications = await db.Notifications.AsNoTracking().OrderBy(n => n.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    return Results.Ok(notifications);
});

app.Run();
