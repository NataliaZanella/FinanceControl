using System.Globalization;
using FinanceControl.Domain.Enums;

namespace FinanceControl.Application.Models;

// Consulta em memória sobre os DTOs mensais; não altera valores nem regras de baixa.
public sealed class FiltroLancamentos
{
    public string Busca { get; set; } = "";
    public TipoLancamento? Tipo { get; set; }
    public string Situacao { get; set; } = "";
    public string Categoria { get; set; } = "";
    public bool Ativo => !string.IsNullOrWhiteSpace(Busca) || Tipo.HasValue ||
        !string.IsNullOrWhiteSpace(Situacao) || !string.IsNullOrWhiteSpace(Categoria);

    public IReadOnlyList<LancamentoDto> Aplicar(IEnumerable<LancamentoDto> itens)
    {
        var busca = Busca.Trim();
        var categoria = Categoria.Trim();
        var situacao = Situacao.Trim();
        var comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        const CompareOptions opcoes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return itens.Where(x =>
            (!Tipo.HasValue || x.Tipo == Tipo.Value) &&
            (situacao.Length == 0 || string.Equals(x.Situacao, situacao, StringComparison.OrdinalIgnoreCase)) &&
            (categoria.Length == 0 || string.Equals(x.Categoria.Trim(), categoria, StringComparison.OrdinalIgnoreCase)) &&
            (busca.Length == 0 || comparador.IndexOf(x.Descricao, busca, opcoes) >= 0 ||
                comparador.IndexOf(x.Observacoes ?? "", busca, opcoes) >= 0)).ToList();
    }
}
