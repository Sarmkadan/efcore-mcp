using EfCoreMcp.Core.Domain;

namespace EfCoreMcp;

/// <summary>
/// Command-line options parser for EfCoreMcp.
/// </summary>
public sealed record CliOptions(ContextConnectionOptions Connection)
{
    /// <summary>
    /// Exception thrown when command-line help is requested.
    /// </summary>
    public sealed class HelpRequestedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HelpRequestedException"/> class.
        /// </summary>
        public HelpRequestedException()
            : base(BuildHelpText())
        {
        }
    }

    /// <summary>
    /// Builds the command-line usage text.
    /// </summary>
    /// <returns>Usage text describing the supported options and environment variables.</returns>
    public static string BuildHelpText() =>
        """
        Usage: EfCoreMcp --assembly <path-to-dll> [options]

        Options:
          --assembly, -a <path-to-dll>  Path to the assembly containing the DbContext (required).
          --context, -c <type-name>     DbContext type name.
          --connection <string>        Database connection string.
          --provider <provider>        Database provider (default: auto).
          --help, -h                   Show this help text.

        Environment fallbacks:
          EFCORE_MCP_ASSEMBLY          Fallback for --assembly.
          EFCORE_MCP_CONTEXT           Fallback for --context.
          EFCORE_MCP_CONNECTION        Fallback for --connection.
        """;

    /// <summary>
    /// Parses command-line arguments into <see cref="CliOptions"/>.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Parsed CLI options.</returns>
    /// <exception cref="ArgumentException">Thrown when required options are missing.</exception>
    public static CliOptions Parse(string[] args)
    {
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            throw new HelpRequestedException();
        }

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

                default:
                    if (args[i].StartsWith('-') &&
                        args[i] is not ("--assembly" or "-a" or "--context" or "-c" or "--connection" or "--provider"))
                    {
                        throw new ArgumentException($"Unknown option '{args[i]}'. Use --help for usage information.");
                    }

                    break;
            }
        }

        assembly ??= Environment.GetEnvironmentVariable("EFCORE_MCP_ASSEMBLY");
        context ??= Environment.GetEnvironmentVariable("EFCORE_MCP_CONTEXT");
        connectionString ??= Environment.GetEnvironmentVariable("EFCORE_MCP_CONNECTION");

        if (assembly is null)
        {
            throw new ArgumentException("Missing required option --assembly <path-to-dll> (or EFCORE_MCP_ASSEMBLY).");
        }

        // Validate connection string for potential injection attempts
        if (connectionString is not null)
        {
            ValidateConnectionString(connectionString);
        }

        return new CliOptions(new ContextConnectionOptions
        {
            AssemblyPath = assembly,
            ContextTypeName = context,
            ConnectionString = connectionString,
            Provider = provider
        });
    }

    /// <summary>
    /// Validates a connection string to prevent injection attacks.
    /// </summary>
    /// <param name="connectionString">The connection string to validate.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the connection string contains suspicious patterns that could indicate injection.
    /// </exception>
    /// <remarks>
    /// Connection string injection can occur when user-supplied values contain semicolons or other
    /// special characters that allow appending additional connection string parameters.
    /// For example: "Server=localhost;Password=secret;User Id=admin;" could be injected with
    /// "Server=localhost;Database=test;User Id=sa;Password=hacked;" to override credentials.
    /// </remarks>
    private static void ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // Normalize the connection string by removing comments and extra whitespace
        var normalized = connectionString.Replace("\r", " ").Replace("\n", " ");
        normalized = string.Join(" ", normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        // Check for multiple semicolons which could indicate parameter injection
        // A legitimate connection string should have a reasonable number of key=value pairs
        var semicolonCount = normalized.Split(';').Length - 1;

        if (semicolonCount > 20)
        {
            throw new ArgumentException(
                "Connection string contains too many parameters (potential injection attempt). " +
                "Connection strings should not exceed 20 key=value pairs.");
        }

        // Check for suspicious patterns that could indicate injection
        // These patterns are often used in injection attacks
        var suspiciousPatterns = new[]
        {
            "--",  // SQL-style comments
            "/*",  // SQL-style comments
            "*/",  // SQL-style comments
            "xp_", // Extended stored procedures
            "sp_", // Stored procedures
            "exec", // Execute command
            "execute", // Execute command
            "cmd=", // Command execution
            "net localgroup", // Windows privilege escalation
            ";shutdown", // Shutdown command
            ";drop", // Drop table command
            ";create", // Create command
            ";alter", // Alter command
            ";delete", // Delete command
            ";insert", // Insert command
            ";update"  // Update command
        };

        foreach (var pattern in suspiciousPatterns)
        {
            if (normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Connection string contains suspicious pattern '{pattern}' (potential injection attempt).");
            }
        }

        // Validate that the connection string follows a reasonable format
        // Connection strings should start with a provider prefix or contain basic key=value pairs
        var startsWithValidPrefix = normalized.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.StartsWith("Provider=", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.StartsWith("User Id=", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.StartsWith("Uid=", StringComparison.OrdinalIgnoreCase);

        if (!startsWithValidPrefix && !normalized.Contains('='))
        {
            throw new ArgumentException(
                "Connection string does not appear to be in a valid format. " +
                "Expected format like 'Server=...;Database=...' or similar.");
        }
    }
}
