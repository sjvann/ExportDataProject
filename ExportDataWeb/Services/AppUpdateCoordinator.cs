using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace ExportDataWeb.Services;

public sealed class AppUpdateCoordinator
{
    private readonly HttpClient _http;
    private readonly ILogger<AppUpdateCoordinator> _logger;
    private readonly string _feedUrl;
    private readonly AppVersion _current;
    private readonly string _rid;
    private readonly string _downloadDirectory;
    private readonly bool _allowLaunch;
    private readonly bool _allowLoopback;
    private readonly object _gate = new();
    private UpdateSnapshot _snapshot;
    private UpdateRelease? _release;
    private int _busy;
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;

    public AppUpdateCoordinator(HttpClient http, ILogger<AppUpdateCoordinator> logger, UpdateRuntime runtime)
    {
        _http = http;
        _logger = logger;
        _feedUrl = runtime.FeedUrl;
        _current = runtime.Current;
        _rid = runtime.Rid;
        _downloadDirectory = runtime.DownloadDirectory;
        _allowLaunch = runtime.AllowLaunch;
        _allowLoopback = runtime.AllowLoopback;
        _snapshot = new UpdateSnapshot("idle", "", null, null, "none");
    }

    public static UpdateRuntime CreateRuntime()
    {
        var feed = Environment.GetEnvironmentVariable("EXPORTDATA_UPDATE_FEED");
        if (string.IsNullOrWhiteSpace(feed))
        {
            feed = AppUpdatePlanner.DefaultFeedUrl;
        }

        var allowLoopback = Uri.TryCreate(feed, UriKind.Absolute, out var feedUri) && feedUri.IsLoopback;
        var currentText = ReadCurrentVersion(typeof(AppUpdateCoordinator).Assembly);
        if (!AppVersion.TryParse(currentText, out var current))
        {
            current = new AppVersion(0, 0, 0);
        }

        var baseDirectory = AppContext.BaseDirectory;
        return new UpdateRuntime(
            feed,
            current,
            AppUpdatePlanner.CurrentRid(),
            Path.Combine(Path.GetTempPath(), "ExportData", "updates"),
            AppUpdatePlanner.IsInstalledLayout(baseDirectory, Environment.ProcessPath),
            allowLoopback);
    }

    public UpdateSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public void QueueCheck(bool force = false)
    {
        if (!force && IsFresh())
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        _ = RunAsync(() => CheckCoreAsync());
    }

