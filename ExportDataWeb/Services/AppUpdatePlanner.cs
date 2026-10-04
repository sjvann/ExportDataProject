using System.Text.Json;

namespace ExportDataWeb.Services;

public readonly record struct AppVersion(int Major, int Minor, int Patch) : IComparable<AppVersion>
{
    public int CompareTo(AppVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
        {
            return minor;
        }

        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            value = value[..dash];
        }

        var parts = value.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return false;
        }

        var patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
        {
            return false;
        }

        version = new AppVersion(major, minor, patch);
        return true;
    }
}

public sealed record UpdateAsset(string Name, Uri Url, long? Size);

public sealed record UpdateRelease(AppVersion Version, string? Notes, IReadOnlyList<UpdateAsset> Assets);

public sealed record UpdateSnapshot(
    string Phase,
    string Message,
    string? Detail,
    int? Percent,
    string Action);

public static class AppUpdatePlanner
{
    public const string Repository = "sjvann/ExportDataProject";

    public static string DefaultFeedUrl =>
        $"https://api.github.com/repos/{Repository}/releases/latest";

    public static string CurrentRid()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win-x64";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx-x64";
        }

        return "unknown";
    }

    public static string FileNameFor(AppVersion version, string rid) => rid switch
    {
        "win-x64" => $"ExportData-Setup-{version}-win-x64.exe",
        "linux-x64" => $"exportdata_{version}_amd64.deb",
        "osx-x64" => $"ExportData-{version}-osx-x64.pkg",
        _ => ""
    };

    public static UpdateAsset? Pick(UpdateRelease release, string rid)
    {
        var expected = FileNameFor(release.Version, rid);
        if (expected.Length == 0)
        {
            return null;
        }

        return release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, expected, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowedDownload(Uri? uri, bool allowLoopback)
    {
        if (uri is null)
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return allowLoopback && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.IdnHost;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseRelease(string json, bool allowLoopback, out UpdateRelease release)
    {
        release = new UpdateRelease(default, null, []);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tag) || !AppVersion.TryParse(tag.GetString(), out var version))
            {
                return false;
            }

            var notes = root.TryGetProperty("body", out var body) ? ShortNote(body.GetString()) : null;
            var assets = new List<UpdateAsset>();
            if (root.TryGetProperty("assets", out var assetList) && assetList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in assetList.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                    var urlText = item.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || !Uri.TryCreate(urlText, UriKind.Absolute, out var url))
                    {
                        continue;
                    }

                    if (!IsAllowedDownload(url, allowLoopback))
                    {
                        continue;
                    }

                    long? size = null;
                    if (item.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var bytes) && bytes >= 0)
                    {
                        size = bytes;
                    }

                    assets.Add(new UpdateAsset(name, url, size));
                }
            }

            release = new UpdateRelease(version, notes, assets);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.0} KB";
        }

        return $"{bytes / 1024d / 1024d:0.0} MB";
    }

    public static int? Percent(long received, long? total)
    {
        if (total is not > 0)
        {
            return null;
        }

        var value = (int)Math.Min(100, received * 100 / total.Value);
        return Math.Max(0, value);
    }

    public static string Downloading(AppVersion version, long received, long? total)
    {
        var got = FormatBytes(received);
        var percent = Percent(received, total);
        if (percent is int value && total is long all)
        {
            return $"正在下載 {version}：{got} / {FormatBytes(all)}（{value}%）";
        }

        return $"正在下載 {version}：已收到 {got}";
    }

    public static bool IsInstalledLayout(string baseDirectory, string? processPath)
    {
        var process = Path.GetFileName(processPath ?? "");
        var installedProcess = process.Equals("ExportDataWeb.exe", StringComparison.OrdinalIgnoreCase)
            || process.Equals("ExportDataWeb", StringComparison.OrdinalIgnoreCase);
        if (!installedProcess)
        {
            return false;
        }

        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        var parent = Directory.GetParent(dir)?.FullName;
        if (parent != null && File.Exists(Path.Combine(parent, "start-workbench.cmd")))
        {
            return true;
        }

        var normalized = dir.Replace('\\', '/');
        return normalized.EndsWith("/opt/exportdata/web", StringComparison.Ordinal)
            || normalized.Contains("/.app/Contents/Resources/web", StringComparison.Ordinal)
            || normalized.Contains(".app/Contents/Resources/web", StringComparison.Ordinal);
    }

    private static string? ShortNote(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var line = body.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(item => !item.StartsWith('#'));
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        line = line.TrimStart('-', '*', ' ');
        return line.Length <= 160 ? line : line[..160];
    }
}
