using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

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

public class BlogContext : DbContext
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

internal sealed class TestContextProvider : IDbContextProvider
{
    private readonly BlogContext _context = new();
    public DbContext GetContext() => _context;
    public ContextInfo GetContextInfo() =>
        new(nameof(BlogContext), "EfCoreMcp.Tests", _context.Database.ProviderName, null, false);
    public void Dispose() => _context.Dispose();
}

public class ModelIntrospectorTests : IDisposable
{
    private readonly TestContextProvider _provider = new();
    private readonly ModelIntrospector _introspector;

    public ModelIntrospectorTests() => _introspector = new ModelIntrospector(_provider);

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void DescribeModel_ReturnsContextAndProviderNames()
    {
        var model = _introspector.DescribeModel();
        Assert.Equal("BlogContext", model.ContextName);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", model.ProviderName);
    }

    [Fact]
    public void DescribeModel_ListsEntitiesSortedByName()
    {
        var model = _introspector.DescribeModel();
        Assert.Equal(["Blog", "Post"], model.Entities.Select(e => e.Name));
    }

    [Fact]
    public void DescribeEntity_MapsTableName()
    {
        var blog = _introspector.DescribeEntity("Blog");
        Assert.NotNull(blog);
        Assert.Equal("blogs", blog.TableName);
    }

    [Fact]
    public void DescribeEntity_IsCaseInsensitive()
    {
        Assert.NotNull(_introspector.DescribeEntity("post"));
    }

    [Fact]
    public void DescribeEntity_ResolvesByTableName()
    {
        var blog = _introspector.DescribeEntity("blogs");
        Assert.NotNull(blog);
        Assert.Equal("Blog", blog.Name);
    }

    [Fact]
    public void DescribeEntity_ReturnsNullForUnknownEntity()
    {
        Assert.Null(_introspector.DescribeEntity("Nonexistent"));
    }

    [Fact]
    public void DescribeEntity_MarksPrimaryKeyProperty()
    {
        var blog = _introspector.DescribeEntity("Blog")!;
        var id = blog.Properties.Single(p => p.Name == "Id");
        Assert.True(id.IsPrimaryKey);
        Assert.NotNull(blog.PrimaryKey);
        Assert.Equal(["Id"], blog.PrimaryKey.Properties);
        Assert.True(blog.PrimaryKey.IsPrimary);
    }

    [Fact]
    public void DescribeEntity_ReportsNullabilityAndMaxLength()
    {
        var post = _introspector.DescribeEntity("Post")!;
        var body = post.Properties.Single(p => p.Name == "Body");
        Assert.True(body.IsNullable);
        var title = _introspector.DescribeEntity("Blog")!.Properties.Single(p => p.Name == "Title");
        Assert.False(title.IsNullable);
        Assert.Equal(200, title.MaxLength);
    }

    [Fact]
    public void DescribeEntity_DescribesForeignKey()
    {
        var post = _introspector.DescribeEntity("Post")!;
        var fk = Assert.Single(post.ForeignKeys);
        Assert.Equal("Blog", fk.PrincipalEntity);
        Assert.Equal("Post", fk.DependentEntity);
        Assert.Equal(["BlogId"], fk.Properties);
        Assert.Equal("Cascade", fk.DeleteBehavior);
        Assert.True(fk.IsRequired);
        Assert.False(fk.IsUnique);
    }

    [Fact]
    public void DescribeEntity_DescribesNavigationsWithInverse()
    {
        var blog = _introspector.DescribeEntity("Blog")!;
        var posts = Assert.Single(blog.Navigations);
        Assert.True(posts.IsCollection);
        Assert.Equal("Post", posts.TargetEntity);
        Assert.Equal("Blog", posts.InverseName);
        var post = _introspector.DescribeEntity("Post")!;
        var nav = Assert.Single(post.Navigations);
        Assert.False(nav.IsCollection);
        Assert.True(nav.IsOnDependent);
    }

    [Fact]
    public void DescribeEntity_DescribesUniqueIndex()
    {
        var blog = _introspector.DescribeEntity("Blog")!;
        var index = Assert.Single(blog.Indexes, i => i.Properties.SequenceEqual(["Title"]));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void ListEntityNames_ReturnsShortNamesSorted()
    {
        Assert.Equal(["Blog", "Post"], _introspector.ListEntityNames());
    }

    [Fact]
    public void EntityNotFoundMessage_SuggestsCloseMatch()
    {
        var message = _introspector.EntityNotFoundMessage("Blug");
        Assert.Contains("Did you mean: Blog", message);
    }

    [Fact]
    public void EntityNotFoundMessage_SuggestsSubstringMatch()
    {
        var message = _introspector.EntityNotFoundMessage("BlogPost");
        Assert.Contains("Did you mean:", message);
    }

    [Fact]
    public void EntityNotFoundMessage_ListsAllEntitiesWhenNothingIsClose()
    {
        var message = _introspector.EntityNotFoundMessage("Zzzzzzz");
        Assert.Contains("Available entities: Blog, Post", message);
    }
}
