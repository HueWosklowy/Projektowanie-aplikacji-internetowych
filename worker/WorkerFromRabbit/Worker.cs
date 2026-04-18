using System.Text;
using System.Text.Json;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnection _connection;
    private readonly InfluxDBClient _influxClient;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly InfluxDbOptions _influxOptions;

    private IChannel? _channel;

    public Worker(
        ILogger<Worker> logger,
        IConnection connection,
        InfluxDBClient influxClient,
        IOptions<RabbitMqOptions> rabbitOptions,
        IOptions<InfluxDbOptions> influxOptions)
    {
        _logger = logger;
        _connection = connection;
        _influxClient = influxClient;
        _rabbitOptions = rabbitOptions.Value;
        _influxOptions = influxOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _rabbitOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _rabbitOptions.DlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("Odebrano wiadomoœæ: {Body}", body);

            try
            {
                var msg = JsonSerializer.Deserialize<TelemetryMessage>(body);

                if (msg is null)
                {
                    throw new Exception("JSON deserializowa³ siê do null.");
                }

                ValidateMessage(msg);

                if (!double.TryParse(msg.Data, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    throw new Exception("Pole Data nie jest poprawn¹ liczb¹.");
                }

                var point = PointData
                    .Measurement("telemetry")
                    .Tag("deviceId", msg.DeviceId)
                    .Field("value", value)
                    .Timestamp(msg.Timestamp, WritePrecision.Ns);

                var writeApi = _influxClient.GetWriteApiAsync();

                await writeApi.WritePointAsync(
                    point,
                    _influxOptions.Bucket,
                    _influxOptions.Org,
                    stoppingToken);

                await writeApi.WritePointAsync(
                    point,
                    _influxOptions.Bucket,
                    _influxOptions.Org,
                    stoppingToken);

                _logger.LogInformation(
                    "Zapisano do InfluxDB: device={DeviceId}, value={Value}",
                    msg.DeviceId, value);

                await _channel.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "B³¹d przetwarzania wiadomoœci.");

                var dlqBody = Encoding.UTF8.GetBytes(body);

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _rabbitOptions.DlqName,
                    mandatory: false,
                    body: dlqBody,
                    cancellationToken: stoppingToken);

                _logger.LogWarning("Wys³ano wiadomoœæ do DLQ.");

                await _channel.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _rabbitOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Worker nas³uchuje kolejki {Queue}", _rabbitOptions.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private static void ValidateMessage(TelemetryMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.DeviceId))
            throw new Exception("DeviceId jest pusty.");

        if (string.IsNullOrWhiteSpace(msg.Data))
            throw new Exception("Data jest puste.");

        if (msg.Timestamp == default)
            throw new Exception("Timestamp jest niepoprawny.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await _connection.CloseAsync(cancellationToken);
        await _connection.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}

public class TelemetryMessage
{
    public string DeviceId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Data { get; set; } = "";
}