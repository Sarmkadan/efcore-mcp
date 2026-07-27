using System;
using System.Collections.Generic;
using System.Linq;

namespace EfCoreMcp.Tests;

/// <summary>
/// Extension methods that provide simple validation helpers for the <see cref="SqlGuardTests"/> test class.
/// </summary>
public static class SqlGuardTestsValidation
{
    /// <summary>
    /// Validates the specified <see cref="SqlGuardTests"/> instance.
    /// </summary>
    /// <param name="value">The <see cref="SqlGuardTests"/> instance to validate.</param>
    /// <returns>
    /// A read‑only list of human‑readable problem descriptions. An empty list indicates that the instance is valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this SqlGuardTests value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // The SqlGuardTests class contains only test methods and no state that requires validation.
        // Therefore, there are no problems to report.
        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether the specified <see cref="SqlGuardTests"/> instance is valid.
    /// </summary>
    /// <param name="value">The <see cref="SqlGuardTests"/> instance to check.</param>
    /// <returns><c>true</c> if the instance is valid; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this SqlGuardTests value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return !value.Validate().Any();
    }

    /// <summary>
    /// Ensures that the specified <see cref="SqlGuardTests"/> instance is valid.
    /// </summary>
    /// <param name="value">The <see cref="SqlGuardTests"/> instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the instance is not valid. The exception message contains a semicolon‑separated list of problems.
    /// </exception>
    public static void EnsureValid(this SqlGuardTests value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var problems = value.Validate();
        if (problems.Any())
        {
            throw new ArgumentException(string.Join("; ", problems), nameof(value));
        }
    }
}
