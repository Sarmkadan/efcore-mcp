using System.Text.RegularExpressions;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Enforces <see cref="RuntimeLimitsOptions"/> on tool requests, rejecting requests that exceed a limit.
/// </summary>
/// <remarks>
/// Limit violations are reported with <see cref="QueryRejectionCode.LimitExceeded"/> or
/// <see cref="QueryRejectionCode.SchemaNotAllowed"/> through <see cref="QueryRejectedException"/>, so MCP clients
/// can handle them the same way as read-only violations. Schema checks on raw SQL are best-effort: they look for
/// schema-qualified names of schemas known from the model and do not parse the statement.
/// </remarks>
public sealed partial class RuntimeLimitGuard : IRuntimeLimitGuard
{
    /// <summary>Row count used when a query does not specify <c>maxRows</c>, before the configured cap is applied.</summary>
    private const int DefaultRequestedRows = 100;

    private readonly RuntimeLimitsOptions limits;
    private readonly IModelIntrospector introspector;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeLimitGuard"/> class and validates the limits.
    /// </summary>
    /// <param name="limits">The runtime limits to enforce.</param>
    /// <param name="introspector">The model introspector used to resolve entity schemas.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric limit is out of range.</exception>
    /// <exception cref="ArgumentException">Thrown when the schema allow-list is invalid.</exception>
    public RuntimeLimitGuard(RuntimeLimitsOptions limits, IModelIntrospector introspector)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(introspector);
        limits.Validate();
        this.limits = limits;
        this.introspector = introspector;
    }

    [GeneratedRegex(@"(?<![\w.])(?:\[([^\]]+)\]|""([^""]+)""|([A-Za-z_]\w*))\s*\.")]
    private static partial Regex SchemaQualifierPattern();

    /// <inheritdoc />
    public int ResolveMaxRows(int? requested)
    {
        if (requested is null)
            return Math.Min(DefaultRequestedRows, limits.MaxRows);

        if (requested.Value < 1)
            throw new ArgumentOutOfRangeException("maxRows", $"maxRows must be at least 1. Got: {requested.Value}.");

        if (requested.Value > limits.MaxRows)
            throw Limit($"maxRows {requested.Value} exceeds the configured limit of {limits.MaxRows}. Lower maxRows, or raise --max-rows (EFCORE_MCP_MAX_ROWS).");

        return requested.Value;
    }

    /// <inheritdoc />
    public void EnsureQueryLength(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (sql.Length > limits.MaxQueryLength)
            throw Limit($"SQL is {sql.Length} characters, which exceeds the configured limit of {limits.MaxQueryLength}. Shorten the query, or raise --max-query-length (EFCORE_MCP_MAX_QUERY_LENGTH).");
    }

    /// <inheritdoc />
    public void EnsureEntitySchemaAllowed(string entityName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        var entity = introspector.DescribeEntity(entityName);
        if (entity is null || limits.IsSchemaAllowed(entity.Schema))
            return;

        throw SchemaNotAllowed($"Entity '{entity.Name}' is in schema '{entity.Schema ?? RuntimeLimitsOptions.DefaultSchemaToken}'");
    }

    /// <inheritdoc />
    public void EnsureSqlSchemasAllowed(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (limits.AllowedSchemas is null)
            return;

        var disallowed = introspector.DescribeModel().Entities
            .Select(e => e.Schema)
            .OfType<string>()
            .Where(schema => !limits.IsSchemaAllowed(schema))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (disallowed.Count == 0)
            return;

        foreach (Match match in SchemaQualifierPattern().Matches(sql))
        {
            var group = match.Groups[1].Success ? match.Groups[1] : match.Groups[2].Success ? match.Groups[2] : match.Groups[3];
            if (disallowed.Contains(group.Value))
                throw SchemaNotAllowed($"SQL references schema '{group.Value}'");
        }
    }

    /// <inheritdoc />
    public void EnsureRelationshipEndpointsAllowed(string fromEntity, string toEntity)
    {
        EnsureEntitySchemaAllowed(fromEntity);
        EnsureEntitySchemaAllowed(toEntity);
    }

    /// <inheritdoc />
    public int ResolveAnalysisDepth(int? requested)
    {
        if (requested is null)
            return limits.MaxAnalysisDepth;

        if (requested.Value < 1)
            throw new ArgumentOutOfRangeException("maxHops", $"maxHops must be at least 1. Got: {requested.Value}.");

        if (requested.Value > limits.MaxAnalysisDepth)
            throw new ArgumentOutOfRangeException(
                "maxHops",
                $"maxHops {requested.Value} exceeds the configured analysis depth of {limits.MaxAnalysisDepth}. Lower maxHops, or raise --max-analysis-depth (EFCORE_MCP_MAX_ANALYSIS_DEPTH).");

        return requested.Value;
    }

    private static QueryRejectedException Limit(string reason) =>
        new(new QueryRejection(QueryRejectionCode.LimitExceeded, reason));

    private QueryRejectedException SchemaNotAllowed(string subject) =>
        new(new QueryRejection(
            QueryRejectionCode.SchemaNotAllowed,
            $"{subject}, which is not in the allowed schemas ({string.Join(", ", limits.AllowedSchemas ?? Array.Empty<string>())}). Add it with --allowed-schemas (EFCORE_MCP_ALLOWED_SCHEMAS)."));
}
