using ExportData;
using ExportData.Models.Config;
using ExportData.Models.EnumType;
using ExportData.SqlGen;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExportData.Tests;

public sealed class PostgreSqlGeneratorTests
{
    private const string BaseTableListSql =
        "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') AND table_schema NOT LIKE 'pg_toast%'";

    private const string ViewListSql =
        "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_type = 'VIEW' AND table_schema NOT IN ('pg_catalog', 'information_schema') AND table_schema NOT LIKE 'pg_toast%'";

    [Fact(DisplayName = "選到 PostgreSQL 時回傳 GenSqlForPostgreSql")]
    public void Choice_PostgreSql_ReturnsPostgreSqlGenerator()
    {
        var provider = DbChoicer.ChoiceProverder(new ConfigDbControlSection
        {
            DbType = EnumDbType.PostgreSql,
            Size = 20
        });

        Assert.IsType<GenSqlForPostgreSql>(provider);
    }

    [Fact(DisplayName = "連線失敗沿用既有例外，DbService 回傳 null 而不是空清單")]
    public async Task ConnectionFailure_ReturnsNullInsteadOfEmptyList()
    {
        var config = new ConfigDbControlSection
        {
            DbType = EnumDbType.PostgreSql,
            ConnectionString = "Not A Valid Connection String",
            TableType = EnumTableType.Table
        };

        var generator = new GenSqlForPostgreSql(config);
        Assert.Throws<ArgumentException>(() => generator.GetConnection());

        var service = new DbService(config, NullLogger<DbService>.Instance);
        var tables = await service.GetTableNamesAsync();
        Assert.Null(tables);
    }

    [Fact(DisplayName = "資料表清單只列基礎表，名稱為綱要.表名，並排除系統綱要")]
    public void TableList_BaseTables_ExcludesSystemSchemas()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection
        {
            TableType = EnumTableType.Table
        }).GetSqlAllTableNameList();

        Assert.Equal(BaseTableListSql, sql);
        Assert.DoesNotContain(",", sql.Split(" FROM ", StringSplitOptions.None)[0]);
    }

    [Fact(DisplayName = "檢視清單只列檢視，系統綱要不出現")]
    public void TableList_Views_ExcludesSystemSchemas()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection
        {
            TableType = EnumTableType.View
        }).GetSqlAllTableNameList();

        Assert.Equal(ViewListSql, sql);
        Assert.DoesNotContain("BASE TABLE", sql);
    }

    [Fact(DisplayName = "前綴只比對表名，小寫比較，單引號跳脫成兩個")]
    public void TableList_Prefix_MatchesTableNameInLowercase()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection
        {
            TableType = EnumTableType.Table,
            Prefix = "O'Brien"
        }).GetSqlAllTableNameList();

        Assert.Equal(BaseTableListSql + " AND lower(table_name) LIKE 'o''brien%' ESCAPE '\\'", sql);
        Assert.DoesNotContain("lower(table_schema)", sql);
    }

    [Fact(DisplayName = "前綴中的反斜線、百分號與底線是字面比對")]
    public void TableList_Prefix_EscapesLikeWildcards()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection
        {
            TableType = EnumTableType.Table,
            Prefix = @"a\%_'b"
        }).GetSqlAllTableNameList();

        Assert.Equal(BaseTableListSql + @" AND lower(table_name) LIKE 'a\\\%\_''b%' ESCAPE '\'", sql);
    }

    [Fact(DisplayName = "未指定物件類型時列基礎表")]
    public void TableList_NullTableType_ListsBaseTables()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection()).GetSqlAllTableNameList();

        Assert.Equal(BaseTableListSql, sql);
    }

    [Fact(DisplayName = "匯出 public.orders 且 Size 20 時使用雙引號與 LIMIT")]
    public void Records_PublicOrders_Size20_QuotesAndLimits()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection { Size = 20 })
            .GetSqlRecords("public.orders");

        Assert.Equal("SELECT * FROM \"public\".\"orders\" LIMIT 20", sql);
    }

    [Fact(DisplayName = "識別字裡的雙引號加倍")]
    public void Records_EmbeddedQuote_IsDoubled()
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection { Size = 20 })
            .GetSqlRecords("public.ord\"ers");

        Assert.Equal("SELECT * FROM \"public\".\"ord\"\"ers\" LIMIT 20", sql);
    }

    [Theory(DisplayName = "缺綱要或空段時不發出匯出語句")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("orders")]
    [InlineData(".orders")]
    [InlineData("public.")]
    [InlineData("public..orders")]
    [InlineData("public.orders.extra")]
    public void Records_MissingSchemaOrEmptySegment_EmitsNothing(string? qualifiedName)
    {
        var sql = new GenSqlForPostgreSql(new ConfigDbControlSection { Size = 20 })
            .GetSqlRecords(qualifiedName!);

        Assert.Equal(string.Empty, sql);
    }

    [Theory(DisplayName = "缺綱要或空段時不發出結構查詢")]
    [InlineData("orders")]
    [InlineData(".orders")]
    [InlineData("public.")]
    [InlineData("public..orders")]
    public async Task Schema_MissingSchemaOrEmptySegment_DoesNotQuery(string qualifiedName)
    {
        var schema = await new GenSqlForPostgreSql(new ConfigDbControlSection())
            .GetTableSchemaAsync(null!, qualifiedName);

        Assert.Null(schema);
    }

    [Fact(DisplayName = "結構查詢用 PRIMARY KEY，外鍵經 referential_constraints 連到 constraint_column_usage")]
    public void SchemaSql_UsesPrimaryKeyAndReferentialConstraints()
    {
        Assert.Contains("constraint_type = 'PRIMARY KEY'", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.Contains("information_schema.referential_constraints", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.Contains("information_schema.constraint_column_usage", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.Contains("ccu.table_schema || '.' || ccu.table_name AS referenced_table", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.Contains("refk.ordinal_position = kcu.position_in_unique_constraint", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.Contains("SELECT DISTINCT ON (kcu.table_schema, kcu.table_name, kcu.column_name)", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.DoesNotContain("REFERENCED_TABLE_NAME", GenSqlForPostgreSql.ColumnCatalogSql);
        Assert.DoesNotContain("`", GenSqlForPostgreSql.ColumnCatalogSql);
    }

    [Fact(DisplayName = "關聯兩端都是綱要.表名，並排除系統綱要")]
    public void RelationSql_BothEndsAreSchemaQualified()
    {
        var sql = GenSqlForPostgreSql.RelationCatalogSql;
        Assert.Contains("information_schema.referential_constraints", sql);
        Assert.Contains("information_schema.constraint_column_usage", sql);
        Assert.Contains("ccu.table_schema || '.' || ccu.table_name AS parent_table", sql);
        Assert.Contains("kcu.table_schema || '.' || kcu.table_name AS child_table", sql);
        Assert.Contains("refk.ordinal_position = kcu.position_in_unique_constraint", sql);
        Assert.Contains("kcu.table_schema NOT IN ('pg_catalog', 'information_schema')", sql);
        Assert.Contains("kcu.table_schema NOT LIKE 'pg_toast%'", sql);
        Assert.Contains("ccu.table_schema NOT IN ('pg_catalog', 'information_schema')", sql);
        Assert.Contains("ccu.table_schema NOT LIKE 'pg_toast%'", sql);
    }

    [Fact(DisplayName = "資料庫資訊的四段 SQL")]
    public void DatabaseInfoSql_IsCatalogOnly()
    {
        Assert.Equal("SELECT current_database()", GenSqlForPostgreSql.DatabaseNameSql);
        Assert.Equal("SELECT version()", GenSqlForPostgreSql.VersionSql);
        Assert.Equal(
            "SELECT COUNT(*)::int FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') AND table_schema NOT LIKE 'pg_toast%'",
            GenSqlForPostgreSql.TableCountSql);
        Assert.Equal(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('pg_catalog', 'information_schema') AND schema_name NOT LIKE 'pg_toast%'",
            GenSqlForPostgreSql.SchemaListSql);
    }

    [Fact(DisplayName = "欄位填入型別、長度、精度、預設值、主鍵與外鍵")]
    public void ColumnInfo_FillsTypeLengthPrecisionDefaultKeyAndForeignKey()
    {
        var column = GenSqlForPostgreSql.ToColumnInfo(
            "customer_id",
            "integer",
            "YES",
            "0",
            null,
            32,
            0,
            true,
            "public.customers",
            "id");

        Assert.Equal("customer_id", column.ColumnName);
        Assert.Equal("integer", column.DataType);
        Assert.True(column.IsNullable);
        Assert.True(column.IsPrimaryKey);
        Assert.True(column.IsForeignKey);
        Assert.Equal("0", column.DefaultValue);
        Assert.Null(column.MaxLength);
        Assert.Equal(32, column.Precision);
        Assert.Equal(0, column.Scale);
        Assert.Equal("public.customers", column.ReferencedTable);
        Assert.Equal("id", column.ReferencedColumn);
    }

    [Fact(DisplayName = "字串欄位填入長度；沒有預設值或外鍵時該格為空")]
    public void ColumnInfo_LengthOnly_EmptyDefaultAndForeignKey()
    {
        var column = GenSqlForPostgreSql.ToColumnInfo(
            "name",
            "character varying",
            "NO",
            null,
            40,
            null,
            null,
            false,
            null,
            null);

        Assert.Equal("character varying", column.DataType);
        Assert.Equal(40, column.MaxLength);
        Assert.Null(column.Precision);
        Assert.Null(column.Scale);
        Assert.False(column.IsPrimaryKey);
        Assert.False(column.IsForeignKey);
        Assert.Null(column.DefaultValue);
        Assert.Null(column.ReferencedTable);
        Assert.Null(column.ReferencedColumn);
    }

    [Fact(DisplayName = "關聯兩端沿用綱要.表名")]
    public void TableRelation_KeepsSchemaQualifiedNames()
    {
        var relation = GenSqlForPostgreSql.ToTableRelation(
            "public.customers",
            "id",
            "public.orders",
            "customer_id",
            "orders_customer_id_fkey");

        Assert.Equal("public.customers", relation.ParentTable);
        Assert.Equal("id", relation.ParentColumn);
        Assert.Equal("public.orders", relation.ChildTable);
        Assert.Equal("customer_id", relation.ChildColumn);
        Assert.Equal("orders_customer_id_fkey", relation.ConstraintName);
    }

    [Fact(DisplayName = "命令列與網頁看得到 PostgreSQL，值為 PostgreSql")]
    public void CliAndWeb_ExposePostgreSql()
    {
        var root = RepoRoot();
        var cli = File.ReadAllText(Path.Combine(root, "ExportData", "Services", "InteractiveCliService.cs"));
        var page = File.ReadAllText(Path.Combine(root, "ExportDataWeb", "Pages", "Index.cshtml"));

        Assert.Contains("5. PostgreSQL", cli);
        Assert.Contains("return EnumDbType.PostgreSql;", cli);
        Assert.Contains("<option value=\"PostgreSql\">PostgreSQL</option>", page);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ExportDataProjects.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("找不到方案根目錄。");
    }
}
