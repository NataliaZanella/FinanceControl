using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;
using FinanceControl.Domain.Entities;
using FinanceControl.Domain.Enums;
using FinanceControl.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FinanceControl.Infrastructure.Services;

public sealed class ValoresReceberService(FinanceDbContext db, ILancamentoService lancamentos) : IValoresReceberService
{
    public async Task<IReadOnlyList<GrupoReceberDto>> ListarAsync(CancellationToken ct = default)
    {
        var grupos = await Consulta().OrderBy(x => x.Pessoa).ThenBy(x => x.Id).ToListAsync(ct);
        return grupos.Select(Mapear).ToList();
    }

    public async Task<GrupoReceberDto> ObterAsync(int grupoId, CancellationToken ct = default) =>
        Mapear(await Consulta().SingleOrDefaultAsync(x => x.Id == grupoId, ct)
            ?? throw new KeyNotFoundException("Grupo de valores a receber não encontrado."));

    public async Task<IReadOnlyList<LancamentoDto>> ListarOrigensAsync(DateOnly competencia, CancellationToken ct = default)
    {
        ValidarCompetencia(competencia);
        var mes = new DateOnly(competencia.Year, competencia.Month, 1);
        var origens = await db.Lancamentos.AsNoTracking().Include(x => x.Baixas)
            .Where(x => x.Competencia == mes && x.Tipo == TipoLancamento.Receita && !x.Baixas.Any()
                && !db.ItensGrupoReceber.Any(i => i.LancamentoId == x.Id))
            .OrderBy(x => x.Descricao).ThenBy(x => x.Id).ToListAsync(ct);
        return origens.Select(LancamentoService.Mapear).ToList();
    }

