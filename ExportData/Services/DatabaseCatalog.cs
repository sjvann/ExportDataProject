using System.Data.Common;
using ExportData.Models.Config;
using ExportData.Models.EnumType;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Data.SqlClient;

namespace ExportData.Services
{
    public sealed class DatabaseCatalogResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }
        public IReadOnlyList<string> Names { get; init; } = [];
    }

    public static class DatabaseCatalog
    {
        public static async Task<DatabaseCatalogResult> ListAsync(
            EnumDbType dbType,
            DbConnectionForm? form,
            CancellationToken cancellationToken = default)
        {
            form ??= new DbConnectionForm();
            if (dbType is not (EnumDbType.PostgreSql or EnumDbType.MySql or EnumDbType.SqlServer))
            {
                return Fail("這個資料庫類型沒有可列出的資料庫清單");
            }

            var catalogs = dbType == EnumDbType.PostgreSql
                ? new[] { "postgres", "template1" }
                : new[] { CatalogName(dbType) };

            Exception? lastError = null;
            foreach (var catalog in catalogs)
            {
                if (!TryBuildCatalogConnection(dbType, form, catalog, out var connectionString, out var error))
                {
                    return Fail(error ?? "無法建立連線");
                }

                try
                {
                    var names = await ReadNamesAsync(dbType, connectionString!, cancellationToken);
                    return new DatabaseCatalogResult { Succeeded = true, Names = names };
                }
                catch (Exception ex) when (IsMissingCatalog(ex) && catalog != catalogs[^1])
                {
                    lastError = ex;
                }
                catch (Exception ex)
                {
                    return Fail(Describe(ex, form.Password));
                }
            }

            return Fail(lastError == null ? "無法讀取資料庫清單。" : Describe(lastError, form.Password));
        }

        internal static bool TryBuildCatalogConnection(
            EnumDbType dbType,
            DbConnectionForm form,
            string catalog,
            out string? connectionString,
            out string? error)
        {
            connectionString = null;
            var config = new ConfigDbControlSection { DbType = dbType };
            var catalogForm = new DbConnectionForm
            {
                Host = form.Host,
                Port = form.Port,
                Database = catalog,
                Username = form.Username,
                Password = form.Password,
                IntegratedSecurity = form.IntegratedSecurity,
                TrustServerCertificate = form.TrustServerCertificate
            };
            var applied = DbConnectionComposer.Apply(config, catalogForm);
            if (!applied.Succeeded || string.IsNullOrEmpty(config.ConnectionString))
            {
                error = applied.Error;
                return false;
            }

            connectionString = WithShortTimeout(dbType, config.ConnectionString);
            error = null;
            return true;
        }

        private static string CatalogName(EnumDbType dbType)
        {
            return dbType switch
            {
                EnumDbType.SqlServer => "master",
                EnumDbType.MySql => "mysql",
                _ => "postgres"
            };
        }

        private static string WithShortTimeout(EnumDbType dbType, string connectionString)
        {
            switch (dbType)
            {
                case EnumDbType.PostgreSql:
                    var postgres = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 5 };
                    return postgres.ConnectionString;
                case EnumDbType.SqlServer:
                    var sqlServer = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
                    return sqlServer.ConnectionString;
                case EnumDbType.MySql:
                    var mysql = new MySqlConnectionStringBuilder(connectionString) { ConnectionTimeout = 5 };
                    return mysql.ConnectionString;
                default:
                    return connectionString;
            }
        }

        private static async Task<IReadOnlyList<string>> ReadNamesAsync(
            EnumDbType dbType,
            string connectionString,
            CancellationToken cancellationToken)
        {
            using var connection = OpenConnection(dbType, connectionString);
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = ListSql(dbType);
            command.CommandTimeout = 5;
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var names = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                var name = reader.GetString(0);
                if (Include(dbType, name))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static DbConnection OpenConnection(EnumDbType dbType, string connectionString)
        {
            return dbType switch
            {
                EnumDbType.PostgreSql => new NpgsqlConnection(connectionString),
                EnumDbType.SqlServer => new SqlConnection(connectionString),
                EnumDbType.MySql => new MySqlConnection(connectionString),
                _ => throw new InvalidOperationException("這個資料庫類型沒有可列出的資料庫清單")
            };
        }

        private static string ListSql(EnumDbType dbType)
        {
            return dbType switch
            {
                EnumDbType.PostgreSql => "SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname",
                EnumDbType.SqlServer => "SELECT name FROM sys.databases ORDER BY name",
                EnumDbType.MySql => "SHOW DATABASES",
                _ => throw new InvalidOperationException("這個資料庫類型沒有可列出的資料庫清單")
            };
        }

        private static bool Include(EnumDbType dbType, string name)
        {
            if (dbType != EnumDbType.MySql)
            {
                return true;
            }

            return !name.Equals("information_schema", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("performance_schema", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingCatalog(Exception ex)
        {
            return ex is PostgresException postgres && postgres.SqlState == "3D000";
        }

        private static DatabaseCatalogResult Fail(string error)
        {
            return new DatabaseCatalogResult { Succeeded = false, Error = error };
        }

        private static string Describe(Exception ex, string? password)
        {
            var parts = new List<string>();
            for (var current = ex; current != null && parts.Count < 3; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    parts.Add(current.Message.Trim());
                }
            }

            var text = DbConnectionComposer.ScrubSecrets(string.Join(" ", parts.Distinct()), password);
            if (text.Length > 500)
            {
                text = text[..500] + "…";
            }

            return string.IsNullOrEmpty(text)
                ? "無法讀取資料庫清單。請確認主機、連接埠、帳號與密碼。"
                : "無法讀取資料庫清單。請確認主機、連接埠、帳號與密碼。 " + text;
        }
    }
}
