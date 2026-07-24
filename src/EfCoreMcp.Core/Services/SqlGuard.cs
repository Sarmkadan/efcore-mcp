using System.Data;
using System.Text.RegularExpressions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

public static partial class SqlGuard
{
	private static readonly HashSet<string> ForbiddenKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"insert", "update", "delete", "merge", "drop", "alter", "create",
		"truncate", "grant", "revoke", "exec", "execute", "attach", "detach",
		"pragma", "vacuum", "into", "set", "commit", "rollback"
	};

	private static readonly HashSet<string> StatementStartKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"select", "with"
	};

	[GeneratedRegex(@"--.*?$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline)]
	private static partial Regex CommentPattern();

	[GeneratedRegex(@"'(?:[^']|'')*'")]
	private static partial Regex StringLiteralPattern();

	[GeneratedRegex(@"\b(?:insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase)]
	private static partial Regex WriteOperationPattern();

	[GeneratedRegex(@"\bwith\b", RegexOptions.IgnoreCase)]
	private static partial Regex WithKeywordPattern();

	public static QueryRejection? Validate(string sql)
	{
		if (string.IsNullOrWhiteSpace(sql))
			return new QueryRejection("Query is empty.");

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
			return new QueryRejection("Multiple statements are not allowed.");

		var statementToCheck = statements.Length == 1 ? statements[0] : stripped;

		return ValidateSingleStatement(statementToCheck);
	}

	private static QueryRejection? ValidateSingleStatement(string statement)
	{
		if (string.IsNullOrEmpty(statement))
			return new QueryRejection("Statement is empty after normalization.");

		// Check for "into" keyword which creates a new table (write operation)
		if (statement.Contains(" into ", StringComparison.OrdinalIgnoreCase))
			return new QueryRejection("Keyword 'into' is not allowed in read-only mode.");

		// Trim leading whitespace for StartWith check
		var trimmedStatement = statement.TrimStart();

		// Check if statement starts with a read-only keyword (case-insensitive)
		var startsWithReadOnly = StatementStartKeywords.Any(kw => trimmedStatement.StartsWith(kw, StringComparison.OrdinalIgnoreCase));
		if (!startsWithReadOnly)
			return new QueryRejection("Only SELECT (or WITH ... SELECT) queries are allowed.");

		// For WITH clauses, check if they contain write operations
		if (WithKeywordPattern().IsMatch(statement))
		{
			// Extract CTE definitions and check for write operations
			var rejection = ValidateCteWithWriteOperations(statement);
			if (rejection is not null)
				return rejection;
		}

		// Check for write operations in the statement
		if (WriteOperationPattern().IsMatch(statement))
		{
			// Extract the actual write operation to report in the error
			var match = Regex.Match(statement, @"\b(insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				var keyword = match.Groups[1].Value.ToLowerInvariant();
				return new QueryRejection($"Keyword '{keyword}' is not allowed in read-only mode.");
			}
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
			return new QueryRejection("WITH clause must be followed by a SELECT statement.");

		var cteDefinitions = afterWith.Substring(0, selectMatch.Index).Trim();
		var mainQuery = afterWith.Substring(selectMatch.Index);

		// Check CTE definitions for write operations
		if (WriteOperationPattern().IsMatch(cteDefinitions))
		{
			var match = Regex.Match(cteDefinitions, @"\b(insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				var keyword = match.Groups[1].Value.ToLowerInvariant();
				return new QueryRejection($"Write operation '{keyword}' in CTE definition is not allowed in read-only mode.");
			}
		}

		// Check main query for write operations
		if (WriteOperationPattern().IsMatch(mainQuery))
		{
			var match = Regex.Match(mainQuery, @"\b(insert|update|delete|merge|drop|alter|create|truncate)\b", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				var keyword = match.Groups[1].Value.ToLowerInvariant();
				return new QueryRejection($"Keyword '{keyword}' is not allowed in read-only mode.");
			}
		}

		return null;
	}

	public static void ValidateOrThrow(string sql)
	{
		if (Validate(sql) is { } rejection)
			throw new ReadOnlyQueryViolationException(rejection.Reason);
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
		return Task.FromResult(true);
	}
}
