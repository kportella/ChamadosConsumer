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

        // Configura a conexão com o RabbitMQ
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Cria uma conexão assíncrona com o RabbitMQ
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        
        // Cria um canal para comunicação com o RabbitMQ
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declara a fila da DLQ caso ela ainda não tenha sido criada
        await channel.QueueDeclareAsync(
            queue: "dlq_chamados",  // Nome da fila de Dead Letter
            durable: true,          // Fila persistente, sobrevive a reinicializações
            exclusive: false,       // Fila pode ser acessada por múltiplas conexões
            autoDelete: false,      // A fila não será excluída automaticamente
            arguments: null,        // Sem argumentos adicionais
            cancellationToken: stoppingToken);

        // Cria um consumidor assíncrono para processar mensagens da DLQ
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Define o evento assíncrono para processamento das mensagens recebidas
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            // Cria um escopo para resolver dependências
            using var scope = _serviceProvider.CreateScope();
            var dataAccess = scope.ServiceProvider.GetRequiredService<ChamadoDataAccess>();

            try
            {
                // Lê e desserializa o corpo da mensagem
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var chamado = JsonSerializer.Deserialize<Chamado>(message);

                _logger.LogInformation("Mensagem recebida na DLQ: Título={Titulo}, Descrição={Descricao}, Data={Data}",
                    chamado.Titulo, chamado.Descricao, chamado.DataAbertura);

                // Tenta salvar o chamado novamente no banco de dados
                await dataAccess.GravarChamado(chamado, stoppingToken);

                _logger.LogInformation("Chamado da DLQ salvo no banco de dados com sucesso.");

                // Confirma que a mensagem foi processada com sucesso
                await channel.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag, // Identificador único da mensagem
                    multiple: false,             // Confirma apenas esta mensagem
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a mensagem na DLQ.");

                // Rejeita a mensagem e não a reencaminha para a fila
                await channel.BasicNackAsync(
                    deliveryTag: ea.DeliveryTag, // Identificador único da mensagem
                    multiple: false,             // Rejeita apenas esta mensagem
                    requeue: false,              // Não reencaminha para a DLQ
                    cancellationToken: stoppingToken);
            }
        };

        // Inicia o consumo da fila DLQ com confirmação manual (autoAck = false)
        await channel.BasicConsumeAsync(
            queue: "dlq_chamados",  // Nome da fila a ser consumida
            autoAck: false,         // Desabilita confirmação automática de mensagens
            consumer: consumer,     // Consumidor configurado
            cancellationToken: stoppingToken);

        // Aguarda o cancelamento do token para encerrar o serviço
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}