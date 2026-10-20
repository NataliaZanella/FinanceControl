using FinanceControl.Application.Models;

namespace FinanceControl.Application.Interfaces;

public interface IValoresReceberService
{
    Task<IReadOnlyList<GrupoReceberDto>> ListarAsync(CancellationToken ct = default);
    Task<GrupoReceberDto> ObterAsync(int grupoId, CancellationToken ct = default);
    Task<GrupoReceberDto> CriarAsync(CriarGrupoReceberRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LancamentoDto>> ListarOrigensAsync(DateOnly competencia, CancellationToken ct = default);
    Task<GrupoReceberDto> DesmembrarAsync(DesmembrarReceberRequest request, CancellationToken ct = default);
    Task<GrupoReceberDto> AdicionarItemAsync(int grupoId, CriarItemReceberRequest request, CancellationToken ct = default);
    Task<GrupoReceberDto> AtualizarGrupoAsync(int grupoId, AtualizarGrupoReceberRequest request, CancellationToken ct = default);
    Task<GrupoReceberDto> AtualizarItemAsync(int grupoId, int lancamentoId, AtualizarItemReceberRequest request, CancellationToken ct = default);
    Task<bool> ExcluirGrupoAsync(int grupoId, CancellationToken ct = default);
    Task<bool> ExcluirItemAsync(int grupoId, int lancamentoId, CancellationToken ct = default);
    Task<GrupoReceberDto> RegistrarRecebimentoAsync(int grupoId, int lancamentoId, RegistrarBaixaRequest request, CancellationToken ct = default);
}
