using EfCoreMcp.Core.Services;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Tests for the <see cref="RelationshipAnalyzer"/> class.
/// </summary>
public class RelationshipAnalyzerTests : IEquatable<RelationshipAnalyzerTests>, IRelationshipAnalyzerTests
{
    private readonly AnalyzerContextProvider _provider = new();
    private readonly RelationshipAnalyzer _analyzer;

    public RelationshipAnalyzerTests() =>
        _analyzer = new RelationshipAnalyzer(new ModelIntrospector(_provider));

    public void Dispose() => _provider.Dispose();

    [Fact]
    /// <summary>Verifies that a direct foreign key relationship is reported as one hop.</summary>
    public void ExplainRelationship_DirectForeignKey_IsOneHop()
    {
        var path = _analyzer.ExplainRelationship(RelationshipAnalyzerTestsConstants.Store, RelationshipAnalyzerTestsConstants.Sale);
        Assert.True(path.Found);
        var hop = Assert.Single(path.Hops);
        Assert.Equal(RelationshipAnalyzerTestsConstants.Store, hop.FromEntity);
        Assert.Equal(RelationshipAnalyzerTestsConstants.Sale, hop.ToEntity);
        Assert.Equal(["StoreId"], hop.ForeignKeyProperties);
        Assert.Equal("one-to-many", hop.Cardinality);
        Assert.Equal("Cascade", hop.DeleteBehavior);
    }

    [Fact]
    /// <summary>Verifies that a transitive relationship via a join entity is correctly reported with two hops.</summary>
    public void ExplainRelationship_TransitivePath_GoesThroughJoinEntity()
    {
        var path = _analyzer.ExplainRelationship(RelationshipAnalyzerTestsConstants.Store, RelationshipAnalyzerTestsConstants.Customer);
        Assert.True(path.Found);
        Assert.Equal(2, path.Hops.Count);
        Assert.Equal(RelationshipAnalyzerTestsConstants.Sale, path.Hops[0].ToEntity);
        Assert.Equal(RelationshipAnalyzerTestsConstants.Customer, path.Hops[1].ToEntity);
    }

    [Fact]
    /// <summary>Verifies that the relationship from an entity to itself has zero hops.</summary>
    public void ExplainRelationship_SameEntity_IsZeroHops()
    {
        var path = _analyzer.ExplainRelationship(RelationshipAnalyzerTestsConstants.Store, RelationshipAnalyzerTestsConstants.Store);
        Assert.True(path.Found);
        Assert.Empty(path.Hops);
    }

    [Fact]
    /// <summary>Verifies that entity names are resolved case-insensitively and by table name.</summary>
    public void ExplainRelationship_ResolvesCaseInsensitiveAndTableNames()
    {
        var path = _analyzer.ExplainRelationship("store", "Sales");
        Assert.True(path.Found);
        Assert.Equal(RelationshipAnalyzerTestsConstants.Store, path.FromEntity);
        Assert.Equal(RelationshipAnalyzerTestsConstants.Sale, path.ToEntity);
    }

    [Fact]
    /// <summary>Verifies that an unknown entity throws an exception with a list of available entities.</summary>
    public void ExplainRelationship_UnknownEntity_ThrowsWithAvailableNames()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _analyzer.ExplainRelationship(RelationshipAnalyzerTestsConstants.Store, "Nope"));
        Assert.Contains("Available", ex.Message);
    }

    [Fact]
    /// <summary>Verifies that the dependency order places principals before dependents and that delete order is the reverse.</summary>
    public void GetDependencyOrder_PrincipalsComeBeforeDependents()
    {
        var order = _analyzer.GetDependencyOrder();
        Assert.Empty(order.CyclicEntities);
        Assert.Empty(order.DetectedCycles);
        Assert.True(order.InsertOrder.IndexOf(RelationshipAnalyzerTestsConstants.Store) < order.InsertOrder.IndexOf(RelationshipAnalyzerTestsConstants.Sale));
        Assert.True(order.InsertOrder.IndexOf(RelationshipAnalyzerTestsConstants.Customer) < order.InsertOrder.IndexOf(RelationshipAnalyzerTestsConstants.Sale));
        Assert.Equal(order.InsertOrder.Reverse(), order.DeleteOrder);
    }

    public bool Equals(RelationshipAnalyzerTests? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;
        return ReferenceEquals(_provider, other._provider) &&
               ReferenceEquals(_analyzer, other._analyzer);
    }

    public override bool Equals(object? obj) => Equals(obj as RelationshipAnalyzerTests);

    public override int GetHashCode() => HashCode.Combine(_provider, _analyzer);

    public static bool operator ==(RelationshipAnalyzerTests? left, RelationshipAnalyzerTests? right) =>
        EqualityComparer<RelationshipAnalyzerTests>.Default.Equals(left, right);

    public static bool operator !=(RelationshipAnalyzerTests? left, RelationshipAnalyzerTests? right) =>
        !(left == right);
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
