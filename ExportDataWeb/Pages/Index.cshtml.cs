using System.Text.Json;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.Models.EnumType;
using ExportData.Services;
using ExportData.SqlGen;
using ExportDataWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExportDataWeb.Pages;

public class IndexModel : PageModel
{
    private static readonly JsonSerializerOptions DriverJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<IndexModel> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public IndexModel(ILogger<IndexModel> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    [BindProperty]
    public ConfigDbControlSection DbConfig { get; set; } = new();

    [BindProperty]
    public DbConnectionForm Connection { get; set; } = new();

    [BindProperty]
    public ConfigExControlSection ExConfig { get; set; } = new();

    public string? Message { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorField { get; set; }
    public string? DriverDownloadUrl { get; set; }
    public string? MaskedConnectionString { get; set; }
    public DatabaseInfo? DatabaseInfo { get; set; }
    public string[]? Tables { get; set; }
    public IReadOnlyList<DatabaseDriverStatus> Drivers { get; private set; } = [];
    public string DriverCatalogJson { get; private set; } = "[]";

    [BindProperty]
    public string? PasswordState { get; set; }

    public bool PasswordRemembered { get; private set; }

    public bool IsConnected { get; private set; }

    public bool InventoryFailed { get; private set; }

    public bool AnalysisFoundNone => Tables is { Length: 0 };

    private bool _openedThisRequest;

    private const string PasswordSessionKey = "exportdata.connection.password";
    private const string IdentitySessionKey = "exportdata.connection.identity";

    public bool HasDbType => DbConfig.DbType.HasValue;
    public bool RawMode => HasDbType && Connection.UseRawConnectionString;
    public bool Structured => HasDbType && !Connection.UseRawConnectionString;
    public bool ShowServer => Structured && DbConfig.DbType != EnumDbType.Sqlite;
    public bool ShowSqlite => Structured && DbConfig.DbType == EnumDbType.Sqlite;
    public bool ShowAccount => ShowServer && !(DbConfig.DbType == EnumDbType.SqlServer && Connection.IntegratedSecurity);
    public bool ShowSqlServer => Structured && DbConfig.DbType == EnumDbType.SqlServer;
    public bool ShowOracle => Structured && DbConfig.DbType == EnumDbType.Oracle;
    public bool ShowDatabaseList => Structured && DbConfig.DbType is EnumDbType.PostgreSql or EnumDbType.MySql or EnumDbType.SqlServer;
    public bool ShowPreview => Structured;

    public DatabaseDriverStatus? SelectedDriver =>
        DbConfig.DbType is { } dbType
            ? Drivers.FirstOrDefault(driver => driver.DbType == dbType.ToString())
            : null;

    public string HostLabel => DbConfig.DbType is EnumDbType.SqlServer or EnumDbType.MySql ? "伺服器" : "主機";

    public string DatabaseLabel => DbConfig.DbType == EnumDbType.Oracle ? "服務名稱" : "資料庫";

    public string HostPlaceholder => DbConfig.DbType == EnumDbType.SqlServer ? @"localhost 或 localhost\SQLEXPRESS" : "localhost";

    public string DatabasePlaceholder => DbConfig.DbType switch
    {
        EnumDbType.PostgreSql => "postgres",
        EnumDbType.Oracle => "ORCL",
        _ => "database"
    };

    public string UsernamePlaceholder => DbConfig.DbType switch
    {
        EnumDbType.PostgreSql => "postgres",
        EnumDbType.MySql => "root",
        EnumDbType.Oracle => "system",
        EnumDbType.SqlServer => "sa",
        _ => ""
    };

    public string PortPlaceholder => DbConfig.DbType switch
    {
        EnumDbType.PostgreSql => "5432",
        EnumDbType.SqlServer => "1433",
        EnumDbType.MySql => "3306",
        EnumDbType.Oracle => "1521",
        _ => ""
    };

    public string InvalidClass(string fieldId) => ErrorField == fieldId ? "is-invalid" : "";

    public string? InvalidAria(string fieldId) => ErrorField == fieldId ? "true" : null;

    public string? InvalidDescribedBy(string fieldId) => ErrorField == fieldId ? fieldId + "-error" : null;

    public bool IsFieldError(string fieldId) => ErrorField == fieldId;

    public async Task OnGetAsync()
    {
        LoadDrivers();
        var saved = WorkspaceConnection.Read(HttpContext.Session);
        if (saved == null)
        {
            ApplyFreshDefaults();
            return;
        }

        saved.CopyTo(DbConfig, Connection, ExConfig);
        IsConnected = true;
        PasswordRemembered = HasRememberedPassword();
        RememberMaskedConnection();
        await ResumeInventoryAsync(saved);
        if (!Connection.UseRawConnectionString)
        {
            DbConfig.ConnectionString = null;
        }
    }

    public IActionResult OnPostPreview()
    {
        AdoptRememberedPassword();
        if (HasError("Port"))
        {
            return new JsonResult(new { ok = false, message = "連接埠必須是 1 到 65535 的整數" });
        }

        if (!ModelState.IsValid)
        {
            return new JsonResult(new { ok = false, message = "請檢查輸入的資料是否正確" });
        }

        var applied = DbConnectionComposer.Apply(DbConfig, Connection);
        if (!applied.Succeeded)
        {
            return new JsonResult(new { ok = false, message = applied.Error });
        }

        var preview = Connection.UseRawConnectionString
            ? DbConnectionComposer.ScrubSecrets(DbConfig.ConnectionString, Connection.Password)
            : DbConnectionComposer.Mask(DbConfig.DbType!.Value, DbConfig.ConnectionString!);
        return new JsonResult(new { ok = true, preview });
    }

    public async Task<IActionResult> OnPostDatabases(CancellationToken cancellationToken)
    {
        AdoptRememberedPassword();
        if (HasError("Port"))
        {
            return new JsonResult(new { ok = false, message = "連接埠必須是 1 到 65535 的整數" });
        }

        if (DbConfig.DbType is not { } dbType)
        {
            return new JsonResult(new { ok = false, message = "請選擇資料庫類型" });
        }

        var listed = await DatabaseCatalog.ListAsync(dbType, Connection, cancellationToken);
        if (!listed.Succeeded)
        {
            return new JsonResult(new { ok = false, message = listed.Error });
        }

        return new JsonResult(new { ok = true, databases = listed.Names });
    }

    public async Task<IActionResult> OnPostAsync(string operation)
    {
        if (string.Equals(operation, "disconnect", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Clear();
            ClearWorkspace();
            ApplyFreshDefaults();
            LoadDrivers();
            Message = "已取消連線。";
            IsSuccess = true;
            return Page();
        }

        LoadDrivers();
        AdoptRememberedPassword();
        IsConnected = WorkspaceConnection.Read(HttpContext.Session) != null;
        if (HasError("Port"))
        {
            Message = "連接埠必須是 1 到 65535 的整數";
            ErrorField = "Connection_Port";
            IsSuccess = false;
            return await ViewAsync();
        }

        if (!ModelState.IsValid)
        {
            Message = "請檢查輸入的資料是否正確";
            IsSuccess = false;
            return await ViewAsync();
        }

        var applied = DbConnectionComposer.Apply(DbConfig, Connection);
        if (!applied.Succeeded)
        {
            Message = applied.Error;
            ErrorField = applied.FieldId;
            IsSuccess = false;
            return await ViewAsync();
        }

        RememberMaskedConnection();

        if (!await OpenConnectionAsync())
        {
            return await ViewAsync();
        }

        RememberWorkspaceKeepingAnalysis();

        try
        {
            switch (operation)
            {
                case "test":
                    Message = FormatConnected(DatabaseInfo);
                    IsSuccess = true;
                    break;
                case "analyze":
                    await AnalyzeDatabaseAsync();
                    if (Tables is { Length: > 0 })
                    {
                        RememberWorkspace(analyzed: true);
                    }
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
            _logger.LogError("Operation {Action} failed for {DbType}: {Error}", operation, DbConfig.DbType, Describe(ex));
            Message = "操作失敗。 " + Describe(ex);
            IsSuccess = false;
        }

        return await ViewAsync();
    }

    private async Task<bool> OpenConnectionAsync()
    {
        if (DbConfig.DbType is not { } dbType)
        {
            Message = "請選擇資料庫類型";
            ErrorField = "DbConfig_DbType";
            IsSuccess = false;
            return false;
        }

        var driver = DatabaseDriverCatalog.Probe(dbType);
        if (!driver.Installed)
        {
            Message = $"尚未載入 {driver.DisplayName} 驅動程式。請下載安裝後重新啟動工具。";
            DriverDownloadUrl = driver.DownloadUrl;
            IsSuccess = false;
            return false;
        }

        var provider = DbChoicer.ChoiceProverder(DbConfig);
        if (provider == null)
        {
            Message = "不支援的資料庫類型";
            ErrorField = "DbConfig_DbType";
            IsSuccess = false;
            return false;
        }

        try
        {
            using var connection = provider.GetConnection();
            try
            {
                DatabaseInfo = await provider.GetDatabaseInfoAsync(connection);
            }
            catch (Exception ex) when (!IsDriverLoadFailure(ex))
            {
                _logger.LogError("Connected to {DbType} but database info failed: {Error}", dbType, Describe(ex));
                DatabaseInfo = null;
            }

            return true;
        }
        catch (Exception ex) when (IsDriverLoadFailure(ex))
        {
            _logger.LogError("Driver load failed for {DbType}: {Error}", dbType, ex.GetType().Name);
            Message = $"無法載入 {driver.DisplayName} 驅動程式。請下載安裝後重新啟動工具。";
            DriverDownloadUrl = driver.DownloadUrl;
            IsSuccess = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Database connection failed for {DbType}: {Error}", dbType, Describe(ex));
            var detail = Describe(ex);
            Message = string.IsNullOrWhiteSpace(detail)
                ? "連線失敗。請確認主機、連接埠、帳號與密碼。"
                : $"連線失敗。請確認主機、連接埠、帳號與密碼。 {detail}";
            IsSuccess = false;
            return false;
        }
    }

    private async Task AnalyzeDatabaseAsync()
    {
        var dbService = new ExportData.DbService(DbConfig, _loggerFactory.CreateLogger<ExportData.DbService>());
        Tables = await dbService.GetTableNamesAsync();

        if (Tables == null)
        {
            Message = "已連線，但讀取資料表清單失敗。";
            IsSuccess = false;
            return;
        }

        if (Tables.Length == 0)
        {
            Message = "沒有找到任何資料表";
            IsSuccess = false;
            return;
        }

        var kind = DbConfig.TableType == EnumTableType.View ? "檢視表" : "資料表";
        Message = $"分析完成，找到 {Tables.Length} 個{kind}。";
        IsSuccess = true;
    }

    private async Task ExportDataAsync()
    {
        try
        {
            var dbService = new ExportData.DbService(DbConfig, _loggerFactory.CreateLogger<ExportData.DbService>());
            var exportService = new ExportData.ExportService(ExConfig,
                new ConfigDeIdentification { DeIdentification = false },
                _loggerFactory.CreateLogger<ExportData.ExportService>());

            string? exportPath = ExConfig.ExportPath;
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                Message = "請指定匯出路徑";
                IsSuccess = false;
                return;
            }

            if (!Directory.Exists(exportPath))
            {
                Directory.CreateDirectory(exportPath);
            }

            string[]? tablesToExport = (DbConfig.TableList != null && DbConfig.TableList.Length > 0)
                ? DbConfig.TableList
                : await dbService.GetTableNamesAsync();

            if (tablesToExport == null || tablesToExport.Length == 0)
            {
                Message = "沒有找到要匯出的資料表";
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

            Message = $"匯出完成，成功匯出 {exportedCount} 個資料表到 {ExConfig.ExportPath}";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Export failed for {DbType}: {Error}", DbConfig.DbType, Describe(ex));
            Message = "匯出失敗。 " + Describe(ex);
            IsSuccess = false;
        }
    }

    private async Task<IActionResult> ViewAsync()
    {
        await EnsureInventoryAsync();
        ConcealPostedSecrets();
        return Page();
    }

    private void ApplyFreshDefaults()
    {
        DbConfig = new ConfigDbControlSection
        {
            Size = 100,
            TableType = EnumTableType.Table
        };
        Connection = new DbConnectionForm
        {
            TrustServerCertificate = true
        };
        ExConfig = new ConfigExControlSection
        {
            ExportPath = @"c:\temp",
            MakeToZip = true,
            ZipFileName = "ExportData"
        };
        PasswordRemembered = false;
        IsConnected = false;
        InventoryFailed = false;
        MaskedConnectionString = null;
        Tables = null;
        DatabaseInfo = null;
    }

    private void ClearWorkspace()
    {
        WorkspaceConnection.Clear(HttpContext.Session);
        HttpContext.Session.Remove(PasswordSessionKey);
        HttpContext.Session.Remove(IdentitySessionKey);
    }

    private void RememberWorkspaceKeepingAnalysis()
    {
        var snapshot = WorkspaceConnection.Capture(DbConfig, Connection, ExConfig);
        var previous = WorkspaceConnection.Read(HttpContext.Session);
        snapshot.Analyzed = previous != null && previous.Analyzed && previous.SameAnalysis(snapshot);
        snapshot.Write(HttpContext.Session);
        IsConnected = true;
        _openedThisRequest = true;
    }

    private void RememberWorkspace(bool analyzed)
    {
        var snapshot = WorkspaceConnection.Capture(DbConfig, Connection, ExConfig);
        snapshot.Analyzed = analyzed;
        snapshot.Write(HttpContext.Session);
        IsConnected = true;
    }

    private async Task EnsureInventoryAsync()
    {
        if (!_openedThisRequest || Tables != null)
        {
            return;
        }

        var saved = WorkspaceConnection.Read(HttpContext.Session);
        if (saved is not { Analyzed: true })
        {
            return;
        }

        var dbService = new ExportData.DbService(saved.ToDbConfig(), _loggerFactory.CreateLogger<ExportData.DbService>());
        var names = await dbService.GetTableNamesAsync();
        if (names != null)
        {
            Tables = names;
        }
    }

    private async Task ResumeInventoryAsync(WorkspaceConnection saved)
    {
        var config = saved.ToDbConfig();
        if (!await TryReadDatabaseInfoAsync(config))
        {
            if (saved.Analyzed)
            {
                InventoryFailed = true;
                Message ??= "連線設定還在，但重新讀取資料庫失敗。可再按一次「分析結構」。";
                IsSuccess = false;
            }

            return;
        }

        if (!saved.Analyzed)
        {
            return;
        }

        var dbService = new ExportData.DbService(config, _loggerFactory.CreateLogger<ExportData.DbService>());
        Tables = await dbService.GetTableNamesAsync();
        if (Tables == null)
        {
            InventoryFailed = true;
            Message = "連線設定還在，但重新讀取資料表清單失敗。可再按一次「分析結構」。";
            IsSuccess = false;
        }
    }

    private async Task<bool> TryReadDatabaseInfoAsync(ConfigDbControlSection config)
    {
        if (config.DbType is not { } dbType)
        {
            return false;
        }

        var driver = DatabaseDriverCatalog.Probe(dbType);
        if (!driver.Installed)
        {
            Message = $"尚未載入 {driver.DisplayName} 驅動程式。請下載安裝後重新啟動工具。";
            DriverDownloadUrl = driver.DownloadUrl;
            return false;
        }

        var provider = DbChoicer.ChoiceProverder(config);
        if (provider == null)
        {
            return false;
        }

        try
        {
            using var connection = provider.GetConnection();
            try
            {
                DatabaseInfo = await provider.GetDatabaseInfoAsync(connection);
            }
            catch (Exception ex) when (!IsDriverLoadFailure(ex))
            {
                _logger.LogError("Resumed {DbType} but database info failed: {Error}", dbType, Describe(ex));
                DatabaseInfo = null;
            }

            return true;
        }
        catch (Exception ex) when (IsDriverLoadFailure(ex))
        {
            _logger.LogError("Driver load failed while resuming {DbType}: {Error}", dbType, ex.GetType().Name);
            Message = $"無法載入 {driver.DisplayName} 驅動程式。請下載安裝後重新啟動工具。";
            DriverDownloadUrl = driver.DownloadUrl;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Resumed connection failed for {DbType}: {Error}", dbType, Describe(ex));
            return false;
        }
    }

    private void ConcealPostedSecrets()
    {
        Connection.Password = null;
        if (!Connection.UseRawConnectionString)
        {
            DbConfig.ConnectionString = null;
        }
    }

    private void LoadDrivers()
    {
        Drivers = DatabaseDriverCatalog.ProbeAll();
        DriverCatalogJson = JsonSerializer.Serialize(Drivers, DriverJsonOptions);
    }

    private void AdoptRememberedPassword()
    {
        var identity = ConnectionSecretMemory.Identity(DbConfig.DbType, Connection);
        var decision = ConnectionSecretMemory.Resolve(
            Connection.Password,
            PasswordState,
            HttpContext.Session.GetString(IdentitySessionKey),
            HttpContext.Session.GetString(PasswordSessionKey),
            identity,
            Connection.UseRawConnectionString);

        if (decision.Forget)
        {
            HttpContext.Session.Remove(PasswordSessionKey);
            HttpContext.Session.Remove(IdentitySessionKey);
            Connection.Password = null;
        }
        else if (decision.Store && !string.IsNullOrEmpty(decision.Password))
        {
            HttpContext.Session.SetString(PasswordSessionKey, decision.Password);
            HttpContext.Session.SetString(IdentitySessionKey, identity);
            Connection.Password = decision.Password;
        }
        else if (!string.IsNullOrEmpty(decision.Password))
        {
            Connection.Password = decision.Password;
        }

        PasswordRemembered = HasRememberedPassword();
    }

    private bool HasRememberedPassword()
    {
        if (Connection.UseRawConnectionString)
        {
            return false;
        }

        var identity = ConnectionSecretMemory.Identity(DbConfig.DbType, Connection);
        return !string.IsNullOrEmpty(HttpContext.Session.GetString(PasswordSessionKey))
            && string.Equals(HttpContext.Session.GetString(IdentitySessionKey), identity, StringComparison.Ordinal);
    }

    private void RememberMaskedConnection()
    {
        if (Connection.UseRawConnectionString || DbConfig.DbType is not { } dbType || string.IsNullOrEmpty(DbConfig.ConnectionString))
        {
            return;
        }

        MaskedConnectionString = DbConnectionComposer.Mask(dbType, DbConfig.ConnectionString);
    }

    private bool HasError(string keyPart)
    {
        return ModelState.Keys.Any(key =>
            key.Contains(keyPart, StringComparison.OrdinalIgnoreCase)
            && ModelState[key]!.Errors.Count > 0);
    }

    private string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current != null && parts.Count < 3; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                parts.Add(current.Message.Trim());
            }
        }

        var text = DbConnectionComposer.ScrubSecrets(string.Join(" ", parts.Distinct()), Connection.Password);
        return text.Length > 500 ? text[..500] + "…" : text;
    }

    private static string FormatConnected(DatabaseInfo? info)
    {
        if (info == null || string.IsNullOrWhiteSpace(info.DatabaseName))
        {
            return "已連上資料庫，但讀不到資料庫資訊。";
        }

        var version = ShortVersion(info.Version);
        return string.IsNullOrEmpty(version)
            ? $"連線成功，已連上 {info.DatabaseName}。"
            : $"連線成功，已連上 {info.DatabaseName}。版本：{version}";
    }

    private static string ShortVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "";
        }

        var line = version.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return line.Length > 180 ? line[..180] + "…" : line;
    }

    private static bool IsDriverLoadFailure(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is DllNotFoundException or TypeLoadException or BadImageFormatException)
            {
                return true;
            }
        }

        return false;
    }
}
