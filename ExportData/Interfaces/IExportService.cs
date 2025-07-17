namespace ExportData.Interfaces
{
    public interface IExportService
    {
        Task ExportAsync(string tableName, IEnumerable<Dictionary<string, object>>? dataSet);
        Task ZipFilesAsync();
        Task<string> ExportTableSchemaAsync(string tableName, object schema);
    }
}
