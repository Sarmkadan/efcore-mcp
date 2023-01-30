using System.Text.RegularExpressions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

public static partial class SqlGuard
{
    private static readonly string[] ForbiddenKeywords =
    [
        "insert", "update", "delete", "merge", "drop", "alter", "create",
        "truncate", "grant", "revoke", "exec", "execute", "attach", "detach",
        "pragma", "vacuum", "into"
    ];

    [GeneratedRegex(@"--.*?$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"'(?:[^']|'')*'")]
    private static partial Regex StringLiteralPattern();

    public static QueryRejection? Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new QueryRejection("Query is empty.");
        var stripped = StringLiteralPattern().Replace(CommentPattern().Replace(sql, " "), "''");
        var statements = stripped.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (statements.Length > 1)
            return new QueryRejection("Multiple statements are not allowed.");
        var trimmed = statements.Length == 1 ? statements[0] : stripped.Trim();
        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
            return new QueryRejection("Only SELECT (or WITH ... SELECT) queries are allowed.");
        var tokens = Regex.Split(trimmed.ToLowerInvariant(), @"[^a-z_]+");
        var hit = tokens.FirstOrDefault(t => ForbiddenKeywords.Contains(t));
        return hit is null ? null : new QueryRejection($"Keyword '{hit}' is not allowed in read-only mode.");
    }

    public static void ValidateOrThrow(string sql)
    {
        if (Validate(sql) is { } rejection)
            throw new ReadOnlyQueryViolationException(rejection.Reason);
    }
}
