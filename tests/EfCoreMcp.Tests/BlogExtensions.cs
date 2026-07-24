using System.Globalization;

namespace EfCoreMcp.Tests;

/// <summary>
/// Provides useful extension methods for working with <see cref="Blog"/> entities.
/// </summary>
public static class BlogExtensions
{
    /// <summary>
    /// Gets the number of posts associated with this blog.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The count of posts, or 0 if the Posts collection is null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static int GetPostCount(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.Count ?? 0;
    }

    /// <summary>
    /// Determines whether this blog has any posts.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>True if the blog has posts or the Posts collection is null; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static bool HasPosts(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.Count > 0;
    }

    /// <summary>
    /// Gets the total character count of all post bodies in this blog.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The sum of all post body character counts (excluding null bodies).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static int GetTotalPostBodyLength(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.Sum(p => p.Body?.Length ?? 0) ?? 0;
    }

    /// <summary>
    /// Gets the average post body length across all posts in this blog.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The average post body length in characters, or 0 if there are no posts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static double GetAveragePostBodyLength(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        if (blog.Posts is null || blog.Posts.Count == 0)
        {
            return 0;
        }

        var totalLength = blog.GetTotalPostBodyLength();
        return (double)totalLength / blog.Posts.Count;
    }

    /// <summary>
    /// Gets the longest post by body length in this blog.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The post with the longest body, or null if there are no posts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static Post? GetLongestPost(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.MaxBy(p => p.Body?.Length ?? 0);
    }

    /// <summary>
    /// Gets the shortest post by body length in this blog.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The post with the shortest body, or null if there are no posts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static Post? GetShortestPost(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.MinBy(p => p.Body?.Length ?? 0);
    }

    /// <summary>
    /// Determines whether this blog has a post with the specified body content.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <param name="bodyContent">The body content to search for (case-sensitive).</param>
    /// <returns>True if a post contains the specified body content; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bodyContent"/> is null or empty.</exception>
    public static bool ContainsPostWithBody(this Blog blog, string bodyContent)
    {
        ArgumentNullException.ThrowIfNull(blog);
        ArgumentException.ThrowIfNullOrEmpty(bodyContent);

        return blog.Posts?.Any(p => p.Body?.Contains(bodyContent) == true) ?? false;
    }

    /// <summary>
    /// Gets all posts with non-null, non-empty body content.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>An enumerable of posts with non-null, non-empty body content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static IEnumerable<Post> GetPostsWithContent(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.Where(p => !string.IsNullOrEmpty(p.Body)) ?? Enumerable.Empty<Post>();
    }

    /// <summary>
    /// Formats the blog title for display, ensuring it's never null or empty.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The formatted title, or "Untitled Blog" if the title is null or empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static string GetDisplayTitle(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return string.IsNullOrEmpty(blog.Title) ? "Untitled Blog" : blog.Title;
    }

    /// <summary>
    /// Gets the total number of words across all post bodies in this blog.
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>The total word count across all posts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static int GetTotalWordCount(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        if (blog.Posts is null)
        {
            return 0;
        }

        var wordCount = 0;
        foreach (var post in blog.Posts)
        {
            if (!string.IsNullOrEmpty(post.Body))
            {
                wordCount += post.Body.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            }
        }

        return wordCount;
    }

    /// <summary>
    /// Gets posts sorted by creation order (assuming Id order represents creation order).
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>Posts sorted by Id in ascending order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static IReadOnlyList<Post> GetPostsSortedById(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.OrderBy(p => p.Id).ToList() ?? [];
    }

    /// <summary>
    /// Gets posts sorted by body length in descending order (longest first).
    /// </summary>
    /// <param name="blog">The blog instance.</param>
    /// <returns>Posts sorted by body length in descending order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blog"/> is null.</exception>
    public static IReadOnlyList<Post> GetPostsSortedByBodyLengthDescending(this Blog blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Posts?.OrderByDescending(p => p.Body?.Length ?? 0).ToList() ?? [];
    }
}