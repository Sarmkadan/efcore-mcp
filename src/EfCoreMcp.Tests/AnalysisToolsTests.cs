using System;
using System.Collections.Generic;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Tests for the <see cref="AnalysisTools"/> class.
/// </summary>
public sealed class AnalysisToolsTests : IDisposable
{
    private readonly AnalyzerContextProvider _provider = new();
    private readonly AnalysisTools _tools;

    public AnalysisToolsTests()
    {
        // The same model is used for both the analyzer and the relationship analyzer.
        var introspector = new ModelIntrospector(_provider);
        var modelAnalyzer = new ModelAnalyzer(introspector);
        var relationshipAnalyzer = new RelationshipAnalyzer(introspector);
        _tools = new AnalysisTools(modelAnalyzer, relationshipAnalyzer);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void ValidateModel_ReturnsNonNullReport()
    {
        ModelValidationReport report = _tools.ValidateModel();
        Assert.NotNull(report);
    }

    [Fact]
    public void SuggestIndexes_ReturnsNonNullList()
    {
        IReadOnlyList<IndexSuggestion> suggestions = _tools.SuggestIndexes();
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void ExplainRelationship_DirectForeignKey_ReturnsOneHop()
    {
        // Arrange & Act
        RelationshipPath path = _tools.ExplainRelationship("Store", "Sale");

        // Assert
        Assert.True(path.Found);
        Assert.Single(path.Hops);
        RelationshipHop hop = path.Hops[0];
        Assert.Equal("Store", hop.FromEntity);
        Assert.Equal("Sale", hop.ToEntity);
        Assert.Equal(["StoreId"], hop.ForeignKeyProperties);
        Assert.Equal("one-to-many", hop.Cardinality);
        Assert.Equal("Cascade", hop.DeleteBehavior);
    }

    [Fact]
    public void ExplainRelationship_NullFromEntity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _tools.ExplainRelationship(null!, "Sale"));
    }

    [Fact]
    public void ExplainRelationship_EmptyToEntity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _tools.ExplainRelationship("Store", ""));
    }

    [Fact]
    public void ExplainRelationship_UnknownEntity_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _tools.ExplainRelationship("Store", "Nonexistent"));
        Assert.Contains("Available", ex.Message);
    }

    [Fact]
    public void DependencyOrder_ReturnsCorrectInsertAndDeleteOrder()
    {
        DependencyOrder order = _tools.DependencyOrder();

        // No cycles in the test model
        Assert.Empty(order.CyclicEntities);
        Assert.Empty(order.DetectedCycles);

        // Principals must appear before dependents in the insert order
        Assert.True(order.InsertOrder.IndexOf("Store") < order.InsertOrder.IndexOf("Sale"));
        Assert.True(order.InsertOrder.IndexOf("Customer") < order.InsertOrder.IndexOf("Sale"));

        // Delete order should be the reverse of the insert order
        Assert.Equal(order.InsertOrder.Reverse(), order.DeleteOrder);
    }
}
