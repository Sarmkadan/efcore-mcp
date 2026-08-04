using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCoreMcp.Tests;

public class EntityQueryExecutorTests
{
    // Minimal DbContext used for the tests – no model is required because we only test argument validation.
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }

    // Test double for IDbContextProvider. It returns an in‑memory DbContext and a dummy ContextInfo.
    private sealed class TestContextProvider : IDbContextProvider
    {
        private readonly DbContext _context;

        public TestContextProvider()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new TestDbContext(options);
        }

        public DbContext GetContext() => _context;

        // The real ContextInfo type is defined in the production code. Returning null is acceptable
        // for the code paths exercised by these tests (the property is only used in error handling).
        public ContextInfo GetContextInfo() => null!;

        public void Dispose() => _context.Dispose();
    }

    // A very small stub for IModelIntrospector – it will never be used because the tests trigger
    // validation errors before any call to ResolveEntityType.
    private sealed class StubModelIntrospector : IModelIntrospector
    {
        public ModelDescriptor DescribeModel() => throw new NotImplementedException();
        public EntityDescriptor? DescribeEntity(string entityName) => throw new NotImplementedException();
        public IReadOnlyList<string> ListEntityNames() => Array.Empty<string>();
        public string EntityNotFoundMessage(string entityName) => $"Entity '{entityName}' not found.";
        public void InvalidateCache() { }
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = new EntityQueryExecutor(new TestContextProvider(), new StubModelIntrospector());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await executor.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_RequestWithoutEntityName_ThrowsInvalidOperationException()
    {
        // Arrange
        var executor = new EntityQueryExecutor(new TestContextProvider(), new StubModelIntrospector());
        var request = new EntityQueryRequest
        {
            // EntityName is null – the validator inside ExecuteAsync will call request.Validate()
            // which checks for a non‑empty entity name.
            EntityName = null!
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await executor.ExecuteAsync(request));
    }

    [Fact]
    public async Task CountAsync_NullOrEmptyEntityName_ThrowsArgumentException()
    {
        // Arrange
        var executor = new EntityQueryExecutor(new TestContextProvider(), new StubModelIntrospector());

        // Null entity name
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await executor.CountAsync(null!));

        // Empty entity name
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await executor.CountAsync(string.Empty));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var executor = new EntityQueryExecutor(new TestContextProvider(), new StubModelIntrospector());
        var request = new EntityQueryRequest
        {
            EntityName = "AnyEntity"
        };
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before the call

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await executor.ExecuteAsync(request, cts.Token));
    }
}
