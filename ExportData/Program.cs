/*
 *  目的：快速匯出資料庫所有資料表的紀錄成CSV檔格式
 */
using ExportData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory());
        config.AddJsonFile("appsettings.json", optional: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddTransient<MainService>();
    })
    .Build();

var mainService = host.Services.GetRequiredService<MainService>();
await mainService.RunAsync();