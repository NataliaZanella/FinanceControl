namespace FinanceControl.Domain.Entities;

// Uma baixa representa um pagamento de despesa ou recebimento de receita.
public sealed class Baixa
{
    public int Id { get; set; }
    public int LancamentoId { get; set; }
    public Lancamento Lancamento { get; set; } = null!;
    public decimal Valor { get; set; }
    public DateOnly Data { get; set; }
    public string? Observacao { get; set; }
}
