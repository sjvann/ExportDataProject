using ExportData.Interfaces;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExportDataWeb.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDbService _dbService;
    private readonly IExportService _exportService;

    public IndexModel(ILogger<IndexModel> logger, IDbService dbService, IExportService exportService)
    {
        _logger = logger;
        _dbService = dbService;
        _exportService = exportService;
    }

    [BindProperty]
    public ConfigDbControlSection DbConfig { get; set; } = new();

    [BindProperty]
    public ConfigExControlSection ExConfig { get; set; } = new();

    public string? Message { get; set; }
    public bool IsSuccess { get; set; }
    public DatabaseInfo? DatabaseInfo { get; set; }
    public string[]? Tables { get; set; }

    public void OnGet()
    {
        // Initialize default values
        DbConfig.Size = 100;
        DbConfig.TableType = EnumTableType.Table;
        ExConfig.ExportPath = @"c:\temp";
        ExConfig.MakeToZip = true;
        ExConfig.ZipFileName = "ExportData";
    }

    public async Task<IActionResult> OnPostAsync(string action)
    {
        if (!ModelState.IsValid)
        {
            Message = "請檢查輸入的資料是否正確";
            IsSuccess = false;
            return Page();
        }

        try
        {
            switch (action)
            {
                case "test":
                    await TestConnectionAsync();
                    break;
                case "analyze":
                    await AnalyzeDatabaseAsync();
                    break;
                case "export":
                    await ExportDataAsync();
                    break;
                default:
                    Message = "未知的操作";
                    IsSuccess = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {Action} operation", action);
            Message = $"操作失敗: {ex.Message}";
            IsSuccess = false;
        }

        return Page();
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            var dbService = new ExportData.DbService(DbConfig, _logger.CreateLogger<ExportData.DbService>());
            var dbInfo = await dbService.GetDatabaseInfoAsync();

            if (dbInfo != null)
            {
                DatabaseInfo = dbInfo;
                Message = "✅ 資料庫連線測試成功！";
                IsSuccess = true;
            }
            else
            {
                Message = "❌ 無法取得資料庫資訊";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            Message = $"❌ 連線測試失敗: {ex.Message}";
            IsSuccess = false;
        }
    }

    private async Task AnalyzeDatabaseAsync()
    {
        try
        {
            var dbService = new ExportData.DbService(DbConfig, _logger.CreateLogger<ExportData.DbService>());

            // Get database info
            DatabaseInfo = await dbService.GetDatabaseInfoAsync();

            // Get table list
            Tables = await dbService.GetTableNamesAsync();

            if (Tables != null && Tables.Length > 0)
            {
                Message = $"✅ 分析完成！找到 {Tables.Length} 個{(DbConfig.TableType == EnumTableType.Table ? "資料表" : "檢視表")}";
                IsSuccess = true;
            }
            else
            {
                Message = "⚠️ 沒有找到任何資料表";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            Message = $"❌ 分析失敗: {ex.Message}";
            IsSuccess = false;
        }
    }

    private async Task ExportDataAsync()
    {
        try
        {
            var dbService = new ExportData.DbService(DbConfig, _logger.CreateLogger<ExportData.DbService>());
            var exportService = new ExportData.ExportService(ExConfig,
                new ConfigDeIdentification { DeIdentification = false },
                _logger.CreateLogger<ExportData.ExportService>());

            // Create export directory if not exists
            if (!Directory.Exists(ExConfig.ExportPath))
            {
                Directory.CreateDirectory(ExConfig.ExportPath);
            }

            // Get tables to export
            string[]? tablesToExport = (DbConfig.TableList != null && DbConfig.TableList.Length > 0)
                ? DbConfig.TableList
                : await dbService.GetTableNamesAsync();

            if (tablesToExport == null || tablesToExport.Length == 0)
            {
                Message = "❌ 沒有找到要匯出的資料表";
                IsSuccess = false;
                return;
            }

            int exportedCount = 0;
            foreach (string tableName in tablesToExport)
            {
                var dataSet = await dbService.GetDataSetAsync(tableName);
                if (dataSet != null)
                {
                    await exportService.ExportAsync(tableName, dataSet);
                    exportedCount++;
                }
            }

            if (ExConfig.MakeToZip)
            {
                await exportService.ZipFilesAsync();
            }

            Message = $"✅ 匯出完成！成功匯出 {exportedCount} 個資料表到 {ExConfig.ExportPath}";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            Message = $"❌ 匯出失敗: {ex.Message}";
            IsSuccess = false;
        }
    }
}
