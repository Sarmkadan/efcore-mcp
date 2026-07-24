using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Tests for DependencyOrder computation in RelationshipAnalyzer.
/// Tests various dependency graph scenarios including linear chains, diamonds, cycles, and isolated entities.
/// </summary>
public class DependencyOrderTests : IDisposable
{
    private readonly TestContextProvider _provider = new();
    private readonly RelationshipAnalyzer _analyzer;

    public DependencyOrderTests()
        => _analyzer = new RelationshipAnalyzer(new ModelIntrospector(_provider));

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void GetDependencyOrder_LinearChain_ProducesCorrectInsertOrder()
    {
        // Arrange: Create a model with linear chain A <- B <- C (C depends on B, B depends on A)
        // Expected insert order: [A, B, C] (principals before dependents)
        var model = CreateLinearChainModel();
        var introspector = new MockIntrospector(model);
        var analyzer = new RelationshipAnalyzer(introspector);

        // Act
        var order = analyzer.GetDependencyOrder();

        // Assert
        Assert.Empty(order.CyclicEntities);
        Assert.Empty(order.DetectedCycles);

        // Insert order should be principals first: A, then B, then C
        Assert.Equal(new[] { "A", "B", "C" }, order.InsertOrder);

        // Delete order should be reverse: C, then B, then A
        Assert.Equal(new[] { "C", "B", "A" }, order.DeleteOrder);
    }

    [Fact]
    public void GetDependencyOrder_DiamondDependency_ProducesCorrectOrder()
    {
        // Arrange: Create a model with diamond dependency:
        //      A
        //     / \
        //    B   C
        //     \ /
        //      D
        // Where D depends on B and C, and B and C both depend on A
        // Expected insert order: A, then B and C (order unspecified), then D
        var model = CreateDiamondModel();
        var introspector = new MockIntrospector(model);
        var analyzer = new RelationshipAnalyzer(introspector);

        // Act
        var order = analyzer.GetDependencyOrder();

        // Assert
        Assert.Empty(order.CyclicEntities);
        Assert.Empty(order.DetectedCycles);

        // A must come first (no dependencies)
        Assert.Equal(0, order.InsertOrder.IndexOf("A"));

        // D must come last (depends on both B and C)
        Assert.Equal(3, order.InsertOrder.IndexOf("D"));

        // B and C must come before D and after A (order between B and C is unspecified)
        var bIndex = order.InsertOrder.IndexOf("B");
        var cIndex = order.InsertOrder.IndexOf("C");
        Assert.True(bIndex > 0 && bIndex < 3); // After A, before D
        Assert.True(cIndex > 0 && cIndex < 3); // After A, before D

        // Delete order should be reverse of insert order
        Assert.Equal(order.InsertOrder.Reverse(), order.DeleteOrder);
    }

    [Fact]
    public void GetDependencyOrder_CircularDependency_DetectedAndReported()
    {
        // Arrange: Create a model with circular dependency A <-> B
        var model = CreateCircularModel();
        var introspector = new MockIntrospector(model);
        var analyzer = new RelationshipAnalyzer(introspector);

        // Act
        var order = analyzer.GetDependencyOrder();

        // Assert
        // Circular dependencies should be detected and reported
        Assert.NotEmpty(order.CyclicEntities);
        Assert.NotEmpty(order.DetectedCycles);

        // The cyclic entities should contain A and B
        Assert.Contains("A", order.CyclicEntities);
        Assert.Contains("B", order.CyclicEntities);

        // At least one cycle should be detected containing A and B
        Assert.Contains(order.DetectedCycles, cycle =>
            cycle.Contains("A") && cycle.Contains("B"));

        // Insert and delete orders should only contain non-cyclic entities (none in this case)
        Assert.Empty(order.InsertOrder);
        Assert.Empty(order.DeleteOrder);
    }

    [Fact]
    public void GetDependencyOrder_IsolatedEntity_AppearsExactlyOnce()
    {
        // Arrange: Create a model with one isolated entity and one connected pair
        // Isolated: Z (no dependencies)
        // Connected: A <- B (B depends on A)
        var model = CreateIsolatedEntityModel();
        var introspector = new MockIntrospector(model);
        var analyzer = new RelationshipAnalyzer(introspector);

        // Act
        var order = analyzer.GetDependencyOrder();

        // Assert
        Assert.Empty(order.CyclicEntities);
        Assert.Empty(order.DetectedCycles);

        // Should contain all three entities: A, B, Z
        Assert.Equal(3, order.InsertOrder.Count);
        Assert.Contains("A", order.InsertOrder);
        Assert.Contains("B", order.InsertOrder);
        Assert.Contains("Z", order.InsertOrder);

        // Isolated entity Z should appear exactly once
        Assert.Equal(1, order.InsertOrder.Count(e => e == "Z"));

        // In insert order: A should come before B (principal before dependent)
        Assert.True(order.InsertOrder.IndexOf("A") < order.InsertOrder.IndexOf("B"));

        // Delete order should be reverse
        Assert.Equal(order.InsertOrder.Reverse(), order.DeleteOrder);
    }

