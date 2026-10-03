using ExportData;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExportDataWeb.Pages
{
    public class TableDetailModel : PageModel
    {
        private readonly ILogger<TableDetailModel> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public TableDetailModel(ILogger<TableDetailModel> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        [BindProperty(SupportsGet = true)]
        public string TableName { get; set; } = string.Empty;

        [BindProperty]
        public string ConnectionString { get; set; } = string.Empty;

        [BindProperty]
        public string DbType { get; set; } = "SqlServer";

        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public TableSchema? TableSchema { get; set; }
        public IEnumerable<Dictionary<string, object>>? SampleData { get; set; }
        public IEnumerable<TableRelation>? Relations { get; set; }

        public void OnGet()
        {
            if (string.IsNullOrEmpty(TableName))
            {
                Message = "請指定資料表名稱";
                IsSuccess = false;
            }
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            if (string.IsNullOrEmpty(TableName) || string.IsNullOrEmpty(ConnectionString))
            {
                Message = "請提供完整的資料表名稱和連線資訊";
                IsSuccess = false;
                return Page();
            }

            try
            {
                var dbConfig = new ConfigDbControlSection
                {
                    ConnectionString = ConnectionString,
                    DbType = Enum.Parse<EnumDbType>(DbType),
                    Size = 10 // For sample data
                };

                var dbService = new DbService(dbConfig, _loggerFactory.CreateLogger<DbService>());

                switch (action)
                {
                    case "schema":
                        await LoadSchemaAsync(dbService);
                        break;
                    case "sample":
                        await LoadSampleDataAsync(dbService);
                        break;
                    case "relations":
                        await LoadRelationsAsync(dbService);
                        break;
                    default:
                        Message = "未知的操作";
                        IsSuccess = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {Action} operation for table {TableName}", action, TableName);
                Message = $"操作失敗: {ex.Message}";
                IsSuccess = false;
            }

            return Page();
        }

        private async Task LoadSchemaAsync(DbService dbService)
        {
            try
            {
                TableSchema = await dbService.GetTableSchemaAsync(TableName);
                
                if (TableSchema != null)
                {
                    Message = $"✅ 成功載入資料表 {TableName} 的結構資訊";
                    IsSuccess = true;
                }
                else
                {
                    Message = $"❌ 無法取得資料表 {TableName} 的結構資訊";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Message = $"❌ 載入結構資訊失敗: {ex.Message}";
                IsSuccess = false;
            }
        }

        private async Task LoadSampleDataAsync(DbService dbService)
        {
            try
            {
                SampleData = await dbService.GetDataSetAsync(TableName);
                
                if (SampleData != null && SampleData.Any())
                {
                    Message = $"✅ 成功載入資料表 {TableName} 的範例資料 ({SampleData.Count()} 筆)";
                    IsSuccess = true;
                }
                else
                {
                    Message = $"⚠️ 資料表 {TableName} 沒有資料或無法存取";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Message = $"❌ 載入範例資料失敗: {ex.Message}";
                IsSuccess = false;
            }
        }

        private async Task LoadRelationsAsync(DbService dbService)
        {
            try
            {
                var allRelations = await dbService.GetTableRelationsAsync();
                
                if (allRelations != null)
                {
                    // Filter relations related to current table
                    Relations = allRelations.Where(r => 
                        r.ParentTable.Equals(TableName, StringComparison.OrdinalIgnoreCase) ||
                        r.ChildTable.Equals(TableName, StringComparison.OrdinalIgnoreCase));
                    
                    if (Relations.Any())
                    {
                        Message = $"✅ 找到 {Relations.Count()} 個與資料表 {TableName} 相關的關聯性";
                        IsSuccess = true;
                    }
                    else
                    {
                        Message = $"⚠️ 資料表 {TableName} 沒有找到相關的關聯性";
                        IsSuccess = false;
                    }
                }
                else
                {
                    Message = "❌ 無法取得資料表關聯性資訊";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Message = $"❌ 載入關聯性資訊失敗: {ex.Message}";
                IsSuccess = false;
            }
        }
    }
}
