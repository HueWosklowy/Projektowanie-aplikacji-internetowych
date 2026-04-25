using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapHub<AlertHub>("/alertsHub");

// Prosty endpoint do testów rêcznych
app.MapPost("/api/temperature", async (
    TemperatureInput input,
    IHubContext<AlertHub> hubContext) =>
{
    var payload = new TemperatureMessage
    {
        Value = input.Value,
        Color = TemperatureColorHelper.GetColor(input.Value),
        Source = "manual",
        Timestamp = DateTime.UtcNow,
        AlertMessage = $"Odebrano temperaturê: {input.Value}°C"
    };

    await hubContext.Clients.All.SendAsync("ReceiveTemperature", payload);
    return Results.Ok(payload);
});

// Webhook z InfluxDB
app.MapPost("/webhook/influx", async (
    HttpContext context,
    IHubContext<AlertHub> hubContext) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    Console.WriteLine("=== WEBHOOK INFLUX ===");
    Console.WriteLine(rawBody);

    // Elastyczny parsing - bo payload mo¿e siê ró¿niæ zale¿nie od rule/template
    using var document = JsonDocument.Parse(rawBody);
    var root = document.RootElement;

    string message = TryGetString(root, "message")
                     ?? TryGetString(root, "text")
                     ?? "Alert z InfluxDB";

    string level = TryGetString(root, "level")
                   ?? TryGetString(root, "status")
                   ?? "unknown";

    double? temperature = TryGetDouble(root, "temperature")
                          ?? TryGetDouble(root, "temp")
                          ?? TryGetDouble(root, "value");

    // Spróbujmy jeszcze znaleŸæ temperaturê w zagnie¿d¿onych polach
    temperature ??= TryFindFirstDouble(root, new[] { "_value", "value", "temperature", "temp" });

    var response = new AlertNotification
    {
        Message = message,
        Level = level,
        Temperature = temperature,
        Color = temperature.HasValue ? TemperatureColorHelper.GetColor(temperature.Value) : "gray",
        Timestamp = DateTime.UtcNow,
        RawPayload = rawBody
    };

    await hubContext.Clients.All.SendAsync("ReceiveAlert", response);

    // Jeœli w webhooku jest temperatura, wyœlij te¿ osobne zdarzenie temperatury
    if (temperature.HasValue)
    {
        var tempMessage = new TemperatureMessage
        {
            Value = temperature.Value,
            Color = TemperatureColorHelper.GetColor(temperature.Value),
            Source = "influx-webhook",
            Timestamp = DateTime.UtcNow,
            AlertMessage = message
        };

        await hubContext.Clients.All.SendAsync("ReceiveTemperature", tempMessage);
    }

    return Results.Ok(new { status = "received" });
});

app.Run();

static string? TryGetString(JsonElement root, string propertyName)
{
    if (root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind == JsonValueKind.String)
    {
        return prop.GetString();
    }

    return null;
}

static double? TryGetDouble(JsonElement root, string propertyName)
{
    if (root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(propertyName, out var prop))
    {
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var val))
            return val;

        if (prop.ValueKind == JsonValueKind.String &&
            double.TryParse(prop.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
    }

    return null;
}

static double? TryFindFirstDouble(JsonElement element, string[] candidateNames)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (candidateNames.Contains(property.Name))
            {
                if (property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetDouble(out var directVal))
                    return directVal;

                if (property.Value.ValueKind == JsonValueKind.String &&
                    double.TryParse(property.Value.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsedVal))
                    return parsedVal;
            }

            var nested = TryFindFirstDouble(property.Value, candidateNames);
            if (nested.HasValue)
                return nested;
        }
    }

    if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in element.EnumerateArray())
        {
            var nested = TryFindFirstDouble(item, candidateNames);
            if (nested.HasValue)
                return nested;
        }
    }

    return null;
}

public class AlertHub : Hub
{
}

public record TemperatureInput(double Value);

public class TemperatureMessage
{
    public double Value { get; set; }
    public string Color { get; set; } = "gray";
    public string Source { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string AlertMessage { get; set; } = "";
}

public class AlertNotification
{
    public string Message { get; set; } = "";
    public string Level { get; set; } = "";
    public double? Temperature { get; set; }
    public string Color { get; set; } = "gray";
    public DateTime Timestamp { get; set; }
    public string RawPayload { get; set; } = "";
}

public static class TemperatureColorHelper
{
    public static string GetColor(double temperature)
    {
        if (temperature <= -20) return "blue";
        if (temperature > -20 && temperature <= 0) return "lightblue";
        if (temperature > 0 && temperature <= 28) return "yellow";
        if (temperature > 28 && temperature <= 40) return "orange";
        return "red";
    }
}