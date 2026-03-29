using Microsoft.AspNetCore.Connections;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var factory = new ConnectionFactory() { HostName = "localhost" }; 
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();


channel.QueueDeclare(queue: "telemetry_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"[LOG - {DateTime.Now:HH:mm:ss}] Otrzymano zadanie: {body}");
    context.Request.Body.Position = 0;
    await next();
});

app.MapPost("/pomiary", (MeasurementRequest request) =>
{
    if (string.IsNullOrEmpty(request.PayloadBase64))
        return Results.BadRequest("Payload nie moze byc pusty.");

    try
    {
        byte[] dataBytes = Convert.FromBase64String(request.PayloadBase64);
        string decodedString = Encoding.UTF8.GetString(dataBytes);

        var messagePayload = new
        {
            DeviceId = request.DeviceId,
            Timestamp = DateTime.UtcNow,
            Data = decodedString
        };

        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messagePayload));

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(exchange: "",
                             routingKey: "telemetry_queue",
                             basicProperties: properties,
                             body: messageBody);

        Console.WriteLine($"[RABBITMQ] Wyslano dane dla urzadzenia: {request.DeviceId}");

        return Results.Accepted(new { status = "Queued", device = request.DeviceId });
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