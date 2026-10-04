using System.Reflection;
using ExportData.Models.EnumType;

namespace ExportData.Services
{
    public sealed class DatabaseDriverStatus
    {
        public DatabaseDriverStatus(
            string dbType,
            string displayName,
            bool installed,
            string? version,
            string packageId,
            string downloadUrl,
            string message)
        {
            DbType = dbType;
            DisplayName = displayName;
            Installed = installed;
            Version = version;
            PackageId = packageId;
            DownloadUrl = downloadUrl;
            Message = message;
        }

        public string DbType { get; }
        public string DisplayName { get; }
        public bool Installed { get; }
        public string? Version { get; }
        public string PackageId { get; }
        public string DownloadUrl { get; }
        public string Message { get; }
    }

    public static class DatabaseDriverCatalog
    {
        private sealed record DriverDefinition(
            EnumDbType DbType,
            string DisplayName,
            string AssemblyName,
            string TypeName,
            string PackageId,
            string DownloadUrl);

        private static readonly DriverDefinition[] Definitions =
        [
            new(EnumDbType.Sqlite, "SQLite", "System.Data.SQLite", "System.Data.SQLite.SQLiteConnection", "System.Data.SQLite", "https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki"),
            new(EnumDbType.SqlServer, "SQL Server", "System.Data.SqlClient", "System.Data.SqlClient.SqlConnection", "System.Data.SqlClient", "https://learn.microsoft.com/sql/connect/ado-net/download-microsoft-sql-server"),
            new(EnumDbType.MySql, "MySQL", "MySql.Data", "MySql.Data.MySqlClient.MySqlConnection", "MySql.Data", "https://dev.mysql.com/downloads/connector/net/"),
            new(EnumDbType.Oracle, "Oracle", "Oracle.ManagedDataAccess", "Oracle.ManagedDataAccess.Client.OracleConnection", "Oracle.ManagedDataAccess.Core", "https://www.oracle.com/database/technologies/net-downloads.html"),
            new(EnumDbType.PostgreSql, "PostgreSQL", "Npgsql", "Npgsql.NpgsqlConnection", "Npgsql", "https://www.npgsql.org/download.html")
        ];

        public static IReadOnlyList<DatabaseDriverStatus> ProbeAll()
        {
            return Definitions.Select(Probe).ToArray();
        }

        public static DatabaseDriverStatus Probe(EnumDbType dbType)
        {
            var definition = Definitions.First(item => item.DbType == dbType);
            return Probe(definition);
        }

        private static DatabaseDriverStatus Probe(DriverDefinition definition)
        {
            var assembly = TryLoad(definition.AssemblyName);
            var driverType = assembly?.GetType(definition.TypeName, throwOnError: false);
            if (assembly == null || driverType == null)
            {
                return new DatabaseDriverStatus(
                    definition.DbType.ToString(),
                    definition.DisplayName,
                    installed: false,
                    version: null,
                    definition.PackageId,
                    definition.DownloadUrl,
                    $"尚未載入 {definition.DisplayName} 驅動程式。");
            }

            return new DatabaseDriverStatus(
                definition.DbType.ToString(),
                definition.DisplayName,
                installed: true,
                version: ReadVersion(assembly),
                definition.PackageId,
                definition.DownloadUrl,
                $"{definition.DisplayName} 驅動程式已內建。");
        }

        private static Assembly? TryLoad(string assemblyName)
        {
            try
            {
                return Assembly.Load(new AssemblyName(assemblyName));
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                return null;
            }
        }

        private static string? ReadVersion(Assembly assembly)
        {
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var raw = string.IsNullOrWhiteSpace(informational)
                ? assembly.GetName().Version?.ToString()
                : informational;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var plus = raw.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? raw[..plus] : raw;
        }
    }
}
