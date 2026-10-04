using ExportData.Models.Config;
using ExportData.Models.EnumType;

namespace ExportData.Services
{
    public readonly record struct ConnectionSecretDecision(string? Password, bool Store, bool Forget);

    public static class ConnectionSecretMemory
    {
        public static string Identity(EnumDbType? dbType, DbConnectionForm form)
        {
            return string.Join('\u001f',
                dbType?.ToString() ?? "",
                form.UseRawConnectionString ? "raw" : "form",
                form.IntegratedSecurity ? "1" : "0",
                (form.Host ?? "").Trim(),
                form.Port?.ToString() ?? "",
                (form.Username ?? "").Trim());
        }

        public static ConnectionSecretDecision Resolve(
            string? postedPassword,
            string? state,
            string? storedIdentity,
            string? storedPassword,
            string currentIdentity,
            bool useRawConnectionString)
        {
            if (useRawConnectionString)
            {
                return new ConnectionSecretDecision(
                    string.IsNullOrEmpty(postedPassword) ? null : postedPassword,
                    false,
                    false);
            }

            if (!string.IsNullOrEmpty(postedPassword))
            {
                return new ConnectionSecretDecision(postedPassword, true, false);
            }

            if (string.Equals(state, "cleared", StringComparison.OrdinalIgnoreCase))
            {
                return new ConnectionSecretDecision(null, false, true);
            }

            if (!string.IsNullOrEmpty(storedPassword)
                && string.Equals(storedIdentity, currentIdentity, StringComparison.Ordinal))
            {
                return new ConnectionSecretDecision(storedPassword, false, false);
            }

            return new ConnectionSecretDecision(null, false, false);
        }
    }
}
