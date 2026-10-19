using EfCoreMcp.Core.Domain;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Boundary tests for <see cref="RuntimeLimitsOptions"/> range checks and schema allow-list matching.
/// </summary>
public class RuntimeLimitsOptionsTests
{
    [Fact]
    public void Validate_Defaults_AreAccepted()
    {
        new RuntimeLimitsOptions().Validate();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    public void Validate_MaxRowsAtBoundary_IsAccepted(int maxRows)
    {
        new RuntimeLimitsOptions { MaxRows = maxRows }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Validate_MaxRowsOutsideRange_Throws(int maxRows)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new RuntimeLimitsOptions { MaxRows = maxRows }.Validate());
        Assert.Contains("--max-rows", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1_000_000)]
    public void Validate_MaxQueryLengthAtBoundary_IsAccepted(int maxQueryLength)
    {
        new RuntimeLimitsOptions { MaxQueryLength = maxQueryLength }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_000_001)]
    public void Validate_MaxQueryLengthOutsideRange_Throws(int maxQueryLength)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new RuntimeLimitsOptions { MaxQueryLength = maxQueryLength }.Validate());
        Assert.Contains("--max-query-length", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void Validate_MaxAnalysisDepthAtBoundary_IsAccepted(int maxAnalysisDepth)
    {
        new RuntimeLimitsOptions { MaxAnalysisDepth = maxAnalysisDepth }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public void Validate_MaxAnalysisDepthOutsideRange_Throws(int maxAnalysisDepth)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new RuntimeLimitsOptions { MaxAnalysisDepth = maxAnalysisDepth }.Validate());
        Assert.Contains("--max-analysis-depth", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NullAllowedSchemas_AllowsAllSchemas()
    {
        new RuntimeLimitsOptions { AllowedSchemas = null }.Validate();
    }

    [Theory]
    [InlineData("dbo")]
    [InlineData("default")]
    [InlineData("_private")]
    [InlineData("audit_2")]
    public void Validate_ValidSchemaName_IsAccepted(string schema)
    {
        new RuntimeLimitsOptions { AllowedSchemas = [schema] }.Validate();
    }

    [Fact]
    public void Validate_EmptySchemaList_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new RuntimeLimitsOptions { AllowedSchemas = [] }.Validate());
        Assert.Contains("--allowed-schemas", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a.b")]
    [InlineData("a b")]
    [InlineData("1abc")]
    [InlineData("x;DROP")]
    public void Validate_InvalidSchemaName_Throws(string schema)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new RuntimeLimitsOptions { AllowedSchemas = [schema] }.Validate());
        Assert.Contains("--allowed-schemas", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NullSchemaName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new RuntimeLimitsOptions { AllowedSchemas = [null!] }.Validate());
    }

    [Fact]
    public void IsSchemaAllowed_NoAllowList_AllowsEverything()
    {
        var limits = new RuntimeLimitsOptions();

        Assert.True(limits.IsSchemaAllowed("audit"));
        Assert.True(limits.IsSchemaAllowed(null));
    }

    [Fact]
    public void IsSchemaAllowed_AllowList_MatchesCaseInsensitively()
    {
        var limits = new RuntimeLimitsOptions { AllowedSchemas = ["Sales"] };

        Assert.True(limits.IsSchemaAllowed("sales"));
        Assert.True(limits.IsSchemaAllowed("SALES"));
        Assert.False(limits.IsSchemaAllowed("audit"));
    }

    [Fact]
    public void IsSchemaAllowed_EntityWithoutSchema_MatchesDefaultToken()
    {
        var withToken = new RuntimeLimitsOptions { AllowedSchemas = [RuntimeLimitsOptions.DefaultSchemaToken] };
        var withoutToken = new RuntimeLimitsOptions { AllowedSchemas = ["sales"] };

        Assert.True(withToken.IsSchemaAllowed(null));
        Assert.False(withoutToken.IsSchemaAllowed(null));
    }
}
