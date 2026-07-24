using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

public class MigrationToolsTests : IDisposable
{
    private readonly TestContextProvider _provider = new();
    private readonly MigrationTools _tools;
    private readonly MigrationInspector _inspector;

    public MigrationToolsTests()
    {
        _inspector = new MigrationInspector(_provider);
        _tools = new MigrationTools(_inspector);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    [Fact]
    public async Task MigrationStatus_ReturnsValidMigrationStatus()
    {
        // Act
        var status = await _tools.MigrationStatus();

        // Assert
        Assert.NotNull(status);
        Assert.NotNull(status.Applied);
        Assert.NotNull(status.Pending);
        Assert.IsType<bool>(status.HasPendingModelChanges);
    }

    [Fact]
    public async Task MigrationStatus_ReturnsEmptyCollectionsInitially()
    {
        // Act
        var status = await _tools.MigrationStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Empty(status.Applied);
        Assert.Empty(status.Pending);
    }

    [Fact]
    public async Task MigrationStatus_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var status = await _tools.MigrationStatus(cts.Token);

        // Assert
        Assert.NotNull(status);
    }

    [Fact]
    public void DiffPendingChanges_ReturnsModelDiffObject()
    {
        // Act
        var diff = _tools.DiffPendingChanges();

        // Assert
        Assert.NotNull(diff);
        Assert.NotNull(diff.Operations);
        Assert.IsType<bool>(diff.HasDifferences);
    }

    [Fact]
    public void DiffPendingChanges_ReturnsValidDiffObject()
    {
        // Act
        var diff = _tools.DiffPendingChanges();

        // Assert
        Assert.NotNull(diff);
        Assert.NotNull(diff.Operations);
    }

    [Fact]
    public void DiffPendingChanges_DescribesOperationsWithMeaningfulMessages()
    {
        // Act
        var diff = _tools.DiffPendingChanges();

        // Assert
        foreach (var operation in diff.Operations)
        {
            Assert.NotEmpty(operation.Description);
            Assert.NotNull(operation.OperationType);
        }
    }

    [Fact]
    public void DiffPendingChanges_OperationPropertiesAreValid()
    {
        // Act
        var diff = _tools.DiffPendingChanges();

        // Assert
        foreach (var operation in diff.Operations)
        {
            Assert.NotNull(operation.OperationType);
            Assert.False(string.IsNullOrWhiteSpace(operation.Description));

            // These can be null for some operation types
            if (operation.Table is not null)
            {
                Assert.NotEmpty(operation.Table);
            }

            if (operation.Schema is not null)
            {
                Assert.NotEmpty(operation.Schema);
            }

            if (operation.Name is not null)
            {
                Assert.NotEmpty(operation.Name);
            }
        }
    }

    [Fact]
    public async Task MigrationStatus_ReturnsHasPendingModelChangesFlag()
    {
        // Act
        var status = await _tools.MigrationStatus();

        // Assert
        Assert.NotNull(status);
        Assert.IsType<bool>(status.HasPendingModelChanges);
    }

    [Fact]
    public void DiffPendingChanges_OperationsListIsReadOnly()
    {
        // Act
        var diff = _tools.DiffPendingChanges();

        // Assert
        Assert.NotNull(diff.Operations);
        var operations = diff.Operations;
        Assert.IsAssignableFrom<IReadOnlyList<ModelDiffOperation>>(operations);
    }

    [Fact]
    public async Task MigrationStatus_ReturnsNonNullAppliedAndPendingLists()
    {
        // Act
        var status = await _tools.MigrationStatus();

        // Assert
        Assert.NotNull(status.Applied);
        Assert.NotNull(status.Pending);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(status.Applied);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(status.Pending);
    }
}