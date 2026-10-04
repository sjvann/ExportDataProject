using ExportData.Models.EnumType;
using ExportData.Services;
using Xunit;

namespace ExportData.Tests;

public sealed class DatabaseDriverCatalogTests
{
    [Fact(DisplayName = "五種資料庫都有官方下載位址，且內建驅動可載入")]
    public void AllDrivers_AreBundledAndLinkToOfficialDownloads()
    {
        var statuses = DatabaseDriverCatalog.ProbeAll();

        Assert.Equal(5, statuses.Count);
        Assert.Equal(
            new[] { "Sqlite", "SqlServer", "MySql", "Oracle", "PostgreSql" },
            statuses.Select(status => status.DbType).ToArray());

        foreach (var status in statuses)
        {
            Assert.True(status.Installed, status.Message);
            Assert.False(string.IsNullOrWhiteSpace(status.Version));
            Assert.StartsWith("https://", status.DownloadUrl);
            Assert.False(string.IsNullOrWhiteSpace(status.PackageId));
        }
    }

    [Fact(DisplayName = "單一類型查詢與清單一致")]
    public void Probe_OneType_MatchesTheCatalog()
    {
        var single = DatabaseDriverCatalog.Probe(EnumDbType.PostgreSql);
        var listed = DatabaseDriverCatalog.ProbeAll().Single(status => status.DbType == "PostgreSql");

        Assert.Equal(listed.Installed, single.Installed);
        Assert.Equal(listed.PackageId, single.PackageId);
        Assert.Equal(listed.DownloadUrl, single.DownloadUrl);
    }
}
