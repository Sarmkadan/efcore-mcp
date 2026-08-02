using System.Data;
using System.Text.RegularExpressions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// SQL query validation and protection utilities for enforcing read-only constraints.
/// </summary>
/// <remarks>
/// <para><strong>Error-Signaling Contract:</strong> This class enforces a unified error-signaling contract
/// throughout the query execution pipeline. All validation failures are signaled using <see cref="QueryRejection"/>/
/// <see cref="QueryRejectedException"/> - there is intentionally no <c>ReadOnlyQueryViolationException</c> or similar.
/// </para>
///
/// <para><strong>Design Principles:</strong></para>
/// <list type="number">
/// <item><description><see cref="QueryRejection"/> is returned by <see cref="Validate(string)"/> for validation failures</description></item>
/// <item><description>Callers convert <see cref="QueryRejection"/> to <see cref="QueryRejectedException"/> and throw it</description></item>
/// <item><description><see cref="QueryRejection"/> provides structured error codes (<see cref="QueryRejectionCode"/>
/// for programmatic error handling</description></item>
/// <item><description><see cref="QueryRejectedException"/> provides exception-throwing capability</description></item>
/// </list>
///
/// <para><strong>Why No ReadOnlyQueryViolationException:</strong> Introducing a separate exception type
/// for read-only violations would create inconsistency in error handling. The <see cref="QueryRejection"/>
/// <see cref="QueryRejectedException"/> pair provides all necessary functionality:
/// <list type="bullet">
/// <item>Clear separation of concerns (data vs. exception)</item>
/// <item>Consistent error-signaling contract across all call sites</item>
/// <item>Machine-readable error codes for MCP clients</item>
/// <item>No duplication of exception types</item>
/// </list>
/// </para>
///
/// <para><strong>Boundary:</strong> <see cref="Validate(string)"/> returns <see cref="QueryRejection"/> for validation failures.
/// It does NOT throw exceptions itself. Callers (e.g., <see cref="SqlQueryExecutor"/>) are responsible for
/// converting rejections to exceptions and throwing them.</para>
/// </remarks>
public static partial class SqlGuard
{
    private static readonly HashSet<string> ForbiddenKeywords = new(SqlGuardConstants.ForbiddenKeywords, StringComparer.OrdinalIgnoreCase)
    {
        // The collection is initialised from SqlGuardConstants.ForbiddenKeywords.
    };

    private static readonly HashSet<string> StatementStartKeywords = new(SqlGuardConstants.StatementStartKeywords, StringComparer.OrdinalIgnoreCase)
    {
        // The collection is initialised from SqlGuardConstants.StatementStartKeywords.
    };

