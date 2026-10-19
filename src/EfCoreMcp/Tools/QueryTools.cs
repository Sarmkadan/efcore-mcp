using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class QueryTools(ISqlQueryExecutor sqlExecutor, IEntityQueryExecutor entityExecutor, IRuntimeLimitGuard guard) : IQueryTools
{
    [McpServerTool(Name = "query_sql"), Description("Run a read-only SQL SELECT query against the database. Non-SELECT statements are rejected.")]
    public async Task<QueryResult> QuerySql(
        [Description("A single SELECT (or WITH ... SELECT) statement")] string sql,
        [Description("Maximum rows to return (default 100, capped by the server's --max-rows limit)")] int? maxRows = null,
        [Description("Query timeout in seconds (default 30, max 300)")] int timeoutSeconds = 30,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutSeconds, 1);
        guard.EnsureQueryLength(sql);
        guard.EnsureSqlSchemasAllowed(sql);
        var rows = guard.ResolveMaxRows(maxRows);
        return await sqlExecutor.ExecuteAsync(new SqlQueryRequest(sql, new QueryLimits(rows, timeoutSeconds)), ct);
    }

    [McpServerTool(Name = "query_entity"), Description("Read rows of an entity set through EF Core with paging, ordering, and filtering. Returns scalar properties only.")]
    public async Task<QueryResult> QueryEntity(
        [Description("Entity name")] string entityName,
        [Description("Rows to take (default 100, capped by the server's --max-rows limit)")] int? maxRows = null,
        [Description("Rows to skip")] int skip = 0,
        [Description("Property name to order by")] string? orderBy = null,
        [Description("Order descending")] bool orderDescending = false,
        [Description("Optional filter expression (e.g., 'Name == @0 && Age > @1')")] string? filter = null,
        [Description("Filter parameter values")] IReadOnlyDictionary<string, object>? filterParameters = null,
        [Description("Query timeout in seconds (default 30, max 300)")] int timeoutSeconds = 30,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (orderBy is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutSeconds, 1);
        guard.EnsureEntitySchemaAllowed(entityName);
        var rows = guard.ResolveMaxRows(maxRows);
        return await entityExecutor.ExecuteAsync(new EntityQueryRequest(entityName, new QueryLimits(rows, timeoutSeconds), orderBy, orderDescending, filter, filterParameters) { Skip = skip }, ct);
    }

    [McpServerTool(Name = "count_entity"), Description("Count rows in an entity set.")]
    public async Task<long> CountEntity([Description("Entity name")] string entityName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        guard.EnsureEntitySchemaAllowed(entityName);
        return await entityExecutor.CountAsync(entityName, ct);
    }

    [McpServerTool(Name = "explain_sql"), Description("Generate an execution plan for a SQL query without executing it. Returns the query plan and performance heuristics.")]
    public async Task<ExecutionPlanResult> ExplainSql(
        [Description("A single SELECT (or WITH ... SELECT) statement to analyze")] string sql,
        [Description("Query timeout in seconds (default 30, max 300)")] int timeoutSeconds = 30,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutSeconds, 1);
        guard.EnsureQueryLength(sql);
        guard.EnsureSqlSchemasAllowed(sql);
        return await sqlExecutor.ExplainAsync(new SqlQueryRequest(sql, new QueryLimits(0, timeoutSeconds)), ct);
    }
}
