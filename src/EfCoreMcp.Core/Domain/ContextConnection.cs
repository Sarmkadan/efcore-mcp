namespace EfCoreMcp.Core.Domain;

public sealed record ContextConnectionOptions
{
    public required string AssemblyPath { get; init; }
    public string? ContextTypeName { get; init; }
    public string? ConnectionString { get; init; }
    public string Provider { get; init; } = "auto";
}

public sealed record ContextInfo(
    string ContextType,
    string AssemblyName,
    string? ProviderName,
    string? Database,
    bool CanConnect);
