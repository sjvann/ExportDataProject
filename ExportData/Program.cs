/*
 *  目的：快速匯出資料庫所有資料表的紀錄成CSV檔格式
 *  支援兩種模式：
 *  1. 設定檔模式：使用 appsettings.json
 *  2. 互動模式：透過 CLI 一問一答設定參數
 */
using ExportData;
using ExportData.Interfaces;
using ExportData.Models.Config;
using ExportData.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

// 建立命令列選項
var interactiveOption = new Option<bool>(
    "--interactive",
    description: "使用互動式模式設定參數")
{
    IsRequired = false
};
interactiveOption.AddAlias("-i");

var configFileOption = new Option<string?>(
    "--config",
    description: "指定設定檔路徑 (預設: appsettings.json)")
{
    IsRequired = false
};
configFileOption.AddAlias("-c");

var testOption = new Option<bool>(
    "--test",
    description: "執行測試模式")
{
    IsRequired = false
};
testOption.AddAlias("-t");

var rootCommand = new RootCommand("資料庫匯出工具")
{
    interactiveOption,
    configFileOption,
    testOption
};

rootCommand.SetHandler(async (bool interactive, string? configFile, bool test) =>
{
    using IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory());

            var configFileName = configFile ?? "appsettings.json";
            if (File.Exists(configFileName))
            {
                config.AddJsonFile(configFileName, optional: true);
            }
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddTransient<MainService>();
            services.AddTransient<InteractiveCliService>();
            services.AddTransient<IDbService, DbService>();
            services.AddTransient<IExportService, ExportService>();
        })
        .Build();

    try
    {
        if (test)
        {
            // 測試模式
            await TestRunner.RunTestsAsync();
        }
        else if (interactive)
        {
            // 互動模式
            var interactiveService = host.Services.GetRequiredService<InteractiveCliService>();
            var (dbConfig, exConfig) = await interactiveService.RunInteractiveSetupAsync();

            // 顯示設定摘要
            interactiveService.DisplayConfiguration(dbConfig, exConfig);

            Console.WriteLine("\n是否要開始匯出？(y/n): ");
            var confirm = Console.ReadLine()?.ToLower();

            if (confirm == "y" || confirm == "yes" || confirm == "是")
            {
                var mainService = new MainService(dbConfig, exConfig, host.Services.GetRequiredService<ILogger<MainService>>());
                await mainService.RunAsync();
            }
            else
            {
                Console.WriteLine("已取消匯出作業。");
            }
        }
        else
        {
            // 設定檔模式
            var mainService = host.Services.GetRequiredService<MainService>();
            await mainService.RunAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "應用程式執行時發生錯誤");
        Console.WriteLine($"❌ 錯誤: {ex.Message}");
        Environment.Exit(1);
    }
}, interactiveOption, configFileOption, testOption);

await rootCommand.InvokeAsync(args);