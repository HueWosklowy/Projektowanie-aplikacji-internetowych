using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();

    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
    {
        var body = await reader.ReadToEndAsync();

        Console.WriteLine($"[LOG] Otrzymano zadanie: {body}");

        context.Request.Body.Position = 0;
    }

    await next();
});

app.MapPost("/pomiary", (MeasurementRequest request) =>
{
    if (string.IsNullOrEmpty(request.PayloadBase64))
    {
        return Results.BadRequest("Payload nie moze byc pusty.");
    }

    try
    {
        byte[] dataBytes = Convert.FromBase64String(request.PayloadBase64);
        string decodedString = Encoding.UTF8.GetString(dataBytes);

        Console.WriteLine($"[DEKODOWANIE] Wynik: {decodedString}");

        return Results.Ok(new { message = "Dane odebrane i zdekodowane", size = dataBytes.Length });
    }
    catch (FormatException)
    {
        return Results.BadRequest("Nieprawidlowy format Base64.");
    }
});

app.Run();
public record MeasurementRequest(string DeviceId, string PayloadBase64);