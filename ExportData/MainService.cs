using ExportData.Interfaces;
using ExportData.Models.Config;
using ExportData.Models.EnumType;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExportData
{
    public class MainService
    {
        private readonly ConfigDbControlSection? _dbControlSection;
        private readonly ConfigExControlSection? _exControlSection;
        private readonly ConfigDeIdentification? _deIdentification;
        private readonly ILogger<MainService> _logger;

        // Constructor for configuration-based mode
        public MainService(IConfiguration config, ILogger<MainService> logger)
        {
            _logger = logger;
            _logger.LogInformation("ExportData Service Start ...");
            LoadParameters(config);
        }

        // Constructor for interactive mode
        public MainService(ConfigDbControlSection dbConfig, ConfigExControlSection exConfig, ILogger<MainService> logger)
        {
            _dbControlSection = dbConfig;
            _exControlSection = exConfig;
            _deIdentification = new ConfigDeIdentification { DeIdentification = false }; // Default
            _logger = logger;
            _logger.LogInformation("ExportData Service Start (Interactive Mode) ...");
        }
        public async Task RunAsync()
        {
            if (_dbControlSection is null || _exControlSection is null || _deIdentification is null)
            {
                _logger.LogError("Configuration is incomplete");
                return;
            }

            try
            {
                var dbService = new DbService(_dbControlSection, _logger.CreateLogger<DbService>());
                var exportService = new ExportService(_exControlSection, _deIdentification, _logger.CreateLogger<ExportService>());

                string[]? tableNames = (_dbControlSection.TableList != null && _dbControlSection.TableList.Length > 0)
                    ? _dbControlSection.TableList
                    : await dbService.GetTableNamesAsync();

                if (tableNames != null && tableNames.Length > 0)
                {
                    _logger.LogInformation("Exporting data for {Count} tables: {Tables}",
                        tableNames.Length, string.Join(", ", tableNames));

                    foreach (string tableName in tableNames)
                    {
                        _logger.LogInformation("Processing table: {TableName}", tableName);

                        var dataSet = await dbService.GetDataSetAsync(tableName);
                        if (dataSet == null)
                        {
                            _logger.LogWarning("No data found for table: {TableName}", tableName);
                        }
                        else
                        {
                            await exportService.ExportAsync(tableName, dataSet);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No tables found");
                }

                if (_exControlSection.MakeToZip)
                {
                    await exportService.ZipFilesAsync();
                }

                _logger.LogInformation("ExportData Service End ...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during export process");
                throw;
            }
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
