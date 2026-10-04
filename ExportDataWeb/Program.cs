using ExportData;
using ExportData.Interfaces;
using ExportData.Models.Config;

var builder = WebApplication.CreateBuilder(args);

// 從原始碼以 Production 啟動（例如 --no-launch-profile）時，樣式表還在建置目錄，
// 要靠靜態資產清單才能找到。發布後的安裝目錄已把樣式表放進 wwwroot；
// 若再載入開發清單，路徑會指回打包那台電腦，別台機器會缺檔。
var webRoot = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var stylesInWebRoot = File.Exists(Path.Combine(webRoot, "ExportDataWeb.styles.css"));
if (!builder.Environment.IsDevelopment() && !stylesInWebRoot)
{
    builder.WebHost.UseStaticWebAssets();
}

// Add services to the container.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.Name = ".ExportData.Connection";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
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
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
