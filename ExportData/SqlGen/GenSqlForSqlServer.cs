using ExportData.Models.Config;
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
                 "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES" :
                 "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS";
            return config.Prefix == null ? firstPart : $"{firstPart} WHERE TABLE_NAME LIKE '{config.Prefix}%'";
        }
    }
}
