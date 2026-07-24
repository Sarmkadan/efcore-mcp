using System.Globalization;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Provides validation helpers for <see cref="ModelIntrospector"/> instances.
/// </summary>
public static class ModelIntrospectorValidation
{
    /// <summary>
    /// Validates the specified <see cref="ModelIntrospector"/> instance.
    /// </summary>
    /// <param name="value">The model introspector to validate.</param>
    /// <returns>A list of validation problems; empty if the instance is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this ModelIntrospector? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        // Validate the contextProvider dependency (internal member)
        // Since we can't access private members directly, we validate based on public behavior
        try
        {
            // Test if the provider can provide a context
            _ = value.DescribeModel();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            problems.Add($"Context provider is invalid: {ex.Message}");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="ModelIntrospector"/> instance is valid.
    /// </summary>
    /// <param name="value">The model introspector to validate.</param>
    /// <returns><see langword="true"/> if the instance is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this ModelIntrospector? value)
        => value is not null && Validate(value).Count == 0;

    /// <summary>
    /// Ensures that the specified <see cref="ModelIntrospector"/> instance is valid.
    /// </summary>
    /// <param name="value">The model introspector to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the instance is invalid, containing a list of validation problems.</exception>
    public static void EnsureValid(this ModelIntrospector? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = Validate(value);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"ModelIntrospector is invalid. Problems: {string.Join("; ", problems)}",
                nameof(value));
        }
    }

    /// <summary>
    /// Validates the specified entity name for use with <see cref="ModelIntrospector.DescribeEntity"/>.
    /// </summary>
    /// <param name="entityName">The entity name to validate.</param>
    /// <returns>A list of validation problems; empty if the entity name is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityName"/> is null.</exception>
    public static IReadOnlyList<string> ValidateEntityName(this string? entityName)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(entityName))
        {
            problems.Add("Entity name cannot be null or whitespace.");
        }
        else if (entityName.Length > 1024)
        {
            problems.Add("Entity name cannot exceed 1024 characters.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified entity name is valid for use with <see cref="ModelIntrospector.DescribeEntity"/>.
    /// </summary>
    /// <param name="entityName">The entity name to validate.</param>
    /// <returns><see langword="true"/> if the entity name is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidEntityName(this string? entityName)
        => ValidateEntityName(entityName).Count == 0;

    /// <summary>
    /// Ensures that the specified entity name is valid for use with <see cref="ModelIntrospector.DescribeEntity"/>.
    /// </summary>
    /// <param name="entityName">The entity name to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the entity name is invalid.</exception>
    public static void EnsureValidEntityName(this string? entityName)
    {
        var problems = ValidateEntityName(entityName);
        if (problems.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", problems), nameof(entityName));
        }
    }
}