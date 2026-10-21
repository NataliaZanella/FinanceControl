using FinanceControl.Domain.Enums;

namespace FinanceControl.Application.Models;

public sealed record AbaImportacaoDto(string Nome, bool TemAno, int? Ano, int? Mes, bool Suportada);
public sealed record SelecaoAbaImportacaoRequest(string Aba, int? AnoManual);

public sealed class LinhaImportacaoDto
{
    public string ChaveOrigem { get; set; } = "";
    public string Aba { get; set; } = "";
    public int LinhaExcel { get; set; }
    public TipoLancamento Tipo { get; set; }
    public string Descricao { get; set; } = "";
    public string Categoria { get; set; } = "";
    public decimal? Valor { get; set; }
    public DateOnly Competencia { get; set; }
    public DateTime? Data { get; set; }
    public string? Observacoes { get; set; }
    public string Aviso { get; set; } = "";
    public bool Bloqueada { get; set; }
    public bool Duplicada { get; set; }
    public bool Selecionada { get; set; }
}

public sealed record PreviaImportacaoDto(string Aba, DateOnly Competencia, IReadOnlyList<LinhaImportacaoDto> Linhas);
public sealed record ResultadoImportacaoDto(int Importadas, int Ignoradas, int Duplicadas);
