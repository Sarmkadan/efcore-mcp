using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Xunit;

namespace EfCoreMcp.Tests;

public class SqlGuardTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("select id, name from users")]
    [InlineData("SELECT * FROM orders WHERE total > 100 ORDER BY total DESC")]
    [InlineData("WITH recent AS (SELECT * FROM orders) SELECT * FROM recent")]
    [InlineData("SELECT * FROM users; ")]
    [InlineData("  SELECT 1  ")]
    public void Validate_AllowsReadOnlyQueries(string sql)
    {
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
    public void Validate_RejectsMutations(string sql)
    {
        Assert.NotNull(SqlGuard.Validate(sql));
    }

    [Theory]
    [InlineData("SELECT * INTO backup FROM users")]
    [InlineData("SELECT id FROM users; DELETE FROM users")]
    [InlineData("WITH x AS (SELECT 1) INSERT INTO t SELECT * FROM x")]
    public void Validate_RejectsSneakyWrites(string sql)
    {
        Assert.NotNull(SqlGuard.Validate(sql));
    }

    [Fact]
    public void Validate_RejectsMultipleStatements()
    {
        var rejection = SqlGuard.Validate("SELECT 1; SELECT 2");
        Assert.NotNull(rejection);
        Assert.Contains("Multiple statements", rejection.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_RejectsEmptyInput(string? sql)
    {
        var rejection = SqlGuard.Validate(sql!);
        Assert.NotNull(rejection);
        Assert.Contains("empty", rejection.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsNonSelectStatements()
    {
        var rejection = SqlGuard.Validate("SHOW TABLES");
        Assert.NotNull(rejection);
        Assert.Contains("Only SELECT", rejection.Reason);
    }

    [Fact]
    public void Validate_IgnoresKeywordsInsideStringLiterals()
    {
        Assert.Null(SqlGuard.Validate("SELECT * FROM logs WHERE message = 'please delete me'"));
    }

    [Fact]
    public void Validate_IgnoresKeywordsInsideEscapedStringLiterals()
    {
        Assert.Null(SqlGuard.Validate("SELECT * FROM logs WHERE note = 'it''s an update log'"));
    }

    [Fact]
    public void Validate_StripsLineCommentsBeforeChecking()
    {
        Assert.Null(SqlGuard.Validate("SELECT 1 -- drop table users"));
    }

    [Fact]
    public void Validate_StripsBlockCommentsBeforeChecking()
    {
        Assert.Null(SqlGuard.Validate("SELECT /* delete */ 1"));
    }

    [Fact]
    public void Validate_DoesNotLetCommentsHideMutations()
    {
        Assert.NotNull(SqlGuard.Validate("/* harmless */ DELETE FROM users"));
    }

    [Fact]
    public void Validate_ReportsOffendingKeyword()
    {
        var rejection = SqlGuard.Validate("SELECT * FROM users WHERE id IN (DELETE FROM t)");
        Assert.NotNull(rejection);
        Assert.Contains("'delete'", rejection.Reason);
    }

    [Fact]
    public void Validate_DoesNotFlagKeywordsAsSubstringsOfIdentifiers()
    {
        Assert.Null(SqlGuard.Validate("SELECT updated_at, deleted_flag FROM audit_log"));
        Assert.Null(SqlGuard.Validate("SELECT created_by FROM history"));
    }

    [Fact]
    public void ValidateOrThrow_ThrowsWithReasonOnViolation()
    {
        var ex = Assert.Throws<ReadOnlyQueryViolationException>(() => SqlGuard.ValidateOrThrow("DROP TABLE x"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Reason));
    }

    [Fact]
    public void ValidateOrThrow_PassesValidQuery()
    {
        SqlGuard.ValidateOrThrow("SELECT count(*) FROM users");
    }
}
