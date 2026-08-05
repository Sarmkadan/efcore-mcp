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
