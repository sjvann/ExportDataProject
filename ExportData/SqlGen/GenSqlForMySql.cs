using Dapper;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using MySql.Data.MySqlClient;
using System.Data;

namespace ExportData.SqlGen
{
    public class GenSqlForMySql : ISqlGenerater
    {
        private readonly ConfigDbControlSection config;

        public GenSqlForMySql(ConfigDbControlSection config)
        {
            this.config = config;
        }
        public IDbConnection GetConnection()
        {
            var connect = new MySqlConnection(config.ConnectionString);
            if (connect.State == ConnectionState.Closed)
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
          return $"SELECT * FROM {tableName} LIMIT {config.Size}";
        }
        public string GetSqlAllTableNameList()
        {
           return config.TableType == EnumTableType.Table
               ? "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
               : "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS";
        }

        public async Task<TableSchema?> GetTableSchemaAsync(IDbConnection connection, string tableName)
        {
            var schema = new TableSchema { TableName = tableName };

            // Get column information
            var columnSql = @"
                SELECT
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE,
                    COLUMN_DEFAULT,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE,
                    COLUMN_KEY,
                    REFERENCED_TABLE_NAME,
                    REFERENCED_COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    ON c.TABLE_NAME = kcu.TABLE_NAME AND c.COLUMN_NAME = kcu.COLUMN_NAME
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
                    IsPrimaryKey = col.COLUMN_KEY == "PRI",
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
            var countSql = $"SELECT COUNT(*) FROM `{tableName}`";
            schema.RowCount = await connection.QuerySingleAsync<long>(countSql);

            return schema;
        }

        public async Task<IEnumerable<TableRelation>?> GetTableRelationsAsync(IDbConnection connection)
        {
            var sql = @"
                SELECT
                    kcu.REFERENCED_TABLE_NAME AS ParentTable,
                    kcu.REFERENCED_COLUMN_NAME AS ParentColumn,
                    kcu.TABLE_NAME AS ChildTable,
                    kcu.COLUMN_NAME AS ChildColumn,
                    kcu.CONSTRAINT_NAME AS ConstraintName
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                WHERE kcu.REFERENCED_TABLE_NAME IS NOT NULL";

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
                DatabaseType = "MySQL"
            };

            // Get database name
            var dbNameSql = "SELECT DATABASE()";
            dbInfo.DatabaseName = await connection.QuerySingleAsync<string>(dbNameSql);

            // Get version
            var versionSql = "SELECT VERSION()";
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
