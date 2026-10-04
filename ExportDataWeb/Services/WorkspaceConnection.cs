using System.Text.Json;
using ExportData.Models.Config;
using ExportData.Models.EnumType;

namespace ExportDataWeb.Services;

public sealed class WorkspaceConnection
{
    public const string SessionKey = "exportdata.connection.active";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EnumDbType DbType { get; set; }
    public string ConnectionString { get; set; } = "";
    public string? Owner { get; set; }
    public string? DbName { get; set; }
    public EnumTableType TableType { get; set; } = EnumTableType.Table;
    public int Size { get; set; } = 100;
    public string? Prefix { get; set; }
    public bool UseRawConnectionString { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? FilePath { get; set; }
    public string? FormOwner { get; set; }
    public bool IntegratedSecurity { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public string? ExportPath { get; set; }
    public bool MakeToZip { get; set; }
    public string? ZipFileName { get; set; }
    public bool Analyzed { get; set; }

    public static WorkspaceConnection Capture(
        ConfigDbControlSection db,
        DbConnectionForm form,
        ConfigExControlSection export)
    {
        return new WorkspaceConnection
        {
            DbType = db.DbType ?? EnumDbType.SqlServer,
            ConnectionString = db.ConnectionString ?? "",
            Owner = db.Owner,
            DbName = db.DbName,
            TableType = db.TableType ?? EnumTableType.Table,
            Size = db.Size,
            Prefix = db.Prefix,
            UseRawConnectionString = form.UseRawConnectionString,
            Host = form.Host,
            Port = form.Port,
            Database = form.Database,
            Username = form.Username,
            FilePath = form.FilePath,
            FormOwner = form.Owner,
            IntegratedSecurity = form.IntegratedSecurity,
            TrustServerCertificate = form.TrustServerCertificate,
            ExportPath = export.ExportPath,
            MakeToZip = export.MakeToZip,
            ZipFileName = export.ZipFileName
        };
    }

    public bool SameAnalysis(WorkspaceConnection other)
    {
        return DbType == other.DbType
            && TableType == other.TableType
            && string.Equals(ConnectionString, other.ConnectionString, StringComparison.Ordinal)
            && string.Equals(Prefix ?? "", other.Prefix ?? "", StringComparison.Ordinal)
            && string.Equals(Owner ?? "", other.Owner ?? "", StringComparison.Ordinal);
    }

    public ConfigDbControlSection ToDbConfig()
    {
        return new ConfigDbControlSection
        {
            DbType = DbType,
            ConnectionString = ConnectionString,
            Owner = Owner,
            DbName = DbName,
            TableType = TableType,
            Size = Size,
            Prefix = Prefix
        };
    }

    public void CopyTo(ConfigDbControlSection db, DbConnectionForm form, ConfigExControlSection export)
    {
        db.DbType = DbType;
        db.ConnectionString = ConnectionString;
        db.Owner = Owner;
        db.DbName = DbName;
        db.TableType = TableType;
        db.Size = Size;
        db.Prefix = Prefix;
        form.UseRawConnectionString = UseRawConnectionString;
        form.Host = Host;
        form.Port = Port;
        form.Database = Database;
        form.Username = Username;
        form.FilePath = FilePath;
        form.Owner = FormOwner;
        form.IntegratedSecurity = IntegratedSecurity;
        form.TrustServerCertificate = TrustServerCertificate;
        form.Password = null;
        export.ExportPath = string.IsNullOrWhiteSpace(ExportPath) ? @"c:\temp" : ExportPath;
        export.MakeToZip = MakeToZip;
        export.ZipFileName = string.IsNullOrWhiteSpace(ZipFileName) ? "ExportData" : ZipFileName;
    }

    public static WorkspaceConnection? Read(ISession session)
    {
        var json = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<WorkspaceConnection>(json, JsonOptions);
            if (saved == null || string.IsNullOrWhiteSpace(saved.ConnectionString))
            {
                return null;
            }

            return saved;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(ISession session)
    {
        session.SetString(SessionKey, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static void Clear(ISession session)
    {
        session.Remove(SessionKey);
    }
}
