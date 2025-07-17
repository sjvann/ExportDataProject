using ExportData.Models.Config;
using ExportData.Models.EnumType;
using Microsoft.Extensions.Logging;

namespace ExportData
{
    public class TestRunner
    {
        public static async Task RunTestsAsync()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<TestRunner>();
            
            Console.WriteLine("=== 資料庫匯出工具測試 ===\n");

            // Test SQLite
            await TestSQLiteAsync(logger);
            
            Console.WriteLine("\n測試完成！");
        }

        private static async Task TestSQLiteAsync(ILogger logger)
        {
            Console.WriteLine("📊 測試 SQLite 功能");
            Console.WriteLine("==================");

            try
            {
                // Create a test SQLite database
                var testDbPath = Path.Combine(Path.GetTempPath(), "test_export.db");
                await CreateTestSQLiteDatabase(testDbPath);

                var dbConfig = new ConfigDbControlSection
                {
                    ConnectionString = $"Data Source={testDbPath};",
                    DbType = EnumDbType.Sqlite,
                    TableType = EnumTableType.Table,
                    Size = 5
                };

                var dbService = new DbService(dbConfig, logger.CreateLogger<DbService>());

                // Test database info
                Console.WriteLine("🔍 測試資料庫資訊...");
                var dbInfo = await dbService.GetDatabaseInfoAsync();
                if (dbInfo != null)
                {
                    Console.WriteLine($"✅ 資料庫類型: {dbInfo.DatabaseType}");
                    Console.WriteLine($"✅ 資料表數量: {dbInfo.TableCount}");
                }

                // Test table names
                Console.WriteLine("\n🔍 測試取得資料表清單...");
                var tables = await dbService.GetTableNamesAsync();
                if (tables != null)
                {
                    Console.WriteLine($"✅ 找到 {tables.Length} 個資料表: {string.Join(", ", tables)}");

                    // Test table schema for first table
                    if (tables.Length > 0)
                    {
                        var firstTable = tables[0];
                        Console.WriteLine($"\n🔍 測試資料表結構: {firstTable}");
                        var schema = await dbService.GetTableSchemaAsync(firstTable);
                        if (schema != null)
                        {
                            Console.WriteLine($"✅ 欄位數量: {schema.Columns.Count}");
                            Console.WriteLine($"✅ 記錄數量: {schema.RowCount}");
                            foreach (var col in schema.Columns.Take(3))
                            {
                                Console.WriteLine($"   - {col.ColumnName} ({col.DataType}) {(col.IsPrimaryKey ? "[PK]" : "")}");
                            }
                        }

                        // Test sample data
                        Console.WriteLine($"\n🔍 測試範例資料: {firstTable}");
                        var sampleData = await dbService.GetDataSetAsync(firstTable);
                        if (sampleData != null)
                        {
                            Console.WriteLine($"✅ 取得 {sampleData.Count()} 筆範例資料");
                        }
                    }
                }

                // Test relations
                Console.WriteLine("\n🔍 測試資料表關聯性...");
                var relations = await dbService.GetTableRelationsAsync();
                if (relations != null)
                {
                    Console.WriteLine($"✅ 找到 {relations.Count()} 個關聯性");
                }

                // Clean up
                if (File.Exists(testDbPath))
                {
                    File.Delete(testDbPath);
                }

                Console.WriteLine("\n✅ SQLite 測試完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SQLite 測試失敗: {ex.Message}");
            }
        }

        private static async Task CreateTestSQLiteDatabase(string dbPath)
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath};");
            connection.Open();

            // Create test tables
            var createTables = @"
                CREATE TABLE Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Email TEXT UNIQUE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    OrderDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    Amount DECIMAL(10,2),
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                INSERT INTO Users (Name, Email) VALUES 
                    ('張三', 'zhang@example.com'),
                    ('李四', 'li@example.com'),
                    ('王五', 'wang@example.com');

                INSERT INTO Orders (UserId, Amount) VALUES 
                    (1, 100.50),
                    (1, 200.75),
                    (2, 150.25),
                    (3, 300.00);
            ";

            using var command = new System.Data.SQLite.SQLiteCommand(createTables, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
