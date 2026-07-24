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

/// <summary>
/// Exception thrown when a SQL query is rejected by validation.
/// This exception carries machine-readable rejection codes that MCP clients can use for programmatic error handling.
/// </summary>
/// <param name="rejection">The rejection information containing error code and message</param>
public sealed class QueryRejectedException : Exception
{
    /// <summary>Gets the rejection information with machine-readable error code.</summary>
    public QueryRejection Rejection { get; }

    /// <summary>Gets the machine-readable rejection code.</summary>
    public QueryRejectionCode Code => Rejection.Code;

    /// <summary>
    /// Initializes a new instance of the QueryRejectedException class.
    /// </summary>
    /// <param name="rejection">The rejection information containing error code and message</param>
    public QueryRejectedException(QueryRejection rejection) : base(rejection.Reason)
    {
        Rejection = rejection ?? throw new ArgumentNullException(nameof(rejection));
    }

    /// <summary>
    /// Creates an exception from a rejection with additional context.
    /// </summary>
    /// <param name="rejection">The rejection information</param>
    /// <param name="message">Additional context message</param>
    public QueryRejectedException(QueryRejection rejection, string message) : base($"{message}: {rejection.Reason}")
    {
        Rejection = rejection ?? throw new ArgumentNullException(nameof(rejection));
    }
}

/// <summary>
/// Machine-readable rejection codes for SQL query validation failures.
/// Allows MCP clients to branch on specific error types instead of parsing strings.
/// </summary>
public enum QueryRejectionCode
{
    /// <summary>Query is empty or whitespace.</summary>
    EmptyQuery,

    /// <summary>Multiple SQL statements separated by semicolons.</summary>
    MultipleStatements,

    /// <summary>Query is not a SELECT statement.</summary>
    NotSelect,

    /// <summary>Forbidden keyword detected in read-only mode.</summary>
    ForbiddenKeyword,

    /// <summary>Statement is empty after normalization.</summary>
    EmptyStatement,

    /// <summary>CTE (WITH clause) must be followed by SELECT.</summary>
    CteMissingSelect,

    /// <summary>Write operation in CTE definition.</summary>
    WriteOperationInCte,

    /// <summary>Timeout constraint violation.</summary>
    TimeoutConstraint
}

/// <summary>
/// Represents a rejected query with machine-readable error code and human-readable reason.
/// Used as a discriminated union with QueryResult to provide consistent error handling.
/// </summary>
/// <param name="Code">Machine-readable error code for programmatic handling.</param>
/// <param name="Reason">Human-readable error message for display.</param>
public sealed record QueryRejection(QueryRejectionCode Code, string Reason);
