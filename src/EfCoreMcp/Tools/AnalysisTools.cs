using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

/// <summary>
/// Provides tools for analyzing the EF Core model.
/// </summary>
[McpServerToolType]
public sealed class AnalysisTools(IModelAnalyzer analyzer, IRelationshipAnalyzer relationships, IRuntimeLimitGuard guard) : IAnalysisTools
{
    /// <summary>
    /// Scans the EF Core model for common pitfalls.
    /// </summary>
    /// <param name="suppress">Rule IDs whose findings should be omitted (for example <c>EFMCP012</c>); matching is case-insensitive.</param>
    /// <returns>A report containing the results of the model validation.</returns>
    /// <exception cref="ArgumentException">Thrown when a suppressed rule ID is empty or does not name a known rule.</exception>
    [McpServerTool(Name = "validate_model"), Description("Scan the EF Core model for common pitfalls. Every finding has a stable rule ID (EFMCP001-EFMCP013) that can be listed in 'suppress' to silence it: keyless entities, unbounded strings (including in keys and indexes), decimals without precision, unindexed foreign keys, optional-cascade deletes, multiple and wide cascade paths, navigation/FK nullability mismatches, missing concurrency tokens, unindexed TPH discriminators, shadow foreign keys.")]
    public ModelValidationReport ValidateModel(
        [Description("Rule IDs to suppress, for example [\"EFMCP012\"] (optional)")] string[]? suppress = null)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        return analyzer.ValidateModel(suppress);
    }

    /// <summary>
    /// Suggests missing indexes based on foreign keys and navigation patterns in the model.
    /// </summary>
    /// <returns>A list of suggested indexes.</returns>
    [McpServerTool(Name = "suggest_indexes"), Description("Suggest missing indexes based on foreign keys and navigation patterns in the model.")]
    public IReadOnlyList<IndexSuggestion> SuggestIndexes()
    {
        return analyzer.SuggestIndexes();
    }

    /// <summary>
    /// Explains how two entities are related.
    /// </summary>
    /// <param name="fromEntity">The name of the starting entity.</param>
    /// <param name="toEntity">The name of the target entity.</param>
    /// <param name="maxHops">The maximum number of hops to search. Defaults to the configured analysis depth.</param>
    /// <returns>The shortest chain of foreign keys between the two entities, including cardinality and delete behavior at each hop.</returns>
    /// <exception cref="QueryRejectedException">Thrown when either entity is in a schema that is not allowed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxHops"/> is outside the configured analysis depth.</exception>
    [McpServerTool(Name = "explain_relationship"), Description("Explain how two entities are related: the shortest chain of foreign keys between them, with cardinality and delete behavior at each hop.")]
    public RelationshipPath ExplainRelationship(
        [Description("Starting entity name")] string fromEntity,
        [Description("Target entity name")] string toEntity,
        [Description("Maximum foreign-key hops to search (default: the server's --max-analysis-depth)")] int? maxHops = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromEntity);
        ArgumentException.ThrowIfNullOrEmpty(toEntity);
        guard.EnsureRelationshipEndpointsAllowed(fromEntity, toEntity);
        return relationships.ExplainRelationship(fromEntity, toEntity, guard.ResolveAnalysisDepth(maxHops));
    }

    /// <summary>
    /// Topologically sorts entities by foreign key dependencies.
    /// </summary>
    /// <returns>The dependency order, including safe insert order, safe delete order, and any cyclic entities that need special handling.</returns>
    [McpServerTool(Name = "dependency_order"), Description("Topologically sort entities by foreign key dependencies: safe insert order, safe delete order, and any cyclic entities that need special handling.")]
    public DependencyOrder DependencyOrder()
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        return relationships.GetDependencyOrder();
    }
}
