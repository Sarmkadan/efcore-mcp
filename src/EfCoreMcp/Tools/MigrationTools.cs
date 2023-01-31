using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools;

[McpServerToolType]
public sealed class MigrationTools(IMigrationInspector inspector)
{
    [McpServerTool(Name = "migration_status"), Description("List applied and pending migrations and whether the model has drifted from the last snapshot.")]
    public Task<MigrationStatus> MigrationStatus(CancellationToken ct = default) => inspector.GetStatusAsync(ct);

    [McpServerTool(Name = "diff_pending_changes"), Description("Diff the current model against the last migration snapshot and describe the schema operations a new migration would contain.")]
    public ModelDiff DiffPendingChanges() => inspector.DiffAgainstSnapshot();
}
