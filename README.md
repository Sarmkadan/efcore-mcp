## ModelFinding

`ModelFinding` is a sealed record that represents a single observation discovered while analyzing or validating an EF Core model. Because it is a record, findings are immutable, compare by value, and provide a readable `ToString()` representation out of the box. This makes findings easy to log, deduplicate, and pattern-match when building reporting or diagnostics on top of the analysis services.

Example usage:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using EfCoreMcp.Core.Domain;

public static class FindingReporter
{
    // Distinct() relies on the record's built-in value equality,
    // so duplicate findings are collapsed automatically.
    public static void LogAll(IEnumerable<ModelFinding> findings)
    {
        foreach (ModelFinding finding in findings.Distinct())
        {
            // Records provide a readable ToString() out of the box.
            Console.WriteLine(finding);
        }
    }
}
```

## Blog

The `Blog` type is a simple entity used in the test suite to represent a blog that contains a collection of `Post` entities. It exposes an identifier, a title, and a navigation property to its posts, while each `Post` references its parent `Blog` via `BlogId` and a navigation property.

```csharp
using System;
using System.Collections.Generic;
using EfCoreMcp.Tests; // Namespace where Blog, Post, and ModelIntrospectorTests live
using Microsoft.EntityFrameworkCore;

public static class BlogDemo
{
    public static void Main()
    {
        // Create a blog with two posts
        var blog = new Blog
        {
            Id = 1,
            Title = "My Blog",
            Posts = new List<Post>
            {
                new Post { Id = 1, Body = "First post", BlogId = 1, Blog = null },
                new Post { Id = 2, Body = "Second post", BlogId = 1, Blog = null }
            }
        };

        // Resolve navigation back‑references
        foreach (var post in blog.Posts)
        {
            post.Blog = blog;
        }

        // Example of using the test helper to get context information
        using var testHelper = new ModelIntrospectorTests();
        ContextInfo info = testHelper.GetContextInfo();
        Console.WriteLine($"Context: {info.ContextName}, Provider: {info.ProviderName}");

        // Clean up
        testHelper.Dispose();
    }
}
```

The example demonstrates constructing a `Blog`, populating its `Posts`, fixing the navigation property, and using the `ModelIntrospectorTests` helper to obtain `ContextInfo` from the underlying `DbContext`. All members used (`Id`, `Title`, `Posts`, `Id`, `Body`, `BlogId`, `Blog`, `GetContextInfo`, `Dispose`) are part of the public API.
