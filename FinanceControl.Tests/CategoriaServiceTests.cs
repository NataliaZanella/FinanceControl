using FinanceControl.Domain.Entities;
using FinanceControl.Domain.Enums;
using FinanceControl.Infrastructure.Data;
using FinanceControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceControl.Tests;

public sealed class CategoriaServiceTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly FinanceDbContext db;
    private readonly CategoriaService servico;
    private readonly LancamentoService lancamentos;
    private static readonly DateOnly Mes = new(2026, 10, 1);

    public CategoriaServiceTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        servico = new(db); lancamentos = new(db);
    }

    [Fact]
    public async Task CadastroLimpaEspacosSemCriarLancamentos()
    {
        var item = await servico.CriarAsync(new("  Categoria   fictícia  "));
        Assert.Equal("Categoria fictícia", item.Nome);
        Assert.True(item.Cadastrada);
        Assert.Equal(0, item.QuantidadeLancamentos);
        Assert.Empty(await db.Lancamentos.ToListAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(new("CATEGORIA FICTICIA")));
    }

    [Theory]
    [InlineData("educacao")]
    [InlineData(" EDUCAÇÃO ")]
    [InlineData("Educação")]
    public async Task NaoDuplicaCategoriasPorAcentosOuMaiusculas(string nome)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(new(nome)));
        Assert.Equal(Categoria.Padroes.Count, (await servico.ListarAsync()).Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(81)]
    public async Task BloqueiaNomeVazioOuLongo(int tamanho)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(new(new string('x', tamanho))));
        Assert.Equal(Categoria.Padroes.Count, (await servico.ListarAsync()).Count);
    }

    [Fact]
    public async Task RenomearAtualizaTodosOsUsosPreservandoFinanceiroEOrigem()
    {
        var categoria = (await servico.ListarAsync()).Single(x => x.Nome == "Educação");
        var um = await lancamentos.CriarAsync(new("Registro fictício A", "educacao", TipoLancamento.Despesa, 100m, Mes, Mes, null));
        var dois = await lancamentos.CriarAsync(new("Registro fictício B", " EDUCAÇÃO ", TipoLancamento.Despesa, 200m, Mes.AddMonths(1), null, null));
        await lancamentos.RegistrarBaixaAsync(um.Id, new(40m, Mes, "Baixa fictícia"));
        db.RegistrosImportados.Add(new RegistroImportacao { LancamentoId = um.Id, ChaveOrigem = new string('F', 64), Aba = "OUTUBRO 2026", LinhaExcel = 2 });
        await db.SaveChangesAsync();
        var resumoAntes = await lancamentos.ResumoAsync(Mes);
        var outroMesAntes = await lancamentos.ResumoAsync(Mes.AddMonths(1));
        var historicoAntes = await lancamentos.HistoricoAsync(um.Id);
        var renomeada = await servico.RenomearAsync(categoria.Id, new("Estudos fictícios"));
        Assert.Equal(2, renomeada.QuantidadeLancamentos);
        Assert.Equal(resumoAntes, await lancamentos.ResumoAsync(Mes));
        Assert.Equal(outroMesAntes, await lancamentos.ResumoAsync(Mes.AddMonths(1)));
        Assert.Equal(historicoAntes.ToArray(), (await lancamentos.HistoricoAsync(um.Id)).ToArray());
        Assert.All(await db.Lancamentos.AsNoTracking().ToListAsync(), x => Assert.Equal("Estudos fictícios", x.Categoria));
        Assert.Equal(um.Id, (await db.RegistrosImportados.AsNoTracking().SingleAsync()).LancamentoId);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
        Assert.Equal(dois.Id, Assert.Single(await lancamentos.ListarAsync(Mes.AddMonths(1))).Id);
    }

    [Fact]
    public async Task ExcluirCategoriaUsadaEmQualquerMesFicaBloqueado()
    {
        var categoria = (await servico.ListarAsync()).Single(x => x.Nome == "Educação");
        await lancamentos.CriarAsync(new("Registro fictício", "EDUCACAO", TipoLancamento.Receita, 10m, Mes.AddYears(-1), null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.ExcluirAsync(categoria.Id));
        Assert.Equal(1, (await servico.ListarAsync()).Single(x => x.Id == categoria.Id).QuantidadeLancamentos);
        Assert.Single(await db.Lancamentos.ToListAsync());
    }

    [Fact]
    public async Task ExcluirCategoriaSemUsoNaoAfetaOutrosCadastros()
    {
        var categoria = await servico.CriarAsync(new("Categoria descartável fictícia"));
        Assert.True(await servico.ExcluirAsync(categoria.Id));
        Assert.False(await servico.ExcluirAsync(categoria.Id));
        Assert.Equal(Categoria.Padroes.Count, (await servico.ListarAsync()).Count);
    }

    [Fact]
    public async Task CategoriasLegadasSemCadastroContinuamVisiveis()
    {
        var item = await lancamentos.CriarAsync(new("Registro fictício", "Nome antigo fictício", TipoLancamento.Receita, 10m, Mes, null, null));
        var antiga = (await servico.ListarAsync()).Single(x => x.Nome == item.Categoria);
        Assert.False(antiga.Cadastrada);
        Assert.Equal(1, antiga.QuantidadeLancamentos);
        var cadastrada = await servico.CriarAsync(new(antiga.Nome));
        Assert.True(cadastrada.Cadastrada);
        Assert.Equal(1, cadastrada.QuantidadeLancamentos);
        Assert.Equal(item, Assert.Single(await lancamentos.ListarAsync(Mes)));
    }

    [Fact]
    public async Task RenomearParaNomeExistenteOuInexistenteNaoAlteraLancamentos()
    {
        var categoria = (await servico.ListarAsync()).Single(x => x.Nome == "Educação");
        var item = await lancamentos.CriarAsync(new("Registro fictício", categoria.Nome, TipoLancamento.Receita, 10m, Mes, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.RenomearAsync(categoria.Id, new("moradia")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.RenomearAsync(9999, new("Novo nome fictício")));
        Assert.Equal(item, Assert.Single(await lancamentos.ListarAsync(Mes)));
    }

    [Fact]
    public async Task FalhaAoRenomearReverteNomesDosLancamentos()
    {
        var categoria = (await servico.ListarAsync()).Single(x => x.Nome == "Educação");
        var item = await lancamentos.CriarAsync(new("Registro fictício", categoria.Nome, TipoLancamento.Receita, 10m, Mes, null, null));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FalhaCategoria BEFORE UPDATE ON Categorias BEGIN SELECT RAISE(ABORT, 'Falha sintética'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => servico.RenomearAsync(categoria.Id, new("Nome fictício corrigido")));
        Assert.Equal(item, Assert.Single(await lancamentos.ListarAsync(Mes)));
        Assert.Equal(categoria.Nome, (await servico.ListarAsync()).Single(x => x.Id == categoria.Id).Nome);
    }

    [Fact]
    public async Task AtualizacaoAntigaCadastraNomesSemReescreverLancamentos()
    {
        var item = await lancamentos.CriarAsync(new("Registro fictício", "Categoria legada fictícia", TipoLancamento.Receita, 10m, Mes, null, null));
        await db.Database.ExecuteSqlRawAsync("DROP TABLE Categorias;");
        await AtualizacaoCategorias.AplicarAsync(db);
        await AtualizacaoCategorias.AplicarAsync(db);
        var categorias = await servico.ListarAsync();
        Assert.Equal(Categoria.Padroes.Count + 1, categorias.Count);
        Assert.True(categorias.Single(x => x.Nome == item.Categoria).Cadastrada);
        Assert.Equal(item, Assert.Single(await lancamentos.ListarAsync(Mes)));
    }

    [Fact]
    public async Task AtualizacaoEmArquivoGeraBackupAnteriorUmaUnicaVez()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "FinanceControl-CategoriasSinteticas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            var arquivo = Path.Combine(pasta, "sintetico.db");
            using var arquivoConexao = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = arquivo, Pooling = false }.ToString());
            arquivoConexao.Open();
            using var arquivoDb = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(arquivoConexao).Options);
            arquivoDb.Database.EnsureCreated();
            var registros = new LancamentoService(arquivoDb);
            var item = await registros.CriarAsync(new("Registro fictício de backup", "Categoria antiga fictícia", TipoLancamento.Receita, 100m, Mes, null, null));
            await registros.RegistrarBaixaAsync(item.Id, new(40m, Mes, null));
            await arquivoDb.Database.ExecuteSqlRawAsync("DROP TABLE Categorias;");
            await AtualizacaoCategorias.AplicarAsync(arquivoDb);
            await AtualizacaoCategorias.AplicarAsync(arquivoDb);
            var backup = Assert.Single(Directory.GetFiles(pasta, "*.antes-categorias-*.db"));
            using var copia = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            copia.Open();
            using var copiaDb = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(copia).Options);
            Assert.Equal(40m, Assert.Single(await new LancamentoService(copiaDb).ListarAsync(Mes)).ValorRealizado);
            using var consulta = copia.CreateCommand();
            consulta.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'Categorias'";
            Assert.Equal(0L, consulta.ExecuteScalar());
        }
        finally
        {
            foreach (var arquivo in Directory.GetFiles(pasta)) File.Delete(arquivo);
            Directory.Delete(pasta);
        }
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }
}
