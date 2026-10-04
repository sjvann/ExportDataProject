using ExportData.Models.Config;
using ExportData.Models.EnumType;
using ExportData.Services;
using Xunit;

namespace ExportData.Tests;

public sealed class DatabaseCatalogTests
{
    [Fact(DisplayName = "PostgreSQL 清單連到 postgres，不必先選目標資料庫")]
    public void PostgreSql_CatalogConnection_UsesPostgresDatabase()
    {
        var built = DatabaseCatalog.TryBuildCatalogConnection(
            EnumDbType.PostgreSql,
            new DbConnectionForm { Host = "localhost", Port = 5432, Username = "postgres", Password = "secret" },
            "postgres",
            out var connectionString,
            out var error);

        Assert.True(built);
        Assert.Null(error);
        var parts = DbConnectionComposer.Inspect(EnumDbType.PostgreSql, connectionString!);
        Assert.Equal("postgres", parts.Database);
        Assert.Equal("postgres", parts.User);
        Assert.Equal("secret", parts.Password);
        Assert.Contains("Timeout=5", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SQL Server 清單連到 master，MySQL 清單連到 mysql")]
    public void OtherServers_UseMaintenanceDatabase()
    {
        Assert.True(DatabaseCatalog.TryBuildCatalogConnection(
            EnumDbType.SqlServer,
            new DbConnectionForm { Host = "localhost", Database = "Erp", Username = "sa", IntegratedSecurity = false },
            "master",
            out var sqlServer,
            out _));
        Assert.Equal("master", DbConnectionComposer.Inspect(EnumDbType.SqlServer, sqlServer!).Database);
        Assert.Contains("Connect Timeout=5", sqlServer, StringComparison.OrdinalIgnoreCase);

        Assert.True(DatabaseCatalog.TryBuildCatalogConnection(
            EnumDbType.MySql,
            new DbConnectionForm { Host = "localhost", Username = "root" },
            "mysql",
            out var mysql,
            out _));
        Assert.Equal("mysql", DbConnectionComposer.Inspect(EnumDbType.MySql, mysql!).Database);
        Assert.Contains("connectiontimeout=5", mysql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "還沒有帳號時不連線，只要求填使用者名稱")]
    public async Task MissingUsername_DoesNotConnect()
    {
        var result = await DatabaseCatalog.ListAsync(EnumDbType.PostgreSql, new DbConnectionForm { Host = "localhost" });

        Assert.False(result.Succeeded);
        Assert.Equal("請填寫使用者名稱", result.Error);
        Assert.Empty(result.Names);
    }
}
