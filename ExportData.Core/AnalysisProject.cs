using System.Text.Json.Serialization;

namespace ExportData.Core;

public enum DatabaseType
{
    Sqlite,
    SqlServer,
    MySql,
    Oracle
}

public enum IdentifierRule
{
    OrdinalIgnoreCase,
    Ordinal,
    OracleUnquotedUppercase
}

public enum SnapshotKind
{
    Table,
    View
}

public enum ConfirmationStatus
{
    [JsonStringEnumMemberName("未看")]
    Unseen,

    [JsonStringEnumMemberName("草稿")]
    Draft,

    [JsonStringEnumMemberName("已確認")]
    Confirmed,

    [JsonStringEnumMemberName("略過")]
    Skipped
}

public sealed class CatalogRow
{
    public CatalogRow(
        string schema,
        string tableName,
        SnapshotKind kind,
        int columnCount,
        int? approximateRowCount)
    {
        Schema = schema;
        TableName = tableName;
        Kind = kind;
        ColumnCount = columnCount;
        ApproximateRowCount = approximateRowCount;
    }

    public string Schema { get; }

    public string TableName { get; }

    public SnapshotKind Kind { get; }

    public int ColumnCount { get; }

    public int? ApproximateRowCount { get; }
}

public sealed class TableSnapshot
{
    [JsonRequired]
    public string Schema { get; init; } = "";

    [JsonRequired]
    public string TableName { get; init; } = "";

    public int? ApproximateRowCount { get; init; }

    [JsonRequired]
    public int DeclaredRelationCount { get; init; }

    [JsonRequired]
    public int ColumnCount { get; init; }

    public ConfirmationStatus? ConfirmationStatus { get; init; }

    [JsonRequired]
    public SnapshotKind Kind { get; init; }
}

public sealed class DeclaredRelation
{
    [JsonRequired]
    public string ConstraintName { get; init; } = "";

    [JsonRequired]
    public string ParentSchema { get; init; } = "";

    [JsonRequired]
    public string ParentTable { get; init; } = "";

    [JsonRequired]
    public string ParentColumn { get; init; } = "";

    [JsonRequired]
    public string ChildSchema { get; init; } = "";

    [JsonRequired]
    public string ChildTable { get; init; } = "";

    [JsonRequired]
    public string ChildColumn { get; init; } = "";
}

public sealed class InferredRelation
{
}

public sealed class AnalysisModule
{
}

public sealed class StructureChange
{
}

public sealed class AnalysisProject
{
    [JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonRequired]
    public DatabaseType DatabaseType { get; init; }

    [JsonRequired]
    public IdentifierRule IdentifierRule { get; init; }

    [JsonRequired]
    public bool IncludeViews { get; init; }

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    [JsonRequired]
    public List<TableSnapshot> TableSnapshots { get; init; } = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    [JsonRequired]
    public List<DeclaredRelation> DeclaredRelations { get; init; } = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    [JsonRequired]
    public List<InferredRelation> InferredRelations { get; init; } = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    [JsonRequired]
    public List<AnalysisModule> Modules { get; init; } = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    [JsonRequired]
    public List<StructureChange> StructureChanges { get; init; } = [];

    public static AnalysisProject Create(
        string displayName,
        DatabaseType databaseType,
        IdentifierRule identifierRule,
        bool includeViews,
        IReadOnlyList<CatalogRow> catalogRows,
        IReadOnlyList<DeclaredRelation> declaredRelations)
    {
        ArgumentNullException.ThrowIfNull(catalogRows);
        ArgumentNullException.ThrowIfNull(declaredRelations);
        DisplayNameRules.EnsureValid(displayName);

        foreach (var row in catalogRows)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.Schema is null)
                throw new ArgumentException("目錄列的綱要不可為 null。", nameof(catalogRows));
            if (string.IsNullOrWhiteSpace(row.TableName))
                throw new ArgumentException("目錄列的表名不可空白。", nameof(catalogRows));
            if (row.ColumnCount < 0)
                throw new ArgumentOutOfRangeException(nameof(catalogRows), "欄位數不可為負數。");
        }

        foreach (var relation in declaredRelations)
        {
            ArgumentNullException.ThrowIfNull(relation);
            if (relation.ConstraintName is null
                || relation.ParentSchema is null
                || string.IsNullOrWhiteSpace(relation.ParentTable)
                || string.IsNullOrWhiteSpace(relation.ParentColumn)
                || relation.ChildSchema is null
                || string.IsNullOrWhiteSpace(relation.ChildTable)
                || string.IsNullOrWhiteSpace(relation.ChildColumn))
            {
                throw new ArgumentException("宣告關聯須有約束名稱、父表、父欄、子表與子欄。", nameof(declaredRelations));
            }
        }

        var snapshots = new List<TableSnapshot>();
        foreach (var row in catalogRows)
        {
            if (!includeViews && row.Kind == SnapshotKind.View)
                continue;

            snapshots.Add(new TableSnapshot
            {
                Schema = row.Schema,
                TableName = row.TableName,
                ApproximateRowCount = row.ApproximateRowCount,
                DeclaredRelationCount = CountRelations(identifierRule, row.Schema, row.TableName, declaredRelations),
                ColumnCount = row.ColumnCount,
                ConfirmationStatus = row.Kind == SnapshotKind.View ? null : ConfirmationStatus.Unseen,
                Kind = row.Kind
            });
        }

        return new AnalysisProject
        {
            DisplayName = displayName,
            DatabaseType = databaseType,
            IdentifierRule = identifierRule,
            IncludeViews = includeViews,
            TableSnapshots = snapshots,
            DeclaredRelations = declaredRelations.ToList(),
            InferredRelations = [],
            Modules = [],
            StructureChanges = []
        };
    }

    private static int CountRelations(
        IdentifierRule rule,
        string schema,
        string tableName,
        IReadOnlyList<DeclaredRelation> relations)
    {
        var count = 0;
        foreach (var relation in relations)
        {
            if (SameTable(rule, relation.ParentSchema, relation.ParentTable, schema, tableName))
                count++;
            if (SameTable(rule, relation.ChildSchema, relation.ChildTable, schema, tableName))
                count++;
        }

        return count;
    }

    private static bool SameTable(
        IdentifierRule rule,
        string schema,
        string tableName,
        string otherSchema,
        string otherTable)
    {
        return SameIdentifier(rule, schema, otherSchema)
            && SameIdentifier(rule, tableName, otherTable);
    }

    private static bool SameIdentifier(IdentifierRule rule, string left, string right)
    {
        return rule switch
        {
            IdentifierRule.Ordinal => string.Equals(left, right, StringComparison.Ordinal),
            IdentifierRule.OrdinalIgnoreCase => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            IdentifierRule.OracleUnquotedUppercase => string.Equals(
                left.ToUpperInvariant(),
                right.ToUpperInvariant(),
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(rule))
        };
    }
}

internal static class DisplayNameRules
{
    public static void EnsureValid(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("顯示名稱不可空白。", nameof(displayName));
        if (displayName != displayName.Trim() || displayName.EndsWith('.'))
            throw new ArgumentException("顯示名稱不能有前導或結尾空白，也不能以句點結尾。", nameof(displayName));
        if (displayName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("顯示名稱含有無法作為檔名的字元。", nameof(displayName));
    }
}
