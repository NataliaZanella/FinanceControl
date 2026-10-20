using FinanceControl.Domain.Enums;

namespace FinanceControl.Domain.Entities;

public sealed class Lancamento
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Outros";
    public TipoLancamento Tipo { get; set; }
    public decimal ValorPrevisto { get; set; }
    // Competência: mês de referência do lançamento; não é a data do pagamento.
    public DateOnly Competencia { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public string? Observacoes { get; set; }
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Baixa> Baixas { get; set; } = new List<Baixa>();
}
