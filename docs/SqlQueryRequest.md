# SqlQueryRequest

`SqlQueryRequest` is a sealed record that represents a request to execute a raw SQL query against the database through the MCP server. It is part of a set of query-related types that includes `EntityQueryRequest` for LINQ-based entity queries, `QueryResult` for successful outcomes, `QueryRejection` for denied requests, and `ReadOnlyQueryViolationException` for enforcement of read-only execution policies. The type is designed to carry the SQL text and any associated parameters in a structured, immutable form that can be validated and executed under the guarded read-only query pipeline.

## API

### SqlQueryRequest

A sealed record encapsulating a raw SQL query request. It is the primary input type for executing arbitrary SQL statements through the MCP tooling, subject to read-only enforcement.

**Purpose:** Carries the SQL command text and optional parameters from the MCP client to the server-side query executor. The record's immutability ensures the request cannot be altered after construction.

**Members:** The record exposes the SQL text and parameters as init-only properties. Construction is via the compiler-generated primary constructor.

**When it throws:** No exceptions during construction. Validation and execution occur downstream in the query pipeline, where `ReadOnlyQueryViolationException` may be thrown if the SQL violates the read-only constraint.

---

### EntityQueryRequest

A sealed record representing a request to query entities using a LINQ expression or entity type specification rather than raw SQL.

**Purpose:** Provides a structured way to request entity data by type and optional filter conditions, distinct from raw SQL execution. It is processed by the same guarded pipeline but follows a different resolution path.

**Members:** Exposes the entity type identifier and any serialized filter criteria. Like `SqlQueryRequest`, it is immutable.

**When it throws:** No exceptions during construction. Rejections are communicated via `QueryRejection` rather than exceptions where possible.

---

### QueryResult

A sealed record representing the successful outcome of a query execution.

**Purpose:** Wraps the result set or affected-row count returned by a successfully executed and permitted query. It is the success case in the query pipeline's result discrimination.

**Members:** Contains the result data in a serializable form (e.g., rows as dictionaries or entity projections) and metadata such as row count.

**When it throws:** Construction does not throw. It is produced only after successful execution.

---

### QueryRejection

A sealed record representing the denial of a query request.

**Purpose:** Communicates that a query was rejected, typically due to violating the read-only policy or failing validation. It is returned instead of thrown to allow graceful handling by the MCP client.

**Members:** Includes a reason string describing why the query was rejected.

**When it throws:** Construction does not throw. It is produced by the validation layer when a query is deemed non-executable.

---

### ReadOnlyQueryViolationException

A sealed exception class inheriting from `InvalidOperationException`. It is thrown when a query is determined to violate the read-only execution policy and the pipeline is configured to throw rather than return a `QueryRejection`.

**Purpose:** Enforces the read-only guard by halting execution with a descriptive error. The `Reason` property provides the specific violation detail.

**Constructor parameter `reason`:** A `string` describing the violation (e.g., "INSERT statements are not permitted").

**Property `Reason`:** Returns the violation description passed to the constructor. This is the sole public member beyond the standard exception members inherited from `InvalidOperationException`.

**When it throws:** This exception itself is thrown by the query guard; its constructor does not throw.

---

## Usage

### Example 1: Submitting a raw SQL query and handling rejection

```csharp
// Construct a read-only SELECT request
var request = new SqlQueryRequest
{
    Sql = "SELECT Id, Name FROM Products WHERE Category = @Category",
    Parameters = new Dictionary<string, object>
    {
        ["@Category"] = "Electronics"
    }
};

// Execute through the guarded pipeline (pseudocode)
object response = await queryExecutor.ExecuteAsync(request);

// Discriminate the result
if (response is QueryResult result)
{
    foreach (var row in result.Rows)
    {
        Console.WriteLine($"Product: {row["Name"]}");
    }
}
else if (response is QueryRejection rejection)
{
    Console.WriteLine($"Query rejected: {rejection.Reason}");
}
```

### Example 2: A mutation attempt that triggers the read-only guard

```csharp
// This request attempts a write operation
var request = new SqlQueryRequest
{
    Sql = "DELETE FROM Logs WHERE Timestamp < @Threshold",
    Parameters = new Dictionary<string, object>
    {
        ["@Threshold"] = DateTime.UtcNow.AddDays(-30)
    }
};

try
{
    // The guard throws because DELETE violates read-only policy
    object response = await queryExecutor.ExecuteAsync(request);
}
catch (ReadOnlyQueryViolationException ex)
{
    Console.WriteLine($"Violation: {ex.Reason}");
    // Log and abort — no data was modified
}
```

---

## Notes

- **Immutability:** All four record types are sealed and immutable by design. Once constructed, their state cannot change, making them safe to pass across thread boundaries without synchronization.
- **Thread safety:** `SqlQueryRequest`, `EntityQueryRequest`, `QueryResult`, and `QueryRejection` are inherently thread-safe due to immutability. `ReadOnlyQueryViolationException` is a standard exception type and should be caught and handled on the throwing thread; its `Reason` property is set at construction and does not change.
- **Edge cases:** An empty or whitespace-only SQL string in `SqlQueryRequest` will likely result in a `QueryRejection` or `ReadOnlyQueryViolationException` depending on pipeline configuration. Parameter dictionaries with keys that do not match the SQL placeholders may cause execution-time errors downstream, not during request construction.
- **Read-only enforcement:** The guard layer inspects the SQL text for DML keywords (INSERT, UPDATE, DELETE, MERGE, etc.) and DDL statements. Even parameterized mutations are blocked. The exact rejection mechanism — exception or `QueryRejection` record — depends on the executor configuration.
- **Serialization:** These records are designed to cross process boundaries (MCP stdio/HTTP). Their property shapes are compatible with JSON serialization without custom converters, assuming parameter values are primitive or JSON-serializable types.
