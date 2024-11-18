namespace ChamadosConsumer.Domain;

// Objeto de chamado.
public class Chamado
{
    public int Id { get; set; }
    public string Titulo { get; set; }
    public string Descricao { get; set; }
    public ETipoManutencao TipoManutencao { get; set; }
    public ECriticidade Criticidade { get; set; }
    public string Tecnico { get; set; }
    public DateTime DataAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public EStatus Status { get; set; }
    public string Equipamento { get; set; }
    public string Localizacao { get; set; }
    public string Modelo { get; set; }
}

    
public enum ETipoManutencao
{
    Preventiva = 1,
    Corretiva = 2,
    Preditiva = 3
}

public enum ECriticidade
{
    Baixa = 1,
    Media = 2,
    Alta = 3
}

public enum EStatus
{
    Aberto = 1,
    Fechado = 0
}