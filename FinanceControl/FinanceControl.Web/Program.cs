using System.Globalization;
using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;
using FinanceControl.Infrastructure;
using FinanceControl.Infrastructure.Data;
using FinanceControl.SharedUI.Components.Pages;
using FinanceControl.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFinanceControl(builder.Configuration);

var app = builder.Build();

// MVP local: cria o arquivo SQLite na primeira execução.
// Antes de produção/mudanças de schema, substituir EnsureCreated por migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    db.Database.EnsureCreated();
    // Compatibilidade com o banco criado no MVP anterior: EnsureCreated não adiciona
    // tabelas em um SQLite que já existe. Esta tabela isolada é criada sem apagar dados.
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "RegistrosImportados" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_RegistrosImportados" PRIMARY KEY AUTOINCREMENT,
            "ChaveOrigem" TEXT NOT NULL,
            "Aba" TEXT NOT NULL,
            "LinhaExcel" INTEGER NOT NULL,
            "LancamentoId" INTEGER NOT NULL,
            CONSTRAINT "FK_RegistrosImportados_Lancamentos_LancamentoId"
                FOREIGN KEY ("LancamentoId") REFERENCES "Lancamentos" ("Id") ON DELETE CASCADE
        );
        """);
    db.Database.ExecuteSqlRaw("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_RegistrosImportados_ChaveOrigem"
        ON "RegistrosImportados" ("ChaveOrigem");
        """);
    db.Database.ExecuteSqlRaw("""
        CREATE INDEX IF NOT EXISTS "IX_RegistrosImportados_LancamentoId"
        ON "RegistrosImportados" ("LancamentoId");
        """);
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

var api = app.MapGroup("/api/lancamentos");
api.MapGet("", async (string? mes, ILancamentoService servico, CancellationToken ct) =>
{
    if (!MesValido(mes, out var competencia)) return Results.BadRequest(new { erro = "Use mes=AAAA-MM." });
    return Results.Ok(await servico.ListarAsync(competencia, ct));
});
api.MapGet("/{id:int}/baixas", async (int id, ILancamentoService servico, CancellationToken ct) =>
    Results.Ok(await servico.HistoricoAsync(id, ct)));
api.MapGet("/resumo", async (string? mes, ILancamentoService servico, CancellationToken ct) =>
{
    if (!MesValido(mes, out var competencia)) return Results.BadRequest(new { erro = "Use mes=AAAA-MM." });
    return Results.Ok(await servico.ResumoAsync(competencia, ct));
});
api.MapPost("", async (CriarLancamentoRequest entrada, ILancamentoService servico, CancellationToken ct) =>
{
    try
    {
        var item = await servico.CriarAsync(entrada, ct);
        return Results.Created($"/api/lancamentos/{item.Id}", item);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
});
api.MapPost("/{id:int}/baixas", async (int id, RegistrarBaixaRequest entrada, ILancamentoService servico, CancellationToken ct) =>
{
    try { return Results.Ok(await servico.RegistrarBaixaAsync(id, entrada, ct)); }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { erro = ex.Message }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { erro = ex.Message }); }
});
api.MapDelete("/{id:int}", async (int id, ILancamentoService servico, CancellationToken ct) =>
    await servico.ExcluirAsync(id, ct) ? Results.NoContent() : Results.NotFound());

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Dashboard).Assembly);

app.Run();

static bool MesValido(string? mes, out DateOnly competencia)
{
    competencia = default;
    return mes is { Length: 7 } &&
        DateOnly.TryParseExact(mes + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out competencia);
}
