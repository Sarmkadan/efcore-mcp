using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Xunit;
using static EfCoreMcp.Tests.RuntimeLimitsTestModels;

namespace EfCoreMcp.Tests;

/// <summary>
/// Verifies that <see cref="AnalysisTools"/> enforces the analysis depth and schema limits on relationship search.
/// </summary>
/// <remarks>
/// The model is the chain A &lt;- B &lt;- C &lt;- D, so A reaches D in exactly three hops.
/// </remarks>
public class AnalysisToolsLimitTests
{
    private static AnalysisTools Tools(RuntimeLimitsOptions? limits = null, ModelDescriptor? model = null)
    {
        var introspector = new StubIntrospector(model ?? Chain());
        var guard = new RuntimeLimitGuard(limits ?? new RuntimeLimitsOptions(), introspector);
        return new AnalysisTools(new ModelAnalyzer(introspector), new RelationshipAnalyzer(introspector), guard);
    }

    [Fact]
    public void ExplainRelationship_NoMaxHops_FindsPathWithinDefaultDepth()
    {
        var path = Tools().ExplainRelationship("A", "D");

        Assert.True(path.Found);
        Assert.Equal(3, path.Hops.Count);
    }

    [Fact]
    public void ExplainRelationship_MaxHopsExactlyPathLength_FindsPath()
    {
        var path = Tools().ExplainRelationship("A", "D", maxHops: 3);

        Assert.True(path.Found);
    }

    [Fact]
    public void ExplainRelationship_MaxHopsOneShort_ReportsNotFound()
    {
        var path = Tools().ExplainRelationship("A", "D", maxHops: 2);

        Assert.False(path.Found);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExplainRelationship_NonPositiveMaxHops_Throws(int maxHops)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Tools().ExplainRelationship("A", "D", maxHops));
        Assert.Equal("maxHops", ex.ParamName);
    }

    [Fact]
    public void ExplainRelationship_MaxHopsAboveConfiguredDepth_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Tools(new RuntimeLimitsOptions { MaxAnalysisDepth = 2 }).ExplainRelationship("A", "D", maxHops: 3));

        Assert.Contains("--max-analysis-depth", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainRelationship_MaxHopsAtConfiguredDepth_IsAccepted()
    {
        var path = Tools(new RuntimeLimitsOptions { MaxAnalysisDepth = 3 }).ExplainRelationship("A", "D", maxHops: 3);

        Assert.True(path.Found);
    }

    [Fact]
    public void ExplainRelationship_NoMaxHops_UsesConfiguredDepth()
    {
        // With the depth capped at 2, the three-hop path is out of reach by default.
        var path = Tools(new RuntimeLimitsOptions { MaxAnalysisDepth = 2 }).ExplainRelationship("A", "D");

        Assert.False(path.Found);
    }

    [Fact]
    public void ExplainRelationship_EndpointInDisallowedSchema_IsRejected()
    {
        var tools = Tools(
            new RuntimeLimitsOptions { AllowedSchemas = [RuntimeLimitsOptions.DefaultSchemaToken] },
            Chain(lastSchema: "audit"));

        var ex = Assert.Throws<QueryRejectedException>(() => tools.ExplainRelationship("A", "D"));

        Assert.Equal(QueryRejectionCode.SchemaNotAllowed, ex.Code);
    }

    [Fact]
    public void ExplainRelationship_EndpointsInAllowedSchemas_Succeeds()
    {
        var tools = Tools(
            new RuntimeLimitsOptions { AllowedSchemas = [RuntimeLimitsOptions.DefaultSchemaToken] },
            Chain(lastSchema: "audit"));

        // A and B are unschematised, so the path between them is allowed.
        Assert.True(tools.ExplainRelationship("A", "B").Found);
    }
}
