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
        var descricao = request.Descricao?.Trim() ?? string.Empty;
        var categoria = request.Categoria?.Trim() ?? string.Empty;
        if (descricao.Length is < 1 or > 180)
            throw new ArgumentException("Informe uma descrição com até 180 caracteres.");
        if (categoria.Length is < 1 or > 80)
            throw new ArgumentException("Informe uma categoria com até 80 caracteres.");
        if (!Enum.IsDefined(request.Tipo))
            throw new ArgumentException("Tipo de lançamento inválido.");
        if (request.ValorPrevisto <= 0 || decimal.Round(request.ValorPrevisto, 2) != request.ValorPrevisto)
            throw new ArgumentException("O valor previsto deve ser positivo e ter até duas casas decimais.");
        if (request.Observacoes?.Length > 1000)
            throw new ArgumentException("As observações devem ter até 1000 caracteres.");
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

    public async Task<LancamentoDto> RegistrarBaixaAsync(int lancamentoId, RegistrarBaixaRequest request, CancellationToken ct = default)
    {
        if (request.Valor <= 0 || decimal.Round(request.Valor, 2) != request.Valor)
            throw new ArgumentException("Informe um valor positivo com até duas casas decimais.");
        if (request.Observacao?.Length > 500)
            throw new ArgumentException("A observação deve ter até 500 caracteres.");
        var lancamento = await db.Lancamentos.Include(x => x.Baixas)
            .SingleOrDefaultAsync(x => x.Id == lancamentoId, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        var pendente = lancamento.ValorPrevisto - lancamento.Baixas.Sum(x => x.Valor);
        if (request.Valor > pendente)
            throw new ArgumentException($"O valor excede o saldo pendente de {pendente:C}.");
        lancamento.Baixas.Add(new Baixa { Valor = request.Valor, Data = request.Data,
            Observacao = request.Observacao?.Trim() });
        await db.SaveChangesAsync(ct);
        return Mapear(lancamento);
    }

    public async Task<bool> ExcluirAsync(int lancamentoId, CancellationToken ct = default)
    {
        var lancamento = await db.Lancamentos.FindAsync([lancamentoId], ct);
        if (lancamento is null) return false;
        db.Lancamentos.Remove(lancamento);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static DateOnly NormalizarMes(DateOnly data) => new(data.Year, data.Month, 1);
    private static LancamentoDto Mapear(Lancamento x)
    {
        var total = x.Baixas.Sum(b => b.Valor);
        var pendente = Math.Max(0, x.ValorPrevisto - total);
        var situacao = pendente == 0 ? "Quitado" : total > 0 ? "Parcial" : "Pendente";
        return new LancamentoDto(x.Id, x.Descricao, x.Categoria, x.Tipo,
            x.ValorPrevisto, total, pendente, x.Competencia, x.DataVencimento,
            x.Observacoes, situacao);
    }
}
