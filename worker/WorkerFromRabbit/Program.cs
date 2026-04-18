using InfluxDB.Client;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<InfluxDbOptions>(
    builder.Configuration.GetSection("InfluxDb"));

builder.Services.AddSingleton<IConnection>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

    var factory = new ConnectionFactory
    {
        HostName = options.HostName,
        Port = options.Port,
        UserName = options.UserName,
        Password = options.Password
    };

    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<InfluxDbOptions>>().Value;

    return InfluxDBClientFactory.Create(
        options.Url,
        options.Token.ToCharArray());
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

public class RabbitMqOptions
{
    public string HostName { get; set; } = "";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string QueueName { get; set; } = "";
    public string DlqName { get; set; } = "";
}

public class InfluxDbOptions
{
    public string Url { get; set; } = "";
    public string Token { get; set; } = "";
    public string Org { get; set; } = "";
    public string Bucket { get; set; } = "";
}