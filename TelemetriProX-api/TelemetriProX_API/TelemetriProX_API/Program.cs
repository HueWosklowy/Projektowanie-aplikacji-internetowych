using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Register RabbitMQ Connection as a Singleton
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "admin",
        Password = "admin123"
    };
    // For simplicity in top-level, we use Task.Run().Result or a synchronous connection. 
    // In 7.0+, async is preferred, but the DI registration needs to handle it.
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

var app = builder.Build();

// 2. Logging Middleware
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"[LOG - {DateTime.Now:HH:mm:ss}] Otrzymano request: {body}");
    context.Request.Body.Position = 0;
    await next();
});

// 3. Endpoint with Injected Connection
app.MapPost("/pomiary", async (MeasurementRequest request, IConnection connection) =>
{
    if (string.IsNullOrEmpty(request.PayloadBase64))
        return Results.BadRequest("Payload nie moze byc pusty.");



    try
    {
        // Decode Base64
        byte[] dataBytes = Convert.FromBase64String(request.PayloadBase64);
        string decodedString = Encoding.UTF8.GetString(dataBytes);

        var messagePayload = new
        {
            DeviceId = request.DeviceId,
            Timestamp = DateTime.UtcNow,
            Data = decodedString
        };

        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messagePayload));

        // Create a short-lived channel for the request (best practice for thread safety)
        using var channel = await connection.CreateChannelAsync();

        // Ensure queue exists
        await channel.QueueDeclareAsync(queue: "telemetry_queue", durable: true, exclusive: false, autoDelete: false);

        var properties = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "telemetry_queue",
            mandatory: true,
            basicProperties: properties,
            body: messageBody);

        Console.WriteLine($"[RABBITMQ] Wyslano dane dla urzadzenia: {request.DeviceId}");
        return Results.Json(new { status = "Queued", device = request.DeviceId }, statusCode: 202);
    }
    catch (FormatException)
    {
        return Results.BadRequest("Nieprawidlowy format Base64.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Blad systemowy: {ex.Message}");
        return Results.StatusCode(500);
    }
});

app.Run();

public record MeasurementRequest(string DeviceId, string PayloadBase64);