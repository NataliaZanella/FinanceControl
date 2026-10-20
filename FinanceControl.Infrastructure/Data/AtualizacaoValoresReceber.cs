using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Infrastructure.Data;

// Atualização aditiva para bancos anteriores a migrations. Não altera tabelas financeiras.
public static class AtualizacaoValoresReceber
{
    public static async Task AplicarAsync(FinanceDbContext db, CancellationToken ct = default)
    {
        var conexao = (SqliteConnection)db.Database.GetDbConnection();
        bool abrir = conexao.State != ConnectionState.Open;
        if (abrir) await conexao.OpenAsync(ct);
        try
        {
            using var consulta = conexao.CreateCommand();
            consulta.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('GruposReceber', 'ItensGrupoReceber')";
            if (Convert.ToInt32(await consulta.ExecuteScalarAsync(ct)) == 2) return;

            // BackupDatabase inclui o WAL e produz uma cópia consistente, inclusive com outra conexão aberta.
            var origem = conexao.DataSource;
            if (!string.IsNullOrWhiteSpace(origem) && origem != ":memory:")
            {
                var destino = Path.GetFullPath(origem) + $".antes-valores-receber-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.db";
                using var backup = new SqliteConnection(new SqliteConnectionStringBuilder {
                    DataSource = destino, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
                }.ToString());
                backup.Open();
                conexao.BackupDatabase(backup);
            }

            using var transacao = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "GruposReceber" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GruposReceber" PRIMARY KEY AUTOINCREMENT,
                    "Pessoa" TEXT NOT NULL,
                    "Descricao" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "ItensGrupoReceber" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_ItensGrupoReceber" PRIMARY KEY AUTOINCREMENT,
                    "GrupoReceberId" INTEGER NOT NULL,
                    "LancamentoId" INTEGER NOT NULL,
                    FOREIGN KEY ("GrupoReceberId") REFERENCES "GruposReceber" ("Id") ON DELETE CASCADE,
                    FOREIGN KEY ("LancamentoId") REFERENCES "Lancamentos" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_ItensGrupoReceber_GrupoReceberId" ON "ItensGrupoReceber" ("GrupoReceberId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ItensGrupoReceber_LancamentoId" ON "ItensGrupoReceber" ("LancamentoId");
                """, ct);
            await transacao.CommitAsync(ct);
        }
        finally { if (abrir) await conexao.CloseAsync(); }
    }
}
