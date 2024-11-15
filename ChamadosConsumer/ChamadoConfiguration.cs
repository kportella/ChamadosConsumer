using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosConsumer;

public class ChamadoConfiguration : IEntityTypeConfiguration<Chamado>
{
    public void Configure(EntityTypeBuilder<Chamado> builder)
    {
        builder.ToTable("Chamados");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Titulo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Descricao)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.TipoManutencao)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Criticidade)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Tecnico)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.DataAbertura)
            .IsRequired();

        builder.Property(c => c.DataFechamento)
            .IsRequired(false);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Equipamento)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Localizacao)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Modelo)
            .IsRequired()
            .HasMaxLength(100);
    }
}
