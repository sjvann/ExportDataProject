using ExportData;
using ExportData.Models.Database;
using ExportDataWeb.Services;
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

        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public bool HasConnection { get; private set; }
        public string ActivePanel { get; private set; } = "schema";
        public TableSchema? TableSchema { get; set; }
        public IEnumerable<Dictionary<string, object>>? SampleData { get; set; }
        public IEnumerable<TableRelation>? Relations { get; set; }

        public async Task OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(TableName))
            {
                return;
            }

            var dbService = OpenSavedConnection();
            if (dbService == null)
            {
                return;
            }

            await LoadSchemaAsync(dbService, announce: false);
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            if (string.IsNullOrWhiteSpace(TableName))
            {
                Message = "請指定資料表名稱";
                IsSuccess = false;
                return Page();
            }

            if (action is "schema" or "sample" or "relations")
            {
                ActivePanel = action;
            }

            var dbService = OpenSavedConnection();
            if (dbService == null)
            {
                return Page();
            }

            try
            {
                switch (action)
                {
                    case "schema":
                        await LoadSchemaAsync(dbService, announce: true);
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

        private DbService? OpenSavedConnection()
        {
            var saved = WorkspaceConnection.Read(HttpContext.Session);
            if (saved == null)
            {
                HasConnection = false;
                IsSuccess = false;
                return null;
            }

            HasConnection = true;
            var config = saved.ToDbConfig();
            config.Size = 10;
            return new DbService(config, _loggerFactory.CreateLogger<DbService>());
        }

        private async Task LoadSchemaAsync(DbService dbService, bool announce)
        {
            try
            {
                TableSchema = await dbService.GetTableSchemaAsync(TableName);

                if (TableSchema != null)
                {
                    if (announce)
                    {
                        Message = $"已載入資料表 {TableName} 的結構";
                    }

                    IsSuccess = true;
                }
                else
                {
                    Message = $"無法取得資料表 {TableName} 的結構";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Message = $"載入結構失敗: {ex.Message}";
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
                    Message = $"已載入資料表 {TableName} 的範例資料，共 {SampleData.Count()} 筆";
                    IsSuccess = true;
                }
                else
                {
                    Message = $"資料表 {TableName} 沒有資料或無法存取";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Message = $"載入範例資料失敗: {ex.Message}";
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
                        Message = $"找到 {Relations.Count()} 個與資料表 {TableName} 相關的關聯";
                        IsSuccess = true;
                    }
                    else
                    {
                        Message = $"資料表 {TableName} 沒有找到相關的關聯";
                        IsSuccess = false;
                    }
                }
                else
                {
                    Message = "無法取得資料表關聯";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Message = $"載入關聯失敗: {ex.Message}";
                IsSuccess = false;
            }
        }
    }
}
