using ChamadosConsumer.Domain;

namespace ChamadosConsumer.Infrastructure;

public class ChamadoDataAccess(ChamadoDbContext context)
{
    public async Task GravarChamado(Chamado chamado, CancellationToken cancellationToken = default)
    {
        // Grava e salva objeto de chamado.
        await context.Chamados.AddAsync(chamado, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}