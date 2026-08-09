using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CarShowJudging.Infrastructure.Data;

// SQLite's default rollback-journal mode serializes all writers and fails fast (no wait) on
// contention, which single-user testing never exercises but a car show with several judges
// scoring at once will. WAL mode lets readers and a writer proceed concurrently, and the busy
// timeout makes a blocked writer wait and retry instead of throwing immediately.
public class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
