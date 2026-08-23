using System.ComponentModel;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools
{
    public interface IMigrationTools
    {
        Task<MigrationStatus> MigrationStatus(CancellationToken ct = default);
        ModelDiff DiffPendingChanges();
    }
}