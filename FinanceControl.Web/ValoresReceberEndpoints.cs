using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;

namespace FinanceControl.Web;

public static class ValoresReceberEndpoints
{
    public static void MapValoresReceber(this WebApplication app)
    {
        var api = app.MapGroup("/api/valores-a-receber");
        api.MapGet("", (IValoresReceberService servico, CancellationToken ct) =>
            Executar(() => servico.ListarAsync(ct)));
        api.MapGet("/origens", async (string? mes, IValoresReceberService servico, CancellationToken ct) =>
        {
            if (!DateOnly.TryParseExact(mes + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var competencia))
                return Results.BadRequest(new { erro = "Use mes=AAAA-MM." });
            return await Executar(() => servico.ListarOrigensAsync(competencia, ct));
        });
        api.MapPost("/desmembrar", async (DesmembrarReceberRequest entrada, IValoresReceberService servico, CancellationToken ct) =>
        {
            try
            {
                var grupo = await servico.DesmembrarAsync(entrada, ct);
                return Results.Created($"/api/valores-a-receber/{grupo.Id}", grupo);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { erro = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
        });
        api.MapGet("/{id:int}", (int id, IValoresReceberService servico, CancellationToken ct) =>
            Executar(() => servico.ObterAsync(id, ct)));
        api.MapPost("", async (CriarGrupoReceberRequest entrada, IValoresReceberService servico, CancellationToken ct) =>
        {
            try
            {
                var grupo = await servico.CriarAsync(entrada, ct);
                return Results.Created($"/api/valores-a-receber/{grupo.Id}", grupo);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
        });
        api.MapPost("/{id:int}/itens", (int id, CriarItemReceberRequest entrada, IValoresReceberService servico, CancellationToken ct) =>
            Executar(() => servico.AdicionarItemAsync(id, entrada, ct)));
        api.MapPut("/{id:int}", (int id, AtualizarGrupoReceberRequest entrada, IValoresReceberService servico, CancellationToken ct) =>
            Executar(() => servico.AtualizarGrupoAsync(id, entrada, ct)));
        api.MapPut("/{id:int}/itens/{lancamentoId:int}", (int id, int lancamentoId, AtualizarItemReceberRequest entrada,
            IValoresReceberService servico, CancellationToken ct) => Executar(() => servico.AtualizarItemAsync(id, lancamentoId, entrada, ct)));
        api.MapDelete("/{id:int}", (int id, IValoresReceberService servico, CancellationToken ct) =>
            Excluir(() => servico.ExcluirGrupoAsync(id, ct)));
        api.MapDelete("/{id:int}/itens/{lancamentoId:int}", (int id, int lancamentoId, IValoresReceberService servico, CancellationToken ct) =>
            Excluir(() => servico.ExcluirItemAsync(id, lancamentoId, ct)));
        api.MapPost("/{id:int}/itens/{lancamentoId:int}/recebimentos", (int id, int lancamentoId,
            RegistrarBaixaRequest entrada, IValoresReceberService servico, CancellationToken ct) =>
            Executar(() => servico.RegistrarRecebimentoAsync(id, lancamentoId, entrada, ct)));
    }

    private static async Task<IResult> Executar<T>(Func<Task<T>> acao)
    {
        try { return Results.Ok(await acao()); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { erro = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
    }

    private static async Task<IResult> Excluir(Func<Task<bool>> acao)
    {
        try { return await acao() ? Results.NoContent() : Results.NotFound(); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { erro = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
    }
}
