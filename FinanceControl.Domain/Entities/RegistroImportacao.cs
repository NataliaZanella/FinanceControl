namespace FinanceControl.Domain.Entities;

// Marca a origem da linha importada. Chave única evita importar novamente a mesma linha.
public sealed class RegistroImportacao
{
    public int Id { get; set; }
    public string ChaveOrigem { get; set; } = "";
    public string Aba { get; set; } = "";
    public int LinhaExcel { get; set; }
    public int LancamentoId { get; set; }
    public Lancamento Lancamento { get; set; } = null!;
}
