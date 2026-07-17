namespace EfCoreMcp.Core.Domain;

public sealed record ModelFinding(
    string Severity,
    string Code,
    string Entity,
    string? Property,
    string Message,
    string Recommendation);

public sealed record ModelValidationReport(
    int EntityCount,
    IReadOnlyList<ModelFinding> Findings);

public sealed record IndexSuggestion(
    string Entity,
    string? Table,
    IReadOnlyList<string> Properties,
    string Reason);

public sealed record RelationshipHop(
    string FromEntity,
    string ToEntity,
    string NavigationDescription,
    IReadOnlyList<string> ForeignKeyProperties,
    string Cardinality,
    string DeleteBehavior);

public sealed record RelationshipPath(
    string FromEntity,
    string ToEntity,
    bool Found,
    IReadOnlyList<RelationshipHop> Hops,
    string Summary);

public sealed record DependencyOrder(
    IReadOnlyList<string> InsertOrder,
    IReadOnlyList<string> DeleteOrder,
    IReadOnlyList<string> CyclicEntities);
