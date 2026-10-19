using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Xunit;
using static EfCoreMcp.Tests.RuntimeLimitsTestModels;

namespace EfCoreMcp.Tests;

/// <summary>
/// Verifies that <see cref="QueryTools"/> enforces runtime limits before any query reaches an executor.
/// </summary>
public class QueryToolsLimitTests
{
    private static readonly ModelDescriptor SchemaModel = Model(
        Entity("Sale", "sales"),
        Entity("Log", "audit"));

    private readonly RecordingSqlExecutor _sql = new();
    private readonly RecordingEntityExecutor _entities = new();

    private QueryTools Tools(RuntimeLimitsOptions? limits = null) =>
        new(_sql, _entities, new RuntimeLimitGuard(limits ?? new RuntimeLimitsOptions(), new StubIntrospector(SchemaModel)));

    [Fact]
    public async Task QuerySql_NoMaxRows_CapsDefaultAtConfiguredLimit()
    {
        await Tools(new RuntimeLimitsOptions { MaxRows = 40 }).QuerySql("SELECT 1");

        Assert.Equal(40, Assert.Single(_sql.Requests).Limits.MaxRows);
    }

    [Fact]
    public async Task QuerySql_MaxRowsExactlyAtLimit_Executes()
    {
        await Tools(new RuntimeLimitsOptions { MaxRows = 40 }).QuerySql("SELECT 1", maxRows: 40);

        Assert.Equal(40, Assert.Single(_sql.Requests).Limits.MaxRows);
    }

    [Fact]
    public async Task QuerySql_MaxRowsOneAboveLimit_RejectedBeforeExecution()
    {
        var ex = await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { MaxRows = 40 }).QuerySql("SELECT 1", maxRows: 41));

        Assert.Equal(QueryRejectionCode.LimitExceeded, ex.Code);
        Assert.Contains("maxRows 41", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_sql.Requests);
    }

    [Fact]
    public async Task QuerySql_ZeroMaxRows_IsRejectedBeforeExecution()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Tools().QuerySql("SELECT 1", maxRows: 0));
        Assert.Empty(_sql.Requests);
    }

    [Fact]
    public async Task QuerySql_QueryExactlyAtMaxLength_Executes()
    {
        var sql = "SELECT 1";

        await Tools(new RuntimeLimitsOptions { MaxQueryLength = sql.Length }).QuerySql(sql);

        Assert.Single(_sql.Requests);
    }

    [Fact]
    public async Task QuerySql_QueryOneCharacterOverMaxLength_RejectedBeforeExecution()
    {
        var sql = "SELECT 1";

        var ex = await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { MaxQueryLength = sql.Length - 1 }).QuerySql(sql));

        Assert.Equal(QueryRejectionCode.LimitExceeded, ex.Code);
        Assert.Contains("--max-query-length", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_sql.Requests);
    }

    [Fact]
    public async Task QuerySql_SchemaOutsideAllowList_RejectedBeforeExecution()
    {
        var ex = await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).QuerySql("SELECT * FROM audit.Log"));

        Assert.Equal(QueryRejectionCode.SchemaNotAllowed, ex.Code);
        Assert.Empty(_sql.Requests);
    }

    [Fact]
    public async Task ExplainSql_QueryOneCharacterOverMaxLength_RejectedBeforeExecution()
    {
        var ex = await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { MaxQueryLength = 5 }).ExplainSql("SELECT 1"));

        Assert.Equal(QueryRejectionCode.LimitExceeded, ex.Code);
        Assert.Empty(_sql.Requests);
    }

    [Fact]
    public async Task ExplainSql_SchemaOutsideAllowList_RejectedBeforeExecution()
    {
        await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).ExplainSql("SELECT * FROM audit.Log"));

        Assert.Empty(_sql.Requests);
    }

    [Fact]
    public async Task QueryEntity_MaxRowsOneAboveLimit_RejectedBeforeExecution()
    {
        var ex = await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { MaxRows = 40 }).QueryEntity("Sale", maxRows: 41));

        Assert.Equal(QueryRejectionCode.LimitExceeded, ex.Code);
        Assert.Empty(_entities.Requests);
    }

    [Fact]
    public async Task QueryEntity_NoMaxRows_CapsDefaultAtConfiguredLimit()
    {
        await Tools(new RuntimeLimitsOptions { MaxRows = 40 }).QueryEntity("Sale");

        Assert.Equal(40, Assert.Single(_entities.Requests).Take);
    }

    [Fact]
    public async Task QueryEntity_DisallowedSchema_RejectedBeforeExecution()
    {
        var ex = await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).QueryEntity("Log"));

        Assert.Equal(QueryRejectionCode.SchemaNotAllowed, ex.Code);
        Assert.Empty(_entities.Requests);
    }

    [Fact]
    public async Task CountEntity_AllowedSchema_Executes()
    {
        await Tools(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).CountEntity("Sale");

        Assert.Equal(["Sale"], _entities.Counted);
    }

    [Fact]
    public async Task CountEntity_DisallowedSchema_RejectedBeforeExecution()
    {
        await Assert.ThrowsAsync<QueryRejectedException>(() =>
            Tools(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).CountEntity("Log"));

        Assert.Empty(_entities.Counted);
    }

    private sealed class RecordingSqlExecutor : ISqlQueryExecutor
    {
        public List<SqlQueryRequest> Requests { get; } = [];

        public Task<QueryResult> ExecuteAsync(SqlQueryRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new QueryResult([], [], 0, false, 0));
        }

        public Task<ExecutionPlanResult> ExplainAsync(SqlQueryRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ExecutionPlanResult(string.Empty, string.Empty, request.Sql));
        }
    }

    private sealed class RecordingEntityExecutor : IEntityQueryExecutor
    {
        public List<EntityQueryRequest> Requests { get; } = [];

        public List<string> Counted { get; } = [];

        public Task<QueryResult> ExecuteAsync(EntityQueryRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new QueryResult([], [], 0, false, 0));
        }

        public Task<long> CountAsync(string entityName, CancellationToken ct = default)
        {
            Counted.Add(entityName);
            return Task.FromResult(0L);
        }
    }
}
