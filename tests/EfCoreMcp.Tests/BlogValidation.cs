using System.Globalization;

namespace EfCoreMcp.Tests;

public static class BlogValidation
{
    /// <summary>
    /// Validates the specified <see cref="Blog"/> instance and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The blog instance to validate.</param>
    /// <returns>A read-only list of validation problems; empty if the blog is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this Blog value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (value.Id < 0)
            problems.Add(string.Format(CultureInfo.InvariantCulture, "Blog.Id must be non-negative, but was {0}", value.Id));



        if (value.Title is null)
            problems.Add("Blog.Title must not be null");
        else if (string.IsNullOrWhiteSpace(value.Title))
            problems.Add("Blog.Title must not be empty or whitespace");
        else if (value.Title.Length > 200)
            problems.Add(string.Format(CultureInfo.InvariantCulture, "Blog.Title must not exceed 200 characters, but was {0}", value.Title.Length));

        if (value.Posts is null)
            problems.Add("Blog.Posts must not be null");

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="Blog"/> instance is valid.
    /// </summary>
    /// <param name="value">The blog instance to check.</param>
    /// <returns>True if the blog is valid; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static bool IsValid(this Blog value)
    {
        return Validate(value).Count == 0;
    }

    /// <summary>
    /// Ensures that the specified <see cref="Blog"/> instance is valid, throwing an <see cref="ArgumentException"/> if it is not.
    /// </summary>
    /// <param name="value">The blog instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the blog has validation problems, listing each issue.</exception>
    public static void EnsureValid(this Blog value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = Validate(value);
        if (problems.Count == 0)
            return;

        throw new ArgumentException(string.Join("\n", problems));
    }
}