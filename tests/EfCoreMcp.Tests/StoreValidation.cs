using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EfCoreMcp.Tests
{
    /// <summary>
    /// Validation helpers for the <see cref="Store"/> test entity.
    /// </summary>
    public static class StoreValidation
    {
        /// <summary>
        /// Validates the <see cref="Store"/> instance and returns a list of validation errors.
        /// </summary>
        /// <param name="value">The <see cref="Store"/> instance to validate.</param>
        /// <returns>A read-only list of validation error messages, or an empty list if the instance is valid.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public static IReadOnlyList<string> Validate(this Store value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var problems = new List<string>();

            if (value.Id <= 0)
            {
                problems.Add("Id must be greater than zero.");
            }

            if (string.IsNullOrEmpty(value.Name))
            {
                problems.Add("Name must not be null or empty.");
            }

            if (value.Sales is null)
            {
                problems.Add("Sales must not be null.");
            }

            return problems;
        }

        /// <summary>
        /// Determines whether the specified <see cref="Store"/> instance is valid.
        /// </summary>
        /// <param name="value">The <see cref="Store"/> instance to validate.</param>
        /// <returns><see langword="true"/> if the instance is valid; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public static bool IsValid(this Store value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return Validate(value).Count == 0;
        }

        /// <summary>
        /// Ensures that the specified <see cref="Store"/> instance is valid.
        /// </summary>
        /// <param name="value">The <see cref="Store"/> instance to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the instance is invalid, with a message describing the validation errors.</exception>
        public static void EnsureValid(this Store value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var problems = Validate(value);
            if (problems.Count > 0)
            {
                throw new ArgumentException(string.Join(Environment.NewLine, problems), nameof(value));
            }
        }
    }
}