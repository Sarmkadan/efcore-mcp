namespace EfCoreMcp.Core.Services;

/// <summary>
/// Validates the operator-supplied path of the compiled DbContext assembly before it is loaded.
/// </summary>
/// <remarks>
/// The assembly path is a trusted setting. It comes from <c>--assembly</c> or <c>EFCORE_MCP_ASSEMBLY</c> at startup,
/// and no MCP tool accepts a path. Loading an assembly runs its code, so a path a client could choose would amount to
/// arbitrary code execution. These checks reject traversal sequences, files that are not assemblies, missing files,
/// and, when an allowed root is configured, any file that resolves outside that directory.
/// </remarks>
public static class AssemblyPathValidator
{
    private static readonly char[] Separators = ['/', '\\'];

    private static readonly string[] AssemblyExtensions = [".dll", ".exe"];

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Validates a directory that restricts where the assembly may be loaded from, and returns its absolute path.
    /// </summary>
    /// <param name="root">The directory path supplied by the operator.</param>
    /// <returns>The absolute path of the directory, without a trailing separator unless it is a filesystem root.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="root"/> is empty, contains a <c>..</c> segment, or does not name an existing directory.
    /// </exception>
    public static string ResolveRoot(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        RejectTraversal(root, nameof(root));

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(fullPath))
        {
            throw new ArgumentException($"Assembly root '{root}' is not an existing directory.", nameof(root));
        }

        return fullPath;
    }

    /// <summary>
    /// Validates the assembly path and returns its absolute, symlink-resolved location.
    /// </summary>
    /// <param name="assemblyPath">The assembly path supplied by the operator. Must not contain a <c>..</c> segment.</param>
    /// <param name="allowedRoot">
    /// Directory the assembly must resolve into, or <see langword="null"/> to accept any location.
    /// </param>
    /// <returns>The absolute path of the assembly file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblyPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the path is empty, contains a <c>..</c> segment, does not end in <c>.dll</c> or <c>.exe</c>,
    /// resolves outside <paramref name="allowedRoot"/>, or <paramref name="allowedRoot"/> is not an existing directory.
    /// </exception>
    /// <remarks>
    /// The file itself is not required to exist here; a missing file fails when the assembly is loaded.
    /// Symlinks are checked at the final path component only. A symlink whose target lies outside
    /// <paramref name="allowedRoot"/> is rejected. Directories above the file are not resolved, so pass the real
    /// directory path as the root.
    /// </remarks>
    public static string Resolve(string assemblyPath, string? allowedRoot = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);
        RejectTraversal(assemblyPath, nameof(assemblyPath));

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!AssemblyExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Assembly path '{assemblyPath}' must end in .dll or .exe.", nameof(assemblyPath));
        }

        if (allowedRoot is null)
        {
            return fullPath;
        }

        var root = ResolveRoot(allowedRoot);
        var target = File.Exists(fullPath)
            ? new FileInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fullPath
            : fullPath;
        if (!IsWithin(fullPath, root) || !IsWithin(target, root))
        {
            throw new ArgumentException(
                $"Assembly '{assemblyPath}' resolves outside the allowed assembly root '{root}'.", nameof(assemblyPath));
        }

        return fullPath;
    }

    private static void RejectTraversal(string path, string paramName)
    {
        if (path.Split(Separators).Any(segment => segment == ".."))
        {
            throw new ArgumentException(
                $"Path '{path}' contains a '..' segment. Use an absolute path instead.", paramName);
        }
    }

    private static bool IsWithin(string path, string root)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison);
    }
}
