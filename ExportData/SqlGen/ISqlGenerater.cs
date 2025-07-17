
using ExportData.Models.Database;
using System.Data;

namespace ExportData.SqlGen
{
    public interface ISqlGenerater
    {
        IDbConnection GetConnection();
        void CloseConnection(IDbConnection conn);
        string GetSqlAllTableNameList();
        string GetSqlRecords(string tableName);
        Task<TableSchema?> GetTableSchemaAsync(IDbConnection connection, string tableName);
        Task<IEnumerable<TableRelation>?> GetTableRelationsAsync(IDbConnection connection);
        Task<DatabaseInfo?> GetDatabaseInfoAsync(IDbConnection connection);
    }
}
