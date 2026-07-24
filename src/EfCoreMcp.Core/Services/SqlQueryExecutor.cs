using System.Data.Common;
using System.Diagnostics;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// SQL query executor with transient fault retry and cancellation support.
/// </summary>
public sealed class SqlQueryExecutor(IDbContextProvider contextProvider) : ISqlQueryExecutor
{
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMilliseconds = 100;

    /// <summary>
    /// Executes a SQL query with transient fault retry and cancellation support.
    /// </summary>
    /// <param name="request">The SQL query request containing query parameters</param>
    /// <param name="ct">Cancellation token for cooperative cancellation</param>
    /// <returns>Query execution result containing either the successful result or rejection information</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    /// <exception cref="TimeoutException">Thrown when the query exceeds the specified timeout</exception>
    public async Task<QueryResult> ExecuteAsync(SqlQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rejection = SqlGuard.Validate(request.Sql);
        if (rejection is not null)
            throw new QueryRejectedException(rejection);

        var limits = request.Limits ?? new QueryLimits();
        var maxRows = Math.Clamp(limits.MaxRows, 1, 10_000);
        var timeoutSeconds = Math.Clamp(limits.TimeoutSeconds, 1, 300);

        var ctx = contextProvider.GetContext();
        var connection = ctx.Database.GetDbConnection();
        var sw = Stopwatch.StartNew();
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(ct);

            // Set connection to read-only mode as defense-in-depth
            var isReadOnly = await SqlGuard.TrySetReadOnlyAsync(connection, ct);

            // Rewrite the SQL query to include a LIMIT clause for server-side row limiting
            var rewrittenSql = RewriteSqlWithLimit(request.Sql, maxRows, ctx.Database.ProviderName);

            await using var command = connection.CreateCommand();
            command.CommandText = rewrittenSql;
            command.CommandTimeout = timeoutSeconds;

            // Execute with retry policy for transient errors
            await using var reader = await ExecuteWithRetryAsync(
                async innerCt => await command.ExecuteReaderAsync(innerCt),
                ct);

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
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
        catch (OperationCanceledException)
        {
            // Distinguish between user cancellation and timeout cancellation
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException("Query execution was cancelled by the client.");
            }
            throw;
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            // Wrap transient errors after retry attempts
            throw new Exception(
                $"Transient error occurred after {MaxRetryAttempts} retry attempts. " +
                $"Please try again. Error: {GetErrorMessage(ex)}",
                ex);
        }
    }

    /// <summary>
    /// Executes a database operation with retry logic for transient errors.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The database operation to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    /// <exception cref="TimeoutException">Thrown when operation times out</exception>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct) where T : class
    {
        var attempt = 0;
        var lastException = (Exception?)null;

        while (true)
        {
            // Check cancellation before each attempt
            ct.ThrowIfCancellationRequested();

            try
            {
                return await operation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientError(ex))
            {
                attempt++;
                lastException = ex;

                if (attempt >= MaxRetryAttempts)
                {
                    throw new Exception(
                        $"Transient error occurred after {MaxRetryAttempts} retry attempts. " +
                        $"Error: {GetErrorMessage(ex)}",
                        ex);
                }

                // Delay before retry, but respect cancellation
                try
                {
                    await Task.Delay(RetryDelayMilliseconds, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Determines if an exception is transient (retryable).
    /// This checks for common transient error patterns in exception messages.
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>True if the error is transient; otherwise false</returns>
    internal static bool IsTransientError(Exception ex)
    {
        if (ex is OperationCanceledException)
            return false;

        var message = ex.Message.ToLowerInvariant();

        // Common transient error patterns
        return message.Contains("timeout") ||
               message.Contains("deadlock") ||
               message.Contains("network") ||
               message.Contains("connection") ||
               message.Contains("server busy") ||
               message.Contains("resource") ||
               message.Contains("temporarily") ||
               message.Contains("retry") ||
               message.Contains("unavailable");
    }

    /// <summary>
    /// Extracts a user-friendly error message from an exception.
    /// </summary>
    /// <param name="ex">The exception</param>
    /// <returns>User-friendly error message</returns>
    internal static string GetErrorMessage(Exception ex)
    {
        return ex.Message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? ex.Message;
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
    /// <param name="sql">The SQL query to check</param>
    /// <returns>True if the query has a limit clause; otherwise false</returns>
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

    /// <summary>
    /// Generates an execution plan for a SQL query without executing it.
    /// The query is validated for read-only compliance and then wrapped with the appropriate EXPLAIN command
    /// based on the database provider.
    /// </summary>
    /// <param name="request">The SQL query request containing query parameters</param>
    /// <param name="ct">Cancellation token for cooperative cancellation</param>
    /// <returns>Execution plan result containing the raw plan and a performance summary</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <exception cref="QueryRejectedException">Thrown when the query is rejected by validation</exception>
    public async Task<ExecutionPlanResult> ExplainAsync(SqlQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rejection = SqlGuard.Validate(request.Sql);
        if (rejection is not null)
            throw new QueryRejectedException(rejection);

        var ctx = contextProvider.GetContext();
        var providerName = ctx.Database.ProviderName ?? "";
        var connection = ctx.Database.GetDbConnection();

        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(ct);

            // Generate the appropriate EXPLAIN command based on provider
            var explainCommand = GetExplainCommand(request.Sql, providerName);

            await using var command = connection.CreateCommand();
            command.CommandText = explainCommand;
            command.CommandTimeout = Math.Clamp(request.Limits?.TimeoutSeconds ?? 30, 1, 300);

            // Execute the explain command
            var result = await command.ExecuteScalarAsync(ct);
            var executionPlan = result?.ToString() ?? string.Empty;

            // Generate a performance summary from the execution plan
            var summary = AnalyzeExecutionPlan(executionPlan, request.Sql, providerName);

            return new ExecutionPlanResult(executionPlan, summary, request.Sql);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException("Query analysis was cancelled by the client.");
            throw;
        }
    }

    /// <summary>
    /// Generates the appropriate EXPLAIN command for the given SQL query based on the database provider.
    /// </summary>
    /// <param name="sql">The SQL query to analyze</param>
    /// <param name="providerName">Database provider name</param>
    /// <returns>SQL command with EXPLAIN prefix appropriate for the provider</returns>
    private static string GetExplainCommand(string sql, string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);

        // Normalize the SQL query
        var normalizedSql = sql.Trim();
        if (normalizedSql.EndsWith(";", StringComparison.OrdinalIgnoreCase))
            normalizedSql = normalizedSql[..^1].Trim();

        // Determine the appropriate EXPLAIN syntax based on provider
        var upperProvider = providerName.ToUpperInvariant();

        if (upperProvider.Contains("SQLSERVER"))
        {
            // SQL Server uses SET SHOWPLAN_TEXT ON or query hint OPTION (SHOWPLAN_TEXT)
            // For simplicity, we'll use the query hint approach
            return $"SELECT * FROM ({normalizedSql}) AS subquery OPTION (SHOWPLAN_TEXT)";
        }
        else if (upperProvider.Contains("POSTGRESQL"))
        {
            // PostgreSQL uses EXPLAIN (ANALYZE) [FORMAT JSON]
            return $"EXPLAIN (FORMAT TEXT) {normalizedSql}";
        }
        else if (upperProvider.Contains("MYSQL"))
        {
            // MySQL uses EXPLAIN [FORMAT=JSON]
            return $"EXPLAIN {normalizedSql}";
        }
        else if (upperProvider.Contains("SQLITE"))
        {
            // SQLite uses EXPLAIN QUERY PLAN
            return $"EXPLAIN QUERY PLAN {normalizedSql}";
        }
        else if (upperProvider.Contains("ORACLE"))
        {
            // Oracle uses EXPLAIN PLAN FOR
            return $"EXPLAIN PLAN FOR {normalizedSql} SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY)";
        }
        else
        {
            // Default to EXPLAIN for other providers (many support it)
            return $"EXPLAIN {normalizedSql}";
        }
    }

    /// <summary>
    /// Analyzes an execution plan and generates a human-readable summary of performance issues.
    /// </summary>
    /// <param name="executionPlan">The raw execution plan text</param>
    /// <param name="originalQuery">The original SQL query being analyzed</param>
    /// <param name="providerName">Database provider name for provider-specific analysis</param>
    /// <returns>Human-readable performance summary highlighting issues like table scans, missing indexes, etc.</returns>
    private static string AnalyzeExecutionPlan(string executionPlan, string originalQuery, string providerName)
    {
        var summary = new List<string>();
        var upperPlan = executionPlan.ToUpperInvariant();
        var upperQuery = originalQuery.ToUpperInvariant();

        // Check for table scans on large tables
        if (upperPlan.Contains("SCAN") && !upperPlan.Contains("INDEX SCAN"))
        {
            summary.Add("Potential table scan detected - consider adding indexes to frequently filtered columns.");
        }

        // Check for index usage
        if (!upperPlan.Contains("INDEX") && !upperPlan.Contains("SEQUENTIAL SCAN"))
        {
            summary.Add("No index usage detected - queries may be inefficient on large datasets.");
        }

        // Check for sorting operations that might require temporary tables
        if (upperPlan.Contains("SORT") || upperPlan.Contains("TEMP"))
        {
            summary.Add("Sorting operation detected - consider adding indexes to ORDER BY columns.");
        }

        // Check for nested loop joins which can be expensive
        if (upperPlan.Contains("NESTED LOOP"))
        {
            summary.Add("Nested loop joins detected - consider optimizing join conditions or adding indexes.");
        }

        // Check for large result sets
        if (originalQuery.Contains("SELECT *") && !originalQuery.Contains("WHERE"))
        {
            summary.Add("SELECT * without WHERE clause - consider adding filtering conditions.");
        }

        // Provider-specific checks
        var upperProvider = providerName.ToUpperInvariant();
        if (upperProvider.Contains("SQLSERVER") && executionPlan.Contains("TABLE SCAN"))
        {
            summary.Add("SQL Server table scan detected - ensure proper indexes exist on filtered columns.");
        }
        else if (upperProvider.Contains("POSTGRESQL") && executionPlan.Contains("SEQ SCAN"))
        {
            summary.Add("PostgreSQL sequential scan detected - consider adding indexes to WHERE clause columns.");
        }
        else if (upperProvider.Contains("SQLITE") && executionPlan.Contains("SCAN TABLE"))
        {
            summary.Add("SQLite full table scan detected - indexes may improve performance.");
        }

        // If no issues found, provide positive feedback
        if (summary.Count == 0)
        {
            summary.Add("Query plan looks efficient - no obvious performance bottlenecks detected.");
        }

        return string.Join(" ", summary);
    }
}