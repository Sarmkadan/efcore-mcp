using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Contains unit tests for the <see cref="SqlGuard"/> validation logic, 
/// verifying that it correctly allows read-only queries and rejects SQL mutations.
/// </summary>
public class SqlGuardTests : IEquatable<SqlGuardTests>
{
    /// <summary>
    /// Compares this <see cref="SqlGuardTests"/> instance with another for equality based on reference identity.
    /// </summary>
    /// <param name="other">The other instance to compare with.</param>
    /// <returns>True if the instances are the same, otherwise false.</returns>
    public bool Equals(SqlGuardTests? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return ReferenceEquals(this, other);
    }

    /// <summary>
    /// Compares this <see cref="SqlGuardTests"/> instance with an object for equality.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is the same instance, otherwise false.</returns>
    public override bool Equals(object? obj) => Equals(obj as SqlGuardTests);

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    /// <returns>The base hash code.</returns>
    public override int GetHashCode() => base.GetHashCode();

    /// <summary>
    /// Equality operator for <see cref="SqlGuardTests"/>.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns>True if they refer to the same instance.</returns>
    public static bool operator ==(SqlGuardTests? left, SqlGuardTests? right) =>
        ReferenceEquals(left, right);

    /// <summary>
    /// Inequality operator for <see cref="SqlGuardTests"/>.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns>True if they refer to different instances.</returns>
    public static bool operator !=(SqlGuardTests? left, SqlGuardTests? right) =>
        !ReferenceEquals(left, right);

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("select id, name from users")]
    [InlineData("SELECT * FROM orders WHERE total > 100 ORDER BY total DESC")]
    [InlineData("WITH recent AS (SELECT * FROM orders) SELECT * FROM recent")]
    [InlineData("SELECT * FROM users; ")]
    [InlineData("  SELECT 1  ")]
    /// <summary>
    /// Verifies that read-only SQL queries are allowed by the <see cref="SqlGuard"/>.
    /// </summary>
    /// <param name="sql">The SQL query string to validate.</param>
    public void Validate_AllowsReadOnlyQueries(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty("sql");
        Assert.Null(SqlGuard.Validate(sql));
    }

    [Theory]
    [InlineData("INSERT INTO users (name) VALUES ('x')")]
    [InlineData("UPDATE users SET name = 'x'")]
    [InlineData("DELETE FROM users")]
    [InlineData("DROP TABLE users")]
    [InlineData("TRUNCATE TABLE users")]
    [InlineData("ALTER TABLE users ADD col int")]
    [InlineData("CREATE TABLE t (id int)")]
    [InlineData("EXEC sp_who")]
    [InlineData("PRAGMA journal_mode = WAL")]
    [InlineData("VACUUM")]
    /// <summary>
    /// Verifies that various SQL mutations (INSERT, UPDATE, DELETE, etc.) are rejected by <see cref="SqlGuard"/>.
    /// </summary>
    /// <param name="sql">The SQL query string to validate.</param>
    public void Validate_RejectsMutations(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty("sql");
        Assert.NotNull(SqlGuard.Validate(sql));
    }

    [Theory]
    [InlineData("SELECT * INTO backup FROM users")]
    [InlineData("SELECT id FROM users; DELETE FROM users")]
    [InlineData("WITH x AS (SELECT 1) INSERT INTO t SELECT * FROM x")]
    /// <summary>
    /// Verifies that SQL queries containing sneaky or combined mutations are rejected by <see cref="SqlGuard"/>.
    /// </summary>
    /// <param name="sql">The SQL query string to validate.</param>
    public void Validate_RejectsSneakyWrites(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty("sql");
        Assert.NotNull(SqlGuard.Validate(sql));
    }

    [Fact]
    /// <summary>
    /// Verifies that queries containing multiple statements are rejected by <see cref="SqlGuard"/> with <see cref="QueryRejectionCode.MultipleStatements"/>.
    /// </summary>
    public void Validate_RejectsMultipleStatements()
    {
        var rejection = SqlGuard.Validate("SELECT 1; SELECT 2");
        Assert.NotNull(rejection);
        Assert.Equal(QueryRejectionCode.MultipleStatements, rejection.Code);
        Assert.Contains("Multiple statements", rejection.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    /// <summary>
    /// Verifies that empty or whitespace-only SQL inputs are rejected by <see cref="SqlGuard"/> with <see cref="QueryRejectionCode.EmptyQuery"/>.
    /// </summary>
    /// <param name="sql">The SQL query string to validate.</param>
    public void Validate_RejectsEmptyInput(string sql)
    {
        var rejection = SqlGuard.Validate(sql);
        Assert.NotNull(rejection);
        Assert.Equal(QueryRejectionCode.EmptyQuery, rejection.Code);
        Assert.Contains("empty", rejection.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies that passing a null SQL string to <see cref="SqlGuard.Validate(string)"/> throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    public void Validate_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => SqlGuard.Validate(null!));
    }

    [Fact]
    /// <summary>
    /// Verifies that SQL statements other than SELECT are rejected by <see cref="SqlGuard"/> with <see cref="QueryRejectionCode.NotSelect"/>.
    /// </summary>
    public void Validate_RejectsNonSelectStatements()
    {
        var rejection = SqlGuard.Validate("SHOW TABLES");
        Assert.NotNull(rejection);
        Assert.Equal(QueryRejectionCode.NotSelect, rejection.Code);
        Assert.Contains("Only SELECT", rejection.Reason);
    }

    [Fact]
    /// <summary>
    /// Verifies that forbidden keywords within SQL string literals do not trigger rejection.
    /// </summary>
    public void Validate_IgnoresKeywordsInsideStringLiterals()
    {
        Assert.Null(SqlGuard.Validate("SELECT * FROM logs WHERE message = 'please delete me'"));
    }

    [Fact]
    /// <summary>
    /// Verifies that forbidden keywords within escaped SQL string literals do not trigger rejection.
    /// </summary>
    public void Validate_IgnoresKeywordsInsideEscapedStringLiterals()
    {
        Assert.Null(SqlGuard.Validate("SELECT * FROM logs WHERE note = 'it''s an update log'"));
    }

    [Fact]
    /// <summary>
    /// Verifies that SQL line comments are correctly stripped before validation.
    /// </summary>
    public void Validate_StripsLineCommentsBeforeChecking()
    {
        Assert.Null(SqlGuard.Validate("SELECT 1 -- drop table users"));
    }

    [Fact]
    /// <summary>
    /// Verifies that SQL block comments are correctly stripped before validation.
    /// </summary>
    public void Validate_StripsBlockCommentsBeforeChecking()
    {
        Assert.Null(SqlGuard.Validate("SELECT /* delete */ 1"));
    }

    [Fact]
    /// <summary>
    /// Verifies that mutations hidden within comments still trigger rejection by <see cref="SqlGuard"/>.
    /// </summary>
    public void Validate_DoesNotLetCommentsHideMutations()
    {
        Assert.NotNull(SqlGuard.Validate("/* harmless */ DELETE FROM users"));
    }

    [Fact]
    /// <summary>
    /// Verifies that the reason returned by <see cref="SqlGuard.Validate(string)"/> includes the offending keyword.
    /// </summary>
    public void Validate_ReportsOffendingKeyword()
    {
        var rejection = SqlGuard.Validate("SELECT * FROM users WHERE id IN (DELETE FROM t)");
        Assert.NotNull(rejection);
        Assert.Contains("'delete'", rejection.Reason);
    }

    [Fact]
    /// <summary>
    /// Verifies that keywords as substrings of SQL identifiers do not trigger false-positive rejections.
    /// </summary>
    public void Validate_DoesNotFlagKeywordsAsSubstringsOfIdentifiers()
    {
        Assert.Null(SqlGuard.Validate("SELECT updated_at, deleted_flag FROM audit_log"));
        Assert.Null(SqlGuard.Validate("SELECT created_by FROM history"));
    }

    [Fact]
    /// <summary>
    /// Verifies that forbidden keywords return the correct <see cref="QueryRejectionCode.NotSelect"/> rejection code.
    /// </summary>
    public void Validate_ReturnsRejectionWithCodeForForbiddenKeyword()
    {
        var rejection = SqlGuard.Validate("DROP TABLE x");
        Assert.NotNull(rejection);
        // DROP TABLE x doesn't start with SELECT or WITH, so it's caught by NotSelect first
        Assert.Equal(QueryRejectionCode.NotSelect, rejection.Code);
        Assert.False(string.IsNullOrWhiteSpace(rejection.Reason));
    }

    [Fact]
    /// <summary>
    /// Verifies that write operations return the correct <see cref="QueryRejectionCode.ForbiddenKeyword"/> rejection code.
    /// </summary>
    public void Validate_ReturnsForbiddenKeywordCodeForWriteOperations()
    {
        var rejection = SqlGuard.Validate("INSERT INTO users VALUES (1)");
        Assert.NotNull(rejection);
        Assert.Equal(QueryRejectionCode.ForbiddenKeyword, rejection.Code);
        Assert.Contains("insert", rejection.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies that a valid SELECT query passes validation by <see cref="SqlGuard"/>.
    /// </summary>
    public void Validate_PassesValidQuery()
    {
        var result = SqlGuard.Validate("SELECT count(*) FROM users");
        Assert.Null(result);
    }
}
