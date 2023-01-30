using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMcp.Core.Abstractions;

public interface IDbContextProvider : IDisposable
{
    DbContext GetContext();
    ContextInfo GetContextInfo();
}

public interface IModelIntrospector
{
    ModelDescriptor DescribeModel();
    EntityDescriptor? DescribeEntity(string entityName);
    IReadOnlyList<string> ListEntityNames();
}

public interface ISqlQueryExecutor
{
    Task<QueryResult> ExecuteAsync(SqlQueryRequest request, CancellationToken ct = default);
}

public interface IEntityQueryExecutor
{
    Task<QueryResult> ExecuteAsync(EntityQueryRequest request, CancellationToken ct = default);
    Task<long> CountAsync(string entityName, CancellationToken ct = default);
}

public interface IMigrationInspector
{
    Task<MigrationStatus> GetStatusAsync(CancellationToken ct = default);
    ModelDiff DiffAgainstSnapshot();
}

public interface ISchemaExplainer
{
    string ExplainModel();
    string ExplainEntity(string entityName);
    string RenderRelationshipGraph();
}
