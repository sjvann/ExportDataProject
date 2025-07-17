using Dapper;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;

namespace ExportData.SqlGen
{
    public class GenSqlForOracle : ISqlGenerater
    {
        private readonly ConfigDbControlSection config;

        public GenSqlForOracle(ConfigDbControlSection config)
        {
            this.config = config;
        }

        public IDbConnection GetConnection()
        {
            var connect = new OracleConnection(config.ConnectionString);
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
            return $"SELECT * FROM {config.Owner}.{tableName} WHERE rownum <= {config.Size}";
        }
        public string GetSqlAllTableNameList()
        {
            StringBuilder sb = new("SELECT");
            if (config.TableType == EnumTableType.Table)
            {
                sb.Append(" TABLE_NAME FROM ALL_TABLES");
            }
            else
            {
                sb.Append(" VIEW_NAME FROM ALL_VIEWS");
            }

            if (!string.IsNullOrEmpty(config.Owner))
            {
                sb.Append($" WHERE OWNER='{config.Owner}'");
            }
            return sb.ToString().Trim();
        }

        public async Task<TableSchema?> GetTableSchemaAsync(IDbConnection connection, string tableName)
        {
            var schema = new TableSchema { TableName = tableName };

            // Get column information
            var columnSql = @"
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.NULLABLE,
                    c.DATA_DEFAULT,
                    c.DATA_LENGTH,
                    c.DATA_PRECISION,
                    c.DATA_SCALE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'Y' ELSE 'N' END AS IS_PRIMARY_KEY,
                    fk.R_TABLE_NAME AS REFERENCED_TABLE_NAME,
                    fk.R_COLUMN_NAME AS REFERENCED_COLUMN_NAME
                FROM ALL_TAB_COLUMNS c
                LEFT JOIN (
                    SELECT cc.COLUMN_NAME, cc.TABLE_NAME
                    FROM ALL_CONSTRAINTS cons
                    INNER JOIN ALL_CONS_COLUMNS cc ON cons.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
                    WHERE cons.CONSTRAINT_TYPE = 'P'
                ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN (
                    SELECT
                        cc.COLUMN_NAME,
                        cc.TABLE_NAME,
                        r_cc.TABLE_NAME AS R_TABLE_NAME,
                        r_cc.COLUMN_NAME AS R_COLUMN_NAME
                    FROM ALL_CONSTRAINTS cons
                    INNER JOIN ALL_CONS_COLUMNS cc ON cons.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
                    INNER JOIN ALL_CONS_COLUMNS r_cc ON cons.R_CONSTRAINT_NAME = r_cc.CONSTRAINT_NAME
                    WHERE cons.CONSTRAINT_TYPE = 'R'
                ) fk ON c.TABLE_NAME = fk.TABLE_NAME AND c.COLUMN_NAME = fk.COLUMN_NAME
                WHERE c.TABLE_NAME = :TableName
                ORDER BY c.COLUMN_ID";

            var columns = await connection.QueryAsync(columnSql, new { TableName = tableName.ToUpper() });

            foreach (var col in columns)
            {
                schema.Columns.Add(new ColumnInfo
                {
                    ColumnName = col.COLUMN_NAME,
                    DataType = col.DATA_TYPE,
                    IsNullable = col.NULLABLE == "Y",
                    IsPrimaryKey = col.IS_PRIMARY_KEY == "Y",
                    IsForeignKey = !string.IsNullOrEmpty(col.REFERENCED_TABLE_NAME),
                    DefaultValue = col.DATA_DEFAULT,
                    MaxLength = col.DATA_LENGTH,
                    Precision = col.DATA_PRECISION,
                    Scale = col.DATA_SCALE,
                    ReferencedTable = col.REFERENCED_TABLE_NAME,
                    ReferencedColumn = col.REFERENCED_COLUMN_NAME
                });
            }

            // Get row count
            var countSql = $"SELECT COUNT(*) FROM {config.Owner}.{tableName}";
            schema.RowCount = await connection.QuerySingleAsync<long>(countSql);

            return schema;
        }

        public async Task<IEnumerable<TableRelation>?> GetTableRelationsAsync(IDbConnection connection)
        {
            var sql = @"
                SELECT
                    r_cc.TABLE_NAME AS ParentTable,
                    r_cc.COLUMN_NAME AS ParentColumn,
                    cc.TABLE_NAME AS ChildTable,
                    cc.COLUMN_NAME AS ChildColumn,
                    cons.CONSTRAINT_NAME AS ConstraintName
                FROM ALL_CONSTRAINTS cons
                INNER JOIN ALL_CONS_COLUMNS cc ON cons.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
                INNER JOIN ALL_CONS_COLUMNS r_cc ON cons.R_CONSTRAINT_NAME = r_cc.CONSTRAINT_NAME
                WHERE cons.CONSTRAINT_TYPE = 'R'";

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
                DatabaseType = "Oracle"
            };

            // Get database name
            var dbNameSql = "SELECT SYS_CONTEXT('USERENV', 'DB_NAME') FROM DUAL";
            dbInfo.DatabaseName = await connection.QuerySingleAsync<string>(dbNameSql);

            // Get version
            var versionSql = "SELECT BANNER FROM V$VERSION WHERE ROWNUM = 1";
            dbInfo.Version = await connection.QuerySingleAsync<string>(versionSql);

            // Get table count
            var tableCountSql = "SELECT COUNT(*) FROM ALL_TABLES";
            dbInfo.TableCount = await connection.QuerySingleAsync<int>(tableCountSql);

            // Get schemas
            var schemaSql = "SELECT DISTINCT OWNER FROM ALL_TABLES ORDER BY OWNER";
            var schemas = await connection.QueryAsync<string>(schemaSql);
            dbInfo.Schemas = schemas.ToList();

            return dbInfo;
        }
    }
}
