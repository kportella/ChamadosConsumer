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

        // Cria uma fábrica de conexão para conectar ao rabbit
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Cria a conexão e o canal de forma assincrona
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declara a Dead Letter Exchange (DLX)
        // Exchange: Nome do exchange
        // Type: Tipo do exchange.
        // Fanout: Envia mensagens para todas as filas vinculadas ao exchange, independentemente de chave de roteamento.
        // Durable: Mensagem fica no disco, mesmo quando o rabbit é reinicializado
        // AutoDelete: Fila não vai ser delatada automaticante quando não tiver mais consumidores
        // Arguments: Sem argumentos adicionais
        await channel.ExchangeDeclareAsync(
            exchange: "dlx_chamados", 
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Declarar a Dead Letter Queue (DLQ)
        // Queue: Esta é a fila onde as mensagens "mortas" (não processadas ou rejeitadas) serão armazenadas.
        // Durable: Mensagem fica no disco, mesmo quando o rabbit é reinicializado
        // Exclusive: Define que a fila não é exclusiva a essa conexão
        // AutoDelete: Fila não vai ser delatada automaticante quando não tiver mais consumidores
        // Arguments: Sem argumentos adicionais
        await channel.QueueDeclareAsync(
            queue: "dlq_chamados",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Vincular a DLQ à DLX
        // Queue: Nome da fila de mensagens mortas
        // Exchance: Nome do Dead Letter Exchange (DLX) responsável por receber mensagens.
        // RoutingKey: No caso de um exchange do tipo Fanout, a chave de roteamento não é relevante.
        // Arguments: Sem argumentos adicionais
        await channel.QueueBindAsync(
            queue: "dlq_chamados",
            exchange: "dlx_chamados",
            routingKey: "",
            arguments: null,
            cancellationToken: stoppingToken);

        // Configurar argumentos para a fila principal
        // x-dead-letter-exchange: Define o Dead Letter Exchange (DLX) para a fila "fila_chamados"
        var args = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "dlx_chamados" }
            // Opcional: { "x-message-ttl", 30000 } // TTL de 30 segundos
        };

        // Declarar a fila principal com DLX
        // Queue:
        // Durable: Mensagem fica no disco, mesmo quando o rabbit é reinicializado
        // Exclusive: Define que a fila não é exclusiva a essa conexão
        // AutoDelete: Fila não vai ser delatada automaticante quando não tiver mais consumidores
        // Arguments: Configurações adicionais para a fila (DLX e TTL, neste caso).
        await channel.QueueDeclareAsync(
            queue: "fila_chamados",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args,
            cancellationToken: stoppingToken);

        // Criação de um consumidor assíncrono para processar mensagens
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Define o evento assíncrono que será chamado quando uma mensagem for recebida
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            // Cria um escopo para obter instâncias dos serviços necessários
            using var scope = _serviceProvider.CreateScope();
            var dataAccess = scope.ServiceProvider.GetRequiredService<ChamadoDataAccess>();

            try
            {
                // Converte o corpo da mensagem para uma string
                // Unmarshaling
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                // Desserializa a mensagem para o objeto Chamado
                var chamado = JsonSerializer.Deserialize<Chamado>(message);

                _logger.LogInformation("Mensagem recebida: Título={Titulo}, Descrição={Descricao}, Data={Data}",
                    chamado.Titulo, chamado.Descricao, chamado.DataAbertura);

                // Simula uma falha ao processar mensagens específicas (exemplo)
                if (chamado.Titulo == "DLQ")
                    throw new Exception();
                
                // Salva os dados do chamado no banco de dados
                await dataAccess.GravarChamado(chamado, stoppingToken);

                _logger.LogInformation("Chamado salvo no banco de dados com sucesso.");

                // Confirma que a mensagem foi processada com sucesso
                await channel.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag,    // Identificador único da mensagem
                    multiple: false,                // Confirma apenas esta mensagem
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a mensagem.");

                // Rejeita a mensagem e não a reencaminha para a fila principal
                await channel.BasicNackAsync(
                    deliveryTag: ea.DeliveryTag,    // Identificador único da mensagem
                    multiple: false,                // Rejeita apenas esta mensagem
                    requeue: false,                 // Não reencaminha para a fila principal
                    cancellationToken: stoppingToken);
            }
        };

        // Inicia o consumo da fila, definindo autoAck como false para controle manual de mensagens
        await channel.BasicConsumeAsync(
            queue: "fila_chamados",     // Nome da fila a ser consumida
            autoAck: false,             // Desabilita confirmação automática de mensagens
            consumer: consumer,         // Consumidor definido
            cancellationToken: stoppingToken);

        // Aguarda o cancelamento do token para encerrar o serviço
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
