using System.Diagnostics;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMcp.Core.Services;

public sealed class SqlQueryExecutor(IDbContextProvider contextProvider) : ISqlQueryExecutor
{
    public async Task<QueryResult> ExecuteAsync(SqlQueryRequest request, CancellationToken ct = default)
    {
        SqlGuard.ValidateOrThrow(request.Sql);
        var ctx = contextProvider.GetContext();
        var connection = ctx.Database.GetDbConnection();
        var sw = Stopwatch.StartNew();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = request.Sql;
        command.CommandTimeout = Math.Clamp(request.TimeoutSeconds, 1, 300);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var maxRows = Math.Clamp(request.MaxRows, 1, 10_000);
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : Normalize(reader.GetValue(i));
            rows.Add(row);
        }
        sw.Stop();
        return new QueryResult(columns, rows, rows.Count, truncated, sw.ElapsedMilliseconds);
    }

    internal static object? Normalize(object? value) => value switch
    {
        null or DBNull => null,
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        Guid g => g.ToString(),
        decimal or double or float or int or long or short or byte or bool or string => value,
        _ => value.ToString()
    };
}
