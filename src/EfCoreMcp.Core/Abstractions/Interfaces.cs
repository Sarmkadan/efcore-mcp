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
    string EntityNotFoundMessage(string entityName);
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

/// <summary>
/// Provides inspection capabilities for database migrations, allowing comparison of the current model
/// against migration snapshots to detect pending changes.
/// </summary>
public interface IMigrationInspector
{
    /// <summary>
    /// Gets the current migration status including applied migrations, pending migrations, and whether the model has drifted.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="MigrationStatus"/> containing applied migrations, pending migrations, and model drift status.</returns>
    Task<MigrationStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Diffs the current model against the last migration snapshot and returns the list of pending operations
    /// that a new migration would contain.
    /// </summary>
    /// <returns>A <see cref="ModelDiff"/> containing whether differences exist and the list of operations.</returns>
    ModelDiff DiffAgainstSnapshot();
}

public interface IModelAnalyzer
{
    ModelValidationReport ValidateModel();
    IReadOnlyList<IndexSuggestion> SuggestIndexes();
}

public interface IRelationshipAnalyzer
{
    RelationshipPath ExplainRelationship(string fromEntity, string toEntity);
    DependencyOrder GetDependencyOrder();
}

public interface ISchemaExplainer
{
    string ExplainModel();
    string ExplainEntity(string entityName);
    string RenderRelationshipGraph();
}
