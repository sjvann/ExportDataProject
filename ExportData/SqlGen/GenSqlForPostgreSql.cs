using Dapper;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using Npgsql;
using System.Data;
using System.Globalization;

namespace ExportData.SqlGen
{
    public class GenSqlForPostgreSql : ISqlGenerater
    {
        internal const string ColumnCatalogSql = """
            SELECT
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale,
                CASE WHEN pk.column_name IS NOT NULL THEN TRUE ELSE FALSE END AS is_primary_key,
                fk.referenced_table,
                fk.referenced_column
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.table_schema, kcu.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_schema = kcu.constraint_schema
                    AND tc.constraint_name = kcu.constraint_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.table_schema = pk.table_schema
                AND c.table_name = pk.table_name
                AND c.column_name = pk.column_name
            LEFT JOIN (
                SELECT
                    kcu.table_schema,
                    kcu.table_name,
                    kcu.column_name,
                    ccu.table_schema || '.' || ccu.table_name AS referenced_table,
                    ccu.column_name AS referenced_column
                FROM information_schema.referential_constraints rc
                INNER JOIN information_schema.key_column_usage kcu
                    ON rc.constraint_schema = kcu.constraint_schema
                    AND rc.constraint_name = kcu.constraint_name
                INNER JOIN information_schema.constraint_column_usage ccu
                    ON rc.unique_constraint_schema = ccu.constraint_schema
                    AND rc.unique_constraint_name = ccu.constraint_name
                INNER JOIN information_schema.key_column_usage refk
                    ON rc.unique_constraint_schema = refk.constraint_schema
                    AND rc.unique_constraint_name = refk.constraint_name
                    AND refk.table_schema = ccu.table_schema
                    AND refk.table_name = ccu.table_name
                    AND refk.column_name = ccu.column_name
                    AND refk.ordinal_position = kcu.position_in_unique_constraint
            ) fk ON c.table_schema = fk.table_schema
                AND c.table_name = fk.table_name
                AND c.column_name = fk.column_name
            WHERE c.table_schema = @Schema
                AND c.table_name = @Table
            ORDER BY c.ordinal_position
            """;

        internal const string RelationCatalogSql = """
            SELECT
                ccu.table_schema || '.' || ccu.table_name AS parent_table,
                ccu.column_name AS parent_column,
                kcu.table_schema || '.' || kcu.table_name AS child_table,
                kcu.column_name AS child_column,
                rc.constraint_name AS constraint_name
            FROM information_schema.referential_constraints rc
            INNER JOIN information_schema.key_column_usage kcu
                ON rc.constraint_schema = kcu.constraint_schema
                AND rc.constraint_name = kcu.constraint_name
            INNER JOIN information_schema.constraint_column_usage ccu
                ON rc.unique_constraint_schema = ccu.constraint_schema
                AND rc.unique_constraint_name = ccu.constraint_name
            INNER JOIN information_schema.key_column_usage refk
                ON rc.unique_constraint_schema = refk.constraint_schema
                AND rc.unique_constraint_name = refk.constraint_name
                AND refk.table_schema = ccu.table_schema
                AND refk.table_name = ccu.table_name
                AND refk.column_name = ccu.column_name
                AND refk.ordinal_position = kcu.position_in_unique_constraint
            WHERE kcu.table_schema NOT IN ('pg_catalog', 'information_schema')
                AND kcu.table_schema NOT LIKE 'pg_toast%'
                AND ccu.table_schema NOT IN ('pg_catalog', 'information_schema')
                AND ccu.table_schema NOT LIKE 'pg_toast%'
            """;

        internal const string DatabaseNameSql = "SELECT current_database()";
        internal const string VersionSql = "SELECT version()";
        internal const string TableCountSql = "SELECT COUNT(*)::int FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') AND table_schema NOT LIKE 'pg_toast%'";
        internal const string SchemaListSql = "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('pg_catalog', 'information_schema') AND schema_name NOT LIKE 'pg_toast%'";

        private readonly ConfigDbControlSection config;

        public GenSqlForPostgreSql(ConfigDbControlSection config)
        {
            this.config = config;
        }

        public IDbConnection GetConnection()
        {
            var connect = new NpgsqlConnection(config.ConnectionString);
            if (connect.State == ConnectionState.Closed)
            {
                connect.Open();
            }
            return connect;
        }

        public void CloseConnection(IDbConnection conn)
        {
            if (conn.State == ConnectionState.Open || conn.State == ConnectionState.Broken)
            {
                conn.Close();
            }
        }

        public string GetSqlRecords(string tableName)
        {
            return BuildRecordsSql(tableName, config.Size);
        }

        public string GetSqlAllTableNameList()
        {
            return BuildTableListSql(config.TableType, config.Prefix);
        }

