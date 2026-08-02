namespace EfCoreMcp.Tools;

internal static class ModelToolsConstants
{
    public const string ContextInfoName = "context_info";
    public const string ContextInfoDescription = "Get information about the loaded DbContext: type, provider, database, connectivity.";

    public const string ListEntitiesName = "list_entities";
    public const string ListEntitiesDescription = "List the names of all entity types in the EF Core model.";

    public const string DescribeModelName = "describe_model";
    public const string DescribeModelDescription = "Get the full EF Core model: every entity with properties, keys, foreign keys, navigations and indexes.";

    public const string DescribeEntityName = "describe_entity";
    public const string DescribeEntityDescription = "Get the full structure of a single entity type by name (CLR name, short name or table name).";
    public const string DescribeEntityParameterDescription = "Entity name, e.g. 'Order' or 'MyApp.Domain.Order' or table name";

    public const string ExplainSchemaName = "explain_schema";
    public const string ExplainSchemaDescription = "Render a human-readable markdown explanation of the whole model.";

    public const string ExplainEntityName = "explain_entity";
    public const string ExplainEntityDescription = "Render a human-readable markdown explanation of one entity.";
    public const string EntityNameParameterDescription = "Entity name";

    public const string RelationshipGraphName = "relationship_graph";
    public const string RelationshipGraphDescription = "Render the entity relationships as a Mermaid erDiagram.";

    public const string ListContextsName = "list_contexts";
    public const string ListContextsDescription = "List all available DbContext types in the loaded assembly.";
}
