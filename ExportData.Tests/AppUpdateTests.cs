using System.Net;
using System.Text;
using ExportDataWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExportData.Tests;

public sealed class AppUpdateTests
{
    [Theory(DisplayName = "版本字串可去掉 v 與組建後綴")]
    [InlineData("v0.1.0", 0, 1, 0)]
    [InlineData("0.1.1+abcdef", 0, 1, 1)]
    [InlineData("1.2.3-preview", 1, 2, 3)]
    public void ParseVersion(string text, int major, int minor, int patch)
    {
        Assert.True(AppVersion.TryParse(text, out var version));
        Assert.Equal(new AppVersion(major, minor, patch), version);
    }

    [Fact(DisplayName = "較新的版本才需要更新")]
    public void NewerVersion_IsGreater()
    {
        Assert.True(AppVersion.TryParse("0.1.1", out var next));
        Assert.True(AppVersion.TryParse("0.1.0", out var current));
        Assert.True(next.CompareTo(current) > 0);
        Assert.Equal(0, current.CompareTo(current));
    }

    [Fact(DisplayName = "安裝檔名稱依系統對上發布檔")]
    public void Pick_SelectsTheAssetForThisSystem()
    {
        var version = new AppVersion(0, 1, 1);
        var release = new UpdateRelease(version, "說明",
        [
            new UpdateAsset("ExportData-Setup-0.1.1-win-x64.exe", new Uri("https://github.com/sjvann/ExportDataProject/releases/download/v0.1.1/ExportData-Setup-0.1.1-win-x64.exe"), 10),
            new UpdateAsset("exportdata_0.1.1_amd64.deb", new Uri("https://github.com/sjvann/ExportDataProject/releases/download/v0.1.1/exportdata_0.1.1_amd64.deb"), 10),
            new UpdateAsset("ExportData-0.1.1-osx-x64.pkg", new Uri("https://github.com/sjvann/ExportDataProject/releases/download/v0.1.1/ExportData-0.1.1-osx-x64.pkg"), 10)
        ]);

        var picked = AppUpdatePlanner.Pick(release, "win-x64");
        Assert.Equal(AppUpdatePlanner.FileNameFor(version, "win-x64"), picked?.Name);
        Assert.Null(AppUpdatePlanner.Pick(release, "unknown"));
    }

    [Fact(DisplayName = "下載進度含已收到、總量與百分比")]
    public void ProgressMessage_IncludesBytesAndPercent()
    {
        var message = AppUpdatePlanner.Downloading(new AppVersion(0, 1, 1), 512 * 1024, 1024 * 1024);
        Assert.Equal("正在下載 0.1.1：512.0 KB / 1.0 MB（50%）", message);
        Assert.Equal(50, AppUpdatePlanner.Percent(512 * 1024, 1024 * 1024));
        Assert.Contains("已收到", AppUpdatePlanner.Downloading(new AppVersion(0, 1, 1), 2048, null));
    }

    [Fact(DisplayName = "只接受 GitHub 或本機測試位址")]
    public void DownloadUrl_RejectsOtherHosts()
    {
        Assert.True(AppUpdatePlanner.IsAllowedDownload(new Uri("https://github.com/sjvann/ExportDataProject/releases/download/v0.1.1/file.exe"), false));
        Assert.True(AppUpdatePlanner.IsAllowedDownload(new Uri("https://release-assets.githubusercontent.com/file"), false));
        Assert.False(AppUpdatePlanner.IsAllowedDownload(new Uri("https://example.com/file.exe"), false));
        Assert.False(AppUpdatePlanner.IsAllowedDownload(new Uri("http://127.0.0.1/file.exe"), false));
        Assert.True(AppUpdatePlanner.IsAllowedDownload(new Uri("http://127.0.0.1/file.exe"), true));
    }

