using FinanceControl.Application.Models;

namespace FinanceControl.Application.Interfaces;

public interface ICategoriaService
{
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default);
    Task<CategoriaDto> CriarAsync(SalvarCategoriaRequest request, CancellationToken ct = default);
    Task<CategoriaDto> RenomearAsync(int id, SalvarCategoriaRequest request, CancellationToken ct = default);
    Task<bool> ExcluirAsync(int id, CancellationToken ct = default);
}
