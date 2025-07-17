using ExportData.Interfaces;
using ExportData.Models.Config;
using ExportData.Models.EnumType;
using Microsoft.Extensions.Logging;

namespace ExportData.Services
{
    public class InteractiveCliService
    {
        private readonly ILogger<InteractiveCliService> _logger;

        public InteractiveCliService(ILogger<InteractiveCliService> logger)
        {
            _logger = logger;
        }

        public async Task<(ConfigDbControlSection dbConfig, ConfigExControlSection exConfig)> RunInteractiveSetupAsync()
        {
            Console.WriteLine("=== 資料庫匯出工具 - 互動式設定 ===\n");

            var dbConfig = await ConfigureDatabaseAsync();
            var exConfig = await ConfigureExportAsync();

            return (dbConfig, exConfig);
        }

        private async Task<ConfigDbControlSection> ConfigureDatabaseAsync()
        {
            Console.WriteLine("📊 資料庫設定");
            Console.WriteLine("================");

            var config = new ConfigDbControlSection();

            // Database Type
            config.DbType = PromptForDatabaseType();

            // Connection String
            Console.Write("請輸入資料庫連線字串: ");
            config.ConnectionString = Console.ReadLine();

            // Table Type
            config.TableType = PromptForTableType();

            // Size
            Console.Write("請輸入每個資料表要匯出的記錄數量 (預設: 100): ");
            var sizeInput = Console.ReadLine();
            config.Size = int.TryParse(sizeInput, out int size) ? size : 100;

            // Prefix
            Console.Write("請輸入資料表名稱前綴過濾 (可選，按 Enter 跳過): ");
            config.Prefix = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(config.Prefix))
                config.Prefix = null;

            // Specific Tables
            if (PromptYesNo("是否要指定特定的資料表？(y/n): "))
            {
                config.TableList = PromptForTableList();
            }
            else
            {
                config.TableList = new string[0];
            }

            return config;
        }

        private async Task<ConfigExControlSection> ConfigureExportAsync()
        {
            Console.WriteLine("\n📁 匯出設定");
            Console.WriteLine("================");

            var config = new ConfigExControlSection();

            // Export Path
            Console.Write("請輸入匯出路徑 (預設: c:\\temp): ");
            var exportPath = Console.ReadLine();
            config.ExportPath = string.IsNullOrWhiteSpace(exportPath) ? "c:\\temp" : exportPath;

            // Create directory if not exists
            if (!Directory.Exists(config.ExportPath))
            {
                Directory.CreateDirectory(config.ExportPath);
                Console.WriteLine($"✅ 已建立目錄: {config.ExportPath}");
            }

            // Zip option
            config.MakeToZip = PromptYesNo("是否要將匯出的檔案壓縮成 ZIP？(y/n): ");

            if (config.MakeToZip)
            {
                Console.Write("請輸入 ZIP 檔案名稱 (不含副檔名，預設: ExportData): ");
                var zipName = Console.ReadLine();
                config.ZipFileName = string.IsNullOrWhiteSpace(zipName) ? "ExportData" : zipName;
            }

            return config;
        }

        private EnumDbType PromptForDatabaseType()
        {
            Console.WriteLine("請選擇資料庫類型:");
            Console.WriteLine("1. SQLite");
            Console.WriteLine("2. SQL Server");
            Console.WriteLine("3. MySQL");
            Console.WriteLine("4. Oracle");

            while (true)
            {
                Console.Write("請輸入選項 (1-4): ");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        return EnumDbType.Sqlite;
                    case "2":
                        return EnumDbType.SqlServer;
                    case "3":
                        return EnumDbType.MySql;
                    case "4":
                        return EnumDbType.Oracle;
                    default:
                        Console.WriteLine("❌ 無效的選項，請重新輸入。");
                        break;
                }
            }
        }

        private EnumTableType PromptForTableType()
        {
            Console.WriteLine("請選擇要匯出的物件類型:");
            Console.WriteLine("1. 資料表 (Table)");
            Console.WriteLine("2. 檢視表 (View)");

            while (true)
            {
                Console.Write("請輸入選項 (1-2): ");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        return EnumTableType.Table;
                    case "2":
                        return EnumTableType.View;
                    default:
                        Console.WriteLine("❌ 無效的選項，請重新輸入。");
                        break;
                }
            }
        }

        private bool PromptYesNo(string message)
        {
            while (true)
            {
                Console.Write(message);
                var input = Console.ReadLine()?.ToLower();

                switch (input)
                {
                    case "y":
                    case "yes":
                    case "是":
                        return true;
                    case "n":
                    case "no":
                    case "否":
                        return false;
                    default:
                        Console.WriteLine("❌ 請輸入 y/n 或 是/否");
                        break;
                }
            }
        }

        private string[] PromptForTableList()
        {
            Console.WriteLine("請輸入要匯出的資料表名稱 (用逗號分隔，例如: Table1,Table2,Table3):");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return new string[0];

            return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => t.Trim())
                       .Where(t => !string.IsNullOrEmpty(t))
                       .ToArray();
        }

        public void DisplayConfiguration(ConfigDbControlSection dbConfig, ConfigExControlSection exConfig)
        {
            Console.WriteLine("\n📋 設定摘要");
            Console.WriteLine("================");
            Console.WriteLine($"資料庫類型: {dbConfig.DbType}");
            Console.WriteLine($"連線字串: {MaskConnectionString(dbConfig.ConnectionString)}");
            Console.WriteLine($"物件類型: {dbConfig.TableType}");
            Console.WriteLine($"匯出數量: {dbConfig.Size} 筆/表");
            Console.WriteLine($"匯出路徑: {exConfig.ExportPath}");
            Console.WriteLine($"壓縮檔案: {(exConfig.MakeToZip ? "是" : "否")}");
            
            if (dbConfig.TableList?.Length > 0)
            {
                Console.WriteLine($"指定資料表: {string.Join(", ", dbConfig.TableList)}");
            }
            
            if (!string.IsNullOrEmpty(dbConfig.Prefix))
            {
                Console.WriteLine($"名稱前綴: {dbConfig.Prefix}");
            }
        }

        private string MaskConnectionString(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "未設定";

            // Simple masking for password
            var masked = connectionString;
            var patterns = new[] { "Password=", "Pwd=", "password=", "pwd=" };
            
            foreach (var pattern in patterns)
            {
                var index = masked.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var start = index + pattern.Length;
                    var end = masked.IndexOf(';', start);
                    if (end == -1) end = masked.Length;
                    
                    var passwordLength = end - start;
                    if (passwordLength > 0)
                    {
                        masked = masked.Substring(0, start) + new string('*', Math.Min(passwordLength, 8)) + masked.Substring(end);
                    }
                }
            }
            
            return masked;
        }
    }
}
