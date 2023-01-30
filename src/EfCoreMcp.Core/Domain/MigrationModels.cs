namespace EfCoreMcp.Core.Domain;

public sealed record MigrationStatus(
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Pending,
    bool HasPendingModelChanges);

public sealed record ModelDiff(
    bool HasDifferences,
    IReadOnlyList<ModelDiffOperation> Operations);

public sealed record ModelDiffOperation(
    string OperationType,
    string? Table,
    string? Schema,
    string? Name,
    string Description);
