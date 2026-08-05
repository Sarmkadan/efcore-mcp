using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

/// <summary>
/// Provides tools for analyzing the EF Core model.
/// </summary>
[McpServerToolType]
public sealed class AnalysisTools(IModelAnalyzer analyzer, IRelationshipAnalyzer relationships) : IAnalysisTools
{
    /// <summary>
    /// Scans the EF Core model for common pitfalls.
    /// </summary>
    /// <returns>A report containing the results of the model validation.</returns>
    [McpServerTool(Name = "validate_model"), Description("Scan the EF Core model for common pitfalls: keyless entities, unbounded strings, decimals without precision, unindexed foreign keys, optional-cascade deletes, multiple cascade paths, navigation-only relationships.")]
    public ModelValidationReport ValidateModel() => analyzer.ValidateModel();

    /// <summary>
    /// Suggests missing indexes based on foreign keys and navigation patterns in the model.
    /// </summary>
    /// <returns>A list of suggested indexes.</returns>
    [McpServerTool(Name = "suggest_indexes"), Description("Suggest missing indexes based on foreign keys and navigation patterns in the model.")]
    public IReadOnlyList<IndexSuggestion> SuggestIndexes() => analyzer.SuggestIndexes();

    /// <summary>
    /// Explains how two entities are related.
    /// </summary>
    /// <param name="fromEntity">The name of the starting entity.</param>
    /// <param name="toEntity">The name of the target entity.</param>
    /// <returns>The shortest chain of foreign keys between the two entities, including cardinality and delete behavior at each hop.</returns>
    [McpServerTool(Name = "explain_relationship"), Description("Explain how two entities are related: the shortest chain of foreign keys between them, with cardinality and delete behavior at each hop.")]
    public RelationshipPath ExplainRelationship(
        [Description("Starting entity name")] string fromEntity,
        [Description("Target entity name")] string toEntity) =>
        relationships.ExplainRelationship(fromEntity, toEntity);

    /// <summary>
    /// Topologically sorts entities by foreign key dependencies.
    /// </summary>
    /// <returns>The dependency order, including safe insert order, safe delete order, and any cyclic entities that need special handling.</returns>
    [McpServerTool(Name = "dependency_order"), Description("Topologically sort entities by foreign key dependencies: safe insert order, safe delete order, and any cyclic entities that need special handling.")]
    public DependencyOrder DependencyOrder() => relationships.GetDependencyOrder();
}
