namespace ExportData.Models.Config
{
    public class DbConnectionForm
    {
        public bool UseRawConnectionString { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Database { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? FilePath { get; set; }
        public string? Owner { get; set; }
        public bool IntegratedSecurity { get; set; }
        public bool TrustServerCertificate { get; set; } = true;
    }
}
