using System.Collections.Generic;
using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

public interface IAnalysisTools
{
    ModelValidationReport ValidateModel();
    IReadOnlyList<IndexSuggestion> SuggestIndexes();
    RelationshipPath ExplainRelationship(string fromEntity, string toEntity);
    DependencyOrder DependencyOrder();
}
