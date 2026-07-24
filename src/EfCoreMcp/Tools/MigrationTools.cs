using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class MigrationTools(IMigrationInspector inspector)
{
    /// <summary>
    /// Lists applied and pending migrations and indicates whether the model has drifted from the last snapshot.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="MigrationStatus"/> containing applied migrations, pending migrations, and model drift status.</returns>
    [McpServerTool(Name = "migration_status"), Description("List applied and pending migrations and whether the model has drifted from the last snapshot.")]
    public Task<MigrationStatus> MigrationStatus(CancellationToken ct = default) => inspector.GetStatusAsync(ct);

    /// <summary>
    /// Diffs the current model against the last migration snapshot and describes the schema operations a new migration would contain.
    /// </summary>
    /// <returns>A <see cref="ModelDiff"/> containing whether differences exist and the list of operations with destructive changes flagged.</returns>
    [McpServerTool(Name = "diff_pending_changes"), Description("Diff the current model against the last migration snapshot and describe the schema operations a new migration would contain.")]
    public ModelDiff DiffPendingChanges() => inspector.DiffAgainstSnapshot();
}
