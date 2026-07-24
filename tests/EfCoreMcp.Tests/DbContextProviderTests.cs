using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using Microsoft.EntityFrameworkCore;
using System;
using Xunit;

namespace EfCoreMcp.Tests;

public class DbContextProviderTests
{
    [Fact]
    public void GetContext_HappyPath_ReturnsDbContext()
    {
        // Arrange
        var options = new ContextConnectionOptions
        {
            AssemblyPath = typeof(AnalyzerContext).Assembly.Location,
            ContextTypeName = typeof(AnalyzerContext).FullName
        };
        var provider = new DbContextProvider(options);

        // Act
        var context = provider.GetContext();

        // Assert
        Assert.IsAssignableFrom<DbContext>(context);
    }

    [Fact]
    public void GetContextInfo_HappyPath_ReturnsContextInfo()
    {
        // Arrange
        var options = new ContextConnectionOptions
        {
            AssemblyPath = typeof(AnalyzerContext).Assembly.Location,
            ContextTypeName = typeof(AnalyzerContext).FullName
        };
        var provider = new DbContextProvider(options);

        // Act
        var contextInfo = provider.GetContextInfo();

        // Assert
        Assert.NotNull(contextInfo);
        Assert.NotEmpty(contextInfo.ContextType);
    }

    [Fact]
    public void Dispose_DisposesProvider()
    {
        // Arrange
        var options = new ContextConnectionOptions
        {
            AssemblyPath = typeof(AnalyzerContext).Assembly.Location,
            ContextTypeName = typeof(AnalyzerContext).FullName
        };
        var provider = new DbContextProvider(options);

        // Act and Assert
        provider.Dispose();
        Assert.Throws<ObjectDisposedException>(() => provider.GetContext());
    }

    [Fact]
    public void GetContext_NullOptions_ThrowsArgumentNullException()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => new DbContextProvider(null));
    }
}
