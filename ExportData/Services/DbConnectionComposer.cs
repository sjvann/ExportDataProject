using System.Text.RegularExpressions;
using ExportData.Models.Config;
using ExportData.Models.EnumType;
using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System.Data.SQLite;
using System.Data.SqlClient;

namespace ExportData.Services
{
    public readonly record struct ConnectionApplyResult(bool Succeeded, string? Error, string? FieldId)
    {
        public static ConnectionApplyResult Success() => new(true, null, null);

        public static ConnectionApplyResult Fail(string error, string fieldId) => new(false, error, fieldId);
    }

    public static class DbConnectionComposer
    {
        private static readonly Regex SecretPattern = new(
            "(?i)(password|pwd)\\s*=\\s*(\"(?:\\\\.|[^\"])*\"|'(?:\\\\.|[^'])*'|[^;\\s]*)",
            RegexOptions.Compiled);

        public static ConnectionApplyResult Apply(ConfigDbControlSection config, DbConnectionForm? form)
        {
            form ??= new DbConnectionForm();
            if (config.DbType is not EnumDbType dbType)
            {
                return ConnectionApplyResult.Fail("請選擇資料庫類型", "DbConfig_DbType");
            }

            if (form.UseRawConnectionString)
            {
                var raw = config.ConnectionString?.Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    return ConnectionApplyResult.Fail("請輸入連線字串", "DbConfig_ConnectionString");
                }

                config.ConnectionString = raw;
                return ConnectionApplyResult.Success();
            }

            if (form.Port is int port && port is < 1 or > 65535)
            {
                return ConnectionApplyResult.Fail("連接埠必須介於 1 到 65535", "Connection_Port");
            }

            try
            {
                switch (dbType)
                {
                    case EnumDbType.Sqlite:
                        return ApplySqlite(config, form);
                    case EnumDbType.SqlServer:
                        return ApplySqlServer(config, form);
                    case EnumDbType.MySql:
                        return ApplyMySql(config, form);
                    case EnumDbType.Oracle:
                        return ApplyOracle(config, form);
                    case EnumDbType.PostgreSql:
                        return ApplyPostgreSql(config, form);
                    default:
                        return ConnectionApplyResult.Fail("不支援的資料庫類型", "DbConfig_DbType");
                }
            }
            catch (Exception)
            {
                return ConnectionApplyResult.Fail("無法建立連線字串，請檢查主機、連接埠與帳號。", "Connection_Host");
            }
        }

        public static string Mask(EnumDbType dbType, string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return string.Empty;
            }

