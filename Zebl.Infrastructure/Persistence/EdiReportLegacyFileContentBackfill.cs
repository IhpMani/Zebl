using Microsoft.Data.SqlClient;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Persistence;

/// <summary>
/// One-time migration of legacy <c>EdiReport.FileContent</c> to <see cref="IEdiReportFileStore"/> and backfill of missing keys.
/// Must run before EF migration that drops <c>FileContent</c>.
/// </summary>
public static class EdiReportLegacyFileContentBackfill
{
    public static async Task RunAsync(
        string connectionString,
        string ediReportsRootDirectory,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var hasFileContent = await ColumnExistsAsync(conn, "FileContent", cancellationToken).ConfigureAwait(false);
        if (!hasFileContent)
            return;
        var hasFileStorageKey = await ColumnExistsAsync(conn, "FileStorageKey", cancellationToken).ConfigureAwait(false);
        var hasFileSize = await ColumnExistsAsync(conn, "FileSize", cancellationToken).ConfigureAwait(false);
        if (!hasFileStorageKey || !hasFileSize)
            return;

        var store = new Services.EdiReportFileStore(ediReportsRootDirectory);
        var rows = new List<(Guid Id, int TenantId, string FileName, byte[]? Blob, string? Key)>();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT [Id], [TenantId], [FileName], [FileContent], [FileStorageKey]
                FROM [EdiReport]
                WHERE ([FileContent] IS NOT NULL AND DATALENGTH([FileContent]) > 0)
                   OR ([FileStorageKey] IS NULL OR LTRIM(RTRIM([FileStorageKey])) = N'')
                """;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetGuid(0);
                var tenantId = reader.GetInt32(1);
                var fileName = reader.GetString(2);
                var fileContentOrdinal = reader.GetOrdinal("FileContent");
                byte[]? blob = reader.IsDBNull(fileContentOrdinal) ? null : (byte[])reader.GetValue(fileContentOrdinal);
                var keyOrdinal = reader.GetOrdinal("FileStorageKey");
                var existingKey = reader.IsDBNull(keyOrdinal) ? null : reader.GetString(keyOrdinal);
                rows.Add((id, tenantId, fileName, blob, existingKey));
            }
        }

        var migrated = 0;
        foreach (var row in rows)
        {
            var bytes = row.Blob is { Length: > 0 } ? row.Blob : Array.Empty<byte>();
            var storageKey = string.IsNullOrWhiteSpace(row.Key)
                ? store.BuildStorageKey(row.TenantId, row.Id, row.FileName)
                : row.Key.Trim();

            await store.WriteAsync(storageKey, bytes, cancellationToken).ConfigureAwait(false);

            await using var up = conn.CreateCommand();
            up.CommandText = """
                UPDATE [EdiReport]
                SET [FileStorageKey] = @k, [FileSize] = @sz, [FileContent] = NULL
                WHERE [Id] = @id
                """;
            up.Parameters.Add(new SqlParameter("@k", storageKey));
            up.Parameters.Add(new SqlParameter("@sz", bytes.LongLength));
            up.Parameters.Add(new SqlParameter("@id", row.Id));
            await up.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            migrated++;
        }

        if (migrated > 0)
            Console.WriteLine($"EdiReport legacy storage backfill: {migrated} row(s) updated.");
    }

    private static async Task<bool> ColumnExistsAsync(SqlConnection conn, string columnName, CancellationToken cancellationToken)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = N'EdiReport' AND COLUMN_NAME = @columnName
            """;
        cmd.Parameters.Add(new SqlParameter("@columnName", columnName));
        var o = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return o != null && o != DBNull.Value;
    }
}