    [Fact]
    public void GetDependencyOrder_MixedDependencies_HandlesComplexScenario()
    {
        // Arrange: Create a complex model:
        // Isolated: X
        // Linear chain: A <- B <- C
        // Diamond:      D
        //              / \
        //             E   F
        //              \ /
        //               G
        var model = CreateComplexModel();
        var introspector = new MockIntrospector(model);
        var analyzer = new RelationshipAnalyzer(introspector);

        // Act
        var order = analyzer.GetDependencyOrder();

        // Assert
        Assert.Empty(order.CyclicEntities);
        Assert.Empty(order.DetectedCycles);

        // Should contain all entities: A, B, C, D, E, F, G, X
        Assert.Equal(8, order.InsertOrder.Count);
        Assert.Contains("X", order.InsertOrder); // Isolated

        // Linear chain: A before B before C
        Assert.True(order.InsertOrder.IndexOf("A") < order.InsertOrder.IndexOf("B"));
        Assert.True(order.InsertOrder.IndexOf("B") < order.InsertOrder.IndexOf("C"));

        // Diamond: D before E and F, E and F before G
        Assert.True(order.InsertOrder.IndexOf("D") < order.InsertOrder.IndexOf("E"));
        Assert.True(order.InsertOrder.IndexOf("D") < order.InsertOrder.IndexOf("F"));
        Assert.True(order.InsertOrder.IndexOf("E") < order.InsertOrder.IndexOf("G"));
        Assert.True(order.InsertOrder.IndexOf("F") < order.InsertOrder.IndexOf("G"));

        // Delete order should be reverse
        Assert.Equal(order.InsertOrder.Reverse(), order.DeleteOrder);
    }

    #region Test Model Creators

