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
public sealed record SqlQueryRequest
{
    /// <summary>
    /// Gets the SQL query to execute.
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// Gets the query limits with defaults applied if not specified.
    /// </summary>
    public QueryLimits Limits { get; init; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlQueryRequest"/> class.
    /// </summary>
    /// <param name="sql">The SQL query to execute. Must not be null, empty, or whitespace.</param>
    /// <param name="limits">Query execution limits including row count and timeout</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="sql"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="sql"/> is empty or whitespace.</exception>
    public SqlQueryRequest(string sql, QueryLimits? limits = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql, nameof(sql));
        Sql = sql;
        Limits = limits ?? new QueryLimits();
    }
}

/// <summary>
/// Entity query request with explicit row limits and timeout constraints.
/// </summary>
public sealed record EntityQueryRequest
{
    /// <summary>
    /// Gets the name of the entity to query.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets the query limits with defaults applied and values clamped to allowed ranges.
    /// </summary>
    public QueryLimits Limits { get; init; } = new();

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
    public string? OrderBy { get; init; }

    /// <summary>
    /// Gets whether to order descending.
    /// </summary>
    public bool OrderDescending { get; init; }

    /// <summary>
    /// Gets the optional filter expression to apply to the query.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// Gets the parameters for the filter expression.
    /// </summary>
    public IReadOnlyDictionary<string, object>? FilterParameters { get; init; }

    /// <summary>
    /// Validates the request inputs.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when entityName is null or empty.</exception>
    /// <exception cref="QueryRejectedException">Thrown when limits are invalid.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(EntityName);

        if (Limits.MaxRows < 1 || Limits.MaxRows > 10_000)
            throw new QueryRejectedException(new QueryRejection(QueryRejectionCode.LimitExceeded, $"MaxRows must be between 1 and 10000. Got: {Limits.MaxRows}"));
        if (Limits.TimeoutSeconds < 1 || Limits.TimeoutSeconds > 300)
            throw new QueryRejectedException(new QueryRejection(QueryRejectionCode.LimitExceeded, $"TimeoutSeconds must be between 1 and 300. Got: {Limits.TimeoutSeconds}"));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityQueryRequest"/> class.
    /// </summary>
    /// <param name="entityName">Name of the entity to query. Must not be null, empty, or whitespace.</param>
    /// <param name="limits">Query execution limits including row count and timeout</param>
    /// <param name="orderBy">Property name to order by</param>
    /// <param name="orderDescending">Whether to order descending</param>
    /// <param name="filter">Optional filter expression to apply to the query</param>
    /// <param name="filterParameters">Parameters for the filter expression</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="entityName"/> is empty or whitespace.</exception>
    public EntityQueryRequest(
        string entityName,
        QueryLimits? limits = null,
        string? orderBy = null,
        bool orderDescending = false,
        string? filter = null,
        IReadOnlyDictionary<string, object>? filterParameters = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(entityName, nameof(entityName));
        EntityName = entityName;
        Limits = new QueryLimits(
            Math.Clamp(limits?.MaxRows ?? 100, 1, 10000),
            Math.Clamp(limits?.TimeoutSeconds ?? 30, 1, 300));
        OrderBy = orderBy;
        OrderDescending = orderDescending;
        Filter = filter;
        FilterParameters = filterParameters;
    }
}

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
/// <remarks>
/// <para>This exception is the standard mechanism for signaling query validation failures to callers.
/// It wraps a <see cref="QueryRejection"/> which contains both machine-readable error codes (<see cref="QueryRejection.Code"/>)
/// and human-readable error messages (<see cref="QueryRejection.Reason"/>).</para>
///
/// <para><strong>Error-Signaling Contract:</strong> The codebase uses a unified error-signaling contract
/// where <see cref="SqlGuard.Validate(string)"/> returns a <see cref="QueryRejection"/> for validation failures.
/// Callers convert this to a <see cref="QueryRejectedException"/> and throw it. This design ensures consistency:
/// <list type="bullet">
/// <item>All validation failures use <see cref="QueryRejection"/>
/// <see cref="QueryRejectedException"/> - no other exception types for read-only violations</item>
/// <item><see cref="QueryRejection"/> provides structured error information with codes</item>
/// <item><see cref="QueryRejectedException"/> provides exception-throwing capability</item>
/// </list>
/// </para>
///
/// <para><strong>Design Decision:</strong> There is intentionally no <c>ReadOnlyQueryViolationException</c>.
/// The <see cref="QueryRejection"/>
/// <see cref="QueryRejectedException"/> pair provides all necessary functionality without duplication.
/// Any attempt to introduce a separate exception type for read-only violations would violate this contract
/// and create inconsistency in error handling.</para>
/// </remarks>
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
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="rejection"/> is null</exception>
    public QueryRejectedException(QueryRejection rejection) : base(rejection.Reason)
    {
        Rejection = rejection ?? throw new ArgumentNullException(nameof(rejection));
    }

    /// <summary>
    /// Creates an exception from a rejection with additional context.
    /// </summary>
    /// <param name="rejection">The rejection information</param>
    /// <param name="message">Additional context message</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="rejection"/> is null</exception>
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
/// This is the primary mechanism for signaling query validation failures in the SqlGuard validation system.
/// It is used as a discriminated union with <see cref="QueryResult"/> to provide consistent error handling.
/// </summary>
/// <remarks>
/// <para>QueryRejection is returned by <see cref="SqlGuard.Validate(string)"/> for validation failures.
/// Callers should convert this to a <see cref="QueryRejectedException"/> and throw it to signal the error to callers.
/// </para>
/// <para>This design ensures a single, consistent error-signaling contract throughout the query execution pipeline.
/// There is no need for a separate <c>ReadOnlyQueryViolationException</c> - the <see cref="QueryRejection"/>
/// <see cref="QueryRejectedException"/> pair provides all necessary functionality with clear separation of concerns:
/// <list type="bullet">
/// <item><see cref="QueryRejection"/> carries machine-readable error codes and human-readable messages</item>
/// <item><see cref="QueryRejectedException"/> wraps the rejection for throwing as an exception</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Code">Machine-readable error code for programmatic handling.</param>
/// <param name="Reason">Human-readable error message for display.</param>
public sealed record QueryRejection(QueryRejectionCode Code, string Reason);