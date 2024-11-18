using ChamadosConsumer.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChamadosConsumer.Infrastructure;

public class ChamadoDbContext : DbContext
{
    private readonly IConfiguration _configuration;
    
    public ChamadoDbContext(DbContextOptions<ChamadoDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }
    public DbSet<Chamado> Chamados { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Configura connection string 
        optionsBuilder.UseSqlite(_configuration.GetConnectionString("DefaultConnection"));
    }
}