using System.Data.Common;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Provides utilities for sanitizing connection strings to remove sensitive information.
/// </summary>
internal static class ConnectionStringSanitizer
{
    /// <summary>
    /// Sanitizes a connection string by removing sensitive credentials.
    /// </summary>
    /// <param name="connectionString">The raw connection string that may contain credentials.</param>
    /// <returns>A sanitized connection string with credentials redacted, or null if input is null or whitespace.</returns>
    /// <remarks>
    /// This method parses the connection string and removes common credential-related keys:
    /// - Password
    /// - Pwd
    /// - User ID
    /// - Uid
    /// - User
    /// - Username
    /// - Account Key
    /// - Access Key
    /// - Authentication
    /// - Token
    /// - Integrated Security
    /// - Persist Security Info
    ///
    /// The sanitized string will only contain server, database, and provider information.
    /// <para>
    /// SECURITY: Never log or expose the original connection string. Always use sanitized versions
    /// in error messages, logs, and ToString() implementations to prevent credential leakage.
    /// </para>
    /// </remarks>
    public static string? Sanitize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            // Remove sensitive keys
            RemoveSensitiveKeys(builder);

            // Return the sanitized connection string
            return builder.ConnectionString;
        }
        catch
        {
            // If parsing fails, return a generic placeholder to avoid exposing any sensitive data
            return "[REDACTED CONNECTION STRING]";
        }
    }

    /// <summary>
    /// Gets a redacted representation of a connection string for display purposes.
    /// </summary>
    /// <param name="connectionString">The raw connection string.</param>
    /// <returns>A redacted string that indicates a connection string is present without exposing details.</returns>
    public static string GetRedactedDisplay(string? connectionString)
    {
        return connectionString switch
        {
            null => string.Empty,
            "" => string.Empty,
            _ => "[CONNECTION STRING PRESENT]"
        };
    }

    /// <summary>
    /// Sanitizes an exception message that may contain connection string information.
    /// </summary>
    /// <param name="message">The original exception message.</param>
    /// <param name="connectionString">The connection string that might be in the message.</param>
    /// <returns>A sanitized message with connection string redacted.</returns>
    public static string SanitizeExceptionMessage(string message, string? connectionString)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            return message;
        }

        try
        {
            // Try to find the connection string in the message and replace it
            // This handles cases where EF Core includes the connection string in error messages
            var sanitizedConnectionString = Sanitize(connectionString);

            if (sanitizedConnectionString != null && sanitizedConnectionString != connectionString)
            {
                return message.Replace(connectionString, sanitizedConnectionString, StringComparison.Ordinal);
            }
        }
        catch
        {
            // If sanitization fails, return the original message
        }

        return message;
    }

    /// <summary>
    /// Removes sensitive credential-related keys from the connection string builder.
    /// </summary>
    /// <param name="builder">The connection string builder.</param>
    private static void RemoveSensitiveKeys(DbConnectionStringBuilder builder)
    {
        var keysToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Password",
            "Pwd",
            "User ID",
            "Uid",
            "User",
            "Username",
            "Account Key",
            "Access Key",
            "Authentication",
            "Token",
            "Integrated Security",
            "Persist Security Info"
        };

        // Remove keys that exist in the builder
        foreach (var key in keysToRemove)
        {
            if (builder.ContainsKey(key))
            {
                builder.Remove(key);
            }
        }
    }
}
