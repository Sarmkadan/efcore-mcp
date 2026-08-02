using System.ComponentModel;
using System.Collections.Generic;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class ModelTools(IModelIntrospector introspector, ISchemaExplainer explainer, IDbContextProvider contextProvider) : IModelTools
{
    [McpServerTool(Name = ModelToolsConstants.ContextInfoName),
    Description(ModelToolsConstants.ContextInfoDescription)]
    public ContextInfo ContextInfo() => contextProvider.GetContextInfo();

    [McpServerTool(Name = ModelToolsConstants.ListEntitiesName),
    Description(ModelToolsConstants.ListEntitiesDescription)]
    public IReadOnlyList<string> ListEntities() => introspector.ListEntityNames();

    [McpServerTool(Name = ModelToolsConstants.DescribeModelName),
    Description(ModelToolsConstants.DescribeModelDescription)]
    public ModelDescriptor DescribeModel() => introspector.DescribeModel();

    [McpServerTool(Name = ModelToolsConstants.DescribeEntityName),
    Description(ModelToolsConstants.DescribeEntityDescription)]
    public EntityDescriptor DescribeEntity(
        [Description(ModelToolsConstants.DescribeEntityParameterDescription)]
        string entityName) => introspector.DescribeEntity(entityName)
        ?? throw new InvalidOperationException(introspector.EntityNotFoundMessage(entityName));

    [McpServerTool(Name = ModelToolsConstants.ExplainSchemaName),
    Description(ModelToolsConstants.ExplainSchemaDescription)]
    public string ExplainSchema() => explainer.ExplainModel();

    [McpServerTool(Name = ModelToolsConstants.ExplainEntityName),
    Description(ModelToolsConstants.ExplainEntityDescription)]
    public string ExplainEntity([Description(ModelToolsConstants.EntityNameParameterDescription)] string entityName) => explainer.ExplainEntity(entityName);

    [McpServerTool(Name = ModelToolsConstants.RelationshipGraphName),
    Description(ModelToolsConstants.RelationshipGraphDescription)]
    public string RelationshipGraph() => explainer.RenderRelationshipGraph();

    [McpServerTool(Name = ModelToolsConstants.ListContextsName),
    Description(ModelToolsConstants.ListContextsDescription)]
    public IReadOnlyList<string> ListContexts()
    {
        var info = contextProvider.GetContextInfo();
        return info.AvailableContextTypes ?? new List<string> { info.ContextType };
    }
}
