using FinanceControl.Application.Models;

namespace FinanceControl.Application.Interfaces;

public interface IImportacaoExcelService
{
    IReadOnlyList<AbaImportacaoDto> ListarAbas(byte[] dados);
    Task<PreviaImportacaoDto> PrepararPreviaAsync(byte[] dados, string nomeArquivo, string aba, int? anoManual,
        CancellationToken ct = default);
    Task<ResultadoImportacaoDto> ConfirmarAsync(IReadOnlyCollection<LinhaImportacaoDto> linhas,
        CancellationToken ct = default);
}
