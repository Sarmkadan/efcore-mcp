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
    void InvalidateCache();
}

public interface ISqlQueryExecutor
{
    Task<QueryResult> ExecuteAsync(SqlQueryRequest request, CancellationToken ct = default);
    Task<ExecutionPlanResult> ExplainAsync(SqlQueryRequest request, CancellationToken ct = default);
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
    /// <summary>
    /// Validates the model against every rule, omitting findings from the suppressed rules.
    /// </summary>
    /// <param name="suppressedRuleIds">Rule IDs to omit; <see langword="null"/> suppresses nothing.</param>
    /// <returns>The validation report.</returns>
    /// <exception cref="ArgumentException">Thrown when a suppressed rule ID is empty or unknown.</exception>
    ModelValidationReport ValidateModel(IReadOnlyCollection<string>? suppressedRuleIds = null);
    IReadOnlyList<IndexSuggestion> SuggestIndexes();
}

public interface IRelationshipAnalyzer
{
    RelationshipPath ExplainRelationship(string fromEntity, string toEntity);

    /// <summary>
    /// Finds the shortest foreign-key chain between two entities, searching no further than <paramref name="maxHops"/> hops.
    /// </summary>
    /// <param name="fromEntity">The starting entity name.</param>
    /// <param name="toEntity">The target entity name.</param>
    /// <param name="maxHops">The maximum number of hops to search.</param>
    /// <returns>The relationship path, or a result with <see cref="RelationshipPath.Found"/> set to false.</returns>
    RelationshipPath ExplainRelationship(string fromEntity, string toEntity, int maxHops);

    DependencyOrder GetDependencyOrder();
}

/// <summary>
/// Enforces <see cref="RuntimeLimitsOptions"/> on tool requests. Each method throws when a request exceeds a limit.
/// </summary>
public interface IRuntimeLimitGuard
{
    /// <summary>
    /// Resolves the effective row limit for a query. A missing value uses the default of 100, capped by the configured maximum.
    /// </summary>
    /// <param name="requested">The row count requested by the caller, or <see langword="null"/> for the default.</param>
    /// <returns>The row limit to apply.</returns>
    int ResolveMaxRows(int? requested);

    /// <summary>Throws if the SQL text is longer than the configured maximum.</summary>
    /// <param name="sql">The SQL text to check.</param>
    void EnsureQueryLength(string sql);

    /// <summary>Throws if the entity's schema is not on the allow-list. Unknown entities are left for the executor to report.</summary>
    /// <param name="entityName">The entity name to check.</param>
    void EnsureEntitySchemaAllowed(string entityName);

    /// <summary>
    /// Throws if the SQL text qualifies a name with a model schema that is not allowed.
    /// This is a best-effort check on the text, not a full SQL parse.
    /// </summary>
    /// <param name="sql">The SQL text to check.</param>
    void EnsureSqlSchemasAllowed(string sql);

    /// <summary>Throws if either endpoint of a relationship is in a schema that is not allowed.</summary>
    /// <param name="fromEntity">The starting entity name.</param>
    /// <param name="toEntity">The target entity name.</param>
    void EnsureRelationshipEndpointsAllowed(string fromEntity, string toEntity);

    /// <summary>
    /// Resolves the effective hop limit for a relationship search. A missing value uses the configured maximum.
    /// </summary>
    /// <param name="requested">The hop limit requested by the caller, or <see langword="null"/> for the configured maximum.</param>
    /// <returns>The hop limit to apply.</returns>
    int ResolveAnalysisDepth(int? requested);
}

public interface ISchemaExplainer
{
    string ExplainModel();
    string ExplainEntity(string entityName);
    string RenderRelationshipGraph();
}
