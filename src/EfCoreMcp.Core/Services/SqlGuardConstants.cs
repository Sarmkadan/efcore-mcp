using System.Collections.Generic;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Centralised constants for <see cref="SqlGuard"/> to avoid magic strings and numbers.
/// </summary>
internal static class SqlGuardConstants
{
    // -------------------------------------------------------------------------
    // Keyword collections
    // -------------------------------------------------------------------------

    /// <summary>
    /// Keywords that are forbidden in read‑only mode.
    /// </summary>
    public static readonly string[] ForbiddenKeywords = new[]
    {
        "insert", "update", "delete", "merge", "drop", "alter", "create",
        "truncate", "grant", "revoke", "exec", "execute", "attach", "detach",
        "pragma", "vacuum", "set", "commit", "rollback"
    };

    /// <summary>
    /// Keywords that a statement may start with to be considered a read‑only query.
    /// </summary>
    public static readonly string[] StatementStartKeywords = new[]
    {
        "select", "with"
    };

    // -------------------------------------------------------------------------
    // Regular expression patterns
    // -------------------------------------------------------------------------

    public const string CommentPattern = @"--.*?$|/\*.*?\*/";
    public const string StringLiteralPattern = @"'(?:[^']|'')*'";
    public const string WriteOperationPattern = @"\b(?:insert|update|delete|merge|drop|alter|create|truncate)\b";
    public const string WithKeywordPattern = @"\bwith\b";

    // -------------------------------------------------------------------------
    // Validation messages
    // -------------------------------------------------------------------------

    public const string EmptyQueryMessage = "Query is empty.";
    public const string MultipleStatementsMessage = "Multiple statements are not allowed.";
    public const string EmptyStatementMessage = "Statement is empty after normalization.";
    public const string ForbiddenKeywordMessageTemplate = "Keyword '{0}' is not allowed in read-only mode.";
    public const string IntoKeywordMessage = "Keyword 'into' is not allowed in read-only mode.";
    public const string NotSelectMessage = "Only SELECT (or WITH ... SELECT) queries are allowed.";
    public const string CteMissingSelectMessage = "WITH clause must be followed by a SELECT statement.";
    public const string WriteOperationInCteMessageTemplate = "Write operation '{0}' in CTE definition is not allowed in read-only mode.";
}
