namespace ExportData.Models.Database
{
    public class TableSchema
    {
        public string TableName { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public List<ColumnInfo> Columns { get; set; } = new();
        public List<IndexInfo> Indexes { get; set; } = new();
        public long RowCount { get; set; }
    }

    public class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string? DefaultValue { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public string? ReferencedTable { get; set; }
        public string? ReferencedColumn { get; set; }
    }

    public class IndexInfo
    {
        public string IndexName { get; set; } = string.Empty;
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public List<string> Columns { get; set; } = new();
    }

    public class TableRelation
    {
        public string ParentTable { get; set; } = string.Empty;
        public string ParentColumn { get; set; } = string.Empty;
        public string ChildTable { get; set; } = string.Empty;
        public string ChildColumn { get; set; } = string.Empty;
        public string ConstraintName { get; set; } = string.Empty;
    }

    public class DatabaseInfo
    {
        public string DatabaseName { get; set; } = string.Empty;
        public string DatabaseType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<string> Schemas { get; set; } = new();
        public int TableCount { get; set; }
    }
}