    public void QueueDownload()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return;
        }

        _ = RunAsync(() => DownloadCoreAsync());
    }

    public Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        return RunAsync(() => CheckCoreAsync(cancellationToken));
    }

    public Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        return RunAsync(() => DownloadCoreAsync(cancellationToken));
    }

    private bool IsFresh()
    {
        lock (_gate)
        {
            if (_checkedAt < DateTimeOffset.UtcNow.AddMinutes(-15))
            {
                return false;
            }

            return _snapshot.Phase is "current" or "available";
        }
    }

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private async Task CheckCoreAsync(CancellationToken cancellationToken = default)
    {
        Set(new UpdateSnapshot("checking", "正在檢查是否有新版本…", null, null, "none"));
        try
        {
            if (!Uri.TryCreate(_feedUrl, UriKind.Absolute, out var feedUri) || !AppUpdatePlanner.IsAllowedDownload(feedUri, _allowLoopback))
            {
                FailCheck("更新來源不在允許的位址。");
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Set(new UpdateSnapshot("current", $"目前已是最新版本 {_current}。", null, null, "none"));
                _checkedAt = DateTimeOffset.UtcNow;
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                FailCheck("更新服務沒有回應。");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!AppUpdatePlanner.TryParseRelease(json, _allowLoopback, out var release))
            {
                FailCheck("無法讀取版本資訊。");
                return;
            }

            _release = release;
            _checkedAt = DateTimeOffset.UtcNow;
            if (release.Version.CompareTo(_current) <= 0)
            {
                Set(new UpdateSnapshot("current", $"目前已是最新版本 {_current}。", null, null, "none"));
                return;
            }

            var asset = AppUpdatePlanner.Pick(release, _rid);
            if (asset == null)
            {
                Set(new UpdateSnapshot(
                    "failed",
                    $"新版本 {release.Version} 沒有這個系統的安裝檔。",
                    release.Notes,
                    null,
                    "retry"));
                return;
            }

            Set(new UpdateSnapshot(
                "available",
                $"發現新版本 {release.Version}。目前是 {_current}。",
                release.Notes,
                null,
                "download"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Update check failed: {Error}", ex.Message);
            FailCheck("無法連上更新服務。");
        }
    }

    private async Task DownloadCoreAsync(CancellationToken cancellationToken = default)
    {
        UpdateRelease? pending;
        bool canDownload;
        lock (_gate)
        {
            pending = _release;
            canDownload = pending != null && (_snapshot.Phase == "available"
                || (_snapshot.Phase == "failed" && _snapshot.Action == "download"));
        }

        if (!canDownload || pending is not UpdateRelease release)
        {
            return;
        }

        var asset = AppUpdatePlanner.Pick(release, _rid);
        if (asset == null || !AppUpdatePlanner.IsAllowedDownload(asset.Url, _allowLoopback))
        {
            Set(new UpdateSnapshot("failed", "下載位址不被允許。", null, null, "retry"));
            return;
        }

        Directory.CreateDirectory(_downloadDirectory);
        var destination = Path.Combine(_downloadDirectory, asset.Name);
        var partial = destination + ".partial";
        Set(new UpdateSnapshot("downloading", AppUpdatePlanner.Downloading(release.Version, 0, asset.Size), release.Notes, AppUpdatePlanner.Percent(0, asset.Size), "none"));

        try
        {
            using var response = await _http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Set(new UpdateSnapshot("failed", "下載失敗。請稍後再試。", null, null, "download"));
                return;
            }

            if (!AppUpdatePlanner.IsAllowedDownload(response.RequestMessage?.RequestUri, _allowLoopback))
            {
                Set(new UpdateSnapshot("failed", "下載被轉到不允許的位址。", null, null, "retry"));
                return;
            }

            var total = response.Content.Headers.ContentLength ?? asset.Size;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[32 * 1024];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    var percent = AppUpdatePlanner.Percent(received, total);
                    Set(new UpdateSnapshot(
                        "downloading",
                        AppUpdatePlanner.Downloading(release.Version, received, total),
                        release.Notes,
                        percent,
                        "none"));
                }
            }

            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(partial, destination);
            if (_allowLaunch && TryLaunch(destination))
            {
                Set(new UpdateSnapshot(
                    "applying",
                    $"下載完成。已開啟 {release.Version} 的安裝程式，完成後請重新開啟析庫。",
                    release.Notes,
                    100,
                    "none"));
                return;
            }

            var manual = _rid switch
            {
                "linux-x64" => $"請執行：sudo dpkg -i \"{destination}\"",
                _ => "請執行這個安裝檔來更新。"
            };
            Set(new UpdateSnapshot(
                "downloaded",
                $"下載完成。{release.Version} 的安裝檔在 {destination}。{manual}",
                release.Notes,
                100,
                "none"));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            _logger.LogWarning("Update download failed: {Error}", ex.Message);
            TryDelete(partial);
            Set(new UpdateSnapshot("failed", "下載失敗。請稍後再試。", release.Notes, null, "download"));
        }
    }

    private bool TryLaunch(string path)
    {
        try
        {
            if (_rid == "win-x64")
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            }

            if (_rid == "osx-x64")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { path },
                    UseShellExecute = false
                });
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not start installer: {Error}", ex.Message);
        }

        return false;
    }

    private void FailCheck(string message)
    {
        Set(new UpdateSnapshot("failed", message, null, null, "retry"));
    }

    private void Set(UpdateSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    public static string ReadCurrentVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (AppVersion.TryParse(informational, out var parsed))
        {
            return parsed.ToString();
        }

        var version = assembly.GetName().Version;
        if (version == null)
        {
            return "0.0.0";
        }

        var patch = version.Build < 0 ? 0 : version.Build;
        return $"{version.Major}.{version.Minor}.{patch}";
    }
}

public sealed record UpdateRuntime(
    string FeedUrl,
    AppVersion Current,
    string Rid,
    string DownloadDirectory,
    bool AllowLaunch,
    bool AllowLoopback);
