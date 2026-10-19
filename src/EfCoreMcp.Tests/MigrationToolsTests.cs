// src/EfCoreMcp.Tests/MigrationToolsTests.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using EfCoreMcp.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EfCoreMcp.Tests
{
    /// <summary>
    /// Integration tests for <see cref="MigrationTools"/> using a real SQLite database
    /// and a temporary migrations folder generated at test time.
    /// </summary>
    public sealed class MigrationToolsTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly string _connectionString;
        private readonly TestDbContextProvider _provider;
        private readonly MigrationInspector _inspector;
        private readonly MigrationTools _tools;

        public MigrationToolsTests()
        {
            // Create a temporary file for SQLite – it will be deleted in Dispose()
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"efcore-mcp-migration-{Guid.NewGuid()}.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = _tempDbPath }.ToString();

            _provider = new TestDbContextProvider(_connectionString);
            _inspector = new MigrationInspector(_provider);
            _tools = new MigrationTools(_inspector);
        }

        public void Dispose()
        {
            // Ensure the SQLite file is removed after each test
            try
            {
                if (File.Exists(_tempDbPath))
                {
                    File.Delete(_tempDbPath);
                }
            }
            catch
            {
                // ignore cleanup errors – they do not affect test results
            }
        }

        [Fact]
        public void MigrationStatus_Lists_Applied_And_Pending_Migrations()
        {
            // No migrations have been applied yet
            MigrationStatus statusBefore = _tools.MigrationStatus().GetAwaiter().GetResult();

            Assert.Empty(statusBefore.AppliedMigrations);
            Assert.Single(statusBefore.PendingMigrations);
            Assert.Contains(nameof(InitialCreate), statusBefore.PendingMigrations.First().MigrationId);

            // Apply pending migrations
            using var ctx = _provider.GetContext();
            ctx.Database.Migrate();

            // After migration, the pending list should be empty and applied should contain the migration
            MigrationStatus statusAfter = _tools.MigrationStatus().GetAwaiter().GetResult();

            Assert.Single(statusAfter.AppliedMigrations);
            Assert.Empty(statusAfter.PendingMigrations);
            Assert.Contains(nameof(InitialCreate), statusAfter.AppliedMigrations.First().MigrationId);
        }

        [Fact]
        public void Applying_Migrations_Creates_EF_MigrationsHistory_Table()
        {
            using var ctx = _provider.GetContext();
            ctx.Database.Migrate();

            // Verify that the __EFMigrationsHistory table exists and contains our migration
            var history = ctx.Database.GetDbConnection()
                .CreateCommand()
                .WithCommandText("SELECT MigrationId FROM __EFMigrationsHistory")
                .ExecuteReader();

            var migrationIds = new List<string>();
            while (history.Read())
            {
                migrationIds.Add(history.GetString(0));
            }

            Assert.Contains(nameof(InitialCreate), migrationIds);
        }

        [Fact]
        public void Generate_Migration_Script_Contains_Create_Table_Statement()
        {
            using var ctx = _provider.GetContext();
            ctx.Database.Migrate();

            // Generate the full SQL script for the current model
            string script = ctx.Database.GenerateCreateScript();

            // The script should contain a CREATE TABLE statement for the Foos table
            Assert.Contains("CREATE TABLE \"Foos\"", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Id\" INTEGER NOT NULL", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Name\" TEXT", script, StringComparison.OrdinalIgnoreCase);
        }

        #region Test infrastructure

        // Simple entity used by the test DbContext
        private sealed class Foo
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        // DbContext with a single DbSet<Foo> and a migration defined in this assembly
        private sealed class SampleContext : DbContext
        {
            private readonly string _connectionString;

            public SampleContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbSet<Foo> Foos => Set<Foo>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite(_connectionString,
                    b => b.MigrationsAssembly(typeof(InitialCreate).Assembly.FullName));
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Foo>(e =>
                {
                    e.ToTable("Foos");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Name).HasMaxLength(200);
                });
            }
        }

        // Minimal IDbContextProvider implementation for the tests
        private sealed class TestDbContextProvider : IDbContextProvider
        {
            private readonly string _connectionString;
            private SampleContext? _cached;

            public TestDbContextProvider(string connectionString)
            {
                _connectionString = connectionString;
            }

            public DbContext GetContext()
            {
                // Reuse the same context instance for the lifetime of the provider
                // (the provider itself is disposed at the end of each test)
                return _cached ??= new SampleContext(_connectionString);
            }

            public void Dispose()
            {
                _cached?.Dispose();
            }
        }

        // Migration generated at test compile‑time.
        // This mimics what `dotnet ef migrations add InitialCreate` would produce.
        [DbContext(typeof(SampleContext))]
        private sealed class InitialCreate : Migration
        {
            protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.CreateTable(
                    name: "Foos",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "INTEGER", nullable: false)
                            .Annotation("Sqlite:Autoincrement", true),
                        Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Foos", x => x.Id);
                    });
            }

            protected override void Down(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.DropTable(name: "Foos");
            }
        }

        // Helper extension to simplify command creation
        private static class DbCommandExtensions
        {
            public static System.Data.Common.DbCommand WithCommandText(this System.Data.Common.DbCommand command, string text)
            {
                command.CommandText = text;
                return command;
            }
        }

        #endregion
    }
}
