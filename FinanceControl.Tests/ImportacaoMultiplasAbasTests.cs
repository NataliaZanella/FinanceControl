using ClosedXML.Excel;
using FinanceControl.Application.Models;
using FinanceControl.Infrastructure.Data;
using FinanceControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceControl.Tests;

public sealed class ImportacaoMultiplasAbasTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly FinanceDbContext db;
    private readonly ImportacaoExcelService servico;

    public ImportacaoMultiplasAbasTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        servico = new(db);
    }

    private static byte[] Planilha(Action<XLWorkbook>? configurar = null)
    {
        using var book = new XLWorkbook();
        AdicionarAba(book, "OUTUBRO 2026", 10);
        AdicionarAba(book, "NOVEMBRO 2026", 11);
        AdicionarAba(book, "DEZEMBRO 2026", 12);
        book.AddWorksheet("VALOR TOTAL DE DÍVIDAS");
        configurar?.Invoke(book);
        using var stream = new MemoryStream(); book.SaveAs(stream); return stream.ToArray();
    }

    private static void AdicionarAba(XLWorkbook book, string nome, int mes, int quantidade = 1)
    {
        var ws = book.AddWorksheet(nome);
        ws.Cell("A1").Value = "CONTAS"; ws.Cell("G1").Value = "TRABALHOS";
        for (int r = 2; r <= quantidade + 1; r++)
        {
            ws.Cell(r, 1).Value = new DateTime(2026, mes, 20);
            ws.Cell(r, 2).Value = "Despesa sintética " + r;
            ws.Cell(r, 3).Value = 100m;
        }
    }

    private static List<LinhaImportacaoDto> Linhas(IReadOnlyList<PreviaImportacaoDto> previas) => previas.SelectMany(x => x.Linhas).ToList();

    [Fact]
    public async Task ImportaSomenteAbasEscolhidasSemAlterarArquivoOuCriarBaixas()
    {
        var bytes = Planilha(); var original = bytes.ToArray();
        var previas = await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("OUTUBRO 2026", null), new("DEZEMBRO 2026", null)]);
        Assert.Equal(2, previas.Count);
        Assert.Empty(await db.Lancamentos.ToListAsync());
        Assert.Equal(original, bytes);
        Assert.Equal(2, (await servico.ConfirmarAsync(Linhas(previas))).Importadas);
        var registros = await db.Lancamentos.AsNoTracking().ToListAsync();
        Assert.Contains(registros, x => x.Competencia == new DateOnly(2026, 10, 1));
        Assert.Contains(registros, x => x.Competencia == new DateOnly(2026, 12, 1));
        Assert.DoesNotContain(registros, x => x.Competencia.Month == 11);
        Assert.Empty(await db.Baixas.ToListAsync());
        Assert.Equal(2, await db.RegistrosImportados.CountAsync());
    }

    [Fact]
    public async Task ExigeAnoIndividualEmCadaAbaAntiga()
    {
        var bytes = Planilha(book => {
            AdicionarAba(book, "JANEIRO", 1); book.Worksheet("JANEIRO").Cell("A2").Clear();
            AdicionarAba(book, "FEVEREIRO", 2); book.Worksheet("FEVEREIRO").Cell("A2").Clear();
        });
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("JANEIRO", 2024), new("FEVEREIRO", null)]));
        var previas = await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("JANEIRO", 2024), new("FEVEREIRO", 2025)]);
        Assert.Equal(new DateOnly(2024, 1, 1), previas[0].Competencia);
        Assert.Equal(new DateOnly(2025, 2, 1), previas[1].Competencia);
        Assert.Empty(await db.Lancamentos.ToListAsync());
    }

    [Theory]
    [InlineData("VALOR TOTAL DE DÍVIDAS")]
    [InlineData("ABA INEXISTENTE")]
    public async Task AbaInvalidaImpedePreviaParcial(string aba)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviasAsync(Planilha(), "sintetica.xlsx", [new("OUTUBRO 2026", null), new(aba, null)]));
        Assert.Empty(await db.Lancamentos.ToListAsync());
    }

    [Fact]
    public async Task RejeitaSelecaoVaziaOuAbaRepetida()
    {
        var bytes = Planilha();
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", []));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("OUTUBRO 2026", null), new("outubro 2026", null)]));
    }

    [Fact]
    public async Task ReimportacaoDeVariasAbasNaoDuplicaRegistros()
    {
        var bytes = Planilha();
        SelecaoAbaImportacaoRequest[] abas = [new("OUTUBRO 2026", null), new("NOVEMBRO 2026", null)];
        await servico.ConfirmarAsync(Linhas(await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", abas)));
        var repetidas = Linhas(await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", abas));
        Assert.All(repetidas, x => { Assert.True(x.Duplicada); x.Selecionada = true; });
        Assert.Equal(2, (await servico.ConfirmarAsync(repetidas)).Duplicadas);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task SobreposicaoEntreAbasDoMesmoMesERevalidadaNaConfirmacao()
    {
        var bytes = Planilha(book => AdicionarAba(book, "OUTUBRO 2026.", 10));
        var previas = await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("OUTUBRO 2026", null), new("OUTUBRO 2026.", null)]);
        Assert.False(previas[0].Linhas[0].Duplicada);
        Assert.True(previas[1].Linhas[0].Duplicada);
        previas[1].Linhas[0].Selecionada = true;
        var resultado = await servico.ConfirmarAsync(Linhas(previas));
        Assert.Equal(1, resultado.Importadas);
        Assert.Equal(1, resultado.Duplicadas);
    }

    [Fact]
    public async Task LinhasIguaisNaMesmaAbaSaoPreservadasEMesesDiferentesNaoColidem()
    {
        var bytes = Planilha(book => {
            var ws = book.Worksheet("OUTUBRO 2026");
            ws.Cell("A3").Value = new DateTime(2026, 10, 20);
            ws.Cell("B3").Value = ws.Cell("B2").Value;
            ws.Cell("C3").Value = 100m;
        });
        var linhas = Linhas(await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("OUTUBRO 2026", null), new("NOVEMBRO 2026", null)]));
        Assert.All(linhas, x => Assert.False(x.Duplicada));
        Assert.Equal(3, (await servico.ConfirmarAsync(linhas)).Importadas);
    }

    [Fact]
    public async Task DatasEditadasNaPreviaNaoContornamDuplicidadeEntreAbas()
    {
        var bytes = Planilha(book => { AdicionarAba(book, "OUTUBRO 2026.", 10); book.Worksheet("OUTUBRO 2026.").Cell("A2").Value = new DateTime(2026, 10, 21); });
        var previas = await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("OUTUBRO 2026", null), new("OUTUBRO 2026.", null)]);
        Assert.All(Linhas(previas), x => Assert.False(x.Duplicada));
        previas[1].Linhas[0].Data = previas[0].Linhas[0].Data;
        var resultado = await servico.ConfirmarAsync(Linhas(previas));
        Assert.Equal(1, resultado.Importadas);
        Assert.Equal(1, resultado.Duplicadas);
    }

    [Fact]
    public async Task LimiteDeConfirmacaoConsideraTodasAsAbas()
    {
        var bytes = Planilha(book => {
            book.Worksheet("OUTUBRO 2026").Delete(); book.Worksheet("NOVEMBRO 2026").Delete();
            AdicionarAba(book, "OUTUBRO 2026", 10, 1001); AdicionarAba(book, "NOVEMBRO 2026", 11, 1001);
        });
        var linhas = Linhas(await servico.PrepararPreviasAsync(bytes, "sintetica.xlsx", [new("OUTUBRO 2026", null), new("NOVEMBRO 2026", null)]));
        Assert.Equal(2002, linhas.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => servico.ConfirmarAsync(linhas));
        Assert.Empty(await db.Lancamentos.ToListAsync());
        linhas[0].Selecionada = linhas[1].Selecionada = false;
        Assert.Equal(2000, (await servico.ConfirmarAsync(linhas)).Importadas);
    }

    [Fact]
    public async Task FalhaNaSegundaAbaReverteLoteESemEstadoPendenteParaProximaTentativa()
    {
        var linhas = Linhas(await servico.PrepararPreviasAsync(Planilha(), "sintetica.xlsx", [new("OUTUBRO 2026", null), new("NOVEMBRO 2026", null)]));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FalhaLote BEFORE INSERT ON Lancamentos WHEN NEW.Competencia = '2026-11-01' BEGIN SELECT RAISE(ABORT, 'Falha sintética'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => servico.ConfirmarAsync(linhas));
        Assert.Equal(0, await db.Lancamentos.CountAsync());
        Assert.Equal(0, await db.RegistrosImportados.CountAsync());
        Assert.Empty(db.ChangeTracker.Entries());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FalhaLote;");
        Assert.Equal(2, (await servico.ConfirmarAsync(linhas)).Importadas);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }
}
