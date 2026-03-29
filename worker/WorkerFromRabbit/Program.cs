/*using WorkerFromRabbit;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();*/

using System.Text;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class MessageWorker : BackgroundService
{
    private readonly ILogger<MessageWorker> _logger;
    private readonly IConnection _rabbitConnection;
    private readonly IModel _channel;
    private readonly InfluxDBClient _influxClient;

    public MessageWorker(ILogger<MessageWorker> logger)
    {
        _logger = logger;

        // Konfiguracja RabbitMQ
        var factory = new ConnectionFactory() { HostName = "localhost" };
        _rabbitConnection = factory.CreateConnection();
        _channel = _rabbitConnection.CreateModel();
        _channel.QueueDeclare(queue: "sensor_data", durable: true, exclusive: false, autoDelete: false);

        // Konfiguracja InfluxDB
        _influxClient = new InfluxDBClient("http://localhost:8086", "MY_TOKEN");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation($"Odebrano: {message}");

            try
            {
                // Logika zapisu do InfluxDB
                SaveToInflux(message);

                // Potwierdzenie przetworzenia (Ack)
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Błąd zapisu: {ex.Message}");
                // Opcjonalnie: BasicNack w zależności od strategii błędów
            }
        };

        _channel.BasicConsume(queue: "sensor_data", autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private void SaveToInflux(string data)
    {
        // Przykład: Parsowanie danych (załóżmy format JSON lub prosty String)
        using (var writeApi = _influxClient.GetWriteApi())
        {
            var point = PointData
                .Measurement("telemetry")
                .Tag("device", "sensor_01")
                .Field("value", double.Parse(data)) // Dostosuj do swojego modelu
                .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

            writeApi.WritePoint(point, "my-bucket", "my-org");
        }
    }

    public override void Dispose()
    {
        _rabbitConnection.Close();
        _influxClient.Dispose();
        base.Dispose();
    }
}
