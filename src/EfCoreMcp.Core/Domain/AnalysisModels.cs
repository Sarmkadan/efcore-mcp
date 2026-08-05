using System;

namespace EfCoreMcp.Core.Domain;

public sealed record ModelFinding
{
    public string Severity { get; }
    public string Code { get; }
    public string Entity { get; }
    public string? Property { get; }
    public string Message { get; }
    public string Recommendation { get; }

    public ModelFinding(string severity, string code, string entity, string? property, string message, string recommendation)
    {
        ArgumentException.ThrowIfNullOrEmpty(severity);
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(entity);
        // Property can be null, skip validation
        ArgumentException.ThrowIfNullOrEmpty(message);
        ArgumentException.ThrowIfNullOrEmpty(recommendation);
        Severity = severity;
        Code = code;
        Entity = entity;
        Property = property;
        Message = message;
        Recommendation = recommendation;
    }
}

public sealed record ModelValidationReport
{
    public int EntityCount { get; }
    public IReadOnlyList<ModelFinding> Findings { get; }

    public ModelValidationReport(int entityCount, IReadOnlyList<ModelFinding> findings)
    {
        EntityCount = entityCount;
        Findings = findings ?? throw new ArgumentNullException(nameof(findings));
    }
}

public sealed record IndexSuggestion
{
    public string Entity { get; }
    public string? Table { get; }
    public IReadOnlyList<string> Properties { get; }
    public string Reason { get; }

    public IndexSuggestion(string entity, string? table, IReadOnlyList<string> properties, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(entity);
        // Table can be null, skip validation
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Entity = entity;
        Table = table;
        Reason = reason;
    }
}

public sealed record RelationshipHop
{
    public string FromEntity { get; }
    public string ToEntity { get; }
    public string NavigationDescription { get; }
    public IReadOnlyList<string> ForeignKeyProperties { get; }
    public string Cardinality { get; }
    public string DeleteBehavior { get; }

    public RelationshipHop(string fromEntity, string toEntity, string navigationDescription, IReadOnlyList<string> foreignKeyProperties, string cardinality, string deleteBehavior)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromEntity);
        ArgumentException.ThrowIfNullOrEmpty(toEntity);
        ArgumentException.ThrowIfNullOrEmpty(navigationDescription);
        ForeignKeyProperties = foreignKeyProperties ?? throw new ArgumentNullException(nameof(foreignKeyProperties));
        ArgumentException.ThrowIfNullOrEmpty(cardinality);
        ArgumentException.ThrowIfNullOrEmpty(deleteBehavior);
        FromEntity = fromEntity;
        ToEntity = toEntity;
        NavigationDescription = navigationDescription;
        Cardinality = cardinality;
        DeleteBehavior = deleteBehavior;
    }
}

public sealed record RelationshipPath
{
    public string FromEntity { get; }
    public string ToEntity { get; }
    public bool Found { get; }
    public IReadOnlyList<RelationshipHop> Hops { get; }
    public string Summary { get; }

    public RelationshipPath(string fromEntity, string toEntity, bool found, IReadOnlyList<RelationshipHop> hops, string summary)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromEntity);
        ArgumentException.ThrowIfNullOrEmpty(toEntity);
        Hops = hops ?? throw new ArgumentNullException(nameof(hops));
        ArgumentException.ThrowIfNullOrEmpty(summary);
        FromEntity = fromEntity;
        ToEntity = toEntity;
        Found = found;
        Hops = hops;
        Summary = summary;
    }
}

public sealed record DependencyOrder
{
    public IReadOnlyList<string> InsertOrder { get; }
    public IReadOnlyList<string> DeleteOrder { get; }
    public IReadOnlyList<string> CyclicEntities { get; }
    public IReadOnlyList<IReadOnlyList<string>> DetectedCycles { get; }

    public DependencyOrder(IReadOnlyList<string> insertOrder, IReadOnlyList<string> deleteOrder, IReadOnlyList<string> cyclicEntities, IReadOnlyList<IReadOnlyList<string>> detectedCycles)
    {
        InsertOrder = insertOrder ?? throw new ArgumentNullException(nameof(insertOrder));
        DeleteOrder = deleteOrder ?? throw new ArgumentNullException(nameof(deleteOrder));
        CyclicEntities = cyclicEntities ?? throw new ArgumentNullException(nameof(cyclicEntities));
        DetectedCycles = detectedCycles ?? throw new ArgumentNullException(nameof(detectedCycles));
    }
}