    [Fact(DisplayName = "安裝目錄才會被當成可直接開啟安裝程式")]
    public void InstalledLayout_RequiresThePackagedProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), "exportdata-layout-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(root, "web");
        Directory.CreateDirectory(web);
        File.WriteAllText(Path.Combine(root, "start-workbench.cmd"), "rem");
        try
        {
            Assert.True(AppUpdatePlanner.IsInstalledLayout(web, Path.Combine(web, "ExportDataWeb.exe")));
            Assert.False(AppUpdatePlanner.IsInstalledLayout(web, Path.Combine(web, "dotnet.exe")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact(DisplayName = "下載時會更新進度，完成後留下安裝檔")]
    public async Task Download_ReportsProgressAndSavesTheFile()
    {
        var payload = new byte[96 * 1024];
        Array.Fill(payload, (byte)'x');
        using var listener = new HttpListener();
        var port = GetFreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serve = Task.Run(() => ServeAsync(listener, payload));

        var directory = Path.Combine(Path.GetTempPath(), "exportdata-update-" + Guid.NewGuid().ToString("N"));
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var coordinator = new AppUpdateCoordinator(http, NullLogger<AppUpdateCoordinator>.Instance, new UpdateRuntime(
            $"http://127.0.0.1:{port}/latest.json",
            new AppVersion(0, 1, 0),
            AppUpdatePlanner.CurrentRid(),
            directory,
            false,
            true));

        try
        {
            await coordinator.CheckAsync();
            var available = coordinator.GetSnapshot();
            Assert.Equal("available", available.Phase);
            Assert.Contains("發現新版本 9.9.9", available.Message);
            Assert.Equal("測試用的新版本說明", available.Detail);

            var download = coordinator.DownloadAsync();
            var sawPartial = false;
            while (!download.IsCompleted)
            {
                var mid = coordinator.GetSnapshot();
                if (mid.Phase == "downloading" && mid.Percent is > 0 and < 100 && mid.Message.Contains('%'))
                {
                    sawPartial = true;
                }

                await Task.Delay(20);
            }

            await download;
            var done = coordinator.GetSnapshot();
            Assert.True(sawPartial);
            Assert.Equal("downloaded", done.Phase);
            Assert.Equal(100, done.Percent);
            Assert.Contains("下載完成", done.Message);
            var saved = File.ReadAllBytes(Path.Combine(directory, AppUpdatePlanner.FileNameFor(new AppVersion(9, 9, 9), AppUpdatePlanner.CurrentRid())));
            Assert.Equal(payload, saved);
        }
        finally
        {
            listener.Stop();
            http.Dispose();
            try { await serve; } catch (HttpListenerException) { }
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static int GetFreePort()
    {
        var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private static async Task ServeAsync(HttpListener listener, byte[] payload)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var path = context.Request.Url?.AbsolutePath ?? "";
            if (path.EndsWith("/latest.json", StringComparison.Ordinal))
            {
                var body = Encoding.UTF8.GetBytes(LatestJson(context.Request.Url!, payload.Length));
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
                continue;
            }

            context.Response.ContentLength64 = payload.Length;
            var sent = 0;
            while (sent < payload.Length)
            {
                var count = Math.Min(24 * 1024, payload.Length - sent);
                await context.Response.OutputStream.WriteAsync(payload.AsMemory(sent, count));
                await context.Response.OutputStream.FlushAsync();
                sent += count;
                if (sent < payload.Length)
                {
                    await Task.Delay(120);
                }
            }

            context.Response.Close();
        }
    }

    private static string LatestJson(Uri request, int size)
    {
        var fileName = AppUpdatePlanner.FileNameFor(new AppVersion(9, 9, 9), AppUpdatePlanner.CurrentRid());
        var url = $"http://127.0.0.1:{request.Port}/{fileName}";
        return $$"""
        {
          "tag_name": "v9.9.9",
          "body": "測試用的新版本說明",
          "assets": [
            { "name": "{{fileName}}", "browser_download_url": "{{url}}", "size": {{size}} }
          ]
        }
        """;
    }
}
