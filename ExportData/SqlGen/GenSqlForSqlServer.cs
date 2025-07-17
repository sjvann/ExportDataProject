using Dapper;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using System.Data;
using System.Data.SqlClient;



namespace ExportData.SqlGen
{
    public class GenSqlForSqlServer : ISqlGenerater
    {
        private readonly ConfigDbControlSection config;

        public GenSqlForSqlServer(ConfigDbControlSection config)
        {
            this.config = config;
        }

        public IDbConnection GetConnection()
        {   
            var connect = new SqlConnection(config.ConnectionString);
            if(connect.State == ConnectionState.Closed)
            {
                connect.Open();
            }
            return connect;
        }
        public void CloseConnection(IDbConnection conn)
        {
            if(conn.State == ConnectionState.Open || conn.State == ConnectionState.Broken)
            {
                conn.Close();
            }
        }

        public string GetSqlRecords(string tableName)
        {
          return $"SELECT TOP {config.Size} * FROM {tableName}";
        }
        public string GetSqlAllTableNameList()
        {
            string firstPart = config.TableType == EnumTableType.Table ?
                 "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" :
                 "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS";
            return config.Prefix == null ? firstPart : $"{firstPart} AND TABLE_NAME LIKE '{config.Prefix}%'";
        }

        public async Task<TableSchema?> GetTableSchemaAsync(IDbConnection connection, string tableName)
        {
            var schema = new TableSchema { TableName = tableName };

            // Get column information
            var columnSql = @"
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                    fk.REFERENCED_TABLE_NAME,
                    fk.REFERENCED_COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN (
                    SELECT
                        ku.TABLE_NAME,
                        ku.COLUMN_NAME,
                        ku2.TABLE_NAME AS REFERENCED_TABLE_NAME,
                        ku2.COLUMN_NAME AS REFERENCED_COLUMN_NAME
                    FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON rc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku2
                        ON rc.UNIQUE_CONSTRAINT_NAME = ku2.CONSTRAINT_NAME
                ) fk ON c.TABLE_NAME = fk.TABLE_NAME AND c.COLUMN_NAME = fk.COLUMN_NAME
                WHERE c.TABLE_NAME = @TableName
                ORDER BY c.ORDINAL_POSITION";

            var columns = await connection.QueryAsync(columnSql, new { TableName = tableName });

            foreach (var col in columns)
            {
                schema.Columns.Add(new ColumnInfo
                {
                    ColumnName = col.COLUMN_NAME,
                    DataType = col.DATA_TYPE,
                    IsNullable = col.IS_NULLABLE == "YES",
                    IsPrimaryKey = col.IS_PRIMARY_KEY == 1,
                    IsForeignKey = !string.IsNullOrEmpty(col.REFERENCED_TABLE_NAME),
                    DefaultValue = col.COLUMN_DEFAULT,
                    MaxLength = col.CHARACTER_MAXIMUM_LENGTH,
                    Precision = col.NUMERIC_PRECISION,
                    Scale = col.NUMERIC_SCALE,
                    ReferencedTable = col.REFERENCED_TABLE_NAME,
                    ReferencedColumn = col.REFERENCED_COLUMN_NAME
                });
            }

            // Get row count
            var countSql = $"SELECT COUNT(*) FROM [{tableName}]";
            schema.RowCount = await connection.QuerySingleAsync<long>(countSql);

            return schema;
        }

        public async Task<IEnumerable<TableRelation>?> GetTableRelationsAsync(IDbConnection connection)
        {
            var sql = @"
                SELECT
                    ku2.TABLE_NAME AS ParentTable,
                    ku2.COLUMN_NAME AS ParentColumn,
                    ku.TABLE_NAME AS ChildTable,
                    ku.COLUMN_NAME AS ChildColumn,
                    rc.CONSTRAINT_NAME AS ConstraintName
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON rc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku2
                    ON rc.UNIQUE_CONSTRAINT_NAME = ku2.CONSTRAINT_NAME";

            var relations = await connection.QueryAsync(sql);
            return relations.Select(r => new TableRelation
            {
                ParentTable = r.ParentTable,
                ParentColumn = r.ParentColumn,
                ChildTable = r.ChildTable,
                ChildColumn = r.ChildColumn,
                ConstraintName = r.ConstraintName
            });
        }

        public async Task<DatabaseInfo?> GetDatabaseInfoAsync(IDbConnection connection)
        {
            var dbInfo = new DatabaseInfo
            {
                DatabaseType = "SQL Server"
            };

            // Get database name
            var dbNameSql = "SELECT DB_NAME()";
            dbInfo.DatabaseName = await connection.QuerySingleAsync<string>(dbNameSql);

            // Get version
            var versionSql = "SELECT @@VERSION";
            dbInfo.Version = await connection.QuerySingleAsync<string>(versionSql);

            // Get table count
            var tableCountSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
            dbInfo.TableCount = await connection.QuerySingleAsync<int>(tableCountSql);

            // Get schemas
            var schemaSql = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA";
            var schemas = await connection.QueryAsync<string>(schemaSql);
            dbInfo.Schemas = schemas.ToList();

            return dbInfo;
        }
    }
}
