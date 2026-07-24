# Store

Central entity representing a retail store in the e-commerce domain model. Tracks store-specific information such as name, sales records, and contextual notes. Serves as an aggregate root for sales data and participates in relationships with customers and sales transactions.

## API

### `public int Id`

Unique identifier for the store. Assigned by the persistence layer and immutable after creation. Used as a foreign key in related entities such as `Sale`.

### `public string Name`

Human-readable name of the store. Required field; must not be null or empty. Used for display and reporting purposes.

### `public List<Sale> Sales`

Collection of sales transactions associated with this store. Mutable list allowing addition and removal of sales records. Populated by the ORM during data loading.

### `public string? Notes`

Optional contextual information about the store. May be null. Intended for administrative or operational notes not captured by structured fields.

### `public int Amount`

Aggregate monetary value associated with the store. Value semantics depend on context; may represent total sales, average transaction value, or another computed metric. Type is `decimal` in related contexts.

### `public int StoreId`

Foreign key referencing the store’s unique identifier. Used for database relationships and joins. Must correspond to a valid `Id` in the `Store` table.

### `public Store Store`

Navigation property representing the store associated with a given entity (e.g., within a `Sale`). May be null if not loaded or not applicable. Used for object graph traversal.

### `public int CustomerId`

Foreign key referencing the customer’s unique identifier. Used to associate sales or interactions with specific customers.

### `public Customer Customer`

Navigation property representing the customer associated with a given entity. May be null if not loaded. Enables access to customer details from related records.

### `public DbContext GetContext()`

Returns the current `DbContext` instance associated with this entity. Useful for executing raw queries or accessing change tracking. The returned context may be disposed elsewhere; clients should not assume ownership.

### `public ContextInfo GetContextInfo()`

Retrieves metadata about the current context, such as connection state, transaction status, or configuration. Return value may be cached or computed on demand.

### `public void Dispose()`

Releases managed and unmanaged resources held by the entity or its associated context. Typically invoked to clean up database connections or change trackers. Safe to call multiple times; subsequent calls have no effect.

### `public ModelDescriptor DescribeModel()`

Generates a high-level description of the entity model, including entity types, relationships, and constraints. Return value is a snapshot and does not reflect subsequent changes unless re-invoked.

### `public EntityDescriptor? DescribeEntity(string entityName)`

Returns a descriptor for the specified entity by name, or `null` if no such entity exists. Enables dynamic inspection of model elements. Throws if `entityName` is null or empty.

### `public IReadOnlyList<string> ListEntityNames()`

Returns an immutable list of all known entity names in the model. Useful for discovery and validation.

### `public string EntityNotFoundMessage`

Static message template used when an entity is not found. Intended for user-facing error messages. Value is constant across instances.

### `public ModelAnalyzerTests`

Test suite validating model analysis rules and suggestions. Not part of the public API for runtime use; may change without notice.
