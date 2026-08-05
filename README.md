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

## Store

`Store` is a test entity that represents a retail location. It holds basic identifying information (`Id`, `Name`) and a collection of related `Sale` records. The type also participates in the test harness that can spin up an in‑memory `DbContext`, expose model metadata, and clean up resources.

```csharp
using System;
using System.Collections.Generic;
using EfCoreMcp.Tests; // Namespace where Store, Sale, Customer, and ModelAnalyzerTests live
using Microsoft.EntityFrameworkCore;

public static class StoreDemo
{
    public static void Main()
    {
        // Create a store with a couple of sales
        var store = new Store
        {
            Id = 1,
            Name = "Main Street Store",
            Sales = new List<Sale>()
        };

        var customer = new Customer { Id = 1, Name = "Alice" };

        var sale1 = new Sale
        {
            Id = 1,
            Amount = 99.95m,
            StoreId = store.Id,
            Store = store,
            CustomerId = customer.Id,
            Customer = customer
        };

        var sale2 = new Sale
        {
            Id = 2,
            Amount = 45.00m,
            StoreId = store.Id,
            Store = store,
            CustomerId = customer.Id,
            Customer = customer,
            Notes = "First purchase"
        };

        store.Sales.Add(sale1);
        store.Sales.Add(sale2);

        // Use the test harness to obtain a context and model information
        using var analyzer = new ModelAnalyzerTests();

        // Get the underlying DbContext (in‑memory for tests)
        DbContext ctx = analyzer.GetContext();

        // Retrieve high‑level context information
        ContextInfo ctxInfo = analyzer.GetContextInfo();
        Console.WriteLine($"Context: {ctxInfo.ContextName}, Provider: {ctxInfo.ProviderName}");

        // Describe the whole model
        var modelDescriptor = analyzer.DescribeModel();
        Console.WriteLine($"Model has {modelDescriptor.EntityCount} entities.");

        // List entity names in the model
        IReadOnlyList<string> entityNames = analyzer.ListEntityNames();
        Console.WriteLine("Entities in model: " + string.Join(", ", entityNames));

        // Attempt to describe a specific entity (Store)
        var storeDescriptor = analyzer.DescribeEntity?.Invoke(typeof(Store));
        if (storeDescriptor != null)
        {
            Console.WriteLine($"Store entity has {storeDescriptor.PropertyCount} properties.");
        }
        else
        {
            Console.WriteLine(analyzer.EntityNotFoundMessage);
        }

        // Clean up test resources
        analyzer.Dispose();
    }
}
```

The example demonstrates creating a `Store`, adding related `Sale` objects, and using the `ModelAnalyzerTests` helper to access the in‑memory `DbContext`, retrieve model metadata, list entity names, and safely dispose of resources. All members used (`Id`, `Name`, `Sales`, `Amount`, `StoreId`, `Store`, `CustomerId`, `Customer`, `Notes`, `GetContext`, `GetContextInfo`, `Dispose`, `DescribeModel`, `DescribeEntity`, `ListEntityNames`, `EntityNotFoundMessage`, `ModelAnalyzerTests`) are part of the public API.
