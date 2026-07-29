namespace EfCoreMcp.Tests;

public interface IBlog
{
    int Id { get; set; }
    string Title { get; set; }
    List<Post> Posts { get; set; }
}
