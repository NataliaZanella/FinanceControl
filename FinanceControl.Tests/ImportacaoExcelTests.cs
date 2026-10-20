using ClosedXML.Excel;
using FinanceControl.Infrastructure.Data;
using FinanceControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceControl.Tests;

public sealed class ImportacaoExcelTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly FinanceDbContext db;
    private readonly ImportacaoExcelService servico;

    public ImportacaoExcelTests()
    {
        conexao.Open();
        db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        servico = new ImportacaoExcelService(db);
    }

    private static byte[] Planilha(Action<IXLWorksheet>? configurar = null, string aba = "OUTUBRO 2026")
    {
        using var book = new XLWorkbook();
        var ws = book.AddWorksheet(aba);
        ws.Cell("A1").Value = "CONTAS";
        ws.Cell("G1").Value = "TRABALHOS";
        ws.Cell("A2").Value = new DateTime(2026, 10, 20);
        ws.Cell("B2").Value = "Despesa sintética";
        ws.Cell("C2").Value = 100m;
        configurar?.Invoke(ws);
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    [Fact]
    public async Task PreviaNaoGravaEConfirmacaoNaoCriaBaixas()
    {
        var previa = await servico.PrepararPreviaAsync(Planilha(), "sintetica.xlsx", "OUTUBRO 2026", null);
        Assert.Empty(await db.Lancamentos.ToListAsync());
        Assert.Single(previa.Linhas);
        Assert.Equal(1, (await servico.ConfirmarAsync(previa.Linhas)).Importadas);
        Assert.Single(await db.RegistrosImportados.ToListAsync());
        Assert.Empty(await db.Baixas.ToListAsync());
    }

    [Theory]
    [InlineData("sintetica.xlsx")]
    [InlineData("renomeada.xlsx")]
    public async Task ReimportacaoBloqueiaPorOrigemOuConteudo(string arquivo)
    {
        var bytes = Planilha();
        var primeira = await servico.PrepararPreviaAsync(bytes, "sintetica.xlsx", "OUTUBRO 2026", null);
        await servico.ConfirmarAsync(primeira.Linhas);
        var segunda = await servico.PrepararPreviaAsync(bytes, arquivo, "OUTUBRO 2026", null);
        Assert.True(Assert.Single(segunda.Linhas).Duplicada);
        segunda.Linhas[0].Selecionada = true;
        Assert.Equal(1, (await servico.ConfirmarAsync(segunda.Linhas)).Duplicadas);
        Assert.Equal(1, await db.Lancamentos.CountAsync());
    }

    [Theory]
    [InlineData("650,00", "650")]
    [InlineData("650.00", "650")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    public async Task ValoresTextuaisNaoMudamDeGrandeza(string texto, string esperado)
    {
        var previa = await servico.PrepararPreviaAsync(Planilha(ws => ws.Cell("C2").Value = texto), "sintetica.xlsx", "OUTUBRO 2026", null);
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), Assert.Single(previa.Linhas).Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("1.234")]
    public async Task ValoresInvalidosOuAmbiguosFicamBloqueados(string texto)
    {
        var previa = await servico.PrepararPreviaAsync(Planilha(ws => ws.Cell("C2").Value = texto), "sintetica.xlsx", "OUTUBRO 2026", null);
        var linha = Assert.Single(previa.Linhas);
        Assert.True(linha.Bloqueada);
        linha.Selecionada = true;
        Assert.Equal(1, (await servico.ConfirmarAsync(previa.Linhas)).Ignoradas);
    }

    [Fact]
    public async Task DatasSuspeitasEObservacoesExigemSelecaoManual()
    {
        var previa = await servico.PrepararPreviaAsync(Planilha(ws => {
            ws.Cell("A2").Value = new DateTime(2026, 11, 20);
            ws.Cell("D2").Value = "PAGUEI";
        }), "sintetica.xlsx", "OUTUBRO 2026", null);
        Assert.False(Assert.Single(previa.Linhas).Selecionada);
        Assert.Equal(0, (await servico.ConfirmarAsync(previa.Linhas)).Importadas);
    }

    [Fact]
    public async Task DividasEAbaSemAnoNaoSaoImportadasAutomaticamente()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviaAsync(Planilha(aba: "VALOR TOTAL DE DÍVIDAS"), "sintetica.xlsx", "VALOR TOTAL DE DÍVIDAS", null));
        var bytes = Planilha(aba: "OUTUBRO");
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviaAsync(bytes, "sintetica.xlsx", "OUTUBRO", null));
        Assert.Equal(new DateOnly(2026, 10, 1), (await servico.PrepararPreviaAsync(bytes, "sintetica.xlsx", "OUTUBRO", 2026)).Competencia);
    }

    [Fact]
    public async Task AbaAcimaDoLimiteNaoProduzImportacaoParcialSilenciosa()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviaAsync(Planilha(ws => ws.Cell("B5001").Value = "Fora do limite"), "sintetica.xlsx", "OUTUBRO 2026", null));
    }

    [Fact]
    public async Task FalhaNaPersistenciaReverteTodoOLote()
    {
        var previa = await servico.PrepararPreviaAsync(Planilha(ws => {
            ws.Cell("B3").Value = "Falha sintética";
            ws.Cell("C3").Value = 200m;
        }), "sintetica.xlsx", "OUTUBRO 2026", null);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FalhaTeste BEFORE INSERT ON Lancamentos WHEN NEW.Descricao = 'Falha sintética' BEGIN SELECT RAISE(ABORT, 'Falha sintética'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => servico.ConfirmarAsync(previa.Linhas));
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Lancamentos.CountAsync());
        Assert.Equal(0, await db.RegistrosImportados.CountAsync());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public async Task DetectaReceitasEmLayoutsHistoricosEIgnoraTotais(int coluna)
    {
        var previa = await servico.PrepararPreviaAsync(Planilha(ws => {
            ws.Cell("G1").Clear();
            ws.Cell(1, coluna).Value = "TRABALHOS";
            ws.Cell(2, coluna).Value = new DateTime(2026, 10, 21);
            ws.Cell(2, coluna + 1).Value = "Receita sintética";
            ws.Cell(2, coluna + 2).Value = 250m;
            ws.Cell("B3").Value = "TOTAL";
            ws.Cell("C3").Value = 100m;
        }), "sintetica.xlsx", "OUTUBRO 2026", null);
        Assert.Equal(2, previa.Linhas.Count);
        Assert.Equal(250m, Assert.Single(previa.Linhas, x => x.Tipo == FinanceControl.Domain.Enums.TipoLancamento.Receita).Valor);
    }

    [Fact]
    public async Task ConfirmacaoReverificaDuplicidadeAposCorrecaoDeData()
    {
        var bytes = Planilha();
        var original = await servico.PrepararPreviaAsync(bytes, "sintetica.xlsx", "OUTUBRO 2026", null);
        await servico.ConfirmarAsync(original.Linhas);
        var outra = await servico.PrepararPreviaAsync(Planilha(ws => ws.Cell("A2").Value = new DateTime(2026, 10, 21)), "outra.xlsx", "OUTUBRO 2026", null);
        Assert.False(Assert.Single(outra.Linhas).Duplicada);
        outra.Linhas[0].Data = new DateTime(2026, 10, 20);
        Assert.Equal(1, (await servico.ConfirmarAsync(outra.Linhas)).Duplicadas);
        Assert.Equal(1, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task EditarImportadoPreservaBloqueioPorOrigem()
    {
        var bytes = Planilha();
        var previa = await servico.PrepararPreviaAsync(bytes, "sintetica.xlsx", "OUTUBRO 2026", null);
        await servico.ConfirmarAsync(previa.Linhas);
        var lancamentos = new LancamentoService(db);
        var item = Assert.Single(await lancamentos.ListarAsync(new(2026, 10, 1)));
        await lancamentos.AtualizarAsync(item.Id, new("Descrição corrigida", item.Categoria, item.Tipo,
            150m, item.Competencia, item.DataVencimento, item.Observacoes));
        var repetida = await servico.PrepararPreviaAsync(bytes, "sintetica.xlsx", "OUTUBRO 2026", null);
        Assert.True(Assert.Single(repetida.Linhas).Duplicada);
        Assert.Equal(1, await db.Lancamentos.CountAsync());
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }
}
