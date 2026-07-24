# Blog

Represents a blog entity in an Entity Framework Core context, typically used to manage collections of posts associated with a single blog instance. This type is part of the `efcore-mcp` project's model introspection and analysis toolkit.

## API

### `public int Id`
Unique identifier for the blog instance. Serves as the primary key in the underlying database table.

### `public string Title`
Gets or sets the title of the blog. Must not be null or empty when saving to the database.

### `public List<Post> Posts`
Collection navigation property representing all posts associated with this blog. May be null if not loaded.

## Usage
