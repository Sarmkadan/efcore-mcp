using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace EfCoreMcp.Core.Services;

public sealed class MigrationInspector(IDbContextProvider contextProvider) : IMigrationInspector
{
    /// <summary>
    /// Gets the current migration status including applied migrations, pending migrations, and whether the model has drifted from the last snapshot.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="MigrationStatus"/> containing applied migrations, pending migrations, and model drift status.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the context provider returns null.</exception>
    public async Task<MigrationStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var ctx = contextProvider.GetContext();
        var applied = (await ctx.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var pending = (await ctx.Database.GetPendingMigrationsAsync(ct)).ToList();
        return new MigrationStatus(applied, pending, DiffAgainstSnapshot().HasDifferences);
    }

    /// <summary>
    /// Diffs the current model against the last migration snapshot and returns the list of pending operations
    /// that a new migration would contain.
    /// </summary>
    /// <returns>A <see cref="ModelDiff"/> containing whether differences exist and the list of operations.</returns>
    public ModelDiff DiffAgainstSnapshot()
    {
        var ctx = contextProvider.GetContext();
        var services = ((IInfrastructure<IServiceProvider>)ctx).Instance;
        var migrationsAssembly = services.GetRequiredService<IMigrationsAssembly>();
        var differ = services.GetRequiredService<IMigrationsModelDiffer>();
        var designTimeModel = services.GetRequiredService<IDesignTimeModel>().Model;
        var snapshotModel = ResolveSnapshotModel(services, migrationsAssembly);
        var operations = differ.GetDifferences(
            snapshotModel?.GetRelationalModel(),
            designTimeModel.GetRelationalModel());
        return new ModelDiff(operations.Count > 0, operations.Select(Describe).ToList());
    }

    private static IModel? ResolveSnapshotModel(IServiceProvider services, IMigrationsAssembly migrationsAssembly)
    {
        var snapshot = migrationsAssembly.ModelSnapshot?.Model;
        if (snapshot is null)
            return null;
        if (snapshot is IMutableModel mutable)
            snapshot = mutable.FinalizeModel();
        return services.GetRequiredService<IModelRuntimeInitializer>().Initialize(snapshot);
    }

    private static ModelDiffOperation Describe(MigrationOperation operation)
    {
        var (table, schema, name) = operation switch
        {
            CreateTableOperation o => (o.Name, o.Schema, o.Name),
            DropTableOperation o => (o.Name, o.Schema, o.Name),
            AlterTableOperation o => (o.Name, o.Schema, o.Name),
            RenameTableOperation o => (o.Name, o.Schema, o.NewName),
            AddColumnOperation o => (o.Table, o.Schema, o.Name),
            DropColumnOperation o => (o.Table, o.Schema, o.Name),
            AlterColumnOperation o => (o.Table, o.Schema, o.Name),
            RenameColumnOperation o => (o.Table, o.Schema, o.Name),
            CreateIndexOperation o => (o.Table, o.Schema, o.Name),
            DropIndexOperation o => (o.Table, o.Schema, o.Name),
            AddForeignKeyOperation o => (o.Table, o.Schema, o.Name),
            DropForeignKeyOperation o => (o.Table, o.Schema, o.Name),
            AddPrimaryKeyOperation o => (o.Table, o.Schema, o.Name),
            DropPrimaryKeyOperation o => (o.Table, o.Schema, o.Name),
            _ => (null, null, null)
        };

        var operationType = operation.GetType().Name.Replace("Operation", "");
        var isDestructive = IsDestructiveOperation(operation);
        return new ModelDiffOperation(
            operationType,
            table,
            schema,
            name,
            FormatDescription(operation, table, name),
            isDestructive);
    }

    private static bool IsDestructiveOperation(MigrationOperation operation) => operation switch
    {
        DropTableOperation => true,
        DropColumnOperation => true,
        DropIndexOperation => true,
        DropForeignKeyOperation => true,
        DropPrimaryKeyOperation => true,
        AlterColumnOperation => true,
        RenameTableOperation => true,
        RenameColumnOperation => true,
        _ => false
    };

    private static string FormatDescription(MigrationOperation operation, string? table, string? name)
    {
        var kind = operation.GetType().Name.Replace("Operation", "");
        return (table, name) switch
        {
            (not null, not null) when table != name => $"{kind}: {name} on {table}",
            (not null, _) => $"{kind}: {table}",
            (_, not null) => $"{kind}: {name}",
            _ => kind
        };
    }
}
