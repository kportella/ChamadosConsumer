using System.Text;
using System.Text.Json;
using ChamadosConsumer.Domain;
using ChamadosConsumer.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChamadosConsumer.BackgroundServices;

public class WorkerDLQ : BackgroundService
{
    private readonly ILogger<WorkerDLQ> _logger;
    private readonly IServiceProvider _serviceProvider;

    public WorkerDLQ(ILogger<WorkerDLQ> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DLQWorker iniciado, aguardando mensagens na DLQ.");

        var factory = new ConnectionFactory { HostName = "localhost" };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declarar a DLQ caso ainda não exista
        await channel.QueueDeclareAsync(
            queue: "dlq_chamados",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
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

                _logger.LogInformation("Mensagem recebida na DLQ: Título={Titulo}, Descrição={Descricao}, Data={Data}",
                    chamado.Titulo, chamado.Descricao, chamado.DataAbertura);

                // Tentar salvar o chamado no banco de dados novamente
                await dataAccess.GravarChamado(chamado, stoppingToken);

                _logger.LogInformation("Chamado da DLQ salvo no banco de dados com sucesso.");

                // Confirma que a mensagem foi processada com sucesso
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a mensagem na DLQ.");

                // Rejeita a mensagem e não a reencaminha para a fila
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        // Consumir a DLQ com autoAck false
        await channel.BasicConsumeAsync(
            queue: "dlq_chamados",
            autoAck: false,
            consumer: consumer, cancellationToken: stoppingToken);

        // Aguarda o cancelamento do token para encerrar o serviço
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}