using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;

namespace FinanceControl.Web;

public static class CategoriaEndpoints
{
    public static void MapCategorias(this WebApplication app)
    {
        var api = app.MapGroup("/api/categorias");
        api.MapGet("", (ICategoriaService servico, CancellationToken ct) => servico.ListarAsync(ct));
        api.MapGet("/{id:int}", async (int id, ICategoriaService servico, CancellationToken ct) =>
        {
            var categoria = (await servico.ListarAsync(ct)).FirstOrDefault(x => x.Cadastrada && x.Id == id);
            return categoria is null ? Results.NotFound() : Results.Ok(categoria);
        });
        api.MapPost("", async (SalvarCategoriaRequest entrada, ICategoriaService servico, CancellationToken ct) =>
        {
            try
            {
                var categoria = await servico.CriarAsync(entrada, ct);
                return Results.Created($"/api/categorias/{categoria.Id}", categoria);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
        });
        api.MapPut("/{id:int}", async (int id, SalvarCategoriaRequest entrada, ICategoriaService servico, CancellationToken ct) =>
        {
            try { return Results.Ok(await servico.RenomearAsync(id, entrada, ct)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { erro = ex.Message }); }
        });
        api.MapDelete("/{id:int}", async (int id, ICategoriaService servico, CancellationToken ct) =>
        {
            try { return await servico.ExcluirAsync(id, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
        });
    }
}
