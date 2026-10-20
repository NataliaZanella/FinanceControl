using FinanceControl.Application.Models;

namespace FinanceControl.Application.Interfaces;

// Interface compartilhada: Web usa EF Core; Desktop poderá usar HttpClient/API.
public interface ILancamentoService
{
    Task<IReadOnlyList<LancamentoDto>> ListarAsync(DateOnly competencia, CancellationToken ct = default);
    Task<IReadOnlyList<BaixaDto>> HistoricoAsync(int lancamentoId, CancellationToken ct = default);
    Task<ResumoMensalDto> ResumoAsync(DateOnly competencia, CancellationToken ct = default);
    Task<LancamentoDto> CriarAsync(CriarLancamentoRequest request, CancellationToken ct = default);
    Task<LancamentoDto> AtualizarAsync(int lancamentoId, AtualizarLancamentoRequest request, CancellationToken ct = default);
    Task<LancamentoDto> RegistrarBaixaAsync(int lancamentoId, RegistrarBaixaRequest request, CancellationToken ct = default);
    Task<bool> ExcluirAsync(int lancamentoId, CancellationToken ct = default);
}
