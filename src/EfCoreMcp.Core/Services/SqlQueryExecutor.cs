using System.Diagnostics;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMcp.Core.Services;

public sealed class SqlQueryExecutor(IDbContextProvider contextProvider) : ISqlQueryExecutor
{
    public async Task<QueryResult> ExecuteAsync(SqlQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        SqlGuard.ValidateOrThrow(request.Sql);
        var ctx = contextProvider.GetContext();
        var connection = ctx.Database.GetDbConnection();
        var sw = Stopwatch.StartNew();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        // Set connection to read-only mode as defense-in-depth
        var isReadOnly = await SqlGuard.TrySetReadOnlyAsync(connection, ct);

        // Rewrite the SQL query to include a LIMIT clause for server-side row limiting
        var rewrittenSql = RewriteSqlWithLimit(request.Sql, request.MaxRows, ctx.Database.ProviderName);

        await using var command = connection.CreateCommand();
        command.CommandText = rewrittenSql;
        command.CommandTimeout = Math.Clamp(request.TimeoutSeconds, 1, 300);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var maxRows = Math.Clamp(request.MaxRows, 1, 10_000);
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;
        while (await reader.ReadAsync(ct))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : Normalize(reader.GetValue(i));
            rows.Add(row);
        }

        // If we got more rows than MaxRows, mark as truncated
        // This can happen if the LIMIT clause couldn't be applied (e.g., complex query structure)
        if (rows.Count > maxRows)
        {
            truncated = true;
            rows = rows.Take(maxRows).ToList();
        }

        sw.Stop();
        return new QueryResult(columns, rows, rows.Count, truncated, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Rewrites a SQL query to include a LIMIT clause for server-side row limiting.
    /// Supports different SQL dialects based on the database provider.
    /// </summary>
    /// <param name="sql">The original SQL query</param>
    /// <param name="maxRows">Maximum number of rows to return</param>
    /// <param name="providerName">Database provider name (e.g., "Microsoft.EntityFrameworkCore.SqlServer")</param>
    /// <returns>Rewritten SQL query with LIMIT clause</returns>
    private static string RewriteSqlWithLimit(string sql, int maxRows, string? providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        var maxRowsClamped = Math.Clamp(maxRows, 1, 10_000);

        // Normalize the SQL by trimming whitespace and removing trailing semicolons
        var normalizedSql = sql.Trim();
        if (normalizedSql.EndsWith(";", StringComparison.OrdinalIgnoreCase))
        {
            normalizedSql = normalizedSql[..^1].Trim();
        }

        // Check if the query already has a LIMIT/OFFSET clause
        if (HasLimitClause(normalizedSql))
        {
            // Query already has limiting, return as-is
            return normalizedSql;
        }

        // Determine the appropriate LIMIT syntax based on provider
        var limitClause = (providerName ?? "").ToUpperInvariant() switch
        {
            var p when p.Contains("SQLSERVER") =>
                $"OFFSET 0 ROWS FETCH NEXT {maxRowsClamped} ROWS ONLY",
            var p when p.Contains("POSTGRESQL") =>
                $"LIMIT {maxRowsClamped}",
            var p when p.Contains("MYSQL") =>
                $"LIMIT {maxRowsClamped}",
            var p when p.Contains("SQLITE") =>
                $"LIMIT {maxRowsClamped}",
            var p when p.Contains("ORACLE") =>
                $"FETCH FIRST {maxRowsClamped} ROWS ONLY",
            _ => $"LIMIT {maxRowsClamped}" // Default to standard LIMIT syntax
        };

        // For SELECT queries, append the LIMIT clause
        // Handle both simple SELECT statements and CTEs (WITH clauses)
        if (normalizedSql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            // For CTEs, we need to add the LIMIT after the main SELECT
            // Find the last SELECT statement
            var lastSelectIndex = normalizedSql.LastIndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            if (lastSelectIndex >= 0)
            {
                // Insert the LIMIT clause before any trailing semicolon or end of string
                var insertPosition = normalizedSql.IndexOf(';', lastSelectIndex);
                if (insertPosition < 0)
                {
                    insertPosition = normalizedSql.Length;
                }
                return normalizedSql.Insert(insertPosition, " " + limitClause);
            }
        }
        else if (normalizedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            // Simple SELECT statement - append LIMIT at the end
            return normalizedSql + " " + limitClause;
        }

        // For non-SELECT queries (shouldn't happen due to SqlGuard, but be safe)
        return normalizedSql;
    }

    /// <summary>
    /// Checks if the SQL query already has a LIMIT or OFFSET clause.
    /// </summary>
    private static bool HasLimitClause(string sql)
    {
        var upperSql = sql.ToUpperInvariant();
        return upperSql.Contains("LIMIT ") ||
               upperSql.Contains("OFFSET ") ||
               upperSql.Contains("FETCH FIRST ") ||
               upperSql.Contains("TOP ") ||
               upperSql.Contains("ROWNUM ") ||
               upperSql.Contains("ROW_NUMBER()");
    }

    internal static object? Normalize(object? value) => value switch
    {
        null or DBNull => null,
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        Guid g => g.ToString(),
        decimal or double or float or int or long or short or byte or bool or string => value,
        _ => value?.ToString()
    };
}