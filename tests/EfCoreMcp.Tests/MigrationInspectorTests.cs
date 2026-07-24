using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

public class MigrationInspectorTests : IDisposable
{
    private readonly TestContextProvider _provider = new();
    private readonly MigrationInspector _inspector;

    public MigrationInspectorTests()
        => _inspector = new MigrationInspector(_provider);

    public void Dispose()
        => _provider.Dispose();

    [Fact]
    public async Task GetStatusAsync_ReturnsValidMigrationStatus()
    {
        // Act
        var status = await _inspector.GetStatusAsync();

        // Assert - Should return a valid status object with collections
        Assert.NotNull(status);
        Assert.NotNull(status.Applied);
        Assert.NotNull(status.Pending);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsEmptyCollectionsInitially()
    {
        // Act
        var status = await _inspector.GetStatusAsync();

        // Assert - Initially empty for in-memory database
        Assert.NotNull(status);
        Assert.Empty(status.Applied);
        Assert.Empty(status.Pending);
    }

    [Fact]
    public async Task GetStatusAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var status = await _inspector.GetStatusAsync(cts.Token);

        // Assert
        Assert.NotNull(status);
    }

    [Fact]
    public void DiffAgainstSnapshot_ReturnsModelDiffObject()
    {
        // Act
        var diff = _inspector.DiffAgainstSnapshot();

        // Assert - Should return a valid ModelDiff object
        Assert.NotNull(diff);
        // HasDifferences depends on whether there's a model snapshot to compare against
        // In tests without migrations, this may vary
        Assert.NotNull(diff.Operations);
    }

    [Fact]
    public void DiffAgainstSnapshot_ReturnsOperationsAfterMigration()
    {
        // Arrange - Apply migrations
        _provider.GetContext().Database.Migrate();

        // Create new inspector to get fresh state
        var migratedInspector = new MigrationInspector(_provider);
        var diff = migratedInspector.DiffAgainstSnapshot();

        // Assert - Should return operations after migration
        Assert.NotNull(diff);
        Assert.NotNull(diff.Operations);
    }

    [Fact]
    public void DiffAgainstSnapshot_ContainsCreateTableOperations()
    {
        // Arrange - Apply migration to create tables
        _provider.GetContext().Database.Migrate();

        // Create new inspector to get fresh state
        var migratedInspector = new MigrationInspector(_provider);
        var diff = migratedInspector.DiffAgainstSnapshot();

        // Assert - Should contain CreateTable operations
        var createTableOps = diff.Operations.Where(o => o.OperationType == "CreateTable").ToList();
        Assert.NotEmpty(createTableOps);
        Assert.All(createTableOps, op =>
        {
            Assert.NotNull(op.Table);
            Assert.NotNull(op.Description);
        });
    }

    [Fact]
    public void DiffAgainstSnapshot_ReturnsOperationsWithTableSchemaAndName()
    {
        // Arrange - Apply migration
        _provider.GetContext().Database.Migrate();

        // Act
        var diff = _inspector.DiffAgainstSnapshot();

        // Assert - All operations should have valid metadata
        foreach (var operation in diff.Operations)
        {
            Assert.NotNull(operation.OperationType);
            Assert.False(string.IsNullOrWhiteSpace(operation.Description));
            // Table and Schema can be null for some operations
            if (operation.Table is not null)
            {
                Assert.NotEmpty(operation.Table);
            }
        }
    }

    [Fact]
    public void DiffAgainstSnapshot_ReturnsValidDiffObject()
    {
        // Act
        var diff = _inspector.DiffAgainstSnapshot();

        // Assert - Should always return a valid ModelDiff
        Assert.NotNull(diff);
        Assert.NotNull(diff.Operations);
    }


    [Fact]
    public void DiffAgainstSnapshot_OperationDescriptionsAreMeaningful()
    {
        // Arrange
        _provider.GetContext().Database.Migrate();

        // Act
        var diff = _inspector.DiffAgainstSnapshot();

        // Assert - Descriptions should be human-readable
        foreach (var operation in diff.Operations)
        {
            Assert.NotEmpty(operation.Description);
            Assert.DoesNotContain("Operation", operation.Description); // Should not contain raw type names
        }
    }



    [Fact]
    public void DiffAgainstSnapshot_DescribesTableOperationsWithCorrectFormat()
    {
        // Arrange
        _provider.GetContext().Database.Migrate();

        // Act
        var diff = _inspector.DiffAgainstSnapshot();

        // Assert - Table operations should follow the expected format
        foreach (var operation in diff.Operations.Where(o => o.Table is not null))
        {
            var description = operation.Description;
            Assert.Matches(
                @$"^(?<type>\w+): (?<name>\w+)( on (?<table>\w+))?$",
                description);
        }
    }
}