    private ModelDescriptor CreateLinearChainModel()
    {
        // A <- B <- C (C depends on B, B depends on A)
        // Means: C has FK to B, B has FK to A
        return new ModelDescriptor(
            "TestContext",
            "TestProvider",
            [
                new EntityDescriptor(
                    "A", "A", "TableA", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys (A has no outgoing FKs)
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "B", "B", "TableB", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "A", "B", ["AId"], ["Id"], "Cascade", true, false)], // B depends on A
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "C", "C", "TableC", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "B", "C", ["BId"], ["Id"], "Cascade", true, false)], // C depends on B
                    [], // Navigations
                    []  // Indexes
                )
            ]
        );
    }

    private ModelDescriptor CreateDiamondModel()
    {
        //      A
        //     / \
        //    B   C
        //     \ /
        //      D
        // Where D depends on B and C, and B and C both depend on A
        return new ModelDescriptor(
            "TestContext",
            "TestProvider",
            [
                new EntityDescriptor(
                    "A", "A", "TableA", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys (A has no outgoing FKs)
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "B", "B", "TableB", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "A", "B", ["AId"], ["Id"], "Cascade", true, false)], // B depends on A
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "C", "C", "TableC", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "A", "C", ["AId"], ["Id"], "Cascade", true, false)], // C depends on A
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "D", "D", "TableD", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [
                        new ForeignKeyDescriptor(null, "B", "D", ["BId"], ["Id"], "Cascade", true, false), // D depends on B
                        new ForeignKeyDescriptor(null, "C", "D", ["CId"], ["Id"], "Cascade", true, false)  // D depends on C
                    ],
                    [], // Navigations
                    []  // Indexes
                )
            ]
        );
    }

    private ModelDescriptor CreateCircularModel()
    {
        // A <-> B (circular dependency)
        // A has FK to B, B has FK to A
        return new ModelDescriptor(
            "TestContext",
            "TestProvider",
            [
                new EntityDescriptor(
                    "A", "A", "TableA", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "B", "A", ["BId"], ["Id"], "Cascade", true, false)], // A depends on B
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "B", "B", "TableB", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "A", "B", ["AId"], ["Id"], "Cascade", true, false)], // B depends on A
                    [], // Navigations
                    []  // Indexes
                )
            ]
        );
    }

    private ModelDescriptor CreateIsolatedEntityModel()
    {
        // Isolated: Z (no dependencies)
        // Connected: A <- B (B depends on A)
        return new ModelDescriptor(
            "TestContext",
            "TestProvider",
            [
                new EntityDescriptor(
                    "A", "A", "TableA", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "B", "B", "TableB", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "A", "B", ["AId"], ["Id"], "Cascade", true, false)], // B depends on A
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "Z", "Z", "TableZ", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys (Z has no dependencies)
                    [], // Navigations
                    []  // Indexes
                )
            ]
        );
    }

    private ModelDescriptor CreateComplexModel()
    {
        // Isolated: X
        // Linear chain: A <- B <- C
        // Diamond:      D
        //              / \
        //             E   F
        //              \ /
        //               G
        return new ModelDescriptor(
            "TestContext",
            "TestProvider",
            [
                // Isolated entity X
                new EntityDescriptor(
                    "X", "X", "TableX", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys
                    [], // Navigations
                    []  // Indexes
                ),

                // Linear chain: A <- B <- C
                new EntityDescriptor(
                    "A", "A", "TableA", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "B", "B", "TableB", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "A", "B", ["AId"], ["Id"], "Cascade", true, false)], // B depends on A
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "C", "C", "TableC", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "B", "C", ["BId"], ["Id"], "Cascade", true, false)], // C depends on B
                    [], // Navigations
                    []  // Indexes
                ),

                // Diamond: D <- E, D <- F, E -> G, F -> G
                new EntityDescriptor(
                    "D", "D", "TableD", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [], // ForeignKeys (D has no outgoing FKs)
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "E", "E", "TableE", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "D", "E", ["DId"], ["Id"], "Cascade", true, false)], // E depends on D
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "F", "F", "TableF", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [new ForeignKeyDescriptor(null, "D", "F", ["DId"], ["Id"], "Cascade", true, false)], // F depends on D
                    [], // Navigations
                    []  // Indexes
                ),
                new EntityDescriptor(
                    "G", "G", "TableG", null, false, null,
                    [], // Properties
                    null, // PrimaryKey
                    [], // AlternateKeys
                    [
                        new ForeignKeyDescriptor(null, "E", "G", ["EId"], ["Id"], "Cascade", true, false), // G depends on E
                        new ForeignKeyDescriptor(null, "F", "G", ["FId"], ["Id"], "Cascade", true, false)  // G depends on F
                    ],
                    [], // Navigations
                    []  // Indexes
                )
            ]
        );
    }

    #endregion

    #region Test Infrastructure

    internal sealed class TestContextProvider : IDbContextProvider
    {
        private readonly BlogContext _context = new();
        public DbContext GetContext() => _context;
        public ContextInfo GetContextInfo() =>
            new(nameof(BlogContext), "EfCoreMcp.Tests", _context.Database.ProviderName, null, false);
        public void Dispose() => _context.Dispose();
    }

    internal sealed class BlogContext : DbContext
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Post> Posts => Set<Post>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite("DataSource=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>(b =>
            {
                b.ToTable("blogs");
                b.Property(x => x.Title).HasMaxLength(200);
                b.HasIndex(x => x.Title).IsUnique();
            });
            modelBuilder.Entity<Post>()
                .HasOne(p => p.Blog)
                .WithMany(b => b.Posts)
                .HasForeignKey(p => p.BlogId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class Blog
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public List<Post> Posts { get; set; } = [];
    }

    public class Post
    {
        public int Id { get; set; }
        public string? Body { get; set; }
        public int BlogId { get; set; }
        public Blog Blog { get; set; } = null!;
    }

    internal sealed class MockIntrospector(ModelDescriptor model) : IModelIntrospector
    {
        public ModelDescriptor DescribeModel() => model;

        public EntityDescriptor? DescribeEntity(string entityName) =>
            model.Entities.FirstOrDefault(e =>
                string.Equals(e.Name, entityName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.ClrType, entityName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.TableName, entityName, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<string> ListEntityNames() =>
            model.Entities.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        public string EntityNotFoundMessage(string entityName)
        {
            var names = ListEntityNames();
            return $"Entity '{entityName}' not found. Available: {string.Join(", ", names)}.";
        }

        public void InvalidateCache() { }
    }

    #endregion
}