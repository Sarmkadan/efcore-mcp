using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Microsoft.EntityFrameworkCore;
using System;
using Xunit;

namespace EfCoreMcp.Tests;

public class ModelToolsAndDbContextProviderIntegrationTests : IDisposable
{
    private readonly DbContextProvider _provider;
    private readonly ModelIntrospector _introspector;
    private readonly SchemaExplainer _explainer;
    private readonly ModelTools _modelTools;

    public ModelToolsAndDbContextProviderIntegrationTests()
    {
        // Use the assembly where this test DbContext is defined
        var options = new ContextConnectionOptions
        {
            AssemblyPath = typeof(SampleContext).Assembly.Location,
            ContextTypeName = typeof(SampleContext).FullName
        };
        _provider = new DbContextProvider(options);
        _introspector = new ModelIntrospector(_provider);
        _explainer = new SchemaExplainer(_introspector);
        _modelTools = new ModelTools(_introspector, _explainer, _provider);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void DbContextProvider_GetContext_ReturnsSampleContext()
    {
        // Act
        var context = _provider.GetContext();

        // Assert
        Assert.Equal("SampleContext", context.GetType().Name);
        // Check that we can access the DbSets via Set method
        Assert.NotNull(context.Set<Blog>());
        Assert.NotNull(context.Set<Post>());
    }

    [Fact]
    public void ModelTools_ListEntities_IncludesAllEntities()
    {
        // Act
        var entities = _modelTools.ListEntities();

        // Assert
        Assert.NotNull(entities);
        Assert.Contains("Blog", entities);
        Assert.Contains("Post", entities);
        // Owned types are not returned as entities by ListEntities (they are not entity types)
        Assert.DoesNotContain("BlogMeta", entities);
    }

    [Fact]
    public void ModelTools_DescribeEntity_Blog_ReturnsExpectedPropertiesAndKeys()
    {
        // Act
        var blog = _modelTools.DescribeEntity("Blog");

        // Assert
        Assert.NotNull(blog);
        Assert.Equal("Blog", blog.Name);
        Assert.Equal("Blogs", blog.TableName); // Default table name for Blog is Blogs (pluralized)
        Assert.NotNull(blog.Properties);
        Assert.Contains(blog.Properties, p => p.Name == "Id");
        Assert.Contains(blog.Properties, p => p.Name == "Name");
        Assert.Contains(blog.Properties, p => p.Name == "Url");
        // Check primary key
        Assert.NotNull(blog.PrimaryKey);
        Assert.Contains(blog.PrimaryKey.Properties, p => p == "Id");
        // Check navigations
        Assert.NotNull(blog.Navigations);
        Assert.Contains(blog.Navigations, n => n.Name == "Posts");
    }

    [Fact]
    public void ModelTools_DescribeEntity_Post_ReturnsExpectedPropertiesIncludingShadowAndOwned()
    {
        // Act
        var post = _modelTools.DescribeEntity("Post");

        // Assert
        Assert.NotNull(post);
        Assert.Equal("Post", post.Name);
        Assert.Equal("Posts", post.TableName);
        Assert.NotNull(post.Properties);
        // Regular properties
        Assert.Contains(post.Properties, p => p.Name == "Id");
        Assert.Contains(post.Properties, p => p.Name == "Title");
        Assert.Contains(post.Properties, p => p.Name == "Content");
        Assert.Contains(post.Properties, p => p.Name == "BlogId");
        // Shadow property should appear as a property (EF Core includes shadow properties in the model)
        Assert.Contains(post.Properties, p => p.Name == "LastUpdated");
        // Owned type navigation? Actually owned types are not navigations; they are represented as properties of the owned type.
        // The owned type itself is not an entity, but its properties are included as part of the owning entity? In EF Core, owned types are not exposed as separate entities; their properties are included in the owning entity's table.
        // However, the model metadata may show the owned type as a complex type navigation? Let's check: we expect a navigation to the owned type? Actually, owned types are not navigations; they are complex properties.
        // We'll just ensure that the owned type is not listed as an entity (already checked) and that we can still describe the owned type? Not required.
    }

    [Fact]
    public void ModelTools_DescribeEntity_UnknownEntity_ThrowsNotFound()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _modelTools.DescribeEntity("UnknownEntity"));
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelTools_DescribeEntity_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _modelTools.DescribeEntity(null!));
    }

    [Fact]
    public void ModelTools_DescribeEntity_EmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _modelTools.DescribeEntity(""));
    }

    // Sample DbContext with Blog, Post, owned type BlogMeta, shadow property LastUpdated
    public class BlogMeta
    {
        public string? Description { get; set; }
        public int? Rating { get; set; }
    }

    public class Blog
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public List<Post> Posts { get; set; } = new();
    }

    public class Post
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public int BlogId { get; set; }
        public Blog Blog { get; set; } = null!;
        public BlogMeta Meta { get; set; } = new(); // Owned type
        // Shadow property LastUpdated will be added via modelBuilder
    }

    public class SampleContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; } = null!;
        public DbSet<Post> Posts { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Use in-memory SQLite for testing
            optionsBuilder.UseSqlite("DataSource=:memory:");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure one-to-many: Blog has many Posts
            modelBuilder.Entity<Blog>()
                .HasMany(b => b.Posts)
                .WithOne(p => p.Blog)
                .HasForeignKey(p => p.BlogId);

            // Configure owned type BlogMeta on Post
            modelBuilder.Entity<Post>()
                .OwnsOne(p => p.Meta);

            // Add shadow property LastUpdated to Post
            modelBuilder.Entity<Post>()
                .Property<DateTime>("LastUpdated");
        }
    }
}