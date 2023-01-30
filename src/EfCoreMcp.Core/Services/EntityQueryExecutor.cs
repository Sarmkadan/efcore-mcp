using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMcp.Core.Services;

public sealed class EntityQueryExecutor(IDbContextProvider contextProvider, IModelIntrospector introspector) : IEntityQueryExecutor
{
    private static readonly MethodInfo FetchMethod =
        typeof(EntityQueryExecutor).GetMethod(nameof(FetchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo CountMethod =
        typeof(EntityQueryExecutor).GetMethod(nameof(CountCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task<QueryResult> ExecuteAsync(EntityQueryRequest request, CancellationToken ct = default)
    {
        var entityType = ResolveEntityType(request.EntityName);
        if (request.OrderBy is { } orderBy && entityType.FindProperty(orderBy) is null)
            throw new InvalidOperationException($"Property '{orderBy}' not found on '{entityType.ShortName()}'.");
        var ctx = contextProvider.GetContext();
        var take = Math.Clamp(request.Take, 1, 1000);
        var sw = Stopwatch.StartNew();
        var task = (Task<List<object>>)FetchMethod
            .MakeGenericMethod(entityType.ClrType)
            .Invoke(null, [ctx, request.OrderBy, request.OrderDescending, Math.Max(request.Skip, 0), take + 1, ct])!;
        var items = await task;
        sw.Stop();
        var truncated = items.Count > take;
        if (truncated)
            items.RemoveAt(items.Count - 1);
        var scalarProps = entityType.GetProperties().Where(p => !p.IsShadowProperty()).ToList();
        var columns = scalarProps.Select(p => p.Name).ToList();
        var rows = items
            .Select(IReadOnlyList<object?> (item) => scalarProps
                .Select(p => SqlQueryExecutor.Normalize(p.PropertyInfo?.GetValue(item) ?? p.FieldInfo?.GetValue(item)))
                .ToList())
            .ToList();
        return new QueryResult(columns, rows, rows.Count, truncated, sw.ElapsedMilliseconds);
    }

    public async Task<long> CountAsync(string entityName, CancellationToken ct = default)
    {
        var entityType = ResolveEntityType(entityName);
        var ctx = contextProvider.GetContext();
        var task = (Task<long>)CountMethod.MakeGenericMethod(entityType.ClrType).Invoke(null, [ctx, ct])!;
        return await task;
    }

    private Microsoft.EntityFrameworkCore.Metadata.IEntityType ResolveEntityType(string entityName)
    {
        if (introspector is ModelIntrospector concrete && concrete.FindEntityType(entityName) is { } found)
            return found;
        throw new InvalidOperationException($"Entity '{entityName}' not found in the model.");
    }

    private static async Task<List<object>> FetchAsync<T>(
        DbContext ctx, string? orderBy, bool descending, int skip, int take, CancellationToken ct)
        where T : class
    {
        var query = ctx.Set<T>().AsNoTracking();
        if (orderBy is not null)
        {
            var parameter = Expression.Parameter(typeof(T), "e");
            var body = Expression.Convert(Expression.PropertyOrField(parameter, orderBy), typeof(object));
            var lambda = Expression.Lambda<Func<T, object>>(body, parameter);
            query = descending ? query.OrderByDescending(lambda) : query.OrderBy(lambda);
        }
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);
        return [.. items.Cast<object>()];
    }

    private static Task<long> CountCoreAsync<T>(DbContext ctx, CancellationToken ct) where T : class =>
        ctx.Set<T>().AsNoTracking().LongCountAsync(ct);
}
