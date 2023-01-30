using System.Reflection;
using System.Runtime.Loader;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EfCoreMcp.Core.Services;

public sealed class DbContextProvider(ContextConnectionOptions options) : IDbContextProvider
{
    private DbContext? _context;
    private readonly Lock _gate = new();

    public DbContext GetContext()
    {
        lock (_gate)
        {
            return _context ??= CreateContext();
        }
    }

    public ContextInfo GetContextInfo()
    {
        var ctx = GetContext();
        bool canConnect;
        try { canConnect = ctx.Database.CanConnect(); }
        catch { canConnect = false; }
        return new ContextInfo(
            ctx.GetType().FullName ?? ctx.GetType().Name,
            ctx.GetType().Assembly.GetName().Name ?? "",
            ctx.Database.ProviderName,
            TryGetDatabaseName(ctx),
            canConnect);
    }

    private static string? TryGetDatabaseName(DbContext ctx)
    {
        try { return ctx.Database.GetDbConnection().Database; }
        catch { return null; }
    }

    private DbContext CreateContext()
    {
        var assembly = LoadAssembly(options.AssemblyPath);
        var contextType = ResolveContextType(assembly);
        var factory = FindDesignTimeFactory(assembly, contextType);
        if (factory is not null)
            return factory();
        var ctor = contextType.GetConstructor(Type.EmptyTypes);
        if (ctor is not null)
            return (DbContext)ctor.Invoke(null);
        throw new InvalidOperationException(
            $"Cannot instantiate '{contextType.FullName}': no IDesignTimeDbContextFactory found and no parameterless constructor.");
    }

    private static Assembly LoadAssembly(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException($"Assembly not found: {full}", full);
        var loadContext = new AssemblyLoadContext("efcore-mcp-target", isCollectible: false);
        loadContext.Resolving += (ctx, name) =>
        {
            var candidate = Path.Combine(Path.GetDirectoryName(full)!, name.Name + ".dll");
            return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
        };
        return loadContext.LoadFromAssemblyPath(full);
    }

    private Type ResolveContextType(Assembly assembly)
    {
        var contextTypes = assembly.GetTypes()
            .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();
        if (options.ContextTypeName is { } name)
        {
            var match = contextTypes.FirstOrDefault(t =>
                string.Equals(t.FullName, name, StringComparison.Ordinal) ||
                string.Equals(t.Name, name, StringComparison.Ordinal));
            return match ?? throw new InvalidOperationException(
                $"DbContext '{name}' not found. Available: {string.Join(", ", contextTypes.Select(t => t.Name))}");
        }
        return contextTypes.Count switch
        {
            0 => throw new InvalidOperationException("No DbContext types found in the assembly."),
            1 => contextTypes[0],
            _ => throw new InvalidOperationException(
                $"Multiple DbContext types found, specify one: {string.Join(", ", contextTypes.Select(t => t.Name))}")
        };
    }

    private static Func<DbContext>? FindDesignTimeFactory(Assembly assembly, Type contextType)
    {
        var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
        var factoryType = assembly.GetTypes()
            .FirstOrDefault(t => !t.IsAbstract && factoryInterface.IsAssignableFrom(t));
        if (factoryType is null)
            return null;
        var factory = Activator.CreateInstance(factoryType)!;
        var method = factoryInterface.GetMethod("CreateDbContext")!;
        return () => (DbContext)method.Invoke(factory, [Array.Empty<string>()])!;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
