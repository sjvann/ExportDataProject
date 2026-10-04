using ExportData.Models.Config;
using ExportData.Models.EnumType;
using ExportData.Services;
using Xunit;

namespace ExportData.Tests;

public sealed class DbConnectionComposerTests
{
    [Fact(DisplayName = "PostgreSQL 表單組成可還原的連線字串，密碼含分號也不會被截斷")]
    public void PostgreSql_Form_RoundTripsPassword()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.PostgreSql };
        var form = new DbConnectionForm
        {
            Host = "localhost",
            Port = 5432,
            Database = "postgres",
            Username = "postgres",
            Password = "p@ss;word"
        };

        var result = DbConnectionComposer.Apply(config, form);

        Assert.True(result.Succeeded);
        Assert.Equal("postgres", config.DbName);
        var parts = DbConnectionComposer.Inspect(EnumDbType.PostgreSql, config.ConnectionString!);
        Assert.Equal("localhost", parts.Host);
        Assert.Equal(5432, parts.Port);
        Assert.Equal("postgres", parts.Database);
        Assert.Equal("postgres", parts.User);
        Assert.Equal("p@ss;word", parts.Password);
    }

    [Fact(DisplayName = "主機空白時用 localhost，連接埠空白時沿用驅動預設")]
    public void PostgreSql_BlankHost_UsesLocalhost()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.PostgreSql };
        var result = DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            Database = "app",
            Username = "app"
        });

        Assert.True(result.Succeeded);
        var parts = DbConnectionComposer.Inspect(EnumDbType.PostgreSql, config.ConnectionString!);
        Assert.Equal("localhost", parts.Host);
        Assert.Equal(5432, parts.Port);
        Assert.True(string.IsNullOrEmpty(parts.Password));
    }

    [Fact(DisplayName = "SQL Server 帳號模式帶連接埠與信任憑證")]
    public void SqlServer_AccountMode_IncludesPortAndTrust()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.SqlServer };
        var result = DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            Host = "db.internal",
            Port = 1433,
            Database = "Erp",
            Username = "sa",
            Password = "secret",
            TrustServerCertificate = true
        });

        Assert.True(result.Succeeded);
        var parts = DbConnectionComposer.Inspect(EnumDbType.SqlServer, config.ConnectionString!);
        Assert.Equal("db.internal,1433", parts.DataSource);
        Assert.Equal("Erp", parts.Database);
        Assert.Equal("sa", parts.User);
        Assert.Equal("secret", parts.Password);
        Assert.False(parts.IntegratedSecurity);
        Assert.True(parts.TrustServerCertificate);
    }

    [Fact(DisplayName = "SQL Server Windows 驗證不必填帳號，具名執行個體不附加連接埠")]
    public void SqlServer_WindowsAuth_SkipsUserAndNamedInstancePort()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.SqlServer };
        var result = DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            Host = @"localhost\SQLEXPRESS",
            Port = 1433,
            Database = "Erp",
            IntegratedSecurity = true,
            TrustServerCertificate = false
        });

        Assert.True(result.Succeeded);
        var parts = DbConnectionComposer.Inspect(EnumDbType.SqlServer, config.ConnectionString!);
        Assert.Equal(@"localhost\SQLEXPRESS", parts.DataSource);
        Assert.True(parts.IntegratedSecurity);
        Assert.False(parts.TrustServerCertificate);
        Assert.True(string.IsNullOrEmpty(parts.Password));
    }

    [Fact(DisplayName = "MySQL 表單寫入伺服器、連接埠、資料庫與帳號")]
    public void MySql_Form_SetsServerFields()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.MySql };
        var result = DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            Host = "127.0.0.1",
            Port = 3306,
            Database = "shop",
            Username = "root",
            Password = "secret"
        });

        Assert.True(result.Succeeded);
        var parts = DbConnectionComposer.Inspect(EnumDbType.MySql, config.ConnectionString!);
        Assert.Equal("127.0.0.1", parts.Host);
        Assert.Equal(3306, parts.Port);
        Assert.Equal("shop", parts.Database);
        Assert.Equal("root", parts.User);
        Assert.Equal("secret", parts.Password);
    }

    [Fact(DisplayName = "Oracle 用 Easy Connect，擁有者空白時沿用使用者")]
    public void Oracle_Form_UsesEasyConnectAndOwner()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.Oracle };
        var result = DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            Host = "localhost",
            Port = 1521,
            Database = "ORCL",
            Username = "system",
            Password = "oracle",
            Owner = "HR"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("HR", config.Owner);
        var parts = DbConnectionComposer.Inspect(EnumDbType.Oracle, config.ConnectionString!);
        Assert.Equal("localhost:1521/ORCL", parts.DataSource);
        Assert.Equal("system", parts.User);
        Assert.Equal("oracle", parts.Password);
    }

    [Fact(DisplayName = "SQLite 只用檔案路徑")]
    public void Sqlite_Form_UsesFilePath()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.Sqlite };
        var result = DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            FilePath = @"c:\temp\my data.db"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("my data.db", config.DbName);
        var parts = DbConnectionComposer.Inspect(EnumDbType.Sqlite, config.ConnectionString!);
        Assert.Equal(@"c:\temp\my data.db", parts.DataSource);
    }

    [Fact(DisplayName = "進階模式保留原來的連線字串")]
    public void RawMode_KeepsConnectionString()
    {
        var config = new ConfigDbControlSection
        {
            DbType = EnumDbType.PostgreSql,
            ConnectionString = " Host=db;Username=app;Password=secret;Database=app "
        };

        var result = DbConnectionComposer.Apply(config, new DbConnectionForm { UseRawConnectionString = true });

        Assert.True(result.Succeeded);
        Assert.Equal("Host=db;Username=app;Password=secret;Database=app", config.ConnectionString);
    }

    [Theory(DisplayName = "缺欄位時指出該填的欄位")]
    [InlineData(EnumDbType.PostgreSql, "Connection_Database", "請選擇資料庫")]
    [InlineData(EnumDbType.Oracle, "Connection_Database", "請填寫服務名稱")]
    [InlineData(EnumDbType.Sqlite, "Connection_FilePath", "請填寫資料庫檔案路徑")]
    public void MissingField_PointsAtTheInput(EnumDbType dbType, string fieldId, string message)
    {
        var result = DbConnectionComposer.Apply(new ConfigDbControlSection { DbType = dbType }, new DbConnectionForm());

        Assert.False(result.Succeeded);
        Assert.Equal(fieldId, result.FieldId);
        Assert.Equal(message, result.Error);
    }

    [Fact(DisplayName = "未選資料庫類型時要求先選擇")]
    public void MissingDbType_AsksForType()
    {
        var result = DbConnectionComposer.Apply(new ConfigDbControlSection(), new DbConnectionForm());

        Assert.False(result.Succeeded);
        Assert.Equal("DbConfig_DbType", result.FieldId);
        Assert.Equal("請選擇資料庫類型", result.Error);
    }

    [Fact(DisplayName = "連接埠超出範圍時指出連接埠")]
    public void PortOutOfRange_PointsAtPort()
    {
        var result = DbConnectionComposer.Apply(
            new ConfigDbControlSection { DbType = EnumDbType.MySql },
            new DbConnectionForm { Port = 70000, Database = "app", Username = "root" });

        Assert.False(result.Succeeded);
        Assert.Equal("Connection_Port", result.FieldId);
    }

    [Fact(DisplayName = "預覽與錯誤訊息都不含密碼原文")]
    public void MaskAndScrub_HidePassword()
    {
        var config = new ConfigDbControlSection { DbType = EnumDbType.PostgreSql };
        DbConnectionComposer.Apply(config, new DbConnectionForm
        {
            Host = "localhost",
            Port = 5432,
            Database = "postgres",
            Username = "postgres",
            Password = "s3cret-value"
        });

        var masked = DbConnectionComposer.Mask(EnumDbType.PostgreSql, config.ConnectionString!);
        var scrubbed = DbConnectionComposer.ScrubSecrets(
            "28P01 password authentication failed for s3cret-value Password=s3cret-value",
            "s3cret-value");

        Assert.DoesNotContain("s3cret-value", masked);
        Assert.Contains("******", masked);
        Assert.DoesNotContain("s3cret-value", scrubbed);
    }
}
