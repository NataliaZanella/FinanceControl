using FinanceControl.Application.Models;
using FinanceControl.Domain.Entities;
using FinanceControl.Domain.Enums;
using FinanceControl.Infrastructure.Data;
using FinanceControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceControl.Tests;

public sealed class ValoresReceberTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly FinanceDbContext db;
    private readonly LancamentoService lancamentos;
    private readonly ValoresReceberService servico;
    private static readonly DateOnly Mes = new(2026, 10, 1);
    private static readonly DateOnly Data = new(2026, 10, 20);
    private static CriarItemReceberRequest Item(decimal valor = 100m) => new("Item fictício", valor, Mes, null, null);
    private static CriarGrupoReceberRequest Grupo(params CriarItemReceberRequest[] itens) => new("Pessoa fictícia", "Grupo sintético", itens);

    public ValoresReceberTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        lancamentos = new(db);
        servico = new(db, lancamentos);
    }

    [Fact]
    public async Task CriaItensSemRecebimentosETotalizaSemDuplicarPainel()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(100m), Item(200m)));
        Assert.Equal(300m, grupo.TotalDevido);
        Assert.Equal(0m, grupo.TotalRecebido);
        Assert.Equal(300m, grupo.SaldoAReceber);
        Assert.Equal(2, grupo.Itens.Count);
        Assert.All(grupo.Itens, x => { Assert.Equal(TipoLancamento.Receita, x.Tipo); Assert.Equal("Pendente", x.Situacao); });
        Assert.Empty(await db.Baixas.ToListAsync());
        Assert.Equal(2, await db.Lancamentos.CountAsync());
        var resumo = await lancamentos.ResumoAsync(Mes);
        Assert.Equal(300m, resumo.ReceitasPrevistas);
        Assert.Equal(0m, resumo.ReceitasRecebidas);
    }

    [Fact]
    public async Task RecebimentosParciaisReconciliamGrupoPainelEHistorico()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        var id = grupo.Itens[0].Id;
        grupo = await servico.RegistrarRecebimentoAsync(grupo.Id, id, new(30m, Data, "Primeira parte"));
        Assert.Equal(30m, grupo.TotalRecebido);
        Assert.Equal(70m, grupo.SaldoAReceber);
        Assert.Equal("Parcial", grupo.Itens[0].Situacao);
        // A mesma baixa pode ser registrada pelo painel e será refletida no grupo.
        await lancamentos.RegistrarBaixaAsync(id, new(70m, Data, "Restante"));
        grupo = await servico.ObterAsync(grupo.Id);
        Assert.Equal(0m, grupo.SaldoAReceber);
        Assert.Equal("Quitado", grupo.Itens[0].Situacao);
        Assert.Equal(2, (await lancamentos.HistoricoAsync(id)).Count);
        Assert.Equal(100m, (await lancamentos.ResumoAsync(Mes)).ReceitasRecebidas);
    }

    [Fact]
    public async Task BloqueiaRecebimentoAcimaDoSaldo()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.RegistrarRecebimentoAsync(grupo.Id, grupo.Itens[0].Id, new(101m, Data, null)));
        Assert.Equal(0m, (await servico.ObterAsync(grupo.Id)).TotalRecebido);
        Assert.Empty(await db.Baixas.ToListAsync());
    }

    [Fact]
    public async Task NaoPermiteReceberItemDeOutroGrupo()
    {
        var um = await servico.CriarAsync(Grupo(Item()));
        var dois = await servico.CriarAsync(Grupo(Item()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.RegistrarRecebimentoAsync(um.Id, dois.Itens[0].Id, new(10m, Data, null)));
        Assert.Empty(await db.Baixas.ToListAsync());
    }

    [Fact]
    public async Task AdicionaItemSemDuplicarOsAnteriores()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        grupo = await servico.AdicionarItemAsync(grupo.Id, Item(50m));
        Assert.Equal(150m, grupo.TotalDevido);
        Assert.Equal(2, grupo.Itens.Count);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task ImpedeReclassificarItemComoDespesaEExcluiSomenteItemSemBaixas()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item()));
        var item = grupo.Itens[0];
        await Assert.ThrowsAsync<ArgumentException>(() => lancamentos.AtualizarAsync(item.Id,
            new(item.Descricao, item.Categoria, TipoLancamento.Despesa, item.ValorPrevisto, Mes, null, null)));
        await servico.RegistrarRecebimentoAsync(grupo.Id, item.Id, new(10m, Data, null));
        await Assert.ThrowsAsync<ArgumentException>(() => lancamentos.ExcluirAsync(item.Id));
        Assert.True(await lancamentos.ExcluirAsync(grupo.Itens[1].Id));
        grupo = await servico.ObterAsync(grupo.Id);
        Assert.Single(grupo.Itens);
        Assert.Equal(90m, grupo.SaldoAReceber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    public async Task ItemInvalidoReverteGrupoInteiro(decimal invalido)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(Grupo(Item(), Item(invalido))));
        Assert.Equal(0, await db.GruposReceber.CountAsync());
        Assert.Equal(0, await db.ItensGrupoReceber.CountAsync());
        Assert.Equal(0, await db.Lancamentos.CountAsync());
        var grupo = await servico.CriarAsync(Grupo(Item()));
        Assert.Single(grupo.Itens);
        Assert.Equal(1, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task FalhaDePersistenciaReverteGrupoEItens()
    {
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FalhaGrupo BEFORE INSERT ON ItensGrupoReceber BEGIN SELECT RAISE(ABORT, 'Falha sintética'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => servico.CriarAsync(Grupo(Item())));
        Assert.Equal(0, await db.GruposReceber.CountAsync());
        Assert.Equal(0, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task ValidacaoDeGrupoEItemInexistenteNaoGrava()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(new(" ", "Teste", [Item()])));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(new("Pessoa fictícia", "Teste", [])));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.CriarAsync(Grupo(Item() with { Competencia = default })));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.AdicionarItemAsync(99, Item()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.ObterAsync(99));
        Assert.Empty(await servico.ListarAsync());
    }

    [Fact]
    public async Task AtualizacaoDeBancoAntigoPreservaLancamentosBaixasEOrigens()
    {
        // Simula o esquema anterior com dados integralmente fictícios.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE ItensGrupoReceber; DROP TABLE GruposReceber;");
        var item = await lancamentos.CriarAsync(new("Registro anterior sintético", "Outros", TipoLancamento.Receita, 100m, Mes, null, null));
        await lancamentos.RegistrarBaixaAsync(item.Id, new(40m, Data, "Anterior"));
        db.RegistrosImportados.Add(new RegistroImportacao { LancamentoId = item.Id, ChaveOrigem = new string('C', 64), Aba = "OUTUBRO 2026", LinhaExcel = 2 });
        await db.SaveChangesAsync();
        await AtualizacaoValoresReceber.AplicarAsync(db);
        await AtualizacaoValoresReceber.AplicarAsync(db);
        Assert.Equal(40m, Assert.Single(await lancamentos.ListarAsync(Mes)).ValorRealizado);
        Assert.Single(await db.RegistrosImportados.ToListAsync());
        Assert.Single(await db.Baixas.ToListAsync());
        Assert.Empty(await servico.ListarAsync());
        await servico.CriarAsync(Grupo(Item()));
        Assert.Equal(2, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task GrupoSomaCompetenciasSemDuplicarResumoMensal()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item(50m) with { Competencia = Mes.AddMonths(1) }));
        Assert.Equal(150m, grupo.TotalDevido);
        Assert.Equal(100m, (await lancamentos.ResumoAsync(Mes)).ReceitasPrevistas);
        Assert.Equal(50m, (await lancamentos.ResumoAsync(Mes.AddMonths(1))).ReceitasPrevistas);
    }

    [Fact]
    public async Task AtualizacaoEmArquivoGeraBackupConsistenteUmaVez()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "FinanceControl-BackupSintetico-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            var arquivo = Path.Combine(pasta, "sintetico.db");
            using var arquivoConexao = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = arquivo, Pooling = false }.ToString());
            arquivoConexao.Open();
            using var arquivoDb = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(arquivoConexao).Options);
            arquivoDb.Database.EnsureCreated();
            var anterior = new LancamentoService(arquivoDb);
            var item = await anterior.CriarAsync(new("Registro fictício para backup", "Outros", TipoLancamento.Receita, 100m, Mes, null, null));
            await anterior.RegistrarBaixaAsync(item.Id, new(40m, Data, null));
            await arquivoDb.Database.ExecuteSqlRawAsync("DROP TABLE ItensGrupoReceber; DROP TABLE GruposReceber;");
            await AtualizacaoValoresReceber.AplicarAsync(arquivoDb);
            await AtualizacaoValoresReceber.AplicarAsync(arquivoDb);
            var backup = Assert.Single(Directory.GetFiles(pasta, "*.antes-valores-receber-*.db"));
            using var backupConexao = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            backupConexao.Open();
            using var backupDb = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(backupConexao).Options);
            Assert.Equal(40m, Assert.Single(await new LancamentoService(backupDb).ListarAsync(Mes)).ValorRealizado);
            using var consulta = backupConexao.CreateCommand();
            consulta.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'GruposReceber'";
            Assert.Equal(0L, consulta.ExecuteScalar());
        }
        finally
        {
            // Somente arquivos sintéticos desta pasta única; sem exclusão recursiva.
            foreach (var arquivo in Directory.GetFiles(pasta)) File.Delete(arquivo);
            Directory.Delete(pasta);
        }
    }

    [Fact]
    public async Task EditaPessoaETituloSemAlterarItensOuRecebimentos()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item(200m)));
        await servico.RegistrarRecebimentoAsync(grupo.Id, grupo.Itens[0].Id, new(40m, Data, "Preservar"));
        var historico = await lancamentos.HistoricoAsync(grupo.Itens[0].Id);
        var editado = await servico.AtualizarGrupoAsync(grupo.Id, new("  Pessoa corrigida  ", "  Título corrigido  "));
        Assert.Equal("Pessoa corrigida", editado.Pessoa);
        Assert.Equal("Título corrigido", editado.Descricao);
        Assert.Equal(grupo.Itens.Select(x => x.Id), editado.Itens.Select(x => x.Id));
        Assert.Equal(300m, editado.TotalDevido);
        Assert.Equal(40m, editado.TotalRecebido);
        Assert.Equal(historico.ToArray(), (await lancamentos.HistoricoAsync(grupo.Itens[0].Id)).ToArray());
    }

    [Fact]
    public async Task EditaItemComRecebimentosPreservandoSaldoEHistorico()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        var id = grupo.Itens[0].Id;
        await servico.RegistrarRecebimentoAsync(grupo.Id, id, new(40m, Data, "Parcial"));
        grupo = await servico.AtualizarItemAsync(grupo.Id, id, new("Item corrigido", 80m, Mes, Data, "Observação corrigida"));
        var item = Assert.Single(grupo.Itens);
        Assert.Equal("Item corrigido", item.Descricao);
        Assert.Equal("Observação corrigida", item.Observacoes);
        Assert.Equal(Data, item.DataVencimento);
        Assert.Equal("Valores a receber", item.Categoria);
        Assert.Equal(TipoLancamento.Receita, item.Tipo);
        Assert.Equal(40m, grupo.SaldoAReceber);
        Assert.Single(await lancamentos.HistoricoAsync(id));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarItemAsync(grupo.Id, id, new("Inválido", 39m, Mes, null, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarItemAsync(grupo.Id, id, new("Inválido", 80m, Mes.AddMonths(1), null, null)));
        Assert.Equal(80m, (await servico.ObterAsync(grupo.Id)).TotalDevido);
        Assert.Equal(40m, (await lancamentos.ResumoAsync(Mes)).ReceitasRecebidas);
    }

    [Fact]
    public async Task EditarItemSemBaixasPodeMudarCompetencia()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        grupo = await servico.AtualizarItemAsync(grupo.Id, grupo.Itens[0].Id, new("Movido", 150m, Mes.AddMonths(1), null, null));
        Assert.Equal(150m, grupo.TotalDevido);
        Assert.Equal(0m, (await lancamentos.ResumoAsync(Mes)).ReceitasPrevistas);
        Assert.Equal(150m, (await lancamentos.ResumoAsync(Mes.AddMonths(1))).ReceitasPrevistas);
    }

    [Fact]
    public async Task EdicaoEExclusaoNaoAcessamItemDeOutroGrupo()
    {
        var um = await servico.CriarAsync(Grupo(Item()));
        var dois = await servico.CriarAsync(Grupo(Item()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.AtualizarItemAsync(um.Id, dois.Itens[0].Id, new("Inválido", 10m, Mes, null, null)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.ExcluirItemAsync(um.Id, dois.Itens[0].Id));
        Assert.Equal(100m, (await servico.ObterAsync(dois.Id)).TotalDevido);
    }

    [Fact]
    public async Task ExcluirItemRemoveLancamentoEVinculoMasPreservaGrupo()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item(200m)));
        Assert.True(await servico.ExcluirItemAsync(grupo.Id, grupo.Itens[0].Id));
        grupo = await servico.ObterAsync(grupo.Id);
        Assert.Single(grupo.Itens);
        Assert.Equal(200m, grupo.TotalDevido);
        Assert.Equal(200m, (await lancamentos.ResumoAsync(Mes)).ReceitasPrevistas);
        Assert.Single(await db.ItensGrupoReceber.ToListAsync());
    }

    [Fact]
    public async Task RecebimentoBloqueiaExclusaoDoItemEDoGrupoInteiro()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item(200m)));
        await servico.RegistrarRecebimentoAsync(grupo.Id, grupo.Itens[0].Id, new(1m, Data, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.ExcluirItemAsync(grupo.Id, grupo.Itens[0].Id));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.ExcluirGrupoAsync(grupo.Id));
        Assert.Equal(2, (await servico.ObterAsync(grupo.Id)).Itens.Count);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
        Assert.Single(await db.Baixas.ToListAsync());
    }

    [Fact]
    public async Task ExcluirGrupoRemoveSeusItensSemAfetarOutrosRegistros()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item(200m)));
        var outro = await servico.CriarAsync(Grupo(Item(50m)));
        var manual = await lancamentos.CriarAsync(new("Receita fictícia avulsa", "Outros", TipoLancamento.Receita, 10m, Mes, null, null));
        Assert.True(await servico.ExcluirGrupoAsync(grupo.Id));
        Assert.False(await servico.ExcluirGrupoAsync(grupo.Id));
        Assert.Equal(outro.Id, Assert.Single(await servico.ListarAsync()).Id);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
        Assert.Contains(await lancamentos.ListarAsync(Mes), x => x.Id == manual.Id);
        Assert.Equal(60m, (await lancamentos.ResumoAsync(Mes)).ReceitasPrevistas);
    }

    [Fact]
    public async Task FalhaAoExcluirGrupoReverteExclusaoDosLancamentos()
    {
        var grupo = await servico.CriarAsync(Grupo(Item(), Item()));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FalhaExclusaoGrupo BEFORE DELETE ON GruposReceber BEGIN SELECT RAISE(ABORT, 'Falha sintética'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => servico.ExcluirGrupoAsync(grupo.Id));
        Assert.Equal(2, await db.Lancamentos.CountAsync());
        Assert.Equal(2, await db.ItensGrupoReceber.CountAsync());
        Assert.Equal(200m, (await servico.ObterAsync(grupo.Id)).TotalDevido);
    }

    [Fact]
    public async Task EditarGrupoInvalidoOuInexistenteNaoAlteraDados()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarGrupoAsync(grupo.Id, new(" ", "Título")));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.AtualizarGrupoAsync(grupo.Id, new("Pessoa", new string('x', 181))));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.AtualizarGrupoAsync(grupo.Id + 1, new("Pessoa", "Título")));
        var salvo = await servico.ObterAsync(grupo.Id);
        Assert.Equal(grupo.Pessoa, salvo.Pessoa);
        Assert.Equal(grupo.Descricao, salvo.Descricao);
    }

    [Fact]
    public async Task PermiteExcluirGrupoAposRemoverSeuUltimoItem()
    {
        var grupo = await servico.CriarAsync(Grupo(Item()));
        Assert.True(await servico.ExcluirItemAsync(grupo.Id, grupo.Itens[0].Id));
        Assert.Empty((await servico.ObterAsync(grupo.Id)).Itens);
        Assert.True(await servico.ExcluirGrupoAsync(grupo.Id));
        Assert.Empty(await servico.ListarAsync());
        Assert.Equal(0, await db.Lancamentos.CountAsync());
    }

    private Task<LancamentoDto> OrigemAsync() => lancamentos.CriarAsync(new("Descrição consolidada fictícia", "Categoria original",
        TipoLancamento.Receita, 300m, Mes, Data, "Observação anterior"));

    private static DesmembrarReceberRequest Desmembramento(int id) => new(id, "Pessoa fictícia", "Grupo revisado", [Item(), Item(200m)]);

    [Fact]
    public async Task DesmembramentoPreservaTotalMensalCategoriaIdentidadeEOrigem()
    {
        var origem = await OrigemAsync();
        var criacao = (await db.Lancamentos.AsNoTracking().SingleAsync()).CriadoEmUtc;
        db.RegistrosImportados.Add(new RegistroImportacao { LancamentoId = origem.Id, ChaveOrigem = new string('D', 64), Aba = "OUTUBRO 2026", LinhaExcel = 5 });
        await db.SaveChangesAsync();
        var resumo = await lancamentos.ResumoAsync(Mes);
        var grupo = await servico.DesmembrarAsync(Desmembramento(origem.Id));
        Assert.Equal(2, grupo.Itens.Count);
        Assert.Equal(origem.Id, grupo.Itens[0].Id);
        Assert.Equal(300m, grupo.TotalDevido);
        Assert.Equal(0m, grupo.TotalRecebido);
        Assert.Equal(resumo, await lancamentos.ResumoAsync(Mes));
        Assert.All(grupo.Itens, x => Assert.Equal(origem.Categoria, x.Categoria));
        Assert.Contains(origem.Descricao, grupo.Itens[0].Observacoes!);
        Assert.Contains(origem.Observacoes!, grupo.Itens[0].Observacoes!);
        Assert.Equal(origem.DataVencimento, grupo.Itens[0].DataVencimento);
        Assert.Equal(criacao, (await db.Lancamentos.AsNoTracking().SingleAsync(x => x.Id == origem.Id)).CriadoEmUtc);
        Assert.Equal(origem.Id, (await db.RegistrosImportados.AsNoTracking().SingleAsync()).LancamentoId);
        Assert.Equal(2, await db.Lancamentos.CountAsync());
        Assert.Empty(await db.Baixas.ToListAsync());
    }

    [Fact]
    public async Task DesmembramentoComSomaDiferenteNaoAlteraOrigem()
    {
        var origem = await OrigemAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id) with { Itens = [Item(), Item(199m)] }));
        Assert.Equal(origem, Assert.Single(await lancamentos.ListarAsync(Mes)));
        Assert.Empty(await servico.ListarAsync());
    }

    [Fact]
    public async Task DesmembramentoNaoReclassificaDespesasNemDistribuiRecebimentos()
    {
        var despesa = await lancamentos.CriarAsync(new("Despesa fictícia", "Outros", TipoLancamento.Despesa, 300m, Mes, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(despesa.Id)));
        var origem = await OrigemAsync();
        await lancamentos.RegistrarBaixaAsync(origem.Id, new(1m, Data, null));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id)));
        Assert.Empty(await servico.ListarAsync());
        Assert.Single(await lancamentos.HistoricoAsync(origem.Id));
    }

    [Fact]
    public async Task NaoPermiteDesmembrarNovamenteNemMudarCompetencia()
    {
        var origem = await OrigemAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id) with { Itens = [Item(), Item(200m) with { Competencia = Mes.AddMonths(1) }] }));
        await servico.DesmembrarAsync(Desmembramento(origem.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id)));
        Assert.Single(await servico.ListarAsync());
        Assert.Equal(2, await db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task FalhaAposAtualizarOrigemReverteTodoDesmembramento()
    {
        var origem = await OrigemAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FalhaDesmembramento BEFORE INSERT ON ItensGrupoReceber BEGIN SELECT RAISE(ABORT, 'Falha sintética'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id)));
        Assert.Equal(origem, Assert.Single(await lancamentos.ListarAsync(Mes)));
        Assert.Empty(await servico.ListarAsync());
        Assert.Equal(0, await db.ItensGrupoReceber.CountAsync());
    }

    [Fact]
    public async Task ItemInvalidoNoDesmembramentoRevertePrimeiraAtualizacao()
    {
        var origem = await OrigemAsync();
        // A soma bate, mas o segundo item é negativo: falha após a primeira atualização.
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id) with { Itens = [Item(400m), Item(-100m)] }));
        Assert.Equal(origem, Assert.Single(await lancamentos.ListarAsync(Mes)));
        Assert.Empty(await servico.ListarAsync());
        Assert.Equal(300m, (await servico.DesmembrarAsync(Desmembramento(origem.Id))).TotalDevido);
    }

    [Fact]
    public async Task ListaSomenteOrigensElegiveisDoMes()
    {
        var livre = await OrigemAsync();
        var recebido = await OrigemAsync();
        await lancamentos.RegistrarBaixaAsync(recebido.Id, new(1m, Data, null));
        await servico.CriarAsync(Grupo(Item()));
        await lancamentos.CriarAsync(new("Outro mês fictício", "Outros", TipoLancamento.Receita, 300m, Mes.AddMonths(1), null, null));
        await lancamentos.CriarAsync(new("Despesa fictícia", "Outros", TipoLancamento.Despesa, 300m, Mes, null, null));
        Assert.Equal(livre.Id, Assert.Single(await servico.ListarOrigensAsync(Mes)).Id);
    }

    [Fact]
    public async Task DesmembramentoRejeitaOrigemInexistenteOuQuantidadeInvalida()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.DesmembrarAsync(Desmembramento(999)));
        var origem = await OrigemAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => servico.DesmembrarAsync(Desmembramento(origem.Id) with { Itens = [Item(300m)] }));
        Assert.Equal(origem, Assert.Single(await lancamentos.ListarAsync(Mes)));
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }
}
