namespace FinanceControl.Application.Models;

public sealed record CriarItemReceberRequest(string Descricao, decimal Valor, DateOnly Competencia,
    DateOnly? Vencimento, string? Observacoes);

public sealed record CriarGrupoReceberRequest(string Pessoa, string Descricao, IReadOnlyList<CriarItemReceberRequest> Itens);

public sealed record AtualizarGrupoReceberRequest(string Pessoa, string Descricao);

public sealed record DesmembrarReceberRequest(int LancamentoId, string Pessoa, string Descricao,
    IReadOnlyList<CriarItemReceberRequest> Itens);

public sealed record AtualizarItemReceberRequest(string Descricao, decimal Valor, DateOnly Competencia,
    DateOnly? Vencimento, string? Observacoes);

public sealed record GrupoReceberDto(int Id, string Pessoa, string Descricao, decimal TotalDevido,
    decimal TotalRecebido, decimal SaldoAReceber, IReadOnlyList<LancamentoDto> Itens);
