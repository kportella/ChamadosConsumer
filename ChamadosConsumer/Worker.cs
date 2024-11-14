using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChamadosConsumer;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine(" [*] Waiting for messages.");
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(queue: "fila_chamados", durable: false, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var chamado = JsonSerializer.Deserialize<Chamado>(message);
                Console.WriteLine(" [x] Recebido Chamado ID: {0}, Descrição: {1}, Data: {2}",
                    chamado.Titulo, chamado.Descricao, chamado.DataAbertura);
                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync("fila_chamados", autoAck: true, consumer: consumer,
                cancellationToken: stoppingToken);
        }
    }
}
