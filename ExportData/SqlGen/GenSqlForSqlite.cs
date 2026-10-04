
using Dapper;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using System.Data;
using System.Data.SQLite;

namespace ExportData.SqlGen
{
    public class GenSqlForSqlite : ISqlGenerater
    {
        private readonly ConfigDbControlSection config;

        public GenSqlForSqlite(ConfigDbControlSection config)
        {
            this.config = config;
        }

        public IDbConnection GetConnection()
        {
            var connect = new SQLiteConnection(config.ConnectionString);
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
          return $"SELECT * FROM {tableName} LIMIT {config.Size}";
        }

        public string GetSqlAllRecords(string tableName)
        {
          return $"SELECT * FROM {tableName}";
        }

        public string GetSqlAllTableNameList()
        {
           return config.TableType == EnumTableType.Table  ? "SELECT name FROM sqlite_master WHERE type='table';" : "SELECT name FROM sqlite_master WHERE type='view';";
        }

        public async Task<TableSchema?> GetTableSchemaAsync(IDbConnection connection, string tableName)
        {
            var schema = new TableSchema { TableName = tableName };

            // Get column information from PRAGMA table_info
            var columnSql = $"PRAGMA table_info({tableName})";
            var columns = await connection.QueryAsync(columnSql);

            foreach (var col in columns)
            {
                schema.Columns.Add(new ColumnInfo
                {
                    ColumnName = col.name,
                    DataType = col.type,
                    IsNullable = col.notnull == 0,
                    IsPrimaryKey = col.pk == 1,
                    DefaultValue = col.dflt_value
                });
            }

            // Get foreign key information
            var fkSql = $"PRAGMA foreign_key_list({tableName})";
            var foreignKeys = await connection.QueryAsync(fkSql);

            foreach (var fk in foreignKeys)
            {
                var column = schema.Columns.FirstOrDefault(c => c.ColumnName == fk.from);
                if (column != null)
                {
                    column.IsForeignKey = true;
                    column.ReferencedTable = fk.table;
                    column.ReferencedColumn = fk.to;
                }
            }

            // Get row count
            var countSql = $"SELECT COUNT(*) FROM [{tableName}]";
            schema.RowCount = await connection.QuerySingleAsync<long>(countSql);

            return schema;
        }

        public async Task<IEnumerable<TableRelation>?> GetTableRelationsAsync(IDbConnection connection)
        {
            var relations = new List<TableRelation>();

            // Get all tables first
            var tablesSql = "SELECT name FROM sqlite_master WHERE type='table'";
            var tables = await connection.QueryAsync<string>(tablesSql);

            foreach (var table in tables)
            {
                var fkSql = $"PRAGMA foreign_key_list({table})";
                var foreignKeys = await connection.QueryAsync(fkSql);

                foreach (var fk in foreignKeys)
                {
                    relations.Add(new TableRelation
                    {
                        ChildTable = table,
                        ChildColumn = fk.from,
                        ParentTable = fk.table,
                        ParentColumn = fk.to,
                        ConstraintName = $"FK_{table}_{fk.from}"
                    });
                }
            }

            return relations;
        }

        public async Task<DatabaseInfo?> GetDatabaseInfoAsync(IDbConnection connection)
        {
            var dbInfo = new DatabaseInfo
            {
                DatabaseType = "SQLite",
                DatabaseName = "SQLite Database"
            };

            // Get SQLite version
            var versionSql = "SELECT sqlite_version()";
            dbInfo.Version = await connection.QuerySingleAsync<string>(versionSql);

            // Get table count
            var tableCountSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
            dbInfo.TableCount = await connection.QuerySingleAsync<int>(tableCountSql);

            // SQLite doesn't have schemas in the traditional sense
            dbInfo.Schemas = new List<string> { "main" };

            return dbInfo;
        }
    }
}
