using ExportData.Models.Config;
using ExportData.Models.EnumType;
using Mysqlx.Crud;
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
    }
}
