using EfCoreMcp.Core.Domain;

namespace EfCoreMcp;

public sealed record CliOptions(ContextConnectionOptions Connection)
{
    public static CliOptions Parse(string[] args)
    {
        string? assembly = null, context = null, connectionString = null;
        var provider = "auto";
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--assembly" or "-a" when i + 1 < args.Length:
                    assembly = args[++i];
                    break;
                case "--context" or "-c" when i + 1 < args.Length:
                    context = args[++i];
                    break;
                case "--connection" when i + 1 < args.Length:
                    connectionString = args[++i];
                    break;
                case "--provider" when i + 1 < args.Length:
                    provider = args[++i];
                    break;
            }
        }
        assembly ??= Environment.GetEnvironmentVariable("EFCORE_MCP_ASSEMBLY");
        context ??= Environment.GetEnvironmentVariable("EFCORE_MCP_CONTEXT");
        connectionString ??= Environment.GetEnvironmentVariable("EFCORE_MCP_CONNECTION");
        if (assembly is null)
            throw new ArgumentException("Missing required option --assembly <path-to-dll> (or EFCORE_MCP_ASSEMBLY).");
        return new CliOptions(new ContextConnectionOptions
        {
            AssemblyPath = assembly,
            ContextTypeName = context,
            ConnectionString = connectionString,
            Provider = provider
        });
    }
}
