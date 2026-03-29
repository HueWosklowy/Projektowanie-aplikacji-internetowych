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
    private readonly InfluxDBClient _influxClient;
    private readonly WriteApi _writeApi;

    private IConnection? _rabbitConnection;
    private IChannel? _channel;

    public MessageWorker(ILogger<MessageWorker> logger, IConfiguration config)
    {
        _logger = logger;
        // InfluxDB Client pozostaje bez zmian (zarządza własnym HTTP clientem)
        _influxClient = new InfluxDBClient("http://localhost:8086", "MY_TOKEN");

        _writeApi = _influxClient.GetWriteApi(new WriteOptions
        {
            BatchSize = 100,
            FlushInterval = 5000
        });

        _writeApi.EventHandler += (sender, eventArgs) => {
            if (eventArgs is WriteErrorEvent error)
                _logger.LogError(error.Exception, "InfluxDB background write error");
        };
    }

    private async Task InitRabbitMQAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory()
        {
            HostName = "localhost"
            // DispatchConsumersAsync nie jest już potrzebne w v7+
        };

        // W wersji 7.x połączenie i kanał tworzymy asynchronicznie
        _rabbitConnection = await factory.CreateConnectionAsync(ct);
        _channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: "sensor_data",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.BasicQosAsync(0, 10, false, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitRabbitMQAsync(stoppingToken);

        // W v7 EventingBasicConsumer obsługuje zadania (Task) domyślnie
        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                if (!double.TryParse(message, out var value))
                    throw new FormatException("Invalid double value");

                var point = PointData
                    .Measurement("telemetry")
                    .Tag("device", "sensor_01")
                    .Field("value", value)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                // WritePoint w tle (nieblokujące)
                _writeApi.WritePoint(point, "my-bucket", "my-org");

                // BasicAck jest teraz asynchroniczne
                await _channel!.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (FormatException)
            {
                _logger.LogError("Malformed message, discarding: {Msg}", message);
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                // Requeue: true w przypadku błędu systemowego
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: "sensor_data",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Utrzymujemy worker przy życiu do momentu otrzymania stoppingToken
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker is stopping...");
        }
    }

    public override void Dispose()
    {
        _writeApi?.Dispose();

        // W 7.x zamknięcie synchroniczne w Dispose jest dopuszczalne, 
        // ale zaleca się asynchroniczne zamykanie w StopAsync
        _channel?.Dispose();
        _rabbitConnection?.Dispose();
        _influxClient?.Dispose();
        base.Dispose();
    }
}