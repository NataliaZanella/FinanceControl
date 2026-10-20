using System.Data;
using FinanceControl.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Infrastructure.Data;

public static class AtualizacaoCategorias
{
    public static async Task AplicarAsync(FinanceDbContext db, CancellationToken ct = default)
    {
        var conexao = (SqliteConnection)db.Database.GetDbConnection();
        bool abrir = conexao.State != ConnectionState.Open;
        if (abrir) await conexao.OpenAsync(ct);
        try
        {
            using var consulta = conexao.CreateCommand();
            consulta.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Categorias'";
            if (Convert.ToInt32(await consulta.ExecuteScalarAsync(ct)) > 0) return;
            BackupBancoSqlite.Criar(conexao, "categorias");
            using var transacao = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "Categorias" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Categorias" PRIMARY KEY AUTOINCREMENT,
                    "Nome" TEXT NOT NULL,
                    "NomeNormalizado" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX "IX_Categorias_NomeNormalizado" ON "Categorias" ("NomeNormalizado");
                """, ct);
            var legadas = await db.Lancamentos.AsNoTracking().Select(x => x.Categoria).Distinct().ToListAsync(ct);
            var nomes = Categoria.Padroes.Concat(legadas).Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Categoria.LimparNome).GroupBy(Categoria.NormalizarNome).Select(x => x.First());
            foreach (var nome in nomes)
            {
                if (nome.Length > 80) throw new InvalidOperationException("Existe categoria antiga com mais de 80 caracteres. Revise o cadastro antes de atualizar.");
                db.Categorias.Add(new Categoria { Nome = nome, NomeNormalizado = Categoria.NormalizarNome(nome) });
            }
            try { await db.SaveChangesAsync(ct); await transacao.CommitAsync(ct); }
            catch { db.ChangeTracker.Clear(); throw; }
        }
        finally { if (abrir) await conexao.CloseAsync(); }
    }
}
