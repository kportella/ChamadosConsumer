using System.Text;
using System.Text.Json;
using ChamadosConsumer.Domain;
using ChamadosConsumer.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChamadosConsumer.BackgroundServices;

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

        // Declarar a Dead Letter Exchange (DLX)
        await channel.ExchangeDeclareAsync(
            exchange: "dlx_chamados",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Declarar a Dead Letter Queue (DLQ)
        await channel.QueueDeclareAsync(
            queue: "dlq_chamados",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Vincular a DLQ à DLX
        await channel.QueueBindAsync(
            queue: "dlq_chamados",
            exchange: "dlx_chamados",
            routingKey: "",
            arguments: null,
            cancellationToken: stoppingToken);

        // Configurar argumentos para a fila principal
        var args = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "dlx_chamados" }
            // Opcional: { "x-message-ttl", 30000 } // TTL de 30 segundos
        };

        // Declarar a fila principal com DLX
        await channel.QueueDeclareAsync(
            queue: "fila_chamados",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var dataAccess = scope.ServiceProvider.GetRequiredService<ChamadoDataAccess>();

            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var chamado = JsonSerializer.Deserialize<Chamado>(message);

                _logger.LogInformation("Mensagem recebida: Título={Titulo}, Descrição={Descricao}, Data={Data}",
                    chamado.Titulo, chamado.Descricao, chamado.DataAbertura);

                if (chamado.Titulo == "DLQ")
                    throw new Exception();
                
                await dataAccess.GravarChamado(chamado, stoppingToken);

                _logger.LogInformation("Chamado salvo no banco de dados com sucesso.");

                // Confirma que a mensagem foi processada com sucesso
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a mensagem.");

                // Rejeita a mensagem e não a reencaminha para a fila principal
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        // Consumir a fila com autoAck false
        await channel.BasicConsumeAsync(
            queue: "fila_chamados",
            autoAck: false,
            consumer: consumer, cancellationToken: stoppingToken);

        // Aguarda o cancelamento do token para encerrar o serviço
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
