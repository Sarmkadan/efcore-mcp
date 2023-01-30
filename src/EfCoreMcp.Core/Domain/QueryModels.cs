namespace EfCoreMcp.Core.Domain;

public sealed record SqlQueryRequest(string Sql, int MaxRows = 100, int TimeoutSeconds = 30);

public sealed record EntityQueryRequest(
    string EntityName,
    int Take = 50,
    int Skip = 0,
    string? OrderBy = null,
    bool OrderDescending = false);

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    bool Truncated,
    long ElapsedMilliseconds);

public sealed record QueryRejection(string Reason);

public sealed class ReadOnlyQueryViolationException(string reason) : InvalidOperationException(reason)
{
    public string Reason { get; } = reason;
}
