using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

public class ModelToolsTests : IDisposable
{
    private readonly TestContextProvider _provider;
    private readonly ModelIntrospector _introspector;
    private readonly SchemaExplainer _explainer;
    private readonly ModelTools _modelTools;

    public ModelToolsTests()
    {
        _provider = new TestContextProvider();
        _introspector = new ModelIntrospector(_provider);
        _explainer = new SchemaExplainer(_introspector);
        _modelTools = new ModelTools(_introspector, _explainer, _provider);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void ContextInfo_ReturnsValidContextInfo()
    {
        var info = _modelTools.ContextInfo();

        Assert.NotNull(info);
        Assert.Equal(nameof(BlogContext), info.ContextType);
        Assert.Equal("EfCoreMcp.Tests", info.AssemblyName);
        Assert.NotNull(info.ProviderName);
        Assert.NotEmpty(info.ProviderName);
    }

    [Fact]
    public void ListEntities_ReturnsEntityNames()
    {
        var entities = _modelTools.ListEntities();

        Assert.NotNull(entities);
        Assert.NotEmpty(entities);
        Assert.Equal(["Blog", "Post"], entities);
    }

    [Fact]
    public void ListEntities_WithEmptyModel_ReturnsEmptyList()
    {
        // Arrange
        var emptyProvider = new EmptyContextProvider();
        var emptyIntrospector = new ModelIntrospector(emptyProvider);
        var emptyExplainer = new SchemaExplainer(emptyIntrospector);
        var emptyTools = new ModelTools(emptyIntrospector, emptyExplainer, emptyProvider);

        // Act
        var entities = emptyTools.ListEntities();

        // Assert
        Assert.NotNull(entities);
        Assert.Empty(entities);

        emptyProvider.Dispose();
    }

    [Fact]
    public void DescribeModel_ReturnsValidModelDescriptor()
    {
        var model = _modelTools.DescribeModel();

        Assert.NotNull(model);
        Assert.Equal("BlogContext", model.ContextName);
        Assert.NotNull(model.ProviderName);
        Assert.NotEmpty(model.ProviderName);
        Assert.NotNull(model.Entities);
        Assert.Equal(2, model.Entities.Count);
        Assert.Contains(model.Entities, e => e.Name == "Blog");
        Assert.Contains(model.Entities, e => e.Name == "Post");
    }

    [Fact]
    public void DescribeEntity_WithValidEntityName_ReturnsEntityDescriptor()
    {
        var blog = _modelTools.DescribeEntity("Blog");

        Assert.NotNull(blog);
        Assert.Equal("Blog", blog.Name);
        Assert.Equal("blogs", blog.TableName);
        Assert.NotNull(blog.Properties);
        Assert.NotEmpty(blog.Properties);
    }

    [Fact]
    public void DescribeEntity_WithInvalidEntityName_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _modelTools.DescribeEntity("Nonexistent"));
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeEntity_WithNullEntityName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _modelTools.DescribeEntity(null!));
    }

    [Fact]
    public void DescribeEntity_WithEmptyEntityName_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _modelTools.DescribeEntity(""));
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplainSchema_ReturnsNonEmptyMarkdown()
    {
        var schema = _modelTools.ExplainSchema();

        Assert.NotNull(schema);
        Assert.NotEmpty(schema);
        Assert.Contains("# BlogContext", schema);
        Assert.Contains("Entities: 2", schema);
    }

    [Fact]
    public void ExplainEntity_WithValidEntityName_ReturnsNonEmptyMarkdown()
    {
        var explanation = _modelTools.ExplainEntity("Blog");

        Assert.NotNull(explanation);
        Assert.NotEmpty(explanation);
        Assert.Contains("# Blog", explanation);
        Assert.Contains("Table: blogs", explanation);
    }

    [Fact]
    public void ExplainEntity_WithInvalidEntityName_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _modelTools.ExplainEntity("Nonexistent"));
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RelationshipGraph_ReturnsValidMermaidDiagram()
    {
        var graph = _modelTools.RelationshipGraph();

        Assert.NotNull(graph);
        Assert.NotEmpty(graph);
        Assert.StartsWith("erDiagram", graph);
        Assert.Contains("Blog {", graph);
        Assert.Contains("Post {", graph);
        Assert.Contains("Blog ||--o{ Post", graph);
    }

    [Fact]
    public void AllMethods_WithCaseInsensitiveEntityNames_WorkCorrectly()
    {
        // Test case insensitivity for entity names
        var blog1 = _modelTools.DescribeEntity("blog");
        var blog2 = _modelTools.DescribeEntity("BLOG");
        var blog3 = _modelTools.DescribeEntity("Blog");

        Assert.NotNull(blog1);
        Assert.NotNull(blog2);
        Assert.NotNull(blog3);
        Assert.Equal(blog1.Name, blog2.Name);
        Assert.Equal(blog2.Name, blog3.Name);
    }

    private sealed class EmptyContextProvider : IDbContextProvider
    {
        public DbContext GetContext() => new EmptyDbContext();
        public ContextInfo GetContextInfo() => new("EmptyContext", "EfCoreMcp.Tests", null, null, false);
        public void Dispose() { }
    }

    private sealed class EmptyDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite("DataSource=:memory:");
    }
}
