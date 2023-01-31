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
    public async Task<MigrationStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var ctx = contextProvider.GetContext();
        var applied = (await ctx.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var pending = (await ctx.Database.GetPendingMigrationsAsync(ct)).ToList();
        return new MigrationStatus(applied, pending, DiffAgainstSnapshot().HasDifferences);
    }

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
        return new ModelDiffOperation(
            operation.GetType().Name.Replace("Operation", ""),
            table,
            schema,
            name,
            FormatDescription(operation, table, name));
    }

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
