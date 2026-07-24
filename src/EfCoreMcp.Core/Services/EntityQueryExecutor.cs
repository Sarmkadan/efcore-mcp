using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Entity query executor with transient fault retry and cancellation support.
/// </summary>
public sealed class EntityQueryExecutor(IDbContextProvider contextProvider, IModelIntrospector introspector) : IEntityQueryExecutor
{
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMilliseconds = 100;

    /// <summary>
    /// Executes an entity query with transient fault retry and cancellation support.
    /// </summary>
    /// <param name="request">The entity query request containing query parameters</param>
    /// <param name="ct">Cancellation token for cooperative cancellation</param>
    /// <returns>Query result with columns, rows, and metadata</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    /// <exception cref="TimeoutException">Thrown when the query exceeds the underlying timeout</exception>
    /// <exception cref="Exception">Thrown for errors after all retry attempts are exhausted</exception>
    public async Task<QueryResult> ExecuteAsync(EntityQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrderBy is { } orderBy && ResolveEntityType(request.EntityName).FindProperty(orderBy) is null)
            throw new InvalidOperationException(
                $"Property '{orderBy}' not found on entity '{request.EntityName}'. " +
                $"Available properties: {string.Join(", ", ResolveEntityType(request.EntityName).GetProperties().Select(p => p.Name))}.");

        var ctx = contextProvider.GetContext();
        var take = Math.Clamp(request.Take, 1, 1000);
        var sw = Stopwatch.StartNew();

        try
        {
            // Execute with retry policy for transient errors
            var items = await ExecuteWithRetryAsync(async innerCt =>
            {
                var task = (Task<List<object>>)FetchMethod
                    .MakeGenericMethod(ResolveEntityType(request.EntityName).ClrType)
                    .Invoke(null, [ctx, request.OrderBy, request.OrderDescending, Math.Max(request.Skip, 0), take + 1, innerCt])!;
                return await task;
            }, ct);

            sw.Stop();
            var truncated = items.Count > take;
            if (truncated)
                items.RemoveAt(items.Count - 1);

            var scalarProps = ResolveEntityType(request.EntityName).GetProperties().Where(p => !p.IsShadowProperty()).ToList();
            var columns = scalarProps.Select(p => p.Name).ToList();
            var rows = items
                .Select(IReadOnlyList<object?> (item) => scalarProps
                    .Select(p => SqlQueryExecutor.Normalize(p.PropertyInfo?.GetValue(item) ?? p.FieldInfo?.GetValue(item)))
                    .ToList())
                .ToList();

            return new QueryResult(columns, rows, rows.Count, truncated, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // Distinguish between user cancellation and timeout cancellation
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException("Entity query execution was cancelled by the client.");
            }
            throw;
        }
        catch (Exception ex) when (SqlQueryExecutor.IsTransientError(ex))
        {
            // Wrap transient errors after retry attempts
            throw new Exception(
                $"Transient error occurred after {MaxRetryAttempts} retry attempts. " +
                $"Please try again. Error: {SqlQueryExecutor.GetErrorMessage(ex)}",
                ex);
        }
    }

    /// <summary>
    /// Counts entities with transient fault retry and cancellation support.
    /// </summary>
    /// <param name="entityName">Name of the entity to count</param>
    /// <param name="ct">Cancellation token for cooperative cancellation</param>
    /// <returns>Count of entities</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
    /// <exception cref="TimeoutException">Thrown when the query exceeds the underlying timeout</exception>
    /// <exception cref="Exception">Thrown for errors after all retry attempts are exhausted</exception>
    public async Task<long> CountAsync(string entityName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        var ctx = contextProvider.GetContext();

        try
        {
            // Execute with retry policy for transient errors
            return await ExecuteWithRetryAsync(async innerCt =>
            {
                var task = (Task<long>)CountMethod.MakeGenericMethod(ResolveEntityType(entityName).ClrType)
                    .Invoke(null, [ctx, innerCt])!;
                return await task;
            }, ct);
        }
        catch (OperationCanceledException)
        {
            // Distinguish between user cancellation and timeout cancellation
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException("Entity count operation was cancelled by the client.");
            }
            throw;
        }
        catch (Exception ex) when (SqlQueryExecutor.IsTransientError(ex))
        {
            // Wrap transient errors after retry attempts
            throw new Exception(
                $"Transient error occurred after {MaxRetryAttempts} retry attempts. " +
                $"Please try again. Error: {SqlQueryExecutor.GetErrorMessage(ex)}",
                ex);
        }
    }

    /// <summary>
    /// Executes a database operation with retry logic for transient errors.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The database operation to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    /// <exception cref="TimeoutException">Thrown when operation times out</exception>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct) where T : notnull
    {
        var attempt = 0;
        var lastException = (Exception?)null;

        while (true)
        {
            // Check cancellation before each attempt
            ct.ThrowIfCancellationRequested();

            try
            {
                return await operation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (SqlQueryExecutor.IsTransientError(ex))
            {
                attempt++;
                lastException = ex;

                if (attempt >= MaxRetryAttempts)
                {
                    throw new Exception(
                        $"Transient error occurred after {MaxRetryAttempts} retry attempts. " +
                        $"Error: {SqlQueryExecutor.GetErrorMessage(ex)}",
                        ex);
                }

                // Delay before retry, but respect cancellation
                try
                {
                    await Task.Delay(RetryDelayMilliseconds, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }
    }

    private Microsoft.EntityFrameworkCore.Metadata.IEntityType ResolveEntityType(string entityName)
    {
        if (introspector is ModelIntrospector concrete && concrete.FindEntityType(entityName) is { } found)
            return found;
        throw new InvalidOperationException(introspector.EntityNotFoundMessage(entityName));
    }

    private static readonly MethodInfo FetchMethod =
        typeof(EntityQueryExecutor).GetMethod(nameof(FetchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CountMethod =
        typeof(EntityQueryExecutor).GetMethod(nameof(CountCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static async Task<List<object>> FetchAsync<T>(
        DbContext ctx,
        string? orderBy,
        bool descending,
        int skip,
        int take,
        CancellationToken ct) where T : class
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
