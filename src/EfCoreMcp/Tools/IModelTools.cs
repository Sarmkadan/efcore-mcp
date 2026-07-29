using System.Collections.Generic;
using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

public interface IModelTools
{
    ContextInfo ContextInfo();

    IReadOnlyList<string> ListEntities();

    ModelDescriptor DescribeModel();

    EntityDescriptor DescribeEntity(
        [Description("Entity name, e.g. 'Order' or 'MyApp.Domain.Order' or table name")]
        string entityName);

    string ExplainSchema();

    string ExplainEntity(
        [Description("Entity name")]
        string entityName);

    string RelationshipGraph();
}
