using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EfCoreMcp.Core.Domain;
using ModelContextProtocol.Server;

namespace EfCoreMcp.Tools
{
    public interface IQueryTools
    {
        Task<QueryResult> QuerySql(string sql, int maxRows = 100, int timeoutSeconds = 30, CancellationToken ct = default);
        Task<QueryResult> QueryEntity(
            string entityName,
            int maxRows = 100,
            int skip = 0,
            string? orderBy = null,
            bool orderDescending = false,
            string? filter = null,
            IReadOnlyDictionary<string, object>? filterParameters = null,
            int timeoutSeconds = 30,
            CancellationToken ct = default);
        Task<long> CountEntity(string entityName, CancellationToken ct = default);
        Task<ExecutionPlanResult> ExplainSql(string sql, int timeoutSeconds = 30, CancellationToken ct = default);
    }
}
