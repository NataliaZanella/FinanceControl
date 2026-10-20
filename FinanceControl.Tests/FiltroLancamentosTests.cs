using FinanceControl.Application.Models;
using FinanceControl.Domain.Enums;
using Xunit;

namespace FinanceControl.Tests;

public sealed class FiltroLancamentosTests
{
    private static readonly LancamentoDto[] Itens = [
        new(1, "Compra sintética", "Educação", TipoLancamento.Despesa, 100m, 0m, 100m, new(2026, 10, 1), null, null, "Pendente"),
        new(2, "Serviço fictício", "Trabalho", TipoLancamento.Receita, 200m, 50m, 150m, new(2026, 10, 1), null, "Transferência parcial", "Parcial"),
        new(3, "Curso fictício", "Educação", TipoLancamento.Despesa, 300m, 300m, 0m, new(2026, 10, 1), null, "Conferência realizada", "Quitado")
    ];

    [Theory]
    [InlineData(" COMPRA SINTETICA ", 1)]
    [InlineData("transferencia", 2)]
    [InlineData("CONFERENCIA", 3)]
    public void BuscaSemAcentosEncontraDescricaoOuObservacao(string busca, int id)
    {
        Assert.Equal(id, Assert.Single(new FiltroLancamentos { Busca = busca }.Aplicar(Itens)).Id);
    }

    [Fact]
    public void FiltrosCombinadosPrecisamCoincidirNoMesmoRegistro()
    {
        var filtro = new FiltroLancamentos { Busca = "ficticio", Tipo = TipoLancamento.Despesa,
            Categoria = " educação ", Situacao = "quitado" };
        Assert.Equal(3, Assert.Single(filtro.Aplicar(Itens)).Id);
        filtro.Situacao = "Parcial";
        Assert.Empty(filtro.Aplicar(Itens));
    }

    [Fact]
    public void BuscaVaziaMantemOrdemEDadosOriginais()
    {
        var filtro = new FiltroLancamentos { Busca = " " };
        Assert.False(filtro.Ativo);
        Assert.Equal(Itens, filtro.Aplicar(Itens).ToArray());
        filtro.Tipo = TipoLancamento.Receita;
        Assert.True(filtro.Ativo);
        Assert.Single(filtro.Aplicar(Itens));
        Assert.Equal(3, Itens.Length);
        Assert.Equal(600m, Itens.Sum(x => x.ValorPrevisto));
    }

    [Fact]
    public void CategoriaCorrespondeAoNomeCompleto()
    {
        Assert.Empty(new FiltroLancamentos { Categoria = "Edu" }.Aplicar(Itens));
        Assert.Equal(2, new FiltroLancamentos { Categoria = "Educação" }.Aplicar(Itens).Count);
    }

    [Fact]
    public void FiltrosNaoInferemQuitacaoDeObservacoes()
    {
        var itens = new[] { Itens[0] with { Observacoes = "PAGUEI", Situacao = "Pendente" } };
        Assert.Empty(new FiltroLancamentos { Situacao = "Quitado" }.Aplicar(itens));
        Assert.Single(new FiltroLancamentos { Situacao = "Pendente" }.Aplicar(itens));
    }
}
