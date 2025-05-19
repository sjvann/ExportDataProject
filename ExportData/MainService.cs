using ExportData.Models.Config;
using ExportData.Models.EnumType;
using Microsoft.Extensions.Configuration;

namespace ExportData
{
    public class MainService
    {
        ConfigDbControlSection? _dbControlSection;               //資料庫控制參數
        ConfigExControlSection? _exControlSection;               //資料匯出控制參數
        ConfigDeIdentification? _deIdentification;              //資料去識別控制參數
        public MainService(IConfiguration config)
        {
            Console.WriteLine("ExportData Service Start ...");
            LoadParameters(config);
        }
        public Task RunAsync()
        {
            if(_dbControlSection is null || _exControlSection is null || _deIdentification is null) return Task.CompletedTask;

            DbService dbService = new(_dbControlSection);               //資料庫存取服務
            ExportService exportService = new(_exControlSection,_deIdentification);       //資料匯出服務
            string[]? tableNames = (_dbControlSection.TableList != null && _dbControlSection.TableList.Length > 0) ? _dbControlSection.TableList : dbService.GetTableNames();
            if (tableNames != null && tableNames.Length > 0)
            {
                Console.WriteLine($"Exporting data for those tables: {Environment.NewLine} {string.Join(", " , tableNames)}");
                foreach (string tableName in tableNames)
                {
                    IEnumerable<Dictionary<string, object>>? _dataSet = dbService.GetDataSet(tableName);
                    if (_dataSet == null)
                    {
                        Console.WriteLine("No data found.");
                    }
                    else
                    {
                        exportService.Export(tableName, _dataSet);
                    }
                }
            }
            else
            {
                Console.WriteLine("No table found.");

            }
            exportService.ZipFiles();
            Console.WriteLine("ExportData Service End ...");
            return Task.CompletedTask;
        }
        private void LoadParameters(IConfiguration config)
        {
            Console.WriteLine("Loading Parameters ...");
            /* Load parameters from appsettings.json */
            if (config.GetSection("DbControl") is IConfigurationSection dbControl)
            {
                _dbControlSection = new ConfigDbControlSection()
                {
                    ConnectionString = dbControl.GetValue<string>("ConnectionString"),
                    DbType = Enum.Parse<EnumDbType>(dbControl.GetValue<string>("DbType") ?? "Sqlite"),
                    TableType = Enum.Parse<EnumTableType>(dbControl.GetValue<string>("TableType") ?? "1"),
                    TableList = dbControl.GetSection("TableList").Get<string[]>() ?? new string[0],
                    SqlAllTable = dbControl.GetValue<string>("SqlAllTable"),
                    SqlOneTable = dbControl.GetValue<string>("SqlOneTable"),
                    Owner = dbControl.GetValue<string>("Owner"),
                    DbName = dbControl.GetValue<string>("DbName"),
                    Prefix = dbControl.GetValue<string>("Prefix"),
                    Size = dbControl.GetValue<int>("Size")
                };
            }
            if (config.GetSection("ExControl") is IConfigurationSection exControl)
            {
                _exControlSection = new ConfigExControlSection()
                {
                    MakeToZip = exControl.GetValue<bool>("MakeToZip"),
                    ExportPath = exControl.GetValue<string>("ExportPath")
                };
            }
            if(config.GetSection("DeIdentification") is IConfigurationSection deIdentification)
            {
               _deIdentification = new()
                {
                    DeIdentification = deIdentification.GetValue<bool>("DeIdentification"),
                    Pii = deIdentification.GetSection("Pii").Get<string[]>() ?? new string[0],
                    Phi = deIdentification.GetValue<bool>("Phi")
                };
            }
        }
    }
}
