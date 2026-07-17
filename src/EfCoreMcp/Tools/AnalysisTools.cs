using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class AnalysisTools(IModelAnalyzer analyzer, IRelationshipAnalyzer relationships)
{
    [McpServerTool(Name = "validate_model"), Description("Scan the EF Core model for common pitfalls: keyless entities, unbounded strings, decimals without precision, unindexed foreign keys, optional-cascade deletes, multiple cascade paths, navigation-only relationships.")]
    public ModelValidationReport ValidateModel() => analyzer.ValidateModel();

    [McpServerTool(Name = "suggest_indexes"), Description("Suggest missing indexes based on foreign keys and navigation patterns in the model.")]
    public IReadOnlyList<IndexSuggestion> SuggestIndexes() => analyzer.SuggestIndexes();

    [McpServerTool(Name = "explain_relationship"), Description("Explain how two entities are related: the shortest chain of foreign keys between them, with cardinality and delete behavior at each hop.")]
    public RelationshipPath ExplainRelationship(
        [Description("Starting entity name")] string fromEntity,
        [Description("Target entity name")] string toEntity) =>
        relationships.ExplainRelationship(fromEntity, toEntity);

    [McpServerTool(Name = "dependency_order"), Description("Topologically sort entities by foreign key dependencies: safe insert order, safe delete order, and any cyclic entities that need special handling.")]
    public DependencyOrder DependencyOrder() => relationships.GetDependencyOrder();
}
