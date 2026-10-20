using FinanceControl.Domain.Enums;

namespace FinanceControl.Application.Models;

public sealed record CriarLancamentoRequest(
    string Descricao,
    string Categoria,
    TipoLancamento Tipo,
    decimal ValorPrevisto,
    DateOnly Competencia,
    DateOnly? DataVencimento,
    string? Observacoes
);

public sealed record RegistrarBaixaRequest(
    decimal Valor,
    DateOnly Data,
    string? Observacao
);

public sealed record BaixaDto(int Id, decimal Valor, DateOnly Data, string? Observacao);

public sealed record LancamentoDto(
    int Id,
    string Descricao,
    string Categoria,
    TipoLancamento Tipo,
    decimal ValorPrevisto,
    decimal ValorRealizado,
    decimal SaldoPendente,
    DateOnly Competencia,
    DateOnly? DataVencimento,
    string? Observacoes,
    string Situacao
);

public sealed record ResumoMensalDto(
    DateOnly Competencia,
    decimal ReceitasPrevistas,
    decimal ReceitasRecebidas,
    decimal DespesasPrevistas,
    decimal DespesasPagas,
    decimal ReceitasPendentes,
    decimal DespesasPendentes,
    decimal SaldoRealizado
);
