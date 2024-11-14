namespace ChamadosConsumer;

public record Chamado(string Titulo, string Descricao, ETipoManutencao TipoManutencao, ECriticidade Criticidade, 
    string Tecnico, DateTime DataAbertura, DateTime DataFechamento, EStatus Status, string Equipamento, 
    string Localizacao, string Modelo);
    
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