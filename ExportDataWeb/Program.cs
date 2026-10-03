using ExportData;
using ExportData.Interfaces;
using ExportData.Models.Config;

var builder = WebApplication.CreateBuilder(args);

// 控制台以 --no-launch-profile 啟動時環境是 Production，預設不會載入建置期的靜態資產清單，
// 請求 ExportDataWeb.styles.css 會在 wwwroot 找不到檔而丟例外。
if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseStaticWebAssets();
}

// Add services to the container.
builder.Services.AddRazorPages();

// DbService / ExportService 建構子直接吃設定物件，必須先註冊才能通過 DI 驗證。
builder.Services.AddSingleton(builder.Configuration.GetSection("DbControl").Get<ConfigDbControlSection>() ?? new ConfigDbControlSection());
builder.Services.AddSingleton(builder.Configuration.GetSection("ExControl").Get<ConfigExControlSection>() ?? new ConfigExControlSection());
builder.Services.AddSingleton(builder.Configuration.GetSection("DeIdentification").Get<ConfigDeIdentification>() ?? new ConfigDeIdentification());

builder.Services.AddTransient<IDbService, DbService>();
builder.Services.AddTransient<IExportService, ExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
