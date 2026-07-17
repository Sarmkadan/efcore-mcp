using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Sale> Sales { get; set; } = [];
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
        options.UseSqlite("DataSource=:memory:");

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
        var findings = _analyzer.ValidateModel().Findings.Where(f => f.Code == "EFMCP002").ToList();
        Assert.Contains(findings, f => f is { Entity: "Store", Property: "Name" });
        Assert.Contains(findings, f => f is { Entity: "Customer", Property: "Notes" });
    }

    [Fact]
    public void ValidateModel_FlagsDecimalWithoutPrecision()
    {
        var finding = Assert.Single(_analyzer.ValidateModel().Findings, f => f.Code == "EFMCP003");
        Assert.Equal("Sale", finding.Entity);
        Assert.Equal("Amount", finding.Property);
        Assert.Equal("warning", finding.Severity);
    }

    [Fact]
    public void ValidateModel_FlagsUnindexedForeignKey()
    {
        // EF conventions always (re)create FK indexes on real models, so exercise the
        // descriptor-based check directly with a model whose FK index was dropped.
        var analyzer = new ModelAnalyzer(new StubIntrospector(UnindexedFkModel()));
        var finding = Assert.Single(analyzer.ValidateModel().Findings, f => f.Code == "EFMCP005");
        Assert.Equal("Order", finding.Entity);
        Assert.Equal("CustomerId", finding.Property);
    }

    [Fact]
    public void ValidateModel_FlagsMultipleCascadePaths()
    {
        var finding = Assert.Single(_analyzer.ValidateModel().Findings, f => f.Code == "EFMCP008");
        Assert.Equal("Sale", finding.Entity);
        Assert.Contains("Store", finding.Message);
        Assert.Contains("Customer", finding.Message);
    }

    [Fact]
    public void ValidateModel_OrdersWarningsBeforeInfo()
    {
        var findings = _analyzer.ValidateModel().Findings;
        var lastWarning = findings.ToList().FindLastIndex(f => f.Severity == "warning");
        var firstInfo = findings.ToList().FindIndex(f => f.Severity == "info");
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
