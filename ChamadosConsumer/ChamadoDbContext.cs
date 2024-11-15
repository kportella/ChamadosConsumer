using Microsoft.EntityFrameworkCore;

namespace ChamadosConsumer;

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

    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     modelBuilder.ApplyConfiguration(new ChamadoConfiguration());
    //     base.OnModelCreating(modelBuilder);
    // }
}