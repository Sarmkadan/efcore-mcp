using EfCoreMcp.Core.Services;
using Xunit;

namespace EfCoreMcp.Tests;

public class RelationshipAnalyzerTests : IDisposable
{
    private readonly AnalyzerContextProvider _provider = new();
    private readonly RelationshipAnalyzer _analyzer;

    public RelationshipAnalyzerTests() =>
        _analyzer = new RelationshipAnalyzer(new ModelIntrospector(_provider));

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void ExplainRelationship_DirectForeignKey_IsOneHop()
    {
        var path = _analyzer.ExplainRelationship("Store", "Sale");
        Assert.True(path.Found);
        var hop = Assert.Single(path.Hops);
        Assert.Equal("Store", hop.FromEntity);
        Assert.Equal("Sale", hop.ToEntity);
        Assert.Equal(["StoreId"], hop.ForeignKeyProperties);
        Assert.Equal("one-to-many", hop.Cardinality);
        Assert.Equal("Cascade", hop.DeleteBehavior);
    }

    [Fact]
    public void ExplainRelationship_TransitivePath_GoesThroughJoinEntity()
    {
        var path = _analyzer.ExplainRelationship("Store", "Customer");
        Assert.True(path.Found);
        Assert.Equal(2, path.Hops.Count);
        Assert.Equal("Sale", path.Hops[0].ToEntity);
        Assert.Equal("Customer", path.Hops[1].ToEntity);
    }

    [Fact]
    public void ExplainRelationship_SameEntity_IsZeroHops()
    {
        var path = _analyzer.ExplainRelationship("Store", "Store");
        Assert.True(path.Found);
        Assert.Empty(path.Hops);
    }

    [Fact]
    public void ExplainRelationship_ResolvesCaseInsensitiveAndTableNames()
    {
        var path = _analyzer.ExplainRelationship("store", "Sales");
        Assert.True(path.Found);
        Assert.Equal("Store", path.FromEntity);
        Assert.Equal("Sale", path.ToEntity);
    }

    [Fact]
    public void ExplainRelationship_UnknownEntity_ThrowsWithAvailableNames()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _analyzer.ExplainRelationship("Store", "Nope"));
        Assert.Contains("Available", ex.Message);
    }

    [Fact]
    public void GetDependencyOrder_PrincipalsComeBeforeDependents()
    {
        var order = _analyzer.GetDependencyOrder();
        Assert.Empty(order.CyclicEntities);
        Assert.True(order.InsertOrder.IndexOf("Store") < order.InsertOrder.IndexOf("Sale"));
        Assert.True(order.InsertOrder.IndexOf("Customer") < order.InsertOrder.IndexOf("Sale"));
        Assert.Equal(order.InsertOrder.Reverse(), order.DeleteOrder);
    }
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return -1;
    }

    public static IEnumerable<string> Reverse(this IReadOnlyList<string> list) =>
        Enumerable.Reverse(list);
}
