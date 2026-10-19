using System.Globalization;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp;

/// <summary>
/// Command-line options parser for EfCoreMcp.
/// </summary>
public sealed record CliOptions(ContextConnectionOptions Connection)
{
    /// <summary>
    /// Runtime limits for the query and analysis tools. Validated by <see cref="Parse"/> at startup.
    /// </summary>
    public RuntimeLimitsOptions Limits { get; init; } = new();

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
          --max-rows <n>               Row cap for query tools (default: 10000, range 1-10000).
          --max-query-length <n>       SQL length cap in characters (default: 10000, range 1-1000000).
          --allowed-schemas <list>     Comma-separated schemas tools may use (default: all).
          --max-analysis-depth <n>     Foreign-key hop cap for explain_relationship (default: 50, range 1-50).
          --help, -h                   Show this help text.

        Environment fallbacks:
          EFCORE_MCP_ASSEMBLY          Fallback for --assembly.
          EFCORE_MCP_CONTEXT           Fallback for --context.
          EFCORE_MCP_CONNECTION        Fallback for --connection.
          EFCORE_MCP_MAX_ROWS          Fallback for --max-rows.
          EFCORE_MCP_MAX_QUERY_LENGTH  Fallback for --max-query-length.
          EFCORE_MCP_ALLOWED_SCHEMAS   Fallback for --allowed-schemas.
          EFCORE_MCP_MAX_ANALYSIS_DEPTH Fallback for --max-analysis-depth.
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
        string? maxRows = null, maxQueryLength = null, allowedSchemas = null, maxAnalysisDepth = null;
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

                case "--max-rows" when i + 1 < args.Length:
                    maxRows = args[++i];
                    break;

                case "--max-query-length" when i + 1 < args.Length:
                    maxQueryLength = args[++i];
                    break;

                case "--allowed-schemas" when i + 1 < args.Length:
                    allowedSchemas = args[++i];
                    break;

                case "--max-analysis-depth" when i + 1 < args.Length:
                    maxAnalysisDepth = args[++i];
                    break;

                default:
                    if (args[i].StartsWith('-') &&
                        args[i] is not ("--assembly" or "-a" or "--context" or "-c" or "--connection" or "--provider"
                            or "--max-rows" or "--max-query-length" or "--allowed-schemas" or "--max-analysis-depth"))
                    {
                        throw new ArgumentException($"Unknown option '{args[i]}'. Use --help for usage information.");
                    }

                    break;
            }
        }

        assembly ??= Environment.GetEnvironmentVariable("EFCORE_MCP_ASSEMBLY");
        context ??= Environment.GetEnvironmentVariable("EFCORE_MCP_CONTEXT");
        connectionString ??= Environment.GetEnvironmentVariable("EFCORE_MCP_CONNECTION");
        maxRows ??= NonEmptyEnvironmentVariable("EFCORE_MCP_MAX_ROWS");
        maxQueryLength ??= NonEmptyEnvironmentVariable("EFCORE_MCP_MAX_QUERY_LENGTH");
        allowedSchemas ??= NonEmptyEnvironmentVariable("EFCORE_MCP_ALLOWED_SCHEMAS");
        maxAnalysisDepth ??= NonEmptyEnvironmentVariable("EFCORE_MCP_MAX_ANALYSIS_DEPTH");

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
        })
        {
            Limits = ParseLimits(maxRows, maxQueryLength, allowedSchemas, maxAnalysisDepth)
        };
    }

    /// <summary>
    /// Builds and validates the runtime limits from their raw command-line or environment values.
    /// </summary>
    /// <param name="maxRows">Raw <c>--max-rows</c> value, or <see langword="null"/> for the default.</param>
    /// <param name="maxQueryLength">Raw <c>--max-query-length</c> value, or <see langword="null"/> for the default.</param>
    /// <param name="allowedSchemas">Raw comma-separated <c>--allowed-schemas</c> value, or <see langword="null"/> to allow all.</param>
    /// <param name="maxAnalysisDepth">Raw <c>--max-analysis-depth</c> value, or <see langword="null"/> for the default.</param>
    /// <returns>The validated runtime limits.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is not an integer, is out of range, or is an invalid schema name.</exception>
    private static RuntimeLimitsOptions ParseLimits(string? maxRows, string? maxQueryLength, string? allowedSchemas, string? maxAnalysisDepth)
    {
        var limits = new RuntimeLimitsOptions
        {
            MaxRows = ParseInteger(maxRows, "--max-rows", RuntimeLimitsOptions.DefaultMaxRows),
            MaxQueryLength = ParseInteger(maxQueryLength, "--max-query-length", RuntimeLimitsOptions.DefaultMaxQueryLength),
            MaxAnalysisDepth = ParseInteger(maxAnalysisDepth, "--max-analysis-depth", RuntimeLimitsOptions.DefaultMaxAnalysisDepth),
            AllowedSchemas = allowedSchemas?.Split(',').Select(schema => schema.Trim()).ToList()
        };

        limits.Validate();
        return limits;
    }

    private static int ParseInteger(string? raw, string option, int defaultValue)
    {
        if (raw is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"{option} must be an integer. Got: '{raw}'.");
        }

        return value;
    }

    private static string? NonEmptyEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
