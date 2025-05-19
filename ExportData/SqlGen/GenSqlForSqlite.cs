
using ExportData.Models.Config;
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

        public string GetSqlAllTableNameList()
        {
           return config.TableType == EnumTableType.Table  ? "SELECT name FROM sqlite_master WHERE type='table';" : "SELECT name FROM sqlite_master WHERE type='view';";
        }

    }
}
