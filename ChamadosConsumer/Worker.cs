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
        _logger.LogInformation("Worker iniciado, aguardando mensagens.");

        var factory = new ConnectionFactory { HostName = "localhost" };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: "fila_chamados",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ChamadoDbContext>();

            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var chamado = JsonSerializer.Deserialize<Chamado>(message);

                _logger.LogInformation("Mensagem recebida: Título={Titulo}, Descrição={Descricao}, Data={Data}",
                    chamado.Titulo, chamado.Descricao, chamado.DataAbertura);

                await dbContext.Chamados.AddAsync(chamado, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Chamado salvo no banco de dados com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a mensagem.");
            }
        };

        await channel.BasicConsumeAsync(
            queue: "fila_chamados",
            autoAck: true,
            consumer: consumer, cancellationToken: stoppingToken);

        // Aguarda o cancelamento do token para encerrar o serviço
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
