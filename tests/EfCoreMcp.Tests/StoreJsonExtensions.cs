using System.Text.Json;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Tests;

/// <summary>
/// Provides JSON serialization and deserialization extensions for the <see cref="Store"/> type.
/// </summary>
public static class StoreJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Serializes a <see cref="Store"/> instance to a JSON string.
    /// </summary>
    /// <param name="value">The <see cref="Store"/> instance to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>A JSON string representation of the <see cref="Store"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ToJson(this Store value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = indented ? new JsonSerializerOptions(_jsonOptions) { WriteIndented = true } : _jsonOptions;
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="Store"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A <see cref="Store"/> instance, or <see langword="null"/> if <paramref name="json"/> is <see langword="null"/> or empty.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="JsonException">Thrown when <paramref name="json"/> is invalid JSON.</exception>
    public static Store? FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize<Store>(json, _jsonOptions);
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to a <see cref="Store"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">When this method returns, contains the <see cref="Store"/> instance if the deserialization succeeded, or <see langword="null"/> if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="json"/> was successfully deserialized; otherwise, <see langword="false"/>.</returns>
    public static bool TryFromJson(string json, out Store? value)
    {
        try
        {
            if (string.IsNullOrEmpty(json))
            {
                value = null;
                return false;
            }

            value = JsonSerializer.Deserialize<Store>(json, _jsonOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}