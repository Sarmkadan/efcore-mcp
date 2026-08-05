using System;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

public class Store : IEquatable<Store>
{
    /// <summary>
    /// Gets or sets the identifier of the store.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the store.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the collection of sales associated with the store.
    /// </summary>
    public List<Sale> Sales { get; set; } = [];

    /// <summary>
    /// Determines whether the specified <see cref="Store"/> is equal to the current <see cref="Store"/> instance.
    /// </summary>
    /// <param name="other">The other <see cref="Store"/> to compare.</param>
    /// <returns><c>true</c> if the stores are equal; otherwise, <c>false</c>.</returns>
    public bool Equals(Store? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        if (Id != other.Id)
            return false;
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal))
            return false;

        if (Sales == null && other.Sales == null)
            return true;
        if (Sales == null || other.Sales == null)
            return false;
        if (Sales.Count != other.Sales.Count)
            return false;

        for (int i = 0; i < Sales.Count; i++)
        {
            var s1 = Sales[i];
            var s2 = other.Sales[i];
            if (s1.Id != s2.Id || s1.Amount != s2.Amount)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="Store"/> instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current store.</param>
    /// <returns><c>true</c> if the specified object is a <see cref="Store"/> and is equal to the current store; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => Equals(obj as Store);

    /// <summary>
    /// Returns a hash code for the current <see cref="Store"/> instance.
    /// </summary>
    /// <returns>An integer hash code.</returns>
    public override int GetHashCode()
    {
        // Combine primary scalar properties and a simple hash of the sales collection
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        hash.Add(Sales?.Count ?? 0);
        foreach (var sale in Sales)
        {
            hash.Add(sale.Id);
            hash.Add(sale.Amount);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two <see cref="Store"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="Store"/> to compare.</param>
    /// <param name="right">The second <see cref="Store"/> to compare.</param>
    /// <returns><c>true</c> if both stores are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Store? left, Store? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two <see cref="Store"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="Store"/> to compare.</param>
    /// <param name="right">The second <see cref="Store"/> to compare.</param>
    /// <returns><c>true</c> if the stores are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Store? left, Store? right) => !Equals(left, right);
}

public class Customer
{
    public int Id { get; set; }
    public string? Notes { get; set; }
    public List<Sale> Sales { get; set; } = [];
}

public class Sale
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}

public class AnalyzerContext : DbContext
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite($"DataSource={StoreConstants.DataSource}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sale>(s =>
        {
            s.HasOne(x => x.Store).WithMany(x => x.Sales)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            s.HasOne(x => x.Customer).WithMany(x => x.Sales)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class AnalyzerContextProvider : IDbContextProvider
{
    private readonly AnalyzerContext _context = new();
    public DbContext GetContext() => _context;
    public ContextInfo GetContextInfo() =>
        new(nameof(AnalyzerContext), "EfCoreMcp.Tests", _context.Database.ProviderName, null, false);
    public void Dispose() => _context.Dispose();
}

internal sealed class StubIntrospector(ModelDescriptor model) : IModelIntrospector
{
    public ModelDescriptor DescribeModel() => model;
    public EntityDescriptor? DescribeEntity(string entityName) =>
        model.Entities.FirstOrDefault(e => e.Name == entityName);
    public IReadOnlyList<string> ListEntityNames() => model.Entities.Select(e => e.Name).ToList();
    public string EntityNotFoundMessage(string entityName) => $"Entity '{entityName}' not found in the model.";
    public void InvalidateCache() { }
}

public class ModelAnalyzerTests : IDisposable
{
    private readonly AnalyzerContextProvider _provider = new();
    private readonly ModelAnalyzer _analyzer;

    public ModelAnalyzerTests() =>
        _analyzer = new ModelAnalyzer(new ModelIntrospector(_provider));

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void ValidateModel_ReportsEntityCount()
    {
        Assert.Equal(3, _analyzer.ValidateModel().EntityCount);
    }

    [Fact]
    public void ValidateModel_FlagsUnboundedStrings()
    {
        var findings = _analyzer.ValidateModel().Findings.Where(f => f.Code == StoreConstants.EfMcp002Code).ToList();
        Assert.Contains(findings, f => f is { Entity: "Store", Property: "Name" });
        Assert.Contains(findings, f => f is { Entity: "Customer", Property: "Notes" });
    }

    [Fact]
    public void ValidateModel_FlagsDecimalWithoutPrecision()
    {
        var finding = Assert.Single(_analyzer.ValidateModel().Findings, f => f.Code == StoreConstants.EfMcp003Code);
        Assert.Equal("Sale", finding.Entity);
        Assert.Equal("Amount", finding.Property);
        Assert.Equal(StoreConstants.WarningSeverity, finding.Severity);
    }

    [Fact]
    public void ValidateModel_FlagsUnindexedForeignKey()
    {
        // EF conventions always (re)create FK indexes on real models, so exercise the
        // descriptor-based check directly with a model whose FK index was dropped.
        var analyzer = new ModelAnalyzer(new StubIntrospector(UnindexedFkModel()));
        var finding = Assert.Single(analyzer.ValidateModel().Findings, f => f.Code == StoreConstants.EfMcp005Code);
        Assert.Equal("Order", finding.Entity);
        Assert.Equal("CustomerId", finding.Property);
    }

    [Fact]
    public void ValidateModel_FlagsMultipleCascadePaths()
    {
        var finding = Assert.Single(_analyzer.ValidateModel().Findings, f => f.Code == StoreConstants.EfMcp008Code);
        Assert.Equal("Sale", finding.Entity);
        Assert.Contains("Store", finding.Message);
        Assert.Contains("Customer", finding.Message);
    }

    [Fact]
    public void ValidateModel_OrdersWarningsBeforeInfo()
    {
        var findings = _analyzer.ValidateModel().Findings;
        var lastWarning = findings.ToList().FindLastIndex(f => f.Severity == StoreConstants.WarningSeverity);
        var firstInfo = findings.ToList().FindIndex(f => f.Severity == StoreConstants.InfoSeverity);
        Assert.True(firstInfo == -1 || lastWarning < firstInfo);
    }

    [Fact]
    public void SuggestIndexes_SuggestsIndexForUncoveredFk()
    {
        var analyzer = new ModelAnalyzer(new StubIntrospector(UnindexedFkModel()));
        var suggestion = Assert.Single(analyzer.SuggestIndexes());
        Assert.Equal("Order", suggestion.Entity);
        Assert.Equal(["CustomerId"], suggestion.Properties);
        Assert.Contains("Customer", suggestion.Reason);
    }

    [Fact]
    public void SuggestIndexes_TreatsIndexPrefixAsCovering()
    {
        var model = UnindexedFkModel() with
        {
            Entities =
            [
                UnindexedFkModel().Entities[0] with
                {
                    Indexes = [new IndexDescriptor("IX_Order_CustomerId_Date", ["CustomerId", "Date"], false, null)]
                }
            ]
        };
        Assert.Empty(new ModelAnalyzer(new StubIntrospector(model)).SuggestIndexes());
    }

    private static ModelDescriptor UnindexedFkModel()
    {
        var order = new EntityDescriptor(
            "Order", "Test.Order", "Orders", null, false, null,
            [],
            new KeyDescriptor("PK_Orders", ["Id"], true),
            [],
            [new ForeignKeyDescriptor("FK_Orders_Customers", "Customer", "Order", ["CustomerId"], ["Id"], "Restrict", true, false)],
            [],
            []);
        return new ModelDescriptor("TestContext", null, [order]);
    }

    [Fact]
    public void CleanModel_ProducesNoFkOrCascadeFindings()
    {
        using var provider = new TestContextProvider();
        var analyzer = new ModelAnalyzer(new ModelIntrospector(provider));
        var report = analyzer.ValidateModel();
        Assert.DoesNotContain(report.Findings, f => f.Code is "EFMCP005" or "EFMCP008" or "EFMCP004" or "EFMCP001");
        Assert.Empty(analyzer.SuggestIndexes());
    }
}