    public async Task<GrupoReceberDto> DesmembrarAsync(DesmembrarReceberRequest request, CancellationToken ct = default)
    {
        var (pessoa, descricao) = ValidarGrupo(request.Pessoa, request.Descricao);
        if (request.Itens is null || request.Itens.Count is < 2 or > 100)
            throw new ArgumentException("Informe de 2 a 100 itens para desmembrar o lançamento.");
        foreach (var item in request.Itens) ValidarItem(item);

        using var transacao = await db.Database.BeginTransactionAsync(ct);
        var origem = await db.Lancamentos.AsNoTracking().Include(x => x.Baixas)
            .SingleOrDefaultAsync(x => x.Id == request.LancamentoId, ct)
            ?? throw new KeyNotFoundException("Lançamento de origem não encontrado.");
        if (origem.Tipo != TipoLancamento.Receita || origem.Baixas.Count > 0)
            throw new ArgumentException("Somente receitas sem recebimentos podem ser desmembradas. Revise o lançamento de origem.");
        if (await db.ItensGrupoReceber.AnyAsync(x => x.LancamentoId == origem.Id, ct))
            throw new ArgumentException("Este lançamento já pertence a um grupo e não pode ser desmembrado novamente.");
        if (request.Itens.Any(x => new DateOnly(x.Competencia.Year, x.Competencia.Month, 1) != origem.Competencia))
            throw new ArgumentException("Todos os itens devem manter a competência original, para preservar os totais do mês.");
        decimal total;
        try { total = request.Itens.Sum(x => x.Valor); }
        catch (OverflowException) { throw new ArgumentException("A soma dos itens excede o limite monetário suportado."); }
        if (total != origem.ValorPrevisto)
            throw new ArgumentException("A soma dos itens deve ser exatamente igual ao valor previsto atual do lançamento de origem. Nenhum valor foi alterado.");

        try
        {
            var grupo = new GrupoReceber { Pessoa = pessoa, Descricao = descricao };
            db.GruposReceber.Add(grupo);
            await db.SaveChangesAsync(ct);
            var primeiro = request.Itens[0];
            var rastreio = $"Desmembrado do lançamento #{origem.Id}: {origem.Descricao}. Total original: {origem.ValorPrevisto.ToString("F2", CultureInfo.GetCultureInfo("pt-BR"))}; competência: {origem.Competencia:MM/yyyy}.";
            var observacoes = string.Join(Environment.NewLine, new[] { origem.Observacoes, rastreio, primeiro.Observacoes }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            // Reutilizar o ID mantém a chave de importação; o registro consolidado não é duplicado.
            await lancamentos.AtualizarAsync(origem.Id, new(primeiro.Descricao, origem.Categoria, TipoLancamento.Receita,
                primeiro.Valor, origem.Competencia, primeiro.Vencimento ?? origem.DataVencimento, observacoes), ct);
            db.ItensGrupoReceber.Add(new ItemGrupoReceber { GrupoReceberId = grupo.Id, LancamentoId = origem.Id });
            foreach (var item in request.Itens.Skip(1)) await AdicionarAoGrupoAsync(grupo.Id, item, ct, origem.Categoria);
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            return await ObterAsync(grupo.Id, ct);
        }
        catch { db.ChangeTracker.Clear(); throw; }
    }

    public async Task<GrupoReceberDto> CriarAsync(CriarGrupoReceberRequest request, CancellationToken ct = default)
    {
        var (pessoa, descricao) = ValidarGrupo(request.Pessoa, request.Descricao);
        if (request.Itens is null || request.Itens.Count is < 1 or > 100)
            throw new ArgumentException("Informe de 1 a 100 itens para revisar neste cadastro.");
        foreach (var item in request.Itens) ValidarItem(item);
        using var transacao = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var grupo = new GrupoReceber { Pessoa = pessoa, Descricao = descricao };
            db.GruposReceber.Add(grupo);
            await db.SaveChangesAsync(ct);
            foreach (var item in request.Itens) await AdicionarAoGrupoAsync(grupo.Id, item, ct);
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            return await ObterAsync(grupo.Id, ct);
        }
        catch
        {
            // Não deixar inserções que falharam pendentes para o próximo clique do circuito Blazor.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<GrupoReceberDto> AdicionarItemAsync(int grupoId, CriarItemReceberRequest request, CancellationToken ct = default)
    {
        ValidarItem(request);
        using var transacao = await db.Database.BeginTransactionAsync(ct);
        if (!await db.GruposReceber.AnyAsync(x => x.Id == grupoId, ct))
            throw new KeyNotFoundException("Grupo de valores a receber não encontrado.");
        try
        {
            await AdicionarAoGrupoAsync(grupoId, request, ct);
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            return await ObterAsync(grupoId, ct);
        }
        catch { db.ChangeTracker.Clear(); throw; }
    }

    public async Task<GrupoReceberDto> RegistrarRecebimentoAsync(int grupoId, int lancamentoId,
        RegistrarBaixaRequest request, CancellationToken ct = default)
    {
        if (!await db.ItensGrupoReceber.AnyAsync(x => x.GrupoReceberId == grupoId && x.LancamentoId == lancamentoId, ct))
            throw new KeyNotFoundException("O item não pertence a este grupo de valores a receber.");
        await lancamentos.RegistrarBaixaAsync(lancamentoId, request, ct);
        return await ObterAsync(grupoId, ct);
    }

    public async Task<GrupoReceberDto> AtualizarGrupoAsync(int grupoId, AtualizarGrupoReceberRequest request, CancellationToken ct = default)
    {
        var (pessoa, descricao) = ValidarGrupo(request.Pessoa, request.Descricao);
        var alterados = await db.GruposReceber.Where(x => x.Id == grupoId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Pessoa, pessoa).SetProperty(x => x.Descricao, descricao), ct);
        if (alterados == 0) throw new KeyNotFoundException("Grupo de valores a receber não encontrado.");
        return await ObterAsync(grupoId, ct);
    }

    public async Task<GrupoReceberDto> AtualizarItemAsync(int grupoId, int lancamentoId,
        AtualizarItemReceberRequest request, CancellationToken ct = default)
    {
        ValidarCompetencia(request.Competencia);
        var item = await db.ItensGrupoReceber.AsNoTracking()
            .Where(x => x.GrupoReceberId == grupoId && x.LancamentoId == lancamentoId)
            .Select(x => x.Lancamento).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("O item não pertence a este grupo de valores a receber.");
        // O serviço de lançamentos preserva baixas e aplica as validações financeiras.
        await lancamentos.AtualizarAsync(lancamentoId, new(request.Descricao, item.Categoria, TipoLancamento.Receita,
            request.Valor, request.Competencia, request.Vencimento, request.Observacoes), ct);
        return await ObterAsync(grupoId, ct);
    }

    public async Task<bool> ExcluirItemAsync(int grupoId, int lancamentoId, CancellationToken ct = default)
    {
        if (!await db.ItensGrupoReceber.AnyAsync(x => x.GrupoReceberId == grupoId && x.LancamentoId == lancamentoId, ct))
            throw new KeyNotFoundException("O item não pertence a este grupo de valores a receber.");
        return await lancamentos.ExcluirAsync(lancamentoId, ct);
    }

    public async Task<bool> ExcluirGrupoAsync(int grupoId, CancellationToken ct = default)
    {
        // Exclusão do grupo e dos lançamentos é atômica. Qualquer recebimento bloqueia o lote inteiro.
        using var transacao = await db.Database.BeginTransactionAsync(ct);
        if (!await db.GruposReceber.AnyAsync(x => x.Id == grupoId, ct)) return false;
        var ids = await db.ItensGrupoReceber.Where(x => x.GrupoReceberId == grupoId)
            .Select(x => x.LancamentoId).ToListAsync(ct);
        if (await db.Baixas.AnyAsync(x => ids.Contains(x.LancamentoId), ct))
            throw new ArgumentException("Não é possível excluir um grupo com recebimentos registrados. Nenhum item foi excluído.");
        await db.Lancamentos.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.GruposReceber.Where(x => x.Id == grupoId).ExecuteDeleteAsync(ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static (string Pessoa, string Descricao) ValidarGrupo(string? pessoa, string? descricao)
    {
        pessoa = pessoa?.Trim() ?? "";
        descricao = descricao?.Trim() ?? "";
        if (pessoa.Length is < 1 or > 120) throw new ArgumentException("Informe a pessoa devedora com até 120 caracteres.");
        if (descricao.Length is < 1 or > 180) throw new ArgumentException("Informe um título com até 180 caracteres.");
        return (pessoa, descricao);
    }

    private async Task AdicionarAoGrupoAsync(int grupoId, CriarItemReceberRequest item, CancellationToken ct,
        string categoria = "Valores a receber")
    {
        var lancamento = await lancamentos.CriarAsync(new(item.Descricao, categoria, TipoLancamento.Receita,
            item.Valor, item.Competencia, item.Vencimento, item.Observacoes), ct);
        db.ItensGrupoReceber.Add(new ItemGrupoReceber { GrupoReceberId = grupoId, LancamentoId = lancamento.Id });
    }

    private static void ValidarItem(CriarItemReceberRequest? item)
    {
        if (item is null) throw new ArgumentException("Item inválido.");
        // Validação geral monetária e de texto continua no serviço de lançamentos.
        ValidarCompetencia(item.Competencia);
    }

    private static void ValidarCompetencia(DateOnly competencia)
    {
        if (competencia.Year < 2000 || competencia.Year > 2100)
            throw new ArgumentException("Informe a competência do item entre 2000 e 2100.");
    }

    private IQueryable<GrupoReceber> Consulta() => db.GruposReceber.AsNoTracking().AsSingleQuery()
        .Include(x => x.Itens).ThenInclude(x => x.Lancamento).ThenInclude(x => x.Baixas);

    private static GrupoReceberDto Mapear(GrupoReceber grupo)
    {
        var itens = grupo.Itens.OrderBy(x => x.Id).Select(x => LancamentoService.Mapear(x.Lancamento)).ToList();
        return new(grupo.Id, grupo.Pessoa, grupo.Descricao, itens.Sum(x => x.ValorPrevisto),
            itens.Sum(x => x.ValorRealizado), itens.Sum(x => x.SaldoPendente), itens);
    }
}
