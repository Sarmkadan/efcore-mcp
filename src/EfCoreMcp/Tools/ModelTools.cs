using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class ModelTools(IModelIntrospector introspector, ISchemaExplainer explainer, IDbContextProvider contextProvider)
{
    [McpServerTool(Name = "context_info"), Description("Get information about the loaded DbContext: type, provider, database, connectivity.")]
    public ContextInfo ContextInfo() => contextProvider.GetContextInfo();

    [McpServerTool(Name = "list_entities"), Description("List the names of all entity types in the EF Core model.")]
    public IReadOnlyList<string> ListEntities() => introspector.ListEntityNames();

    [McpServerTool(Name = "describe_model"), Description("Get the full EF Core model: every entity with properties, keys, foreign keys, navigations and indexes.")]
    public ModelDescriptor DescribeModel() => introspector.DescribeModel();

    [McpServerTool(Name = "describe_entity"), Description("Get the full structure of a single entity type by name (CLR name, short name or table name).")]
    public EntityDescriptor DescribeEntity(
        [Description("Entity name, e.g. 'Order' or 'MyApp.Domain.Order' or table name")] string entityName) =>
        introspector.DescribeEntity(entityName)
            ?? throw new InvalidOperationException($"Entity '{entityName}' not found in the model.");

    [McpServerTool(Name = "explain_schema"), Description("Render a human-readable markdown explanation of the whole model.")]
    public string ExplainSchema() => explainer.ExplainModel();

    [McpServerTool(Name = "explain_entity"), Description("Render a human-readable markdown explanation of one entity.")]
    public string ExplainEntity([Description("Entity name")] string entityName) =>
        explainer.ExplainEntity(entityName);

    [McpServerTool(Name = "relationship_graph"), Description("Render the entity relationships as a Mermaid erDiagram.")]
    public string RelationshipGraph() => explainer.RenderRelationshipGraph();
}
