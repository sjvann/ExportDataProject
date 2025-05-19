

using System.Data;

namespace ExportData.SqlGen
{
    public interface ISqlGenerater
    {
        public IDbConnection GetConnection();
        public void CloseConnection(IDbConnection conn);
        public string GetSqlAllTableNameList();
        public string GetSqlRecords(string tableName);
    }
}
