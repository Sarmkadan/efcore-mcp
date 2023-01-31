using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class QueryTools(ISqlQueryExecutor sqlExecutor, IEntityQueryExecutor entityExecutor)
{
    [McpServerTool(Name = "query_sql"), Description("Run a read-only SQL SELECT query against the database. Non-SELECT statements are rejected.")]
    public Task<QueryResult> QuerySql(
        [Description("A single SELECT (or WITH ... SELECT) statement")] string sql,
        [Description("Maximum rows to return (default 100, max 10000)")] int maxRows = 100,
        CancellationToken ct = default) =>
        sqlExecutor.ExecuteAsync(new SqlQueryRequest(sql, maxRows), ct);

    [McpServerTool(Name = "query_entity"), Description("Read rows of an entity set through EF Core with paging and ordering. Returns scalar properties only.")]
    public Task<QueryResult> QueryEntity(
        [Description("Entity name")] string entityName,
        [Description("Rows to take (default 50, max 1000)")] int take = 50,
        [Description("Rows to skip")] int skip = 0,
        [Description("Property name to order by")] string? orderBy = null,
        [Description("Order descending")] bool orderDescending = false,
        CancellationToken ct = default) =>
        entityExecutor.ExecuteAsync(new EntityQueryRequest(entityName, take, skip, orderBy, orderDescending), ct);

    [McpServerTool(Name = "count_entity"), Description("Count rows in an entity set.")]
    public Task<long> CountEntity([Description("Entity name")] string entityName, CancellationToken ct = default) =>
        entityExecutor.CountAsync(entityName, ct);
}
