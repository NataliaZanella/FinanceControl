using FinanceControl.Application.Models;
using FinanceControl.Domain.Entities;
using FinanceControl.Domain.Enums;
using FinanceControl.Infrastructure.Data;
using FinanceControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceControl.Tests;

public sealed class LancamentoServiceTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly FinanceDbContext db;
    private readonly LancamentoService servico;
    private static readonly DateOnly Mes = new(2026, 10, 1);
    private static readonly DateOnly Data = new(2026, 10, 20);

    public LancamentoServiceTests()
    {
        conexao.Open();
        db = NovoContexto();
        db.Database.EnsureCreated();
        servico = new LancamentoService(db);
    }

    private FinanceDbContext NovoContexto() => new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(conexao).Options);
    private Task<LancamentoDto> CriarAsync(TipoLancamento tipo = TipoLancamento.Despesa) =>
        servico.CriarAsync(new("Registro sintético", "Outros", tipo, 100m, Mes, Data, "Original"));
    private static AtualizarLancamentoRequest Edicao(LancamentoDto item) =>
        new(item.Descricao, item.Categoria, item.Tipo, item.ValorPrevisto, item.Competencia, item.DataVencimento, item.Observacoes);

    [Fact]
    public async Task EdicaoSemBaixasMoveCompetenciaEAtualizaResumo()
    {
        var item = await CriarAsync();
        var atualizado = await servico.AtualizarAsync(item.Id, Edicao(item) with {
            Descricao = "  Alterado  ", Categoria = "  Trabalho  ", Tipo = TipoLancamento.Receita,
            ValorPrevisto = 200m, Competencia = new(2026, 11, 25), DataVencimento = null, Observacoes = "  Revisado  "
        });
        Assert.Equal("Alterado", atualizado.Descricao);
        Assert.Equal("Trabalho", atualizado.Categoria);
        Assert.Equal("Revisado", atualizado.Observacoes);
        Assert.Null(atualizado.DataVencimento);
        Assert.Equal(new DateOnly(2026, 11, 1), atualizado.Competencia);
        Assert.Empty(await servico.ListarAsync(Mes));
        Assert.Equal(200m, (await servico.ResumoAsync(atualizado.Competencia)).ReceitasPrevistas);
        Assert.Equal(0m, atualizado.ValorRealizado);
        Assert.Equal("Pendente", atualizado.Situacao);
    }

    [Theory]
    [InlineData(TipoLancamento.Despesa)]
    [InlineData(TipoLancamento.Receita)]
    public async Task EdicaoPreservaBaixasEOrigemImportada(TipoLancamento tipo)
    {
        var item = await CriarAsync(tipo);
        await servico.RegistrarBaixaAsync(item.Id, new(40m, Data, "Baixa sintética"));
        db.RegistrosImportados.Add(new RegistroImportacao { LancamentoId = item.Id, ChaveOrigem = new string('A', 64), Aba = "OUTUBRO 2026", LinhaExcel = 2 });
        await db.SaveChangesAsync();
        var historicoAntes = await servico.HistoricoAsync(item.Id);
        var atualizado = await servico.AtualizarAsync(item.Id, Edicao(item) with { ValorPrevisto = 80m, Descricao = "Corrigido" });
        Assert.Equal(40m, atualizado.ValorRealizado);
        Assert.Equal(40m, atualizado.SaldoPendente);
        Assert.Equal("Parcial", atualizado.Situacao);
        Assert.Equal(historicoAntes.ToArray(), (await servico.HistoricoAsync(item.Id)).ToArray());
        Assert.Equal(item.Id, (await db.RegistrosImportados.SingleAsync()).LancamentoId);
        atualizado = await servico.AtualizarAsync(item.Id, Edicao(atualizado) with { ValorPrevisto = 40m });
        Assert.Equal("Quitado", atualizado.Situacao);
        Assert.Equal(0m, atualizado.SaldoPendente);
    }

    [Fact]
    public async Task PrevistoAbaixoDoRealizadoNaoAlteraRegistro()
    {
        var item = await CriarAsync();
        await servico.RegistrarBaixaAsync(item.Id, new(40m, Data, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarAsync(item.Id, Edicao(item) with { ValorPrevisto = 39m, Descricao = "Não salvar" }));
        var salvo = Assert.Single(await servico.ListarAsync(Mes));
        Assert.Equal(item.Descricao, salvo.Descricao);
        Assert.Equal(100m, salvo.ValorPrevisto);
        Assert.Single(await servico.HistoricoAsync(item.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TipoECompetenciaComBaixasNaoPodemMudar(bool mudarTipo)
    {
        var item = await CriarAsync();
        await servico.RegistrarBaixaAsync(item.Id, new(10m, Data, null));
        var request = mudarTipo ? Edicao(item) with { Tipo = TipoLancamento.Receita }
            : Edicao(item) with { Competencia = Mes.AddMonths(1) };
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarAsync(item.Id, request));
        var salvo = Assert.Single(await servico.ListarAsync(Mes));
        Assert.Equal(item.Tipo, salvo.Tipo);
        Assert.Equal(item.Competencia, salvo.Competencia);
    }

    [Theory]
    [InlineData("descricao")]
    [InlineData("categoria")]
    [InlineData("tipo")]
    [InlineData("zero")]
    [InlineData("negativo")]
    [InlineData("precisao")]
    [InlineData("observacoes")]
    public async Task EdicaoRejeitaCamposInvalidosSemPersistir(string campo)
    {
        var item = await CriarAsync();
        var request = campo switch {
            "descricao" => Edicao(item) with { Descricao = " " },
            "categoria" => Edicao(item) with { Categoria = " " },
            "tipo" => Edicao(item) with { Tipo = (TipoLancamento)99 },
            "zero" => Edicao(item) with { ValorPrevisto = 0m },
            "negativo" => Edicao(item) with { ValorPrevisto = -1m },
            "precisao" => Edicao(item) with { ValorPrevisto = 1.001m },
            _ => Edicao(item) with { Observacoes = new string('x', 1001) }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarAsync(item.Id, request));
        Assert.Equal(item, Assert.Single(await servico.ListarAsync(Mes)));
    }

    [Fact]
    public async Task EdicaoDeInexistenteRetornaErroEExclusaoRetornaFalse()
    {
        var item = await CriarAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.AtualizarAsync(item.Id + 1, Edicao(item)));
        Assert.False(await servico.ExcluirAsync(item.Id + 1));
        Assert.Single(await servico.ListarAsync(Mes));
    }

    [Fact]
    public async Task ExclusaoSemBaixasRemoveApenasLancamentoEscolhidoESuaOrigem()
    {
        var item = await CriarAsync();
        var outro = await CriarAsync();
        db.RegistrosImportados.Add(new RegistroImportacao { LancamentoId = item.Id, ChaveOrigem = new string('B', 64), Aba = "OUTUBRO 2026", LinhaExcel = 3 });
        await db.SaveChangesAsync();
        Assert.True(await servico.ExcluirAsync(item.Id));
        Assert.Equal(outro.Id, Assert.Single(await servico.ListarAsync(Mes)).Id);
        Assert.Empty(await db.RegistrosImportados.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public async Task ExclusaoComBaixasPreservaLancamentoEHistorico(int valor)
    {
        var item = await CriarAsync();
        await servico.RegistrarBaixaAsync(item.Id, new(valor, Data, "Preservar"));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.ExcluirAsync(item.Id));
        Assert.Single(await servico.ListarAsync(Mes));
        Assert.Single(await servico.HistoricoAsync(item.Id));
    }

    [Fact]
    public async Task BaixaAposEdicaoUsaValorAtualDoBanco()
    {
        var item = await CriarAsync();
        await servico.AtualizarAsync(item.Id, Edicao(item) with { ValorPrevisto = 50m });
        await Assert.ThrowsAsync<ArgumentException>(() => servico.RegistrarBaixaAsync(item.Id, new(60m, Data, null)));
        Assert.Empty(await servico.HistoricoAsync(item.Id));
        var quitado = await servico.RegistrarBaixaAsync(item.Id, new(50m, Data, null));
        Assert.Equal("Quitado", quitado.Situacao);
    }

    [Fact]
    public async Task EdicaoReconsultaBaixasRegistradasPorOutraSessao()
    {
        var item = await CriarAsync();
        using var outroContexto = NovoContexto();
        await new LancamentoService(outroContexto).RegistrarBaixaAsync(item.Id, new(80m, Data, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarAsync(item.Id, Edicao(item) with { ValorPrevisto = 50m }));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.ExcluirAsync(item.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.RegistrarBaixaAsync(item.Id, new(30m, Data, null)));
        Assert.Equal(80m, Assert.Single(await servico.ListarAsync(Mes)).ValorRealizado);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }
}
