using ChamadosConsumer.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChamadosConsumer.Infrastructure;

public class ChamadoDbContext : DbContext
{
    public ChamadoDbContext(DbContextOptions<ChamadoDbContext> options)
        : base(options)
    {
    }
    public DbSet<Chamado> Chamados { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=database.db");
    }
}