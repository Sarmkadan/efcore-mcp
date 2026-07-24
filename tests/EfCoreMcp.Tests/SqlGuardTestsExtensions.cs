using System;
using System.Collections.Generic;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Extension methods for <see cref="SqlGuardTests"/> that provide additional testing utilities
/// for SQL query validation scenarios.
/// </summary>
public static class SqlGuardTestsExtensions
{
    /// <summary>
    /// Validates that a SQL query is rejected with a specific rejection code.
    /// </summary>
    /// <param name="tests">The test instance (unused, for extension method syntax)</param>
    /// <param name="sql">The SQL query to validate</param>
    /// <param name="expectedCode">The expected rejection code</param>
    /// <returns>True if the query was rejected with the expected code; otherwise false</returns>
    /// <exception cref="ArgumentNullException">Thrown when sql is null</exception>
    public static bool ValidateRejectsWithCode(this SqlGuardTests tests, string sql, QueryRejectionCode expectedCode)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var rejection = SqlGuard.Validate(sql);
        return rejection?.Code == expectedCode;
    }

    /// <summary>
    /// Validates that a SQL query is accepted (not rejected) by the guard.
    /// </summary>
    /// <param name="tests">The test instance (unused, for extension method syntax)</param>
    /// <param name="sql">The SQL query to validate</param>
    /// <returns>True if the query was accepted; otherwise false</returns>
    /// <exception cref="ArgumentNullException">Thrown when sql is null</exception>
    public static bool ValidateAccepts(this SqlGuardTests tests, string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        return SqlGuard.Validate(sql) is null;
    }

    /// <summary>
    /// Gets all forbidden keywords that SqlGuard detects in read-only mode.
    /// </summary>
    /// <param name="tests">The test instance (unused, for extension method syntax)</param>
    /// <returns>An enumerable of all forbidden keywords</returns>
    public static IEnumerable<string> GetForbiddenKeywords(this SqlGuardTests tests)
    {
        return new[]
        {
            "insert", "update", "delete", "merge", "drop", "alter", "create",
            "truncate", "grant", "revoke", "exec", "execute", "attach", "detach",
            "pragma", "vacuum", "set", "commit", "rollback"
        };
    }

    /// <summary>
    /// Validates that a query with comments is properly handled by SqlGuard.
    /// </summary>
    /// <param name="tests">The test instance (unused, for extension method syntax)</param>
    /// <param name="baseQuery">The base SQL query without comments</param>
    /// <param name="comment">The comment text to append</param>
    /// <returns>True if the query with comment is accepted; otherwise false</returns>
    /// <exception cref="ArgumentNullException">Thrown when either parameter is null</exception>
    public static bool ValidateAcceptsWithComment(this SqlGuardTests tests, string baseQuery, string comment)
    {
        ArgumentNullException.ThrowIfNull(baseQuery);
        ArgumentNullException.ThrowIfNull(comment);

        var queryWithComment = $"{baseQuery} -- {comment}";
        return SqlGuard.Validate(queryWithComment) is null;
    }
}