using System.Data;
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

        [BindProperty(SupportsGet = true)]
        public int Depth { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string? Panel { get; set; }

        public bool HasConnection { get; private set; }
        public string ActivePanel { get; private set; } = "schema";
        public TableSchema? TableSchema { get; private set; }
        public string? SchemaError { get; private set; }
        public List<Dictionary<string, object>>? SampleData { get; private set; }
        public IReadOnlyList<string> SampleColumnNames { get; private set; } = [];
        public string? SampleError { get; private set; }
        public ErGraph? Diagram { get; private set; }
        public ErCanvas? Canvas { get; private set; }
        public string? DiagramError { get; private set; }

        public async Task OnGetAsync()
        {
            if (Depth < 1)
            {
                Depth = 1;
            }

            ActivePanel = string.Equals(Panel, "relations", StringComparison.OrdinalIgnoreCase)
                ? "relations"
                : "schema";

            if (string.IsNullOrWhiteSpace(TableName))
            {
                return;
            }

            var dbService = OpenSavedConnection();
            if (dbService == null)
            {
                return;
            }

            await LoadSchemaAsync(dbService);
            await LoadSampleAsync(dbService);
            await LoadDiagramAsync(dbService);
        }

        public static object? ReadCell(IReadOnlyDictionary<string, object> row, string column)
        {
            if (row.TryGetValue(column, out var direct))
            {
                return direct is DBNull ? null : direct;
            }

            foreach (var pair in row)
            {
                if (string.Equals(pair.Key, column, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value is DBNull ? null : pair.Value;
                }
            }

            return null;
        }

        public static string LengthText(ColumnInfo column)
        {
            if (column.MaxLength.HasValue)
            {
                return column.MaxLength.Value.ToString();
            }

            if (column.Precision.HasValue && column.Scale.HasValue)
            {
                return $"{column.Precision},{column.Scale}";
            }

            if (column.Precision.HasValue)
            {
                return column.Precision.Value.ToString();
            }

            return string.Empty;
        }

        public static string ReferenceText(ColumnInfo column)
        {
            if (!column.IsForeignKey || string.IsNullOrEmpty(column.ReferencedTable))
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(column.ReferencedColumn)
                ? column.ReferencedTable
                : $"{column.ReferencedTable}.{column.ReferencedColumn}";
        }

        private DbService? OpenSavedConnection()
        {
            var saved = WorkspaceConnection.Read(HttpContext.Session);
            if (saved == null)
            {
                HasConnection = false;
                return null;
            }

            HasConnection = true;
            return new DbService(saved.ToDbConfig(), _loggerFactory.CreateLogger<DbService>());
        }

        private async Task LoadSchemaAsync(DbService dbService)
        {
            try
            {
                TableSchema = await dbService.GetTableSchemaAsync(TableName);
                if (TableSchema == null)
                {
                    SchemaError = $"無法取得資料表 {TableName} 的結構";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入資料表 {TableName} 的結構失敗", TableName);
                SchemaError = ex.Message;
            }
        }

        private async Task LoadSampleAsync(DbService dbService)
        {
            try
            {
                SampleData = (await dbService.GetAllDataAsync(TableName)).ToList();
                if (TableSchema != null && TableSchema.Columns.Count > 0)
                {
                    SampleColumnNames = TableSchema.Columns.Select(column => column.ColumnName).ToList();
                }
                else if (SampleData.Count > 0)
                {
                    SampleColumnNames = SampleData[0].Keys.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入資料表 {TableName} 的資料失敗", TableName);
                SampleError = ex.Message;
            }
        }

        private async Task LoadDiagramAsync(DbService dbService)
        {
            try
            {
                var relations = await dbService.GetTableRelationsAsync();
                if (relations == null)
                {
                    DiagramError = "無法取得資料表關聯";
                    return;
                }

                var preview = ErNeighborhood.Build(TableName, Depth, relations, null);
                var schemas = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in preview.Nodes)
                {
                    if (string.Equals(node.TableName, TableName, StringComparison.OrdinalIgnoreCase))
                    {
                        schemas[node.TableName] = TableSchema;
                        continue;
                    }

                    try
                    {
                        schemas[node.TableName] = await dbService.GetTableSchemaAsync(node.TableName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "載入關聯表 {TableName} 的結構失敗", node.TableName);
                        schemas[node.TableName] = null;
                    }
                }

                Diagram = ErNeighborhood.Build(TableName, Depth, relations, schemas);
                Canvas = ErDiagramLayout.Arrange(Diagram);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入資料表 {TableName} 的關聯失敗", TableName);
                DiagramError = ex.Message;
            }
        }
    }
}
