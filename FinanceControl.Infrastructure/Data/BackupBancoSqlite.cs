using Microsoft.Data.Sqlite;

namespace FinanceControl.Infrastructure.Data;

internal static class BackupBancoSqlite
{
    public static void Criar(SqliteConnection conexao, string etapa)
    {
        if (string.IsNullOrWhiteSpace(conexao.DataSource) || conexao.DataSource == ":memory:") return;
        var destino = Path.GetFullPath(conexao.DataSource) + $".antes-{etapa}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.db";
        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = destino, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString());
        backup.Open();
        // Inclui o WAL e produz uma cópia consistente; falhar impede a atualização do esquema.
        conexao.BackupDatabase(backup);
    }
}
