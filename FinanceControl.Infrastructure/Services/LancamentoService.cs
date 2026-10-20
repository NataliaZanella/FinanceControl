using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;
using FinanceControl.Domain.Entities;
using FinanceControl.Domain.Enums;
using FinanceControl.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Infrastructure.Services;

public sealed class LancamentoService(FinanceDbContext db) : ILancamentoService
{
    public async Task<IReadOnlyList<LancamentoDto>> ListarAsync(DateOnly competencia, CancellationToken ct = default)
    {
        var mes = NormalizarMes(competencia);
        var dados = await db.Lancamentos.AsNoTracking().Include(x => x.Baixas)
            .Where(x => x.Competencia == mes)
            .OrderBy(x => x.DataVencimento).ThenBy(x => x.Descricao)
            .ToListAsync(ct);
        return dados.Select(Mapear).ToList();
    }

    public async Task<IReadOnlyList<BaixaDto>> HistoricoAsync(int lancamentoId, CancellationToken ct = default)
    {
        return await db.Baixas.AsNoTracking().Where(x => x.LancamentoId == lancamentoId)
            .OrderBy(x => x.Data).ThenBy(x => x.Id)
            .Select(x => new BaixaDto(x.Id, x.Valor, x.Data, x.Observacao))
            .ToListAsync(ct);
    }

    public async Task<ResumoMensalDto> ResumoAsync(DateOnly competencia, CancellationToken ct = default)
    {
        var mes = NormalizarMes(competencia);
        var itens = await ListarAsync(mes, ct);
        var receitas = itens.Where(x => x.Tipo == TipoLancamento.Receita).ToList();
        var despesas = itens.Where(x => x.Tipo == TipoLancamento.Despesa).ToList();
        decimal Somar(IEnumerable<LancamentoDto> grupo, Func<LancamentoDto, decimal> campo) => grupo.Sum(campo);
        var recebidas = Somar(receitas, x => x.ValorRealizado);
        var pagas = Somar(despesas, x => x.ValorRealizado);
        return new ResumoMensalDto(mes,
            Somar(receitas, x => x.ValorPrevisto), recebidas,
            Somar(despesas, x => x.ValorPrevisto), pagas,
            Somar(receitas, x => x.SaldoPendente),
            Somar(despesas, x => x.SaldoPendente),
            recebidas - pagas);
    }

    public async Task<LancamentoDto> CriarAsync(CriarLancamentoRequest request, CancellationToken ct = default)
    {
        var (descricao, categoria) = ValidarDados(request.Descricao, request.Categoria, request.Tipo,
            request.ValorPrevisto, request.Observacoes);
        var lancamento = new Lancamento
        {
            Descricao = descricao, Categoria = categoria, Tipo = request.Tipo,
            ValorPrevisto = request.ValorPrevisto,
            Competencia = NormalizarMes(request.Competencia),
            DataVencimento = request.DataVencimento,
            Observacoes = request.Observacoes?.Trim()
        };
        db.Lancamentos.Add(lancamento);
        await db.SaveChangesAsync(ct);
        return Mapear(lancamento);
    }

