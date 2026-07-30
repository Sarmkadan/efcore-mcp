using System;
using System.Text;
using EfCoreMcp.Core.Services;

namespace EfCoreMcp.Core.Domain;

/// <summary>
/// Options for connecting to a DbContext, including assembly path and optional connection string.
/// </summary>
/// <param name="AssemblyPath">Path to the assembly containing the DbContext.</param>
/// <param name="ContextTypeName">Optional name of the DbContext type to use.</param>
/// <param name="ConnectionString">
/// Optional connection string with credentials.
/// <para>SECURITY: This value is sanitized before logging or displaying. Never expose the raw value.</para>
/// </param>
/// <param name="Provider">Database provider name (defaults to "auto").</param>
public sealed class ContextConnectionOptions : IEquatable<ContextConnectionOptions>
{
    public required string AssemblyPath { get; init; }

    public string? ContextTypeName { get; init; }

    public string? ConnectionString { get; init; }

    public string Provider { get; init; } = "auto";

    /// <summary>
    /// Returns a string representation that excludes sensitive connection string information.
    /// </summary>
    /// <returns>A sanitized string representation.</returns>
    /// <remarks>
    /// SECURITY: This method ensures that no credentials are exposed in the string representation.
    /// The ConnectionString property is always displayed using <see cref="ConnectionStringSanitizer.GetRedactedDisplay"/>.
    /// </remarks>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(nameof(ContextConnectionOptions));
        sb.Append(" { AssemblyPath = ");
        sb.Append(AssemblyPath);
        sb.Append(", ContextTypeName = ");
        sb.Append(ContextTypeName);
        sb.Append(", Provider = ");
        sb.Append(Provider);
        sb.Append(", ConnectionString = ");
        sb.Append(ConnectionStringSanitizer.GetRedactedDisplay(ConnectionString));
        sb.Append(" }");
        return sb.ToString();
    }

    // IEquatable implementation
    public bool Equals(ContextConnectionOptions? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(AssemblyPath, other.AssemblyPath, StringComparison.Ordinal) &&
               string.Equals(ContextTypeName, other.ContextTypeName, StringComparison.Ordinal) &&
               string.Equals(ConnectionString, other.ConnectionString, StringComparison.Ordinal) &&
               string.Equals(Provider, other.Provider, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as ContextConnectionOptions);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            AssemblyPath,
            ContextTypeName,
            ConnectionString,
            Provider);
    }

    public static bool operator ==(ContextConnectionOptions? left, ContextConnectionOptions? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ContextConnectionOptions? left, ContextConnectionOptions? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Information about a loaded DbContext, excluding sensitive connection details.
/// </summary>
/// <param name="ContextType">Full name of the DbContext type.</param>
/// <param name="AssemblyName">Name of the assembly containing the DbContext.</param>
/// <param name="ProviderName">Database provider name.</param>
/// <param name="Database">Name of the database.</param>
/// <param name="CanConnect">Whether the context can connect to the database.</param>
/// <param name="AvailableContextTypes">
/// List of all available DbContext types in the assembly (null if single context).
/// </param>
public sealed record ContextInfo(
    string ContextType,
    string AssemblyName,
    string? ProviderName,
    string? Database,
    bool CanConnect,
    IReadOnlyList<string>? AvailableContextTypes = null
);
