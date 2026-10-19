using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Xunit;
using static EfCoreMcp.Tests.RuntimeLimitsTestModels;

namespace EfCoreMcp.Tests;

/// <summary>
/// Boundary tests for <see cref="RuntimeLimitGuard"/>.
/// </summary>
public class RuntimeLimitGuardTests
{
    private static readonly ModelDescriptor SchemaModel = Model(
        Entity("Sale", "sales"),
        Entity("Log", "audit"),
        Entity("Lookup"));

    private static RuntimeLimitGuard Guard(RuntimeLimitsOptions? limits = null, ModelDescriptor? model = null) =>
        new(limits ?? new RuntimeLimitsOptions(), new StubIntrospector(model ?? SchemaModel));

    [Fact]
    public void Constructor_InvalidLimits_FailsFast()
    {
        Assert.ThrowsAny<ArgumentException>(() => Guard(new RuntimeLimitsOptions { MaxRows = 0 }));
    }

    [Fact]
    public void ResolveMaxRows_NoRequest_UsesDefaultOf100()
    {
        Assert.Equal(100, Guard().ResolveMaxRows(null));
    }

    [Fact]
    public void ResolveMaxRows_NoRequestAndLowCap_CapsDefault()
    {
        Assert.Equal(40, Guard(new RuntimeLimitsOptions { MaxRows = 40 }).ResolveMaxRows(null));
    }

    [Fact]
    public void ResolveMaxRows_RequestExactlyAtLimit_IsAccepted()
    {
        Assert.Equal(50, Guard(new RuntimeLimitsOptions { MaxRows = 50 }).ResolveMaxRows(50));
    }

    [Fact]
    public void ResolveMaxRows_RequestOneAboveLimit_IsRejectedWithLimitCode()
    {
        var ex = Assert.Throws<QueryRejectedException>(() => Guard(new RuntimeLimitsOptions { MaxRows = 50 }).ResolveMaxRows(51));

        Assert.Equal(QueryRejectionCode.LimitExceeded, ex.Code);
        Assert.Contains("maxRows 51 exceeds the configured limit of 50", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--max-rows", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveMaxRows_NonPositiveRequest_Throws(int requested)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Guard().ResolveMaxRows(requested));
        Assert.Equal("maxRows", ex.ParamName);
    }

    [Fact]
    public void EnsureQueryLength_ExactlyAtLimit_IsAccepted()
    {
        Guard(new RuntimeLimitsOptions { MaxQueryLength = 10 }).EnsureQueryLength(new string('x', 10));
    }

    [Fact]
    public void EnsureQueryLength_OneOverLimit_IsRejectedWithLimitCode()
    {
        var ex = Assert.Throws<QueryRejectedException>(() =>
            Guard(new RuntimeLimitsOptions { MaxQueryLength = 10 }).EnsureQueryLength(new string('x', 11)));

        Assert.Equal(QueryRejectionCode.LimitExceeded, ex.Code);
        Assert.Contains("11 characters", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--max-query-length", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureEntitySchemaAllowed_AllowedSchema_IsAccepted()
    {
        Guard(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).EnsureEntitySchemaAllowed("Sale");
    }

    [Fact]
    public void EnsureEntitySchemaAllowed_DisallowedSchema_IsRejectedWithSchemaCode()
    {
        var ex = Assert.Throws<QueryRejectedException>(() =>
            Guard(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).EnsureEntitySchemaAllowed("Log"));

        Assert.Equal(QueryRejectionCode.SchemaNotAllowed, ex.Code);
        Assert.Contains("schema 'audit'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--allowed-schemas", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureEntitySchemaAllowed_EntityWithoutSchema_UsesDefaultToken()
    {
        Guard(new RuntimeLimitsOptions { AllowedSchemas = ["default"] }).EnsureEntitySchemaAllowed("Lookup");
        Assert.Throws<QueryRejectedException>(() =>
            Guard(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).EnsureEntitySchemaAllowed("Lookup"));
    }

    [Fact]
    public void EnsureEntitySchemaAllowed_UnknownEntity_IsLeftToExecutor()
    {
        Guard(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).EnsureEntitySchemaAllowed("Missing");
    }

    [Fact]
    public void EnsureSqlSchemasAllowed_NoAllowList_AllowsAnySchemaReference()
    {
        Guard().EnsureSqlSchemasAllowed("SELECT * FROM audit.Log");
    }

    [Theory]
    [InlineData("SELECT * FROM audit.Log")]
    [InlineData("SELECT * FROM [audit].[Log]")]
    [InlineData("SELECT * FROM \"audit\".\"Log\"")]
    [InlineData("select l.Id from sales.Sale s join audit.Log l on l.Id = s.Id")]
    public void EnsureSqlSchemasAllowed_ReferenceToDisallowedSchema_IsRejected(string sql)
    {
        var ex = Assert.Throws<QueryRejectedException>(() =>
            Guard(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).EnsureSqlSchemasAllowed(sql));

        Assert.Equal(QueryRejectionCode.SchemaNotAllowed, ex.Code);
        Assert.Contains("schema 'audit'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT * FROM sales.Sale")]
    [InlineData("SELECT s.Id FROM Lookup s")]
    [InlineData("SELECT COUNT(*) FROM Lookup")]
    public void EnsureSqlSchemasAllowed_OnlyAllowedOrUnqualifiedNames_IsAccepted(string sql)
    {
        Guard(new RuntimeLimitsOptions { AllowedSchemas = ["sales"] }).EnsureSqlSchemasAllowed(sql);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void ResolveAnalysisDepth_AtBoundary_IsAccepted(int requested)
    {
        Assert.Equal(requested, Guard().ResolveAnalysisDepth(requested));
    }

    [Fact]
    public void ResolveAnalysisDepth_NoRequest_UsesConfiguredMaximum()
    {
        Assert.Equal(7, Guard(new RuntimeLimitsOptions { MaxAnalysisDepth = 7 }).ResolveAnalysisDepth(null));
    }

    [Fact]
    public void ResolveAnalysisDepth_OneAboveConfiguredMaximum_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard(new RuntimeLimitsOptions { MaxAnalysisDepth = 7 }).ResolveAnalysisDepth(8));

        Assert.Equal("maxHops", ex.ParamName);
        Assert.Contains("--max-analysis-depth", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveAnalysisDepth_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard().ResolveAnalysisDepth(0));
    }
}
