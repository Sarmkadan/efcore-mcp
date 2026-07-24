using System.Globalization;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Provides consistent serialization behavior for provider-specific and non-JSON-safe values.
/// Ensures QueryResult can safely carry arbitrary cell values to MCP (JSON) clients.
/// </summary>
public static class ValueSerializer
{
    /// <summary>
    /// Maximum string length before truncation with indicator. Prevents context window flooding.
    /// </summary>
    public const int MaxStringLength = 1024;


    /// <summary>
    /// Maximum byte array length to display before truncation. Prevents memory bloat.
    /// </summary>
    public const int MaxByteArrayLength = 1024;

    /// <summary>
    /// Maximum collection size to display before truncation. Prevents array flooding.
    /// </summary>
    public const int MaxCollectionSize = 100;

    /// <summary>
    /// Serializes a value for JSON-safe output in QueryResult.
    /// Handles common database types, provider-specific types, and edge cases.
    /// </summary>
    /// <param name="value">The value to serialize</param>
    /// <param name="providerName">Database provider name for provider-specific handling</param>
    /// <returns>JSON-safe string representation of the value</returns>
    public static string Serialize(object? value, string? providerName = null)
    {
        if (value is null or DBNull)
            return "null";

        // Handle string first to avoid conflicts with IEnumerable<string>
        if (value is string s)
            return TruncateString(s);

        // Handle byte arrays - convert to base64 with length limit
        if (value is byte[] bytes)
            return SerializeByteArray(bytes);

        // Handle temporal types with ISO-8601 format
        if (value is DateTime dt)
            return dt.ToString("O");

        if (value is DateTimeOffset dto)
            return dto.ToString("O");

        if (value is TimeSpan ts)
            return ts.ToString("c");

        if (value is TimeOnly to)
            return to.ToString("O");

        if (value is DateOnly d)
            return d.ToString("O");

        // Handle numeric types - preserve precision
        if (value is decimal dec)
            return dec.ToString(CultureInfo.InvariantCulture);

        if (value is float f)
            return f.ToString(CultureInfo.InvariantCulture);

        if (value is double db)
            return db.ToString(CultureInfo.InvariantCulture);

        // Handle Guid
        if (value is Guid g)
            return g.ToString();

        // Handle arrays
        if (value is Array array)
            return SerializeArray(array);

        // Handle IEnumerable (excluding string which was already handled)
        if (value is IEnumerable<object?> enumerable)
            return SerializeEnumerable(enumerable);

        // Handle common primitives
        if (value is bool b)
            return b.ToString().ToLowerInvariant();

        if (value is char c)
            return c.ToString();

        if (value is sbyte sb)
            return sb.ToString(CultureInfo.InvariantCulture);

        if (value is short sh)
            return sh.ToString(CultureInfo.InvariantCulture);

        if (value is int i)
            return i.ToString(CultureInfo.InvariantCulture);

        if (value is long l)
            return l.ToString(CultureInfo.InvariantCulture);

        if (value is byte by)
            return by.ToString(CultureInfo.InvariantCulture);

        if (value is ushort us)
            return us.ToString(CultureInfo.InvariantCulture);

        if (value is uint ui)
            return ui.ToString(CultureInfo.InvariantCulture);

        if (value is ulong ul)
            return ul.ToString(CultureInfo.InvariantCulture);

        // Fallback: use ToString() with truncation for large objects
        return TruncateString(value.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Serializes a byte array to a base64 string with length limit.
    /// </summary>
    private static string SerializeByteArray(byte[] bytes)
    {
        if (bytes.Length <= MaxByteArrayLength)
        {
            return Convert.ToBase64String(bytes);
        }

        var truncated = new byte[MaxByteArrayLength];
        Array.Copy(bytes, truncated, MaxByteArrayLength);
        return $"byte[{bytes.Length}]={Convert.ToBase64String(truncated)}...(truncated)";
    }

    /// <summary>
    /// Truncates a string with indicator if it exceeds MaxStringLength.
    /// </summary>
    private static string TruncateString(string s)
    {
        if (s.Length <= MaxStringLength)
        {
            return s;
        }

        return s[..MaxStringLength] + "...(truncated)";
    }

    /// <summary>
    /// Serializes an array with size limit.
    /// </summary>
    private static string SerializeArray(Array array)
    {
        if (array.Length == 0)
        {
            return "[]";
        }

        if (array.Length > MaxCollectionSize)
        {
            return $"[{array.Length} items]...";
        }

        var items = new List<string>();
        foreach (var item in array)
        {
            items.Add(Serialize(item));
        }

        return $"[{string.Join(", ", items)}]";
    }

    /// <summary>
    /// Serializes an enumerable with size limit.
    /// </summary>
    private static string SerializeEnumerable(IEnumerable<object?> enumerable)
    {
        var list = enumerable.ToList();

        if (list.Count == 0)
        {
            return "[]";
        }

        if (list.Count > MaxCollectionSize)
        {
            return $"[{list.Count} items]...";
        }

        var items = new List<string>();
        foreach (var item in list)
        {
            items.Add(Serialize(item));
        }

        return $"[{string.Join(", ", items)}]";
    }

    /// <summary>
    /// Determines if a type is a provider-specific type that needs special handling.
    /// </summary>
    public static bool IsProviderSpecificType(Type type, string? providerName = null)
    {
        if (type == typeof(byte[]))
            return true;

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
            return true;

        if (type == typeof(decimal) || type == typeof(float) || type == typeof(double))
            return true;

        // PostgreSQL-specific types
        if (providerName?.Contains("POSTGRESQL", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (type.FullName?.StartsWith("Npgsql", StringComparison.Ordinal) == true)
                return true;

            // PostgreSQL range types
            if (type.Name.Contains("Range") || type.Name.Contains("Range`1"))
                return true;
        }

        return false;
    }
}