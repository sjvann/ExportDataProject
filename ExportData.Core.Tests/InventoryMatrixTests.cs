using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using ExportData.Core;
using Xunit;

namespace ExportData.Core.Tests;

public sealed class InventoryMatrixTests
{
    private static readonly string[] ForbiddenPropertyNames =
    [
        "ConnectionString",
        "ConnectionStrings",
        "Password",
        "Pwd",
        "Secret",
        "Cell",
        "Cells",
        "連線字串",
        "密碼",
        "儲存格"
    ];

    [Fact(DisplayName = "新專案：空資料夾與顯示名稱寫出 .analysis.json，集合為空")]
    public void NewProject_EmptyFolder_WritesAnalysisJsonWithEmptyCollections()
    {
        using var folder = new TempFolder();
        var project = AnalysisProject.Create(
            "舊系統甲",
            DatabaseType.Sqlite,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: false,
            catalogRows: [],
            declaredRelations: []);

        var store = new AnalysisProjectStore();
        store.Save(folder.Path, project);

        var official = Path.Combine(folder.Path, "舊系統甲.analysis.json");
        Assert.Equal(new[] { official }, Directory.GetFiles(folder.Path));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.secret"));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));

        var loaded = store.Load(folder.Path, "舊系統甲");
        Assert.Equal("舊系統甲", loaded.DisplayName);
        Assert.Empty(loaded.TableSnapshots);
        Assert.Empty(loaded.DeclaredRelations);
        Assert.Empty(loaded.InferredRelations);
        Assert.Empty(loaded.Modules);
        Assert.Empty(loaded.StructureChanges);
    }

    [Fact(DisplayName = "不含檢視表：快照只有資料表")]
    public void ExcludeViews_SnapshotContainsOnlyTheTable()
    {
        using var folder = new TempFolder();
        var project = AnalysisProject.Create(
            "不含檢視",
            DatabaseType.SqlServer,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: false,
            catalogRows:
            [
                new CatalogRow("dbo", "Order", SnapshotKind.Table, columnCount: 4, approximateRowCount: 10),
                new CatalogRow("dbo", "OrderView", SnapshotKind.View, columnCount: 2, approximateRowCount: 10)
            ],
            declaredRelations:
            [
                Relation("FK_OrderView", "dbo", "Order", "Id", "dbo", "OrderView", "OrderId")
            ]);

        var store = new AnalysisProjectStore();
        store.Save(folder.Path, project);
        var loaded = store.Load(folder.Path, "不含檢視");

        var snapshot = Assert.Single(loaded.TableSnapshots);
        Assert.Equal("Order", snapshot.TableName);
        Assert.Equal(SnapshotKind.Table, snapshot.Kind);
        Assert.Equal(ConfirmationStatus.Unseen, snapshot.ConfirmationStatus);
        Assert.Equal(1, snapshot.DeclaredRelationCount);
        Assert.False(loaded.IncludeViews);
    }

    [Fact(DisplayName = "包含檢視表：兩者都在，檢視表確認狀態不適用")]
    public void IncludeViews_BothPresent_ViewConfirmationStatusEmpty()
    {
        using var folder = new TempFolder();
        var project = AnalysisProject.Create(
            "包含檢視",
            DatabaseType.SqlServer,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: true,
            catalogRows:
            [
                new CatalogRow("dbo", "Order", SnapshotKind.Table, columnCount: 4, approximateRowCount: 10),
                new CatalogRow("dbo", "OrderView", SnapshotKind.View, columnCount: 2, approximateRowCount: 10)
            ],
            declaredRelations: []);

        var store = new AnalysisProjectStore();
        store.Save(folder.Path, project);
        var loaded = store.Load(folder.Path, "包含檢視");

        Assert.Equal(2, loaded.TableSnapshots.Count);
        Assert.Equal(SnapshotKind.Table, loaded.TableSnapshots[0].Kind);
        Assert.Equal(ConfirmationStatus.Unseen, loaded.TableSnapshots[0].ConfirmationStatus);
        Assert.Equal(SnapshotKind.View, loaded.TableSnapshots[1].Kind);
        Assert.Null(loaded.TableSnapshots[1].ConfirmationStatus);
        Assert.True(loaded.IncludeViews);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder.Path, "包含檢視.analysis.json")));
        var statuses = document.RootElement.GetProperty("tableSnapshots").EnumerateArray()
            .Select(row => row.GetProperty("confirmationStatus"))
            .ToArray();
        Assert.Equal("未看", statuses[0].GetString());
        Assert.Equal(JsonValueKind.Null, statuses[1].ValueKind);
    }

    [Fact(DisplayName = "筆數未知：該列留下，筆數為未知")]
    public void UnknownRowCount_RowRemainsWithNullCount()
    {
        using var folder = new TempFolder();
        var project = AnalysisProject.Create(
            "筆數未知",
            DatabaseType.MySql,
            IdentifierRule.Ordinal,
            includeViews: false,
            catalogRows:
            [
                new CatalogRow("", "kept", SnapshotKind.Table, columnCount: 3, approximateRowCount: null),
                new CatalogRow("", "other", SnapshotKind.Table, columnCount: 1, approximateRowCount: 8)
            ],
            declaredRelations: []);

        var store = new AnalysisProjectStore();
        store.Save(folder.Path, project);
        var loaded = store.Load(folder.Path, "筆數未知");

        Assert.Equal(2, loaded.TableSnapshots.Count);
        Assert.Equal("kept", loaded.TableSnapshots[0].TableName);
        Assert.Null(loaded.TableSnapshots[0].ApproximateRowCount);
        Assert.Equal(8, loaded.TableSnapshots[1].ApproximateRowCount);
        Assert.Equal(ConfirmationStatus.Unseen, loaded.TableSnapshots[0].ConfirmationStatus);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder.Path, "筆數未知.analysis.json")));
        var count = document.RootElement.GetProperty("tableSnapshots")[0].GetProperty("approximateRowCount");
        Assert.Equal(JsonValueKind.Null, count.ValueKind);
    }

    [Fact(DisplayName = "名稱重複：不覆寫既有檔，拒絕並說明")]
    public void DuplicateDisplayName_RefusesWithoutOverwrite()
    {
        using var folder = new TempFolder();
        var store = new AnalysisProjectStore();
        var first = EmptyProject("重複名");
        store.Save(folder.Path, first);
        var official = Path.Combine(folder.Path, "重複名.analysis.json");
        var before = File.ReadAllBytes(official);

        var second = AnalysisProject.Create(
            "重複名",
            DatabaseType.Oracle,
            IdentifierRule.OracleUnquotedUppercase,
            includeViews: true,
            catalogRows: [new CatalogRow("", "Emp", SnapshotKind.Table, columnCount: 1, approximateRowCount: 1)],
            declaredRelations: []);

        var error = Assert.Throws<AnalysisProjectAlreadyExistsException>(() => store.Save(folder.Path, second));
        Assert.Contains("拒絕覆寫", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(official));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));

        var loaded = store.Load(folder.Path, "重複名");
        Assert.Equal(DatabaseType.Sqlite, loaded.DatabaseType);
        Assert.Empty(loaded.TableSnapshots);
    }

    [Fact(DisplayName = "讀回：與寫入內容一致")]
    public void ReadBack_MatchesWrittenContent()
    {
        using var folder = new TempFolder();
        var project = AnalysisProject.Create(
            "讀回",
            DatabaseType.Oracle,
            IdentifierRule.OracleUnquotedUppercase,
            includeViews: false,
            catalogRows: [new CatalogRow("HR", "Emp", SnapshotKind.Table, columnCount: 5, approximateRowCount: 20)],
            declaredRelations:
            [
                Relation("FK_Emp_Dept", "HR", "Dept", "Id", "HR", "Emp", "DeptId")
            ]);

        var store = new AnalysisProjectStore();
        store.Save(folder.Path, project);
        var loaded = store.Load(folder.Path, "讀回");

        Assert.Equal(project.DisplayName, loaded.DisplayName);
        Assert.Equal(project.DatabaseType, loaded.DatabaseType);
        Assert.Equal(project.IdentifierRule, loaded.IdentifierRule);
        Assert.Equal(project.IncludeViews, loaded.IncludeViews);
        Assert.Equal(project.TableSnapshots.Count, loaded.TableSnapshots.Count);
        Assert.Equal(project.TableSnapshots[0].Schema, loaded.TableSnapshots[0].Schema);
        Assert.Equal(project.TableSnapshots[0].TableName, loaded.TableSnapshots[0].TableName);
        Assert.Equal(project.TableSnapshots[0].ApproximateRowCount, loaded.TableSnapshots[0].ApproximateRowCount);
        Assert.Equal(1, loaded.TableSnapshots[0].DeclaredRelationCount);
        Assert.Equal(project.TableSnapshots[0].ColumnCount, loaded.TableSnapshots[0].ColumnCount);
        Assert.Equal(project.TableSnapshots[0].ConfirmationStatus, loaded.TableSnapshots[0].ConfirmationStatus);
        Assert.Equal(project.TableSnapshots[0].Kind, loaded.TableSnapshots[0].Kind);
        Assert.Equal(project.DeclaredRelations.Count, loaded.DeclaredRelations.Count);
        Assert.Equal("FK_Emp_Dept", loaded.DeclaredRelations[0].ConstraintName);
        Assert.Equal("HR", loaded.DeclaredRelations[0].ParentSchema);
        Assert.Equal("Dept", loaded.DeclaredRelations[0].ParentTable);
        Assert.Equal("Id", loaded.DeclaredRelations[0].ParentColumn);
        Assert.Equal("HR", loaded.DeclaredRelations[0].ChildSchema);
        Assert.Equal("Emp", loaded.DeclaredRelations[0].ChildTable);
        Assert.Equal("DeptId", loaded.DeclaredRelations[0].ChildColumn);
        Assert.Empty(loaded.InferredRelations);
        Assert.Empty(loaded.Modules);
        Assert.Empty(loaded.StructureChanges);
    }

    [Fact(DisplayName = "讀回：檔案不存在則失敗，不產生空快照")]
    public void ReadBack_MissingFile_FailsWithoutEmptySnapshot()
    {
        using var folder = new TempFolder();
        var official = Path.Combine(folder.Path, "不存在.analysis.json");
        var store = new AnalysisProjectStore();

        var error = Assert.Throws<AnalysisProjectNotFoundException>(() => store.Load(folder.Path, "不存在"));

        Assert.Contains("不存在.analysis.json", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(official));
        Assert.Empty(Directory.GetFiles(folder.Path));
    }

    [Fact(DisplayName = "寫入中斷於暫存檔尚未改名：正式檔不存在")]
    public void InterruptedBeforeRename_OfficialFileDoesNotExist()
    {
        using var folder = new TempFolder();
        var project = EmptyProject("中斷");
        var store = new AnalysisProjectStore(commitRename: false);

        store.Save(folder.Path, project);

        var official = Path.Combine(folder.Path, "中斷.analysis.json");
        Assert.False(File.Exists(official));
        var temps = Directory.GetFiles(folder.Path, "*.tmp");
        var temp = Assert.Single(temps);
        Assert.Contains("\"displayName\": \"中斷\"", File.ReadAllText(temp), StringComparison.Ordinal);

        var reader = new AnalysisProjectStore();
        Assert.Throws<AnalysisProjectNotFoundException>(() => reader.Load(folder.Path, "中斷"));
    }

    [Fact(DisplayName = "寫入中斷於暫存檔尚未改名：正式檔仍是改名前的內容")]
    public void InterruptedBeforeRename_ExistingOfficialFileKeepsPreviousContent()
    {
        using var folder = new TempFolder();
        var store = new AnalysisProjectStore();
        store.Save(folder.Path, EmptyProject("已存在"));
        var official = Path.Combine(folder.Path, "已存在.analysis.json");
        var before = File.ReadAllBytes(official);
        File.WriteAllText(Path.Combine(folder.Path, "已存在.analysis.json.leftover.tmp"), """{"displayName":"其他"}""");

        var loaded = store.Load(folder.Path, "已存在");

        Assert.Equal("已存在", loaded.DisplayName);
        Assert.Equal(DatabaseType.Sqlite, loaded.DatabaseType);
        Assert.Empty(loaded.TableSnapshots);
        Assert.Equal(before, File.ReadAllBytes(official));
    }

    [Fact(DisplayName = "寫入後的 JSON 與型別都沒有連線字串欄位")]
    public void WrittenJsonAndTypes_HaveNoConnectionStringPasswordOrCell()
    {
        using var folder = new TempFolder();
        var project = AnalysisProject.Create(
            "無秘密",
            DatabaseType.SqlServer,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: true,
            catalogRows:
            [
                new CatalogRow("dbo", "T", SnapshotKind.Table, columnCount: 1, approximateRowCount: null),
                new CatalogRow("dbo", "V", SnapshotKind.View, columnCount: 1, approximateRowCount: 1)
            ],
            declaredRelations:
            [
                Relation("FK", "dbo", "T", "Id", "dbo", "V", "TId")
            ]);
        new AnalysisProjectStore().Save(folder.Path, project);

        var json = File.ReadAllText(Path.Combine(folder.Path, "無秘密.analysis.json"));
        using var document = JsonDocument.Parse(json);
        var names = JsonNames(document.RootElement).ToArray();
        Assert.Equal(
            [
                "displayName",
                "databaseType",
                "identifierRule",
                "includeViews",
                "tableSnapshots",
                "declaredRelations",
                "inferredRelations",
                "modules",
                "structureChanges"
            ],
            document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());

        foreach (var name in names)
            Assert.DoesNotContain(ForbiddenPropertyNames, forbidden => Same(forbidden, name));

        foreach (var property in typeof(AnalysisProject).Assembly.GetTypes().SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
            Assert.DoesNotContain(ForbiddenPropertyNames, forbidden => Same(forbidden, property.Name));

        var referenced = typeof(AnalysisProject).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain(referenced, name => name is "ExportData" or "ExportDataWeb" or "Dapper" or "MySql.Data" or "Oracle.ManagedDataAccess" or "System.Data.SQLite" or "System.Data.SqlClient" or "System.Security.Cryptography.ProtectedData");
        Assert.DoesNotContain(referenced, name => name?.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) == true);

        var framework = typeof(AnalysisProject).Assembly
            .GetCustomAttributes<TargetFrameworkAttribute>()
            .Single()
            .FrameworkName;
        Assert.Contains("Version=v10.0", framework, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "宣告關聯數把作為父表與作為子表都計入")]
    public void DeclaredRelationCount_IncludesParentAndChildRoles()
    {
        var self = AnalysisProject.Create(
            "自參",
            DatabaseType.SqlServer,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: false,
            catalogRows: [new CatalogRow("dbo", "Employee", SnapshotKind.Table, columnCount: 2, approximateRowCount: 4)],
            declaredRelations:
            [
                Relation("FK_Employee_Manager", "dbo", "Employee", "Id", "dbo", "Employee", "ManagerId")
            ]);
        Assert.Equal(2, Assert.Single(self.TableSnapshots).DeclaredRelationCount);

        var ignoreCase = AnalysisProject.Create(
            "忽略大小寫",
            DatabaseType.SqlServer,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: false,
            catalogRows: [new CatalogRow("dbo", "Employee", SnapshotKind.Table, columnCount: 2, approximateRowCount: 4)],
            declaredRelations:
            [
                Relation("FK_Employee_Manager", "DBO", "employee", "Id", "dbo", "Other", "EmployeeId")
            ]);
        Assert.Equal(1, Assert.Single(ignoreCase.TableSnapshots).DeclaredRelationCount);

        var oracle = AnalysisProject.Create(
            "甲骨",
            DatabaseType.Oracle,
            IdentifierRule.OracleUnquotedUppercase,
            includeViews: false,
            catalogRows: [new CatalogRow("HR", "Emp", SnapshotKind.Table, columnCount: 2, approximateRowCount: 4)],
            declaredRelations:
            [
                Relation("FK_EMP", "hr", "EMP", "ID", "HR", "emp", "DEPT_ID")
            ]);
        Assert.Equal(2, Assert.Single(oracle.TableSnapshots).DeclaredRelationCount);

        var ordinal = AnalysisProject.Create(
            "區分",
            DatabaseType.MySql,
            IdentifierRule.Ordinal,
            includeViews: false,
            catalogRows: [new CatalogRow("", "Emp", SnapshotKind.Table, columnCount: 1, approximateRowCount: 1)],
            declaredRelations:
            [
                Relation("FK_EMP", "", "emp", "Id", "", "Other", "EmpId")
            ]);
        Assert.Equal(0, Assert.Single(ordinal.TableSnapshots).DeclaredRelationCount);
    }

    private static AnalysisProject EmptyProject(string displayName) =>
        AnalysisProject.Create(
            displayName,
            DatabaseType.Sqlite,
            IdentifierRule.OrdinalIgnoreCase,
            includeViews: false,
            catalogRows: [],
            declaredRelations: []);

    private static DeclaredRelation Relation(
        string constraintName,
        string parentSchema,
        string parentTable,
        string parentColumn,
        string childSchema,
        string childTable,
        string childColumn) =>
        new()
        {
            ConstraintName = constraintName,
            ParentSchema = parentSchema,
            ParentTable = parentTable,
            ParentColumn = parentColumn,
            ChildSchema = childSchema,
            ChildTable = childTable,
            ChildColumn = childColumn
        };

    private static IEnumerable<string> JsonNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var child in JsonNames(property.Value))
                        yield return child;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in JsonNames(item))
                        yield return child;
                }
                break;
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "edp-core", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