            try
            {
                return dbType switch
                {
                    EnumDbType.PostgreSql => MaskPostgreSql(connectionString),
                    EnumDbType.MySql => MaskMySql(connectionString),
                    EnumDbType.SqlServer => MaskSqlServer(connectionString),
                    EnumDbType.Oracle => MaskOracle(connectionString),
                    EnumDbType.Sqlite => connectionString,
                    _ => ScrubSecrets(connectionString, null)
                };
            }
            catch (Exception)
            {
                return ScrubSecrets(connectionString, null);
            }
        }

        public static string ScrubSecrets(string? text, string? password)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var masked = SecretPattern.Replace(text, "$1=******");
            if (!string.IsNullOrEmpty(password))
            {
                masked = masked.Replace(password, "******", StringComparison.Ordinal);
            }

            return masked;
        }

        internal static ConnectionParts Inspect(EnumDbType dbType, string connectionString)
        {
            switch (dbType)
            {
                case EnumDbType.PostgreSql:
                    var postgres = new NpgsqlConnectionStringBuilder(connectionString);
                    return new ConnectionParts(postgres.Host, postgres.Port, postgres.Database, postgres.Username, postgres.Password, null, false, false);
                case EnumDbType.MySql:
                    var mysql = new MySqlConnectionStringBuilder(connectionString);
                    return new ConnectionParts(mysql.Server, (int)mysql.Port, mysql.Database, mysql.UserID, mysql.Password, null, false, false);
                case EnumDbType.SqlServer:
                    var sqlServer = new SqlConnectionStringBuilder(connectionString);
                    return new ConnectionParts(null, null, sqlServer.InitialCatalog, sqlServer.UserID, sqlServer.Password, sqlServer.DataSource, sqlServer.IntegratedSecurity, sqlServer.TrustServerCertificate);
                case EnumDbType.Oracle:
                    var oracle = new OracleConnectionStringBuilder(connectionString);
                    return new ConnectionParts(null, null, null, oracle.UserID, oracle.Password, oracle.DataSource, false, false);
                case EnumDbType.Sqlite:
                    var sqlite = new SQLiteConnectionStringBuilder(connectionString);
                    return new ConnectionParts(null, null, null, null, null, sqlite.DataSource, false, false);
                default:
                    return new ConnectionParts(null, null, null, null, null, null, false, false);
            }
        }

        private static ConnectionApplyResult ApplySqlite(ConfigDbControlSection config, DbConnectionForm form)
        {
            if (string.IsNullOrWhiteSpace(form.FilePath))
            {
                return ConnectionApplyResult.Fail("請填寫資料庫檔案路徑", "Connection_FilePath");
            }

            var path = form.FilePath.Trim();
            var builder = new SQLiteConnectionStringBuilder { DataSource = path };
            config.ConnectionString = builder.ConnectionString;
            config.DbName = Path.GetFileName(path);
            return ConnectionApplyResult.Success();
        }

        private static ConnectionApplyResult ApplySqlServer(ConfigDbControlSection config, DbConnectionForm form)
        {
            if (string.IsNullOrWhiteSpace(form.Database))
            {
                return ConnectionApplyResult.Fail("請選擇資料庫", "Connection_Database");
            }

            if (!form.IntegratedSecurity && string.IsNullOrWhiteSpace(form.Username))
            {
                return ConnectionApplyResult.Fail("請填寫使用者名稱", "Connection_Username");
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = SqlServerDataSource(form),
                InitialCatalog = form.Database.Trim(),
                IntegratedSecurity = form.IntegratedSecurity,
                TrustServerCertificate = form.TrustServerCertificate
            };
            if (!form.IntegratedSecurity)
            {
                builder.UserID = form.Username!.Trim();
                if (!string.IsNullOrEmpty(form.Password))
                {
                    builder.Password = form.Password;
                }
            }

            config.ConnectionString = builder.ConnectionString;
            config.DbName = form.Database.Trim();
            return ConnectionApplyResult.Success();
        }

        private static ConnectionApplyResult ApplyMySql(ConfigDbControlSection config, DbConnectionForm form)
        {
            var databaseError = RequireDatabase(form, "請選擇資料庫");
            if (databaseError is not null)
            {
                return databaseError.Value;
            }

            var userError = RequireUsername(form);
            if (userError is not null)
            {
                return userError.Value;
            }

            var builder = new MySqlConnectionStringBuilder
            {
                Server = HostOrLocal(form),
                Database = form.Database!.Trim(),
                UserID = form.Username!.Trim()
            };
            if (form.Port is int port)
            {
                builder.Port = (uint)port;
            }

            AssignPassword(form, value => builder.Password = value);
            config.ConnectionString = builder.ConnectionString;
            config.DbName = form.Database.Trim();
            return ConnectionApplyResult.Success();
        }

        private static ConnectionApplyResult ApplyPostgreSql(ConfigDbControlSection config, DbConnectionForm form)
        {
            var databaseError = RequireDatabase(form, "請選擇資料庫");
            if (databaseError is not null)
            {
                return databaseError.Value;
            }

            var userError = RequireUsername(form);
            if (userError is not null)
            {
                return userError.Value;
            }

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = HostOrLocal(form),
                Database = form.Database!.Trim(),
                Username = form.Username!.Trim()
            };
            if (form.Port is int port)
            {
                builder.Port = port;
            }

            AssignPassword(form, value => builder.Password = value);
            config.ConnectionString = builder.ConnectionString;
            config.DbName = form.Database.Trim();
            return ConnectionApplyResult.Success();
        }

        private static ConnectionApplyResult ApplyOracle(ConfigDbControlSection config, DbConnectionForm form)
        {
            var databaseError = RequireDatabase(form, "請填寫服務名稱");
            if (databaseError is not null)
            {
                return databaseError.Value;
            }

            var userError = RequireUsername(form);
            if (userError is not null)
            {
                return userError.Value;
            }

            var host = HostOrLocal(form);
            var service = form.Database!.Trim();
            var dataSource = form.Port is int port ? $"{host}:{port}/{service}" : $"{host}/{service}";
            var builder = new OracleConnectionStringBuilder
            {
                DataSource = dataSource,
                UserID = form.Username!.Trim()
            };
            AssignPassword(form, value => builder.Password = value);
            config.ConnectionString = builder.ConnectionString;
            config.DbName = service;
            config.Owner = string.IsNullOrWhiteSpace(form.Owner) ? form.Username!.Trim() : form.Owner.Trim();
            return ConnectionApplyResult.Success();
        }

        private static ConnectionApplyResult? RequireDatabase(DbConnectionForm form, string message)
        {
            return string.IsNullOrWhiteSpace(form.Database)
                ? ConnectionApplyResult.Fail(message, "Connection_Database")
                : null;
        }

        private static ConnectionApplyResult? RequireUsername(DbConnectionForm form)
        {
            return string.IsNullOrWhiteSpace(form.Username)
                ? ConnectionApplyResult.Fail("請填寫使用者名稱", "Connection_Username")
                : null;
        }

        private static void AssignPassword(DbConnectionForm form, Action<string> assign)
        {
            if (!string.IsNullOrEmpty(form.Password))
            {
                assign(form.Password);
            }
        }

        private static string HostOrLocal(DbConnectionForm form)
        {
            return string.IsNullOrWhiteSpace(form.Host) ? "localhost" : form.Host.Trim();
        }

        private static string SqlServerDataSource(DbConnectionForm form)
        {
            var host = HostOrLocal(form);
            if (form.Port is not int port || host.Contains('\\') || host.Contains(','))
            {
                return host;
            }

            return $"{host},{port}";
        }

        private static string MaskPostgreSql(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "******";
            }

            return builder.ConnectionString;
        }

        private static string MaskMySql(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "******";
            }

            return builder.ConnectionString;
        }

        private static string MaskSqlServer(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "******";
            }

            return builder.ConnectionString;
        }

        private static string MaskOracle(string connectionString)
        {
            var builder = new OracleConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "******";
            }

            return builder.ConnectionString;
        }
    }

    internal readonly record struct ConnectionParts(
        string? Host,
        int? Port,
        string? Database,
        string? User,
        string? Password,
        string? DataSource,
        bool IntegratedSecurity,
        bool TrustServerCertificate);
}
