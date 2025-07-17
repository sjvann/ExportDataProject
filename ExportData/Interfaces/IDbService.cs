using ExportData.Models.Database;

namespace ExportData.Interfaces
{
    public interface IDbService
    {
        Task<IEnumerable<Dictionary<string, object>>?> GetDataSetAsync(string tableName);
        Task<string[]?> GetTableNamesAsync();
        Task<TableSchema?> GetTableSchemaAsync(string tableName);
        Task<IEnumerable<TableRelation>?> GetTableRelationsAsync();
        Task<DatabaseInfo?> GetDatabaseInfoAsync();
    }
}
