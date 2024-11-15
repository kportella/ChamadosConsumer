using ChamadosConsumer.Domain;

namespace ChamadosConsumer.Infrastructure;

public class ChamadoDataAccess(ChamadoDbContext context)
{
    public async Task GravarChamado(Chamado chamado, CancellationToken cancellationToken = default)
    {
        await context.Chamados.AddAsync(chamado, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}