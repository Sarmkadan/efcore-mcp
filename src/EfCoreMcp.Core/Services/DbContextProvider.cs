using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Provides access to a DbContext loaded from a specified assembly.
/// Supports automatic reload when the assembly changes on disk.
/// </summary>
public sealed class DbContextProvider : IDbContextProvider
{
    private readonly ContextConnectionOptions _options;
    private readonly Lock _gate = new();
    private DbContextCache _cache;

    public DbContextProvider(ContextConnectionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cache = new DbContextCache(_options, this);
    }

    public DbContext GetContext()
    {
        lock (_gate)
        {
            return _cache.GetOrCreate();
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

    /// <summary>
    /// Reloads the assembly and creates a fresh DbContext.
    /// This should be called when the assembly file changes on disk.
    /// </summary>
    public void Reload()
    {
        lock (_gate)
        {
            _cache.Dispose();
            _cache = new DbContextCache(_options, this);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cache.Dispose();
        }
    }

    private sealed class DbContextCache : IDisposable
    {
        private readonly ContextConnectionOptions _options;
	private readonly IModelIntrospector? _introspector;
        private AssemblyLoadContext? _loadContext;
        private Func<DbContext>? _factory;
        private FileSystemWatcher? _watcher;
        private DateTime _lastAssemblyWriteTime;
        private bool _disposed;

        public DbContextCache(ContextConnectionOptions options, IDbContextProvider? provider = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
		_introspector = provider as IModelIntrospector;
            Initialize();
        }

        private void Initialize()
        {
            var assemblyPath = Path.GetFullPath(_options.AssemblyPath);
            var assemblyDir = Path.GetDirectoryName(assemblyPath)!;
            var assemblyName = Path.GetFileName(assemblyPath);

            // Copy assembly to temp directory for shadow loading
            var tempPath = CreateTempAssemblyCopy(assemblyPath);

            // Load assembly in a collectible context
            _loadContext = new AssemblyLoadContext("efcore-mcp-target-" + Guid.NewGuid(), isCollectible: true);
            _loadContext.Resolving += (ctx, name) =>
            {
                var candidate = Path.Combine(Path.GetDirectoryName(tempPath)!, name.Name + ".dll");
                return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
            };

            var assembly = _loadContext.LoadFromAssemblyPath(tempPath);
            _lastAssemblyWriteTime = File.GetLastWriteTimeUtc(assemblyPath);

            // Set up file watcher to detect assembly changes
            SetupAssemblyWatcher(assemblyPath, tempPath);

            // Create context factory
            var contextType = ResolveContextType(assembly);
            var factory = FindDesignTimeFactory(assembly, contextType);
            _factory = factory ?? CreateDefaultFactory(contextType);
        }

        private static string CreateTempAssemblyCopy(string sourcePath)
        {
            var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
            var tempDir = Path.Combine(Path.GetTempPath(), "efcore-mcp-assemblies", sourceFileName);
            Directory.CreateDirectory(tempDir);

            var tempPath = Path.Combine(tempDir, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, tempPath, overwrite: true);

            // Clean up temp files on application exit
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* Best effort cleanup */ }
            };

            return tempPath;
        }

        private void SetupAssemblyWatcher(string assemblyPath, string tempPath)
        {
            var assemblyDir = Path.GetDirectoryName(assemblyPath)!;

            _watcher = new FileSystemWatcher(assemblyDir, Path.GetFileName(assemblyPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, e) => HandleAssemblyChanged(assemblyPath, tempPath, e);
            _watcher.Deleted += (_, e) => HandleAssemblyChanged(assemblyPath, tempPath, e);
            _watcher.Created += (_, e) => HandleAssemblyChanged(assemblyPath, tempPath, e);
        }

        private void HandleAssemblyChanged(string assemblyPath, string tempPath, FileSystemEventArgs e)
        {
            // Debounce rapid changes
            Thread.Sleep(200);

            try
            {
                var currentWriteTime = File.GetLastWriteTimeUtc(assemblyPath);
                if (currentWriteTime != _lastAssemblyWriteTime)
                {
                    lock (this)
                    {
                        if (!_disposed && currentWriteTime != _lastAssemblyWriteTime)
                        {
                            _lastAssemblyWriteTime = currentWriteTime;
                            ReloadInternal(assemblyPath, tempPath);
                        }
                    }
                }
            }
            catch { /* Ignore errors during watcher events */ }
        }

        private void ReloadInternal(string assemblyPath, string tempPath)
        {
            try
            {
                // Dispose old context and load context
                _loadContext?.Unload();
		 _introspector?.InvalidateCache();
                _loadContext = null;

                // Clean up temp files before recreating
                try { File.Delete(tempPath); }
                catch { /* Best effort */ }

                // Recreate cache with new assembly
                Initialize();
            }
            catch (Exception ex)
            {
                // If reload fails, keep using old context
                // This prevents crashes during development when assembly is in invalid state
                Console.Error.WriteLine($"Failed to reload assembly: {ex.Message}");
            }
        }

        public DbContext GetOrCreate()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextCache));

            return _factory!();
        }

        private Func<DbContext> CreateDefaultFactory(Type contextType)
        {
            var ctor = contextType.GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                throw new InvalidOperationException(
                    $"Cannot instantiate '{contextType.FullName}': no parameterless constructor.");
            }

            var connectionString = _options.ConnectionString;
            return () =>
            {
                var context = (DbContext)ctor.Invoke(null);
                if (connectionString is { Length: > 0 })
                    context.Database.SetConnectionString(connectionString);
                return context;
            };
        }

        private Type ResolveContextType(Assembly assembly)
        {
            var contextTypes = assembly.GetTypes()
                .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            if (_options.ContextTypeName is { } name)
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

        private Func<DbContext>? FindDesignTimeFactory(Assembly assembly, Type contextType)
        {
            var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
            var factoryType = assembly.GetTypes()
                .FirstOrDefault(t => !t.IsAbstract && factoryInterface.IsAssignableFrom(t));
            if (factoryType is null) return null;

            var factory = Activator.CreateInstance(factoryType)!;
            var method = factoryInterface.GetMethod("CreateDbContext")!;
            return () => (DbContext)method.Invoke(factory, [Array.Empty<string>()])!;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _watcher?.Dispose(); }
            catch { /* Best effort */ }

            try { _loadContext?.Unload();
		 _introspector?.InvalidateCache(); }
            catch { /* Best effort */ }
        }
    }
}