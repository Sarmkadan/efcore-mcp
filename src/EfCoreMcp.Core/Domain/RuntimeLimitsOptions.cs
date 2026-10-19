using System.Text.RegularExpressions;
using EfCoreMcp.Core.Services;

namespace EfCoreMcp.Core.Domain;

/// <summary>
/// Runtime limits enforced by the query and analysis tools.
/// </summary>
/// <remarks>
/// Values come from the command line or environment and are checked once at startup by <see cref="Validate"/>,
/// so an out-of-range value stops the server before it accepts requests.
/// </remarks>
public sealed record RuntimeLimitsOptions
{
    /// <summary>Default and upper bound for <see cref="MaxRows"/>. Matches the executor's hard limit.</summary>
    public const int DefaultMaxRows = 10_000;

    /// <summary>Upper bound accepted for <see cref="MaxRows"/>.</summary>
    public const int MaxRowsCeiling = 10_000;

    /// <summary>Default for <see cref="MaxQueryLength"/>, in characters.</summary>
    public const int DefaultMaxQueryLength = 10_000;

    /// <summary>Upper bound accepted for <see cref="MaxQueryLength"/>, in characters.</summary>
    public const int MaxQueryLengthCeiling = 1_000_000;

    /// <summary>Default for <see cref="MaxAnalysisDepth"/>. Matches the relationship search's built-in depth.</summary>
    public const int DefaultMaxAnalysisDepth = RelationshipAnalyzer.MaxPathFindingDepth;

    /// <summary>
    /// Name that matches entities without an explicit schema (for example, SQLite tables) in <see cref="AllowedSchemas"/>.
    /// </summary>
    public const string DefaultSchemaToken = "default";

    private static readonly Regex SchemaNamePattern = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    /// <summary>Maximum number of rows a query tool may return. Range: 1 to <see cref="MaxRowsCeiling"/>.</summary>
    public int MaxRows { get; init; } = DefaultMaxRows;

    /// <summary>Maximum length of a SQL statement in characters. Range: 1 to <see cref="MaxQueryLengthCeiling"/>.</summary>
    public int MaxQueryLength { get; init; } = DefaultMaxQueryLength;

    /// <summary>
    /// Schemas that query and relationship tools may touch. <see langword="null"/> allows every schema.
    /// When set, the list must be non-empty and each entry must be a plain identifier.
    /// </summary>
    public IReadOnlyList<string>? AllowedSchemas { get; init; }

    /// <summary>Maximum number of foreign-key hops explored by relationship analysis. Range: 1 to <see cref="DefaultMaxAnalysisDepth"/>.</summary>
    public int MaxAnalysisDepth { get; init; } = DefaultMaxAnalysisDepth;

    /// <summary>
    /// Returns whether a schema is permitted. An entity with no explicit schema is matched by <see cref="DefaultSchemaToken"/>.
    /// </summary>
    /// <param name="schema">The schema name, or <see langword="null"/> when the entity has none.</param>
    /// <returns><see langword="true"/> if no allow-list is configured or the schema is on it; otherwise <see langword="false"/>.</returns>
    public bool IsSchemaAllowed(string? schema) =>
        AllowedSchemas is null || AllowedSchemas.Contains(schema ?? DefaultSchemaToken, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks that every value is within its allowed range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric limit is outside its range.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="AllowedSchemas"/> is empty or contains an invalid name.</exception>
    public void Validate()
    {
        if (MaxRows < 1 || MaxRows > MaxRowsCeiling)
            throw new ArgumentOutOfRangeException(nameof(MaxRows), $"--max-rows must be between 1 and {MaxRowsCeiling}. Got: {MaxRows}.");

        if (MaxQueryLength < 1 || MaxQueryLength > MaxQueryLengthCeiling)
            throw new ArgumentOutOfRangeException(nameof(MaxQueryLength), $"--max-query-length must be between 1 and {MaxQueryLengthCeiling}. Got: {MaxQueryLength}.");

        if (MaxAnalysisDepth < 1 || MaxAnalysisDepth > DefaultMaxAnalysisDepth)
            throw new ArgumentOutOfRangeException(nameof(MaxAnalysisDepth), $"--max-analysis-depth must be between 1 and {DefaultMaxAnalysisDepth}. Got: {MaxAnalysisDepth}.");

        if (AllowedSchemas is null)
            return;

        if (AllowedSchemas.Count == 0)
            throw new ArgumentException("--allowed-schemas must name at least one schema. Omit the option to allow all schemas.", nameof(AllowedSchemas));

        foreach (var schema in AllowedSchemas)
        {
            if (schema is null || !SchemaNamePattern.IsMatch(schema))
                throw new ArgumentException(
                    $"Schema name '{schema}' in --allowed-schemas is not a valid identifier. Use letters, digits and underscores, starting with a letter or underscore.",
                    nameof(AllowedSchemas));
        }
    }
}