        public async Task<TableSchema?> GetTableSchemaAsync(IDbConnection connection, string tableName)
        {
            if (!TrySplitQualifiedName(tableName, out var schemaName, out var relationName))
            {
                return null;
            }

            var schema = new TableSchema { TableName = tableName };
            var columns = await connection.QueryAsync(ColumnCatalogSql, new { Schema = schemaName, Table = relationName });
            foreach (var col in columns)
            {
                object? columnName = col.column_name;
                object? dataType = col.data_type;
                object? isNullable = col.is_nullable;
                object? columnDefault = col.column_default;
                object? maxLength = col.character_maximum_length;
                object? precision = col.numeric_precision;
                object? scale = col.numeric_scale;
                object? isPrimaryKey = col.is_primary_key;
                object? referencedTable = col.referenced_table;
                object? referencedColumn = col.referenced_column;
                schema.Columns.Add(ToColumnInfo(
                    AsString(columnName),
                    AsString(dataType),
                    AsString(isNullable),
                    AsString(columnDefault),
                    maxLength,
                    precision,
                    scale,
                    isPrimaryKey,
                    AsString(referencedTable),
                    AsString(referencedColumn)));
            }

            return schema;
        }

        public async Task<IEnumerable<TableRelation>?> GetTableRelationsAsync(IDbConnection connection)
        {
            var rows = await connection.QueryAsync(RelationCatalogSql);
            return rows.Select(row =>
            {
                object? parentTable = row.parent_table;
                object? parentColumn = row.parent_column;
                object? childTable = row.child_table;
                object? childColumn = row.child_column;
                object? constraintName = row.constraint_name;
                return ToTableRelation(
                    AsString(parentTable),
                    AsString(parentColumn),
                    AsString(childTable),
                    AsString(childColumn),
                    AsString(constraintName));
            });
        }

        public async Task<DatabaseInfo?> GetDatabaseInfoAsync(IDbConnection connection)
        {
            var dbInfo = new DatabaseInfo
            {
                DatabaseType = "PostgreSQL"
            };

            dbInfo.DatabaseName = await connection.QuerySingleAsync<string>(DatabaseNameSql);
            dbInfo.Version = await connection.QuerySingleAsync<string>(VersionSql);
            dbInfo.TableCount = await connection.QuerySingleAsync<int>(TableCountSql);
            var schemas = await connection.QueryAsync<string>(SchemaListSql);
            dbInfo.Schemas = schemas.ToList();
            return dbInfo;
        }

        internal static string BuildTableListSql(EnumTableType? tableType, string? prefix)
        {
            var tableTypeLiteral = tableType == EnumTableType.Table ? "BASE TABLE" : "VIEW";
            var sql =
                "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_type = '"
                + tableTypeLiteral
                + "' AND table_schema NOT IN ('pg_catalog', 'information_schema') AND table_schema NOT LIKE 'pg_toast%'";
            if (string.IsNullOrEmpty(prefix))
            {
                return sql;
            }

            var literal = prefix.ToLowerInvariant().Replace("'", "''", StringComparison.Ordinal);
            return sql + " AND lower(table_name) LIKE '" + literal + "%'";
        }

        internal static string BuildRecordsSql(string? qualifiedName, int size)
        {
            var quoted = TryQuoteQualifiedName(qualifiedName);
            if (quoted == null)
            {
                return string.Empty;
            }

            return $"SELECT * FROM {quoted} LIMIT {size}";
        }

        internal static string? TryQuoteQualifiedName(string? qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return null;
            }

            var parts = qualifiedName.Split('.');
            if (parts.Length < 2 || parts.Any(part => part.Length == 0))
            {
                return null;
            }

            return string.Join(".", parts.Select(QuoteIdentifier));
        }

        internal static bool TrySplitQualifiedName(string? qualifiedName, out string schema, out string relation)
        {
            schema = string.Empty;
            relation = string.Empty;
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return false;
            }

            var parts = qualifiedName.Split('.');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                return false;
            }

            schema = parts[0];
            relation = parts[1];
            return true;
        }

        internal static ColumnInfo ToColumnInfo(
            string? columnName,
            string? dataType,
            string? isNullable,
            string? columnDefault,
            object? maxLength,
            object? precision,
            object? scale,
            object? isPrimaryKey,
            string? referencedTable,
            string? referencedColumn)
        {
            var hasForeignKey = !string.IsNullOrEmpty(referencedTable);
            return new ColumnInfo
            {
                ColumnName = columnName ?? string.Empty,
                DataType = dataType ?? string.Empty,
                IsNullable = isNullable == "YES",
                IsPrimaryKey = AsBool(isPrimaryKey),
                IsForeignKey = hasForeignKey,
                DefaultValue = string.IsNullOrEmpty(columnDefault) ? null : columnDefault,
                MaxLength = AsInt(maxLength),
                Precision = AsInt(precision),
                Scale = AsInt(scale),
                ReferencedTable = hasForeignKey ? referencedTable : null,
                ReferencedColumn = hasForeignKey && !string.IsNullOrEmpty(referencedColumn) ? referencedColumn : null
            };
        }

        internal static TableRelation ToTableRelation(
            string? parentTable,
            string? parentColumn,
            string? childTable,
            string? childColumn,
            string? constraintName)
        {
            return new TableRelation
            {
                ParentTable = parentTable ?? string.Empty,
                ParentColumn = parentColumn ?? string.Empty,
                ChildTable = childTable ?? string.Empty,
                ChildColumn = childColumn ?? string.Empty,
                ConstraintName = constraintName ?? string.Empty
            };
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        private static string? AsString(object? value)
        {
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static bool AsBool(object? value)
        {
            return value switch
            {
                bool flag => flag,
                int number => number != 0,
                long number => number != 0,
                _ => false
            };
        }

        private static int? AsInt(object? value)
        {
            if (value is null or DBNull)
            {
                return null;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