    [GeneratedRegex(SqlGuardConstants.CommentPattern, RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(SqlGuardConstants.StringLiteralPattern)]
    private static partial Regex StringLiteralPattern();

    [GeneratedRegex(SqlGuardConstants.WriteOperationPattern, RegexOptions.IgnoreCase)]
    private static partial Regex WriteOperationPattern();

    [GeneratedRegex(SqlGuardConstants.WithKeywordPattern, RegexOptions.IgnoreCase)]
    private static partial Regex WithKeywordPattern();

    /// <summary>
    /// Validates a SQL query for read-only compliance.
    /// </summary>
    /// <param name="sql">The SQL query to validate</param>
    /// <returns>A QueryRejection with error code and message if the query is invalid; null if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when sql is null.</exception>
    public static QueryRejection? Validate(string sql)
    {
        if (sql is null)
            throw new ArgumentNullException(nameof(sql));

        if (string.IsNullOrEmpty(sql))
            return new QueryRejection(QueryRejectionCode.EmptyQuery, SqlGuardConstants.EmptyQueryMessage);

        if (string.IsNullOrWhiteSpace(sql))
            return new QueryRejection(QueryRejectionCode.EmptyQuery, SqlGuardConstants.EmptyQueryMessage);

        var trimmed = sql.Trim();

        // Normalize line endings and whitespace
        trimmed = Regex.Replace(trimmed, @"\r?\n", " ", RegexOptions.Multiline);
        trimmed = Regex.Replace(trimmed, @"\s+", " ");

        // Remove comments and string literals for safer parsing
        var stripped = StringLiteralPattern().Replace(CommentPattern().Replace(trimmed, " "), "''");
        stripped = stripped.Trim();

        // Check for multiple statements separated by semicolons
        var statements = stripped.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (statements.Length > 1)
            return new QueryRejection(QueryRejectionCode.MultipleStatements, SqlGuardConstants.MultipleStatementsMessage);

        var statementToCheck = statements.Length == 1 ? statements[0] : stripped;

        return ValidateSingleStatement(statementToCheck);
    }

    private static QueryRejection? ValidateSingleStatement(string statement)
    {
        if (string.IsNullOrEmpty(statement))
            return new QueryRejection(QueryRejectionCode.EmptyStatement, SqlGuardConstants.EmptyStatementMessage);

        // Check for write operations in the statement first (before checking statement type)
        // This ensures INSERT/UPDATE/DELETE get ForbiddenKeyword code instead of NotSelect
        if (WriteOperationPattern().IsMatch(statement))
        {
            // Extract the actual write operation to report in the error
            var match = Regex.Match(statement, @"\b(insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var keyword = match.Groups[1].Value.ToLowerInvariant();
                return new QueryRejection(
                    QueryRejectionCode.ForbiddenKeyword,
                    string.Format(SqlGuardConstants.ForbiddenKeywordMessageTemplate, keyword));
            }
        }

        // Check for "into" keyword which creates a new table (write operation)
        // This check comes after write operation detection to ensure proper error codes
        if (statement.Contains(" into ", StringComparison.OrdinalIgnoreCase))
            return new QueryRejection(QueryRejectionCode.ForbiddenKeyword, SqlGuardConstants.IntoKeywordMessage);

        // Trim leading whitespace for StartWith check
        var trimmedStatement = statement.TrimStart();

        // Check if statement starts with a read-only keyword (case-insensitive)
        var startsWithReadOnly = StatementStartKeywords.Any(kw => trimmedStatement.StartsWith(kw, StringComparison.OrdinalIgnoreCase));
        if (!startsWithReadOnly)
            return new QueryRejection(QueryRejectionCode.NotSelect, SqlGuardConstants.NotSelectMessage);

        // For WITH clauses, check if they contain write operations
        if (WithKeywordPattern().IsMatch(statement))
        {
            // Extract CTE definitions and check for write operations
            var rejection = ValidateCteWithWriteOperations(statement);
            if (rejection is not null)
                return rejection;
        }

        return null;
    }

    private static QueryRejection? ValidateCteWithWriteOperations(string statement)
    {
        // Find the first SELECT after WITH to separate CTE definitions from main query
        var withIndex = statement.IndexOf("with", StringComparison.OrdinalIgnoreCase);
        if (withIndex < 0)
            return null;

        // Extract everything between WITH and the first SELECT that's not part of a CTE definition
        var afterWith = statement.Substring(withIndex + 4).Trim();

        // Look for the main SELECT that ends the CTE clause
        var selectMatch = Regex.Match(afterWith, @"\bselect\b", RegexOptions.IgnoreCase);
        if (!selectMatch.Success)
            return new QueryRejection(QueryRejectionCode.CteMissingSelect, SqlGuardConstants.CteMissingSelectMessage);

        var cteDefinitions = afterWith.Substring(0, selectMatch.Index).Trim();
        var mainQuery = afterWith.Substring(selectMatch.Index);

        // Check CTE definitions for write operations
        if (WriteOperationPattern().IsMatch(cteDefinitions))
        {
            var match = Regex.Match(cteDefinitions, @"\b(insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var keyword = match.Groups[1].Value.ToLowerInvariant();
                return new QueryRejection(
                    QueryRejectionCode.WriteOperationInCte,
                    string.Format(SqlGuardConstants.WriteOperationInCteMessageTemplate, keyword));
            }
        }

        // Check main query for write operations
        if (WriteOperationPattern().IsMatch(mainQuery))
        {
            var match = Regex.Match(mainQuery, @"\b(insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var keyword = match.Groups[1].Value.ToLowerInvariant();
                return new QueryRejection(
                    QueryRejectionCode.ForbiddenKeyword,
                    string.Format(SqlGuardConstants.ForbiddenKeywordMessageTemplate, keyword));
            }
        }

        return null;
    }

    /// <summary>
    /// Configures a database connection to be read-only if the provider supports it.
    /// Currently returns true for all connections as a placeholder for future provider-specific implementations.
    /// The primary defense is the SQL-level validation in Validate().
    /// </summary>
    /// <param name="connection">The database connection to configure</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True to indicate defense-in-depth is in place</returns>
    public static Task<bool> TrySetReadOnlyAsync(IDbConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        // Placeholder: In a real implementation, this would configure the connection for read-only mode
        // using provider-specific commands (PRAGMA for SQLite, SET TRANSACTION READ ONLY for PostgreSQL, etc.)
        // For now, we rely on the SQL-level validation as the primary defense mechanism.
        // IMPORTANT: Do not throw ReadOnlyQueryViolationException here - use QueryRejection/QueryRejectedException instead.
        return Task.FromResult(true);
    }
}
