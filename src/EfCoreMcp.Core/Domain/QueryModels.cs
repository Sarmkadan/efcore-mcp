namespace EfCoreMcp.Core.Domain;

/// <summary>
/// Common query execution limits applied to both SQL and entity queries.
/// Ensures consistent safety semantics across all query execution paths.
/// </summary>
/// <param name="MaxRows">Maximum number of rows to return (clamped to 1-10000)</param>
/// <param name="TimeoutSeconds">Maximum execution timeout in seconds (clamped to 1-300)</param>
public sealed record QueryLimits(int MaxRows = 100, int TimeoutSeconds = 30);

/// <summary>
/// SQL query request with explicit row limits and timeout constraints.
/// </summary>
/// <param name="Sql">The SQL query to execute</param>
/// <param name="limits">Query execution limits including row count and timeout</param>
public sealed record SqlQueryRequest(string Sql, QueryLimits? limits = null)
{
    /// <summary>
    /// Gets the query limits with defaults applied if not specified.
    /// </summary>
    public QueryLimits Limits => limits ?? new QueryLimits();
}

/// <summary>
/// Entity query request with explicit row limits and timeout constraints.
/// </summary>
/// <param name="entityName">Name of the entity to query</param>
/// <param name="limits">Query execution limits including row count and timeout</param>
/// <param name="orderBy">Property name to order by</param>
/// <param name="orderDescending">Whether to order descending</param>
/// <param name="filter">Optional filter expression to apply to the query</param>
/// <param name="filterParameters">Parameters for the filter expression</param>
public sealed record EntityQueryRequest(
    string entityName,
    QueryLimits? limits = null,
    string? orderBy = null,
    bool orderDescending = false,
    string? filter = null,
    IReadOnlyDictionary<string, object>? filterParameters = null)
{
    /// <summary>
    /// Gets the name of the entity to query.
    /// </summary>
    public string EntityName { get; init; } = entityName ?? throw new ArgumentNullException(nameof(entityName));

    /// <summary>
    /// Gets the query limits with defaults applied if not specified.
    /// </summary>
    public QueryLimits Limits => limits ?? new QueryLimits();

    /// <summary>
    /// Gets the effective take value for this query.
    /// </summary>
    public int Take => Limits.MaxRows;

    /// <summary>
    /// Gets the effective skip value for this query.
    /// </summary>
    public int Skip { get; init; } = 0;

    /// <summary>
    /// Gets the property name to order by.
    /// </summary>
    public string? OrderBy { get; init; } = orderBy;

    /// <summary>
    /// Gets whether to order descending.
    /// </summary>
    public bool OrderDescending { get; init; } = orderDescending;

    /// <summary>
    /// Gets the optional filter expression to apply to the query.
    /// </summary>
    public string? Filter { get; init; } = filter;

    /// <summary>
    /// Gets the parameters for the filter expression.
    /// </summary>
    public IReadOnlyDictionary<string, object>? FilterParameters { get; init; } = filterParameters;
};

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    bool Truncated,
    long ElapsedMilliseconds);

/// <summary>
/// Result of an execution plan analysis showing the query plan and performance heuristics.
/// </summary>
/// <param name="ExecutionPlan">The raw execution plan text from the database engine.</param>
/// <param name="Summary">Human-readable summary of performance issues detected in the plan.</param>
/// <param name="Query">The original query being analyzed.</param>
public sealed record ExecutionPlanResult(
    string ExecutionPlan,
    string Summary,
    string Query);

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
    TimeoutConstraint,

    /// <summary>Limit constraint violation.</summary>
    LimitExceeded
}

/// <summary>
/// Represents a rejected query with machine-readable error code and human-readable reason.
/// Used as a discriminated union with QueryResult to provide consistent error handling.
/// </summary>
/// <param name="Code">Machine-readable error code for programmatic handling.</param>
/// <param name="Reason">Human-readable error message for display.</param>
public sealed record QueryRejection(QueryRejectionCode Code, string Reason);
