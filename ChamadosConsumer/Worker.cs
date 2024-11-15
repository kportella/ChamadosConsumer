using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChamadosConsumer;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Console.WriteLine(" [*] Waiting for messages.");
            var factory = new ConnectionFactory { HostName = "localhost" };
            await using var connection = await factory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ChamadoDbContext>();

            await dbContext.Database.MigrateAsync(stoppingToken);

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

                    dbContext.Chamados.AddAsync(chamado, stoppingToken);
                    dbContext.SaveChangesAsync(stoppingToken);

                    return Task.CompletedTask;
                };

                await channel.BasicConsumeAsync("fila_chamados", autoAck: true, consumer: consumer,
                    cancellationToken: stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro no Background Service: {ex.Message}");
        }
    }
}
