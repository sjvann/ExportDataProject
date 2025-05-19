
using Dapper;
using ExportData.Models.Config;
using ExportData.SqlGen;
using MySqlX.XDevAPI.Common;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;

namespace ExportData
{
    public class DbService
    {
        private readonly ISqlGenerater? sqlProvider;        // 依據資料庫來取得SQL語句
        private readonly string? sqlGetAllTableNameList;    // 從參數檔取得SQL語句
        private readonly string? sqlGetRecords;             // 從參數檔取得SQL語句

        public DbService(ConfigDbControlSection dbControlSection)
        {
            sqlProvider = DbChoicer.ChoiceProverder(dbControlSection);
            sqlGetAllTableNameList = dbControlSection.SqlAllTable;
            sqlGetRecords = dbControlSection.SqlOneTable;
        }

        public IEnumerable<Dictionary<string, object>>? GetDataSet(string tableName)
        {
            string? sql = string.IsNullOrEmpty(sqlGetRecords) ? sqlProvider?.GetSqlRecords(tableName) : sqlGetRecords;
            Console.WriteLine(sql ?? string.Empty);
            if (sqlProvider == null || string.IsNullOrEmpty(sql)) return null;
            IDbConnection connection = sqlProvider.GetConnection();
            IEnumerable<Dictionary<string, object>>? result = null;
            try
            {
                result = connection.Query<Dictionary<string, object>>(sql, commandTimeout: 300);
            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            sqlProvider.CloseConnection(connection);
            return result;
        }

        public string[]? GetTableNames()
        {
            string? sql = string.IsNullOrEmpty(sqlGetAllTableNameList) ? sqlProvider?.GetSqlAllTableNameList() : sqlGetAllTableNameList;
            Console.WriteLine(sql ?? string.Empty);
            if (sqlProvider == null || string.IsNullOrEmpty(sql)) return null;
            IDbConnection connection = sqlProvider.GetConnection();
            IEnumerable<string>? result = null;
            try
            {
                result = connection.Query<string>(sql);
            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }

            sqlProvider.CloseConnection(connection);
            return result?.ToArray();
        }
    }
}