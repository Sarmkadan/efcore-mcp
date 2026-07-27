using System;
using System.Collections.Generic;
using System.Linq;

namespace EfCoreMcp.Tests;

/// <summary>
/// Validation helpers for <see cref="RelationshipAnalyzerTests"/>.
/// </summary>
public static class RelationshipAnalyzerTestsValidation
{
    /// <summary>
    /// Validates the state of a <see cref="RelationshipAnalyzerTests"/> instance.
    /// </summary>
    /// <param name="value">The test instance to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of human‑readable problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this RelationshipAnalyzerTests value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // The test class does not expose any mutable state that can be validated.
        // Therefore, there are currently no validation problems to report.
        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether a <see cref="RelationshipAnalyzerTests"/> instance is valid.
    /// </summary>
    /// <param name="value">The test instance to check.</param>
    /// <returns><c>true</c> if <see cref="Validate"/> reports no problems; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this RelationshipAnalyzerTests value) =>
        value.Validate().Count == 0;

    /// <summary>
    /// Ensures that a <see cref="RelationshipAnalyzerTests"/> instance is valid.
    /// </summary>
    /// <param name="value">The test instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are found. The exception message contains the list of problems.
    /// </exception>
    public static void EnsureValid(this RelationshipAnalyzerTests value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            var message = $"Validation failed: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }
}
