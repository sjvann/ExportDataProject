# 技術版本核對 — ExportDataProject 架構骨架

- 審查對象：`ARCHITECTURE-SPINE.md`（未修改）
- 審查日：2026-10-03
- 範圍：骨架承諾的技術決定是否對過網頁或既有專案，而不是只靠訓練資料。棕地釘選優先於「最新版」。Deferred 裡的建議升級，版本敘述本身沒錯就不列為錯誤。
- verdict：**needs-fixes**

## 結論

.NET 10 的 LTS、修補版與支援日，以及 Stack 表上四個直接釘選的套件版本，都與 2026-10-03 的官方頁面和倉庫 `csproj` 一致。Razor Pages 仍隨 ASP.NET Core／.NET 10 交付。唯一要改的是 `System.Data.SqlClient` 那一列：直接 `PackageReference` 確實沒有，但還原圖已經傳遞釘上 **4.8.1**。骨架把它寫成沒有套件釘選，版本敘述不完整。

## 查核方法

| 主張 | 對照 |
| --- | --- |
| .NET 支援窗口 | [官方支援政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)（頁面標 Last updated: September 8, 2026）、[產品生命週期](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core)、[.NET 部落格：.NET 8／9 於 2026-11-10 結束支援](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/) |
| Razor Pages | [Razor Pages 架構（view=aspnetcore-10.0）](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/?view=aspnetcore-10.0)、[教學：建立 Razor Pages 應用（.NET 10）](https://learn.microsoft.com/en-us/aspnet/core/tutorials/razor-pages/razor-pages-start?view=aspnetcore-10.0)；倉庫 `ExportDataWeb` |
| 套件版本 | NuGet 各套件頁；`ExportData/ExportData.csproj`、`ExportDataWeb/ExportDataWeb.csproj` |
| SqlClient 實際引用 | 上述 csproj、`ExportData/SqlGen/GenSqlForSqlServer.cs`、`ExportData/DbService.cs`、`ExportData/obj/project.assets.json`（2026-10-03 `dotnet build ExportData` 還原） |
| DPAPI 是否仍能配 `net10.0` | [System.Security.Cryptography.ProtectedData 10.0.12](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/10.0.12)、[如何使用資料保護](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection) |

本機 `Microsoft.NETCore.App.Ref` 已裝到 10.0.10。支援政策頁的最新修補仍是 10.0.12（2026-09-08）。下一個 Patch Tuesday 是 2026-10-13，所以 2026-10-03 當天 10.0.12 仍是最新修補。

## 逐項

### .NET 10 為現行 LTS，10.0.12，支援到 2028-11-14 — 正確

支援政策表（2026-09-08 更新，2026-10-03 擷取）：

| 版本 | 最新修補 | 修補日 | 類型 | 階段 | 支援結束 |
| --- | --- | --- | --- | --- | --- |
| .NET 10 | 10.0.12 | 2026-09-08 | LTS | Active | 2028-11-14 |
| .NET 9 | 9.0.20 | 2026-09-08 | STS | Maintenance | 2026-11-10 |
| .NET 8 | 8.0.31 | 2026-09-08 | LTS | Maintenance | 2026-11-10 |

生命週期頁把結束時間寫成太平洋時間次日 06:59:59（.NET 10 為 2028-11-15 06:59:59、.NET 9 為 2026-11-11 06:59:59）。這是「支援日當天結束」的時戳寫法，與政策頁的 2028-11-14、2026-11-10 同一天。部落格也寫 .NET 9 於 2026-11-10 結束支援。骨架與 memlog 的日期跟政策頁一致。

骨架 Stack 寫「10.0 LTS，最新修補 10.0.12（2026-09-08 查核）」。括號裡的日期是修補發布日，不是過期的查核。memlog 另記 2026-10-03 對過政策頁。2026-10-03 重查，10.0.12 仍是最新修補。

倉庫現況是 `ExportData` `net9.0-windows`、`ExportDataWeb` `net9.0`。骨架 AD-8 採用的是目標框架 `net10.0`，不是宣稱 csproj 已經是 net10。這是已查過支援窗口的升級決定。驅動維持倉庫釘選寫在 Deferred，不把「還沒改 csproj」當成版本說錯。

.NET 11 RC1 已在支援政策的 Go-live 表（支援到 2026-10-13）。現行 LTS 仍是 .NET 10，骨架沒有誤把 11 當成 LTS。

### .NET 9 支援於 2026-11-10 結束 — 正確

見上表。此日期在 memlog，不在 Stack 表；與政策頁、部落格一致。

### Dapper、SQLite、MySQL、Oracle — 與 csproj 一致

兩個專案的直接 `PackageReference` 相同：

| 套件 | 骨架 | `ExportData.csproj` | `ExportDataWeb.csproj` | NuGet 該版是否存在 |
| --- | --- | --- | --- | --- |
| Dapper | 2.1.35 | 2.1.35 | 2.1.35 | 有。目標 net5.0／net7.0／netstandard2.0，NuGet 將 net10.0 列為相容。最新 2.1.89（2026-09-23） |
| System.Data.SQLite | 1.0.118 | 1.0.118 | 1.0.118 | 有。netstandard2.0／2.1。NuGet 標 1.0.118 與 1.0.119 因重大缺陷棄用，建議 2.x。最新穩定 2.0.4（2026-08-04） |
| MySql.Data | 8.3.0 | 8.3.0 | 8.3.0 | 有。明示目標到 net8.0 與 netstandard2.0／2.1，net9.0／net10.0 為推算相容。較新線為 9.7.0、26.7.0 |
| Oracle.ManagedDataAccess.Core | 3.21.140 | 3.21.140 | 3.21.140 | 有。netstandard2.1，net10.0 為推算相容。23.26.301 發布於 2026-09-08；3.21 線本身仍有較新版（例如 3.21.230） |

四個版本數字都是倉庫釘選，不是把 NuGet 最新版寫進骨架。Dapper 較新版、Oracle 23.x、以及「資料庫驅動升級」已在 Deferred（Oracle 3.21 是否換 23.x 寫明了）。SQLite 1.0.118 被 NuGet 標成棄用，但版本敘述與 csproj 一致，升級落在同一條 Deferred，不列為未查證錯誤。

### ASP.NET Core Razor Pages 仍隨 .NET 10 交付 — 正確

支援政策開宗明義：.NET 與 .NET Core 的支援涵蓋 runtime、SDK、ASP.NET Core、EF Core。Learn 的 Razor Pages 文章在 `aspnetcore-10.0` 的 moniker 裡仍說明 `AddRazorPages`／`MapRazorPages`／`@page`。.NET 10 教學仍以 Razor Pages 當網頁起手式，目標框架選 .NET 10.0。

.NET 10 棄用的是 Razor **執行期編譯**（`AddRazorRuntimeCompilation`，警告 ASPDEPR003），不是 Razor Pages 本身。骨架沒有承諾執行期編譯。

倉庫已是 Razor Pages，不是另起一套 UI：`ExportDataWeb` 使用 `Microsoft.NET.Sdk.Web`，`Program.cs` 呼叫 `AddRazorPages` 與 `MapRazorPages`，`Pages/*.cshtml` 有 `@page`。這是棕地慣例，骨架沿用並把目標框架放到 .NET 10，與文件相符。

### System.Data.SqlClient — 骨架少寫了傳遞釘選

骨架 Stack：「現有原始碼依賴，csproj 無 PackageReference 釘選。」

直接 `PackageReference` 這半句對：`ExportData.csproj` 與 `ExportDataWeb.csproj` 都沒有 `System.Data.SqlClient`。沒有 `Directory.Build.props`、`nuget.config` 或 lock 檔另做中央釘選。

實際引用不是「只有 using、沒有套件」：

1. `ExportData/SqlGen/GenSqlForSqlServer.cs` 有 `using System.Data.SqlClient`，並 `new SqlConnection(...)`。
2. `ExportData/DbService.cs` 有同一個 using；該檔沒有 `SqlConnection`／`SqlException` 等型別使用。
3. 2026-10-03 還原的 `ExportData/obj/project.assets.json`（TFM `net9.0-windows`）依賴鏈是：`System.Data.SQLite` 1.0.118 → `System.Data.SQLite.EF6` 1.0.118 → `EntityFramework` 6.4.4 → **`System.Data.SqlClient` 4.8.1**。4.8.1 的編譯資產是 `ref/netcoreapp2.1/System.Data.SqlClient.dll`。同一次 `dotnet build` 的錯誤在 `ILogger.CreateLogger` 與唯讀欄位，沒有 `SqlConnection` 找不到。型別是靠這條傳遞參考編過，不是靠共用框架。
4. 它不在 .NET 9／10 共用框架裡。NuGet 上 `System.Data.SqlClient` 最新穩定版是 **4.9.1**（2026-02-18）。套件頁寫明已棄用，請改用 `Microsoft.Data.SqlClient`。版本表把 4.8.1 標成至少一個高嚴重性弱點；4.9.0／4.9.1 那兩列沒有同一句標示。

Deferred 已把「是否換成 `Microsoft.Data.SqlClient`」以及「驅動仍用倉庫釘選，直到建置證明不相容」列為以後再定。這次不要求升到 4.9.1 或換套件。要改的是敘述：有效釘選是傳遞而來的 4.8.1，不是沒有釘選。否則「維持倉庫釘選」對 SQL Server 驅動對不到版本。

### 其他已點名、沒有獨立版本號的技術

- **Kestrel 只聽 localhost：** 隨 ASP.NET Core 共用框架，不另釘套件。倉庫是 `Sdk.Web`。沒有過時的獨立版本主張。
- **Windows DPAPI：** 倉庫尚無 `ProtectedData` 呼叫，這是新決定，不是既有釘選。NuGet `System.Security.Cryptography.ProtectedData` 10.0.12（2026-09-08）明示支援 `net10.0`，不要求 `net10.0-windows`；執行仍只在 Windows。與 AD-8「Web 與命令列都不用 `net10.0-windows`」以及 AD-6 的 DPAPI 附檔可以並存。骨架沒有寫錯的套件版本。Learn 的作法是對現代 .NET 加這個 NuGet，而不是改 TFM。

`Microsoft.Extensions.*` 8.0.0 與 `System.CommandLine` 2.0.0-beta4.22272.1 在 `ExportData.csproj`，骨架 Stack 沒有承諾它們，這次不另判。

## 不列為錯誤

- 目標框架從 net9 改為 net10.0：支援窗口已對過政策頁；csproj 仍是 net9 是現況，不是骨架把修補版寫錯。
- Dapper 2.1.35 落後 2.1.89、Oracle 3.21.140 落後 23.26.x、MySql.Data 8.3.0 落後 9.x／26.x、SQLite 1.0.118 被 NuGet 標棄用：版本與兩個 csproj 一致，升級在 Deferred。
- `System.Data.SqlClient` 套件棄用、以及 4.8.1 的弱點標示：換 `Microsoft.Data.SqlClient` 已在 Deferred。問題是骨架沒寫出 4.8.1 這條傳遞釘選。

## 發現

1. **中。** Stack 把 `System.Data.SqlClient` 寫成只有原始碼、csproj 無套件釘選。還原圖的有效版本是傳遞相依 **4.8.1**（`System.Data.SQLite` 1.0.118 → `System.Data.SQLite.EF6` → `EntityFramework` 6.4.4）。直接 `PackageReference` 確實沒有。敘述應寫出這條鏈與 4.8.1；是否改 `Microsoft.Data.SqlClient` 維持 Deferred。

## verdict

needs-fixes