    public async Task<LancamentoDto> AtualizarAsync(int lancamentoId, AtualizarLancamentoRequest request, CancellationToken ct = default)
    {
        var (descricao, categoria) = ValidarDados(request.Descricao, request.Categoria, request.Tipo,
            request.ValorPrevisto, request.Observacoes);
        // Participa de uma transação externa no desmembramento; só confirma a própria transação.
        using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        var lancamento = await db.Lancamentos.AsNoTracking().Include(x => x.Baixas)
            .SingleOrDefaultAsync(x => x.Id == lancamentoId, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        var competencia = NormalizarMes(request.Competencia);
        if (request.Tipo != TipoLancamento.Receita &&
            await db.ItensGrupoReceber.AnyAsync(x => x.LancamentoId == lancamentoId, ct))
            throw new ArgumentException("Um item de valores a receber deve permanecer como receita.");
        if (request.ValorPrevisto < lancamento.Baixas.Sum(x => x.Valor))
            throw new ArgumentException("O valor previsto não pode ser menor que o total já pago ou recebido.");
        if (lancamento.Baixas.Count > 0 &&
            (request.Tipo != lancamento.Tipo || competencia != lancamento.Competencia))
            throw new ArgumentException("Lançamentos com baixas não podem mudar de tipo ou competência, para preservar o histórico.");

        await db.Lancamentos.Where(x => x.Id == lancamentoId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Descricao, descricao)
            .SetProperty(x => x.Categoria, categoria)
            .SetProperty(x => x.Tipo, request.Tipo)
            .SetProperty(x => x.ValorPrevisto, request.ValorPrevisto)
            .SetProperty(x => x.Competencia, competencia)
            .SetProperty(x => x.DataVencimento, request.DataVencimento)
            .SetProperty(x => x.Observacoes, request.Observacoes == null ? null : request.Observacoes.Trim()), ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        lancamento.Descricao = descricao;
        lancamento.Categoria = categoria;
        lancamento.Tipo = request.Tipo;
        lancamento.ValorPrevisto = request.ValorPrevisto;
        lancamento.Competencia = competencia;
        lancamento.DataVencimento = request.DataVencimento;
        lancamento.Observacoes = request.Observacoes?.Trim();
        return Mapear(lancamento);
    }

    public async Task<LancamentoDto> RegistrarBaixaAsync(int lancamentoId, RegistrarBaixaRequest request, CancellationToken ct = default)
    {
        if (request.Valor <= 0 || decimal.Round(request.Valor, 2) != request.Valor)
            throw new ArgumentException("Informe um valor positivo com até duas casas decimais.");
        if (request.Observacao?.Length > 500)
            throw new ArgumentException("A observação deve ter até 500 caracteres.");
        using var transaction = await db.Database.BeginTransactionAsync(ct);
        var lancamento = await db.Lancamentos.AsNoTracking().Include(x => x.Baixas)
            .SingleOrDefaultAsync(x => x.Id == lancamentoId, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        var pendente = lancamento.ValorPrevisto - lancamento.Baixas.Sum(x => x.Valor);
        if (request.Valor > pendente)
            throw new ArgumentException($"O valor excede o saldo pendente de {pendente:C}.");
        var baixa = new Baixa { LancamentoId = lancamentoId, Valor = request.Valor, Data = request.Data,
            Observacao = request.Observacao?.Trim() };
        db.Baixas.Add(baixa);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        lancamento.Baixas.Add(baixa);
        return Mapear(lancamento);
    }

    public async Task<bool> ExcluirAsync(int lancamentoId, CancellationToken ct = default)
    {
        using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Lancamentos.AnyAsync(x => x.Id == lancamentoId, ct)) return false;
        if (await db.Baixas.AnyAsync(x => x.LancamentoId == lancamentoId, ct))
            throw new ArgumentException("Não é possível excluir um lançamento com pagamentos ou recebimentos registrados.");
        var grupoId = await db.ItensGrupoReceber.Where(x => x.LancamentoId == lancamentoId)
            .Select(x => (int?)x.GrupoReceberId).SingleOrDefaultAsync(ct);
        if (grupoId.HasValue)
        {
            var proximo = await db.ItensGrupoReceber.Where(x => x.GrupoReceberId == grupoId.Value && x.LancamentoId != lancamentoId)
                .OrderBy(x => x.Id).Select(x => (int?)x.LancamentoId).FirstOrDefaultAsync(ct);
            if (proximo.HasValue)
                await db.RegistrosImportados.Where(x => x.LancamentoId == lancamentoId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LancamentoId, proximo.Value), ct);
        }
        await db.Lancamentos.Where(x => x.Id == lancamentoId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static (string Descricao, string Categoria) ValidarDados(string? descricao, string? categoria,
        TipoLancamento tipo, decimal valorPrevisto, string? observacoes)
    {
        descricao = descricao?.Trim() ?? string.Empty;
        categoria = categoria?.Trim() ?? string.Empty;
        if (descricao.Length is < 1 or > 180)
            throw new ArgumentException("Informe uma descrição com até 180 caracteres.");
        if (categoria.Length is < 1 or > 80)
            throw new ArgumentException("Informe uma categoria com até 80 caracteres.");
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("Tipo de lançamento inválido.");
        if (valorPrevisto <= 0 || decimal.Round(valorPrevisto, 2) != valorPrevisto)
            throw new ArgumentException("O valor previsto deve ser positivo e ter até duas casas decimais.");
        if (observacoes?.Length > 1000)
            throw new ArgumentException("As observações devem ter até 1000 caracteres.");
        return (descricao, categoria);
    }

    private static DateOnly NormalizarMes(DateOnly data) => new(data.Year, data.Month, 1);
    internal static LancamentoDto Mapear(Lancamento x)
    {
        var total = x.Baixas.Sum(b => b.Valor);
        var pendente = Math.Max(0, x.ValorPrevisto - total);
        var situacao = pendente == 0 ? "Quitado" : total > 0 ? "Parcial" : "Pendente";
        return new LancamentoDto(x.Id, x.Descricao, x.Categoria, x.Tipo,
            x.ValorPrevisto, total, pendente, x.Competencia, x.DataVencimento,
            x.Observacoes, situacao);
    }
}
