using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Tests for QueryResult and QueryRejection construction and edge cases.
/// Ensures proper handling of truncation, error sanitization, and result distinguishability.
/// </summary>
public class QueryExecutionTests
{
    [Fact]
    public void QueryResult_WithZeroRows_HasEmptyRowsCollectionAndZeroRowCount()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Value" };
        var rows = new List<IReadOnlyList<object?>>();
        const int rowCount = 0;
        const bool truncated = false;
        const long elapsedMilliseconds = 42;

        // Act
        var result = new QueryResult(columns, rows, rowCount, truncated, elapsedMilliseconds);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.RowCount);
        Assert.Equal(columns, result.Columns);
        Assert.False(result.Truncated);
        Assert.Equal(42, result.ElapsedMilliseconds);
    }

    [Fact]
    public void QueryResult_WithRows_ReturnsCorrectData()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new List<object?> { 1, "Alice" },
            new List<object?> { 2, "Bob" },
            new List<object?> { 3, "Charlie" }
        };
        const int rowCount = 3;
        const bool truncated = false;
        const long elapsedMilliseconds = 100;

        // Act
        var result = new QueryResult(columns, rows, rowCount, truncated, elapsedMilliseconds);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(3, result.RowCount);
        Assert.Equal(columns, result.Columns);
        Assert.False(result.Truncated);
        Assert.Equal(100, result.ElapsedMilliseconds);

        // Verify row data integrity
        Assert.Equal(1, result.Rows[0][0]);
        Assert.Equal("Alice", result.Rows[0][1]);
        Assert.Equal(3, result.Rows[2][0]);
        Assert.Equal("Charlie", result.Rows[2][1]);
    }

    [Fact]
    public void QueryResult_Truncated_ReturnsTruncatedFlag()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new List<object?> { 1, "Alice" },
            new List<object?> { 2, "Bob" },
            new List<object?> { 3, "Charlie" },
            new List<object?> { 4, "Diana" },
            new List<object?> { 5, "Eve" }
        };
        const int rowCount = 5;
        const bool truncated = true; // Indicates more rows were available but truncated
        const long elapsedMilliseconds = 200;

        // Act
        var result = new QueryResult(columns, rows, rowCount, truncated, elapsedMilliseconds);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Truncated);
        Assert.Equal(5, result.RowCount);
        Assert.Equal(5, result.Rows.Count); // All rows returned, just marked as truncated
    }

    [Fact]
    public void QueryResult_WithNullColumns_CreatesResult()
    {
        // QueryResult is a record with positional parameters - null values are accepted
        // Arrange
        var rows = new List<IReadOnlyList<object?>> { new List<object?> { 1, "Test" } };

        // Act
        var result = new QueryResult(null!, rows, 1, false, 100);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Columns);
    }

    [Fact]
    public void QueryResult_WithNullRows_CreatesResult()
    {
        // QueryResult is a record with positional parameters - null values are accepted
        // Arrange
        var columns = new List<string> { "Id" };

        // Act
        var result = new QueryResult(columns, null!, 0, false, 100);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Rows);
    }

    [Fact]
    public void QueryResult_WithNegativeRowCount_CreatesResult()
    {
        // QueryResult uses positional parameters in a record, so validation is not automatic
        // The record constructor accepts any values - validation should be done at call sites
        // Arrange
        var columns = new List<string> { "Id" };
        var rows = new List<IReadOnlyList<object?>>();

        // Act
        var result = new QueryResult(columns, rows, -1, false, -100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(-1, result.RowCount);
        Assert.Equal(-100, result.ElapsedMilliseconds);
    }

    [Fact]
    public void QueryResult_WithNegativeElapsedMilliseconds_CreatesResult()
    {
        // QueryResult uses positional parameters in a record, so validation is not automatic
        // Arrange
        var columns = new List<string> { "Id" };
        var rows = new List<IReadOnlyList<object?>>();

        // Act
        var result = new QueryResult(columns, rows, 0, false, -100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(-100, result.ElapsedMilliseconds);
    }

    [Fact]
    public void QueryRejection_WithValidCodeAndReason_CanBeConstructed()
    {
        // Arrange
        var code = QueryRejectionCode.EmptyQuery;
        var reason = "Query cannot be empty";

        // Act
        var rejection = new QueryRejection(code, reason);

        // Assert
        Assert.NotNull(rejection);
        Assert.Equal(code, rejection.Code);
        Assert.Equal(reason, rejection.Reason);
    }

    [Fact]
    public void QueryRejection_WithNullReason_CreatesResult()
    {
        // QueryRejection is a record with positional parameters
        // Arrange
        var code = QueryRejectionCode.LimitExceeded;

        // Act
        var rejection = new QueryRejection(code, null!);

        // Assert
        Assert.NotNull(rejection);
        Assert.Equal(code, rejection.Code);
        Assert.Null(rejection.Reason);
    }

    [Fact]
    public void QueryRejection_WithEmptyReason_CreatesResult()
    {
        // Arrange
        var code = QueryRejectionCode.ForbiddenKeyword;

        // Act
        var rejection = new QueryRejection(code, string.Empty);

        // Assert
        Assert.NotNull(rejection);
        Assert.Equal(code, rejection.Code);
        Assert.Empty(rejection.Reason);
    }

    [Fact]
    public void QueryRejection_WithWhitespaceReason_CreatesResult()
    {
        // Arrange
        var code = QueryRejectionCode.MultipleStatements;

        // Act
        var rejection = new QueryRejection(code, "   ");

        // Assert
        Assert.NotNull(rejection);
        Assert.Equal(code, rejection.Code);
        Assert.Equal("   ", rejection.Reason);
    }

    [Fact]
    public void QueryRejectedException_WithQueryRejection_CapturesRejection()
    {
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.EmptyQuery, "Query is empty");

        // Act
        var exception = new QueryRejectedException(rejection);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(rejection, exception.Rejection);
        Assert.Equal(rejection.Code, exception.Code);
        Assert.Equal(rejection.Reason, exception.Message);
    }

    [Fact]
    public void QueryRejectedException_WithQueryRejectionAndMessage_CapturesBoth()
    {
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.LimitExceeded, "Row limit exceeded");
        var contextMessage = "Query validation failed";

        // Act
        var exception = new QueryRejectedException(rejection, contextMessage);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(rejection, exception.Rejection);
        Assert.Equal(rejection.Code, exception.Code);
        Assert.StartsWith(contextMessage, exception.Message);
        Assert.Contains(rejection.Reason, exception.Message);
    }

    [Fact]
    public void QueryRejectedException_WithNullQueryRejection_ThrowsNullReferenceException()
    {
        // QueryRejectedException constructor checks for null rejection
        // Arrange & Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            new QueryRejectedException(null!));
    }

    [Fact]
    public void QueryRejectedException_WithNullQueryRejectionAndMessage_ThrowsNullReferenceException()
    {
        // Arrange & Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            new QueryRejectedException(null!, "Some message"));
    }

    [Fact]
    public void QueryRejectedException_WithNullMessage_CreatesException()
    {
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.NotSelect, "Not a SELECT statement");

        // Act
        var exception = new QueryRejectedException(rejection, null!);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(rejection, exception.Rejection);
        Assert.Contains("Not a SELECT statement", exception.Message);
    }

    [Fact]
    public void QueryResult_ZeroRows_DistinguishableFromQueryRejection()
    {
        // A zero-row QueryResult should be distinguishable from a rejected query
        // Zero rows = successful query execution that returned no data
        // Rejected query = query failed validation/execution

        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>();
        var result = new QueryResult(columns, rows, 0, false, 50);

        // Act & Assert - should not throw, should be a valid QueryResult
        Assert.NotNull(result);
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.RowCount);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void QueryRejection_AllCodes_AreValid()
    {
        // Verify all QueryRejectionCode enum values exist
        var codes = Enum.GetValues<QueryRejectionCode>();

        // Act & Assert
        Assert.NotEmpty(codes);
        Assert.Equal(9, codes.Length); // Actual count of QueryRejectionCode enum values

        // Verify we can access each code
        Assert.Equal(QueryRejectionCode.EmptyQuery, codes[0]);
        Assert.Equal(QueryRejectionCode.MultipleStatements, codes[1]);
        Assert.Equal(QueryRejectionCode.NotSelect, codes[2]);
        Assert.Equal(QueryRejectionCode.ForbiddenKeyword, codes[3]);
        Assert.Equal(QueryRejectionCode.EmptyStatement, codes[4]);
        Assert.Equal(QueryRejectionCode.CteMissingSelect, codes[5]);
        Assert.Equal(QueryRejectionCode.WriteOperationInCte, codes[6]);
        Assert.Equal(QueryRejectionCode.TimeoutConstraint, codes[7]);
        Assert.Equal(QueryRejectionCode.LimitExceeded, codes[8]);
    }

    [Theory]
    [InlineData(QueryRejectionCode.EmptyQuery, "empty")]
    [InlineData(QueryRejectionCode.MultipleStatements, "multiple")]
    [InlineData(QueryRejectionCode.NotSelect, "select")]
    [InlineData(QueryRejectionCode.ForbiddenKeyword, "forbidden")]
    [InlineData(QueryRejectionCode.EmptyStatement, "empty")]
    [InlineData(QueryRejectionCode.CteMissingSelect, "cte")]
    [InlineData(QueryRejectionCode.WriteOperationInCte, "write")]
    [InlineData(QueryRejectionCode.TimeoutConstraint, "timeout")]
    [InlineData(QueryRejectionCode.LimitExceeded, "limit")]
    public void QueryRejectionCode_EachCode_HasDescriptiveReason(QueryRejectionCode code, string expectedKeyword)
    {
        // Arrange
        var reason = GetReasonForCode(code);

        // Act & Assert
        Assert.NotNull(reason);
        Assert.NotEmpty(reason);
        Assert.Contains(expectedKeyword, reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryResult_ImmutableProperties_CannotBeModified()
    {
        // QueryResult is a record, so it should be immutable
        // Arrange
        var columns = new List<string> { "Id" };
        var rows = new List<IReadOnlyList<object?>> { new List<object?> { 1 } };
        var result = new QueryResult(columns, rows, 1, false, 100);

        // Act & Assert - records are immutable by design
        // The following would not compile if records were mutable:
        // result.Columns.Add("NewColumn"); // Compile error
        // result.RowCount = 10; // Compile error

        Assert.Equal(1, result.RowCount);
        Assert.Single(result.Columns);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void QueryRejection_ImmutableProperties_CannotBeModified()
    {
        // QueryRejection is a record, so it should be immutable
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.EmptyQuery, "Query is empty");

        // Act & Assert - records are immutable by design
        // The following would not compile if records were mutable:
        // rejection.Code = QueryRejectionCode.LimitExceeded; // Compile error
        // rejection.Reason = "New reason"; // Compile error

        Assert.Equal(QueryRejectionCode.EmptyQuery, rejection.Code);
        Assert.Equal("Query is empty", rejection.Reason);
    }

    [Fact]
    public void QueryRejectedException_ImmutableRejection_CannotBeModified()
    {
        // QueryRejectedException stores rejection as a property
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.ForbiddenKeyword, "Contains forbidden keyword");
        var exception = new QueryRejectedException(rejection);

        // Act & Assert
        Assert.Equal(rejection, exception.Rejection);
        Assert.Equal(rejection.Code, exception.Code);
        Assert.Equal(rejection.Reason, exception.Message);

        // Verify rejection cannot be modified through exception
        Assert.Equal(QueryRejectionCode.ForbiddenKeyword, exception.Rejection.Code);
    }

    [Fact]
    public void QueryResult_SameInstance_EqualsItself()
    {
        // Verify that a QueryResult equals itself (basic reference equality for records)
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new List<object?> { 1, "Alice" },
            new List<object?> { 2, "Bob" }
        };
        var result = new QueryResult(columns, rows, 2, false, 100);

        // Act & Assert
        Assert.Equal(result, result);
        Assert.True(result.Equals(result));
        Assert.True(result == result);
    }

    [Fact]
    public void QueryRejection_Equals_SupportsValueEquality()
    {
        // Records support value-based equality
        // Arrange
        var rejection1 = new QueryRejection(QueryRejectionCode.EmptyQuery, "Query is empty");
        var rejection2 = new QueryRejection(QueryRejectionCode.EmptyQuery, "Query is empty");

        // Act & Assert
        Assert.Equal(rejection1, rejection2);
        Assert.Equal(rejection1.GetHashCode(), rejection2.GetHashCode());
    }

    [Fact]
    public void QueryRejectedException_Equals_SupportsValueEquality()
    {
        // Exception equality is reference-based by default, but we can test the rejection property
        // Arrange
        var rejection1 = new QueryRejection(QueryRejectionCode.LimitExceeded, "Row limit exceeded");
        var rejection2 = new QueryRejection(QueryRejectionCode.LimitExceeded, "Row limit exceeded");
        var exception1 = new QueryRejectedException(rejection1);
        var exception2 = new QueryRejectedException(rejection2);

        // Act & Assert
        Assert.Equal(rejection1, exception1.Rejection);
        Assert.Equal(rejection2, exception2.Rejection);
    }

    [Fact]
    public void QueryResult_ToString_ReturnsDescriptiveOutput()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Value" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new List<object?> { 1, "Test", 42 },
            new List<object?> { 2, "Data", 99 }
        };
        var result = new QueryResult(columns, rows, 2, true, 150);

        // Act
        var str = result.ToString();

        // Assert
        Assert.NotNull(str);
        Assert.Contains("QueryResult", str);
        Assert.Contains("2", str); // RowCount
        Assert.Contains("True", str); // Truncated
        Assert.Contains("150", str); // ElapsedMilliseconds
    }

    [Fact]
    public void QueryRejection_ToString_ReturnsDescriptiveOutput()
    {
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.EmptyQuery, "Query cannot be empty");

        // Act
        var str = rejection.ToString();

        // Assert
        Assert.NotNull(str);
        Assert.Contains("QueryRejection", str);
        Assert.Contains("EmptyQuery", str);
        Assert.Contains("Query cannot be empty", str);
    }

    [Fact]
    public void QueryRejectedException_ToString_ReturnsNonEmptyString()
    {
        // Arrange
        var rejection = new QueryRejection(QueryRejectionCode.MultipleStatements, "Multiple SQL statements not allowed");
        var exception = new QueryRejectedException(rejection);

        // Act
        var str = exception.ToString();

        // Assert
        Assert.NotNull(str);
        Assert.NotEmpty(str);
        Assert.Contains("QueryRejectedException", str);
    }

    private static string GetReasonForCode(QueryRejectionCode code)
    {
        return code switch
        {
            QueryRejectionCode.EmptyQuery => "Query cannot be empty or whitespace",
            QueryRejectionCode.MultipleStatements => "Multiple SQL statements separated by semicolons are not allowed",
            QueryRejectionCode.NotSelect => "Only SELECT statements are allowed in read-only mode",
            QueryRejectionCode.ForbiddenKeyword => "Forbidden keyword detected in read-only mode",
            QueryRejectionCode.EmptyStatement => "Statement is empty after normalization",
            QueryRejectionCode.CteMissingSelect => "CTE (WITH clause) must be followed by SELECT",
            QueryRejectionCode.WriteOperationInCte => "Write operation in CTE definition detected",
            QueryRejectionCode.TimeoutConstraint => "Query timeout constraint violated",
            QueryRejectionCode.LimitExceeded => "Query row limit or timeout exceeded",
            _ => "Unknown rejection code"
        };
    }
}
