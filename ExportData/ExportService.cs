
using ExportData.Interfaces;
using ExportData.Models.Config;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;

namespace ExportData
{
    public class ExportService : IExportService
    {
        private readonly ConfigExControlSection _exControlSection;
        private readonly ConfigDeIdentification _deIdentification;
        private readonly ILogger<ExportService> _logger;

        public ExportService(ConfigExControlSection exControlSection, ConfigDeIdentification deIdentification, ILogger<ExportService> logger)
        {
            _exControlSection = exControlSection;
            _deIdentification = deIdentification;
            _logger = logger;
        }

        public async Task ExportAsync(string tableName, IEnumerable<Dictionary<string, object>>? dataSet)
        {
            if (dataSet == null)
            {
                _logger.LogWarning("DataSet is null for table: {TableName}", tableName);
                return;
            }

            try
            {
                if (_deIdentification.DeIdentification)
                {
                    // TODO: Implement de-identification logic
                    _logger.LogInformation("De-identification is enabled for table: {TableName}", tableName);
                }

                string fileName = $"{tableName}_{DateTime.Now:yyyyMMdd}.csv";
                string fullPath = Path.Combine(_exControlSection?.ExportPath ?? @"c:\", fileName);
                string allCsvLine = CreateCSVString(dataSet);

                await File.WriteAllTextAsync(fullPath, allCsvLine, Encoding.UTF8);
                _logger.LogInformation("Exported {Count} records to: {FilePath}", dataSet.Count(), fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting table: {TableName}", tableName);
                throw;
            }
        }

        public async Task ZipFilesAsync()
        {
            try
            {
                string exportPath = _exControlSection.ExportPath ?? @"c:\";
                string zipFileName = _exControlSection.ZipFileName ?? "ExportZip";
                string zipFullFileName = Path.Combine(exportPath, $"{zipFileName}_{DateTime.Now:yyyyMMdd}.zip");
                string[] files = Directory.GetFiles(exportPath, "*.csv");

                if (files.Length == 0)
                {
                    _logger.LogWarning("No CSV files found to zip in: {ExportPath}", exportPath);
                    return;
                }

                if (File.Exists(zipFullFileName))
                {
                    File.Delete(zipFullFileName);
                }

                using (var zip = ZipFile.Open(zipFullFileName, ZipArchiveMode.Create))
                {
                    foreach (string file in files)
                    {
                        zip.CreateEntryFromFile(file, Path.GetFileName(file));
                        File.Delete(file);
                    }
                }

                _logger.LogInformation("Created ZIP file: {ZipFile} with {FileCount} files", zipFullFileName, files.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ZIP file");
                throw;
            }
        }

        public async Task<string> ExportTableSchemaAsync(string tableName, object schema)
        {
            try
            {
                string fileName = $"{tableName}_schema_{DateTime.Now:yyyyMMdd}.json";
                string fullPath = Path.Combine(_exControlSection?.ExportPath ?? @"c:\", fileName);

                var json = System.Text.Json.JsonSerializer.Serialize(schema, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(fullPath, json, Encoding.UTF8);
                _logger.LogInformation("Exported schema to: {FilePath}", fullPath);

                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting schema for table: {TableName}", tableName);
                throw;
            }
        }
        private string CreateCSVString(IEnumerable<Dictionary<string, object>>? dataSet)
        {
            List<string> csvSource = new ();
            bool needHead = true;
            if (dataSet != null && dataSet.Any())
            {
                foreach (var item in dataSet)
                {
                    if (needHead)
                    {
                        csvSource.Add(string.Join(",", item.Keys));
                        needHead = false;
                    }
                    csvSource.Add(string.Join(",", item.Values));
                }
            }
            return string.Join("\r", csvSource);
        }

    }
}