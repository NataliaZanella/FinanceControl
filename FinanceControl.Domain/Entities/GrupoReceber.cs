namespace FinanceControl.Domain.Entities;

public sealed class GrupoReceber
{
    public int Id { get; set; }
    public string Pessoa { get; set; } = "";
    public string Descricao { get; set; } = "";
    public ICollection<ItemGrupoReceber> Itens { get; set; } = new List<ItemGrupoReceber>();
}

// O valor e os recebimentos pertencem ao lançamento; não há cópia de saldo.
public sealed class ItemGrupoReceber
{
    public int Id { get; set; }
    public int GrupoReceberId { get; set; }
    public GrupoReceber GrupoReceber { get; set; } = null!;
    public int LancamentoId { get; set; }
    public Lancamento Lancamento { get; set; } = null!;
}
