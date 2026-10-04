<p align="center">
  <img src="docs/images/app-icon.png" width="96" height="96" alt="">
</p>

<h1 align="center">析庫</h1>

<p align="center">舊系統資料庫解析</p>

<p align="center">
  <a href="https://github.com/sjvann/ExportDataProject/releases/tag/v0.1.1"><img alt="版本 0.1.1" src="https://img.shields.io/badge/%E7%89%88%E6%9C%AC-0.1.1-2563EB?style=flat"></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white"></a>
  <a href="#安裝"><img alt="平台：Windows、Linux、macOS" src="https://img.shields.io/badge/%E5%B9%B3%E5%8F%B0-Windows%20%7C%20Linux%20%7C%20macOS-1E3A5F?style=flat"></a>
  <img alt="介面語言：繁體中文" src="https://img.shields.io/badge/%E8%AA%9E%E8%A8%80-%E7%B9%81%E9%AB%94%E4%B8%AD%E6%96%87-475569?style=flat">
</p>

<p align="center">
  <a href="https://github.com/sjvann/ExportDataProject/releases/tag/v0.1.1"><img alt="下載 v0.1.1" src="https://img.shields.io/badge/%E4%B8%8B%E8%BC%89-v0.1.1-2563EB?style=for-the-badge"></a>
  <a href="#建置"><img alt="從原始碼建置與測試" src="https://img.shields.io/badge/%E5%8E%9F%E5%A7%8B%E7%A2%BC-%E5%BB%BA%E7%BD%AE%E8%88%87%E6%B8%AC%E8%A9%A6-1E3A5F?style=for-the-badge"></a>
</p>

本機的舊系統資料庫工具。連上 SQLite、SQL Server、MySQL、Oracle 或 PostgreSQL 之後，可以查看資料表清單、欄位結構、少量範例與宣告關聯，並把資料表匯出成 CSV。畫面與命令列提示都是繁體中文。

> [!NOTE]
> 工作台目前做到連線、分析結構與 CSV 匯出。`ExportData.Core` 已能保存解析專案（盤點快照、欄位確認狀態、宣告關聯）。欄位用途確認與分析包匯出還沒接到畫面。

| 看懂結構 | 對照真實資料 | 帶走結果 |
| --- | --- | --- |
| 資料表、欄位與宣告關聯 | 詳細頁載入 10 筆範例 | UTF-8 CSV，可壓成 ZIP |

## 兩種用法

| | 網頁工作台 | 命令列 |
| --- | --- | --- |
| 專案 | `ExportDataWeb` | `ExportData` |
| 適合 | 填表連線、看結構、匯出選定範圍 | 用同一份設定重複批量匯出 |
| 現在能做 | 表單或連線字串、測試連線、列出資料庫、分析結構、匯出 CSV | 互動問答或 `appsettings.json`、批量 CSV、可壓成 ZIP |

還沒有自己的資料庫時，先做[命令列煙霧測試](#先確認能跑)。要看畫面，直接進[網頁工作台](#網頁工作台)。

## 目錄

- [環境與建置](#環境與建置)
- [網頁工作台](#網頁工作台)
- [命令列](#命令列)
- [匯出結果](#匯出結果)
- [設定檔](#設定檔)
- [連線字串](#連線字串)
- [專案配置](#專案配置)
- [接下來](#接下來)

## 環境與建置

### 環境

- [.NET 10 SDK](https://dotnet.microsoft.com/download)（從原始碼建置時才需要）
- 可執行平台：Windows x64、Linux x64、macOS x64（Apple 晶片請搭配 Rosetta）
- 要連線的資料庫，以及該資料庫的讀取帳號

## 安裝

一般使用請到 [GitHub Releases](https://github.com/sjvann/ExportDataProject/releases/tag/v0.1.1) 下載安裝檔。安裝檔已含 .NET 執行環境。

| 系統 | 檔案 | 安裝後 |
| --- | --- | --- |
| Windows x64 | `ExportData-Setup-0.1.1-win-x64.exe` | 開始功能表開啟「析庫」 |
| Linux x64（Debian、Ubuntu） | `exportdata_0.1.1_amd64.deb` | `sudo dpkg -i exportdata_0.1.1_amd64.deb`，應用程式清單開啟「析庫」 |
| macOS x64 | `ExportData-0.1.1-osx-x64.pkg` | 打開安裝程式 |

工作台啟動後會向 GitHub Releases 檢查是否有較新版本。有新版本時，畫面上方會說明差異，按下「下載並更新」可看到已下載大小、總大小與百分比。Windows 安裝版下載完成後會開啟安裝程式。

工作台位址是 http://127.0.0.1:5107 。Windows 關掉標題為「析庫」的主控台視窗即停止；Linux 執行 `exportdata-workbench stop`；macOS 在活動監視器結束 `ExportDataWeb`。命令列在 Windows 開始功能表的「析庫命令列」，在 Linux 與 macOS 終端機執行 `exportdata --interactive`。

從原始碼重做安裝檔：

```powershell
powershell -File installer/build-installers.ps1
```

Windows 安裝程式需要 [Inno Setup 6](https://jrsoftware.org/isinfo.php)。Linux 與 macOS 套件在 WSL 裡打包。

網頁內建 SQLite、SQL Server、MySQL、Oracle、PostgreSQL 的驅動。若執行時回報驅動尚未載入，依畫面連結安裝後重新啟動工具。

### 建置

在儲存庫根目錄執行。`ExportDataProjects.sln` 含命令列、核心程式庫與測試。網頁專案請用第二行單獨建置。

```bash
dotnet build ExportDataProjects.sln
dotnet build ExportDataWeb/ExportDataWeb.csproj
```

### 測試

```bash
dotnet test ExportDataProjects.sln
```

## 網頁工作台

在儲存庫根目錄啟動：

```bash
dotnet run --project ExportDataWeb --launch-profile https
```

瀏覽器開啟 <https://localhost:7046>。同一設定也聽 <http://localhost:5107>。

只要 HTTP：

```bash
dotnet run --project ExportDataWeb --launch-profile http
```

操作順序：

1. 選擇資料庫類型。SQL Server、MySQL、PostgreSQL 可載入伺服器上的資料庫清單。
2. 用表單填寫連線，或改貼完整連線字串。預覽裡的密碼以星號顯示。
3. **測試連線**，確認主機、連接埠與帳號。
4. **分析結構**，在右側列出資料表或檢視表。
5. **匯出 CSV**。可指定路徑、每表筆數（1–10000）、名稱前綴、資料表或檢視表，以及是否壓成 ZIP。

密碼在測試連線後不會留在輸入框。同一瀏覽器工作階段會記住，以便接著分析或匯出。從資料表清單按「開啟」時，網址只帶表名；詳細頁要再提供連線字串與資料庫類型，才能載入結構、範例（10 筆）或關聯。

## 命令列

以下指令都在儲存庫根目錄執行。

### 先確認能跑

內建 SQLite 煙霧測試會在暫存目錄建立測試庫並匯出，不必自備資料庫：

```bash
dotnet run --project ExportData -- --test
```

### 互動模式

會詢問資料庫類型、連線字串、匯出筆數、前綴、指定資料表、路徑與是否壓縮。ZIP 檔名可自訂。

```bash
dotnet run --project ExportData -- --interactive
```

### 設定檔模式

讀取目前目錄的 `appsettings.json`，或用 `--config` 指定路徑。此模式讀取 `ExControl:ExportPath` 與 `ExControl:MakeToZip`。ZIP 檔名固定為 `ExportZip` 加上日期。

```bash
dotnet run --project ExportData -- --config appsettings.json
```

## 匯出結果

匯出檔名是 `{資料表名稱}_{yyyyMMdd}.csv`，編碼 UTF-8。ZIP 檔名是 `{名稱}_{yyyyMMdd}.zip`。目錄不存在時，網頁匯出會建立。

## 設定檔

命令列設定檔的鍵名如下。

> [!IMPORTANT]
> 請換成自己的連線。不要把含密碼的檔案提交進儲存庫。

```json
{
  "DbControl": {
    "ConnectionString": "Data Source=c:\\temp\\database.db;",
    "DbType": "Sqlite",
    "TableType": "Table",
    "TableList": [],
    "Size": 100,
    "Prefix": "",
    "Owner": "",
    "DbName": ""
  },
  "ExControl": {
    "ExportPath": "c:\\temp",
    "MakeToZip": true
  }
}
```

| 鍵 | 說明 |
| --- | --- |
| `DbControl:ConnectionString` | 連線字串 |
| `DbControl:DbType` | `Sqlite`、`SqlServer`、`MySql`、`Oracle`、`PostgreSql` |
| `DbControl:TableType` | `Table` 或 `View`（也可寫 `1`、`2`） |
| `DbControl:TableList` | 要匯出的資料表。空陣列表示依類型與前綴列出全部 |
| `DbControl:Size` | 每個物件匯出的列數上限 |
| `DbControl:Prefix` | 只匯出名稱以此開頭的物件。留空表示不過濾 |
| `DbControl:Owner` | Oracle 擁有者。空白時命令列沿用設定檔原值；網頁表單空白時改用使用者名稱 |
| `ExControl:ExportPath` | CSV 與 ZIP 的目錄。不存在時網頁匯出會建立 |
| `ExControl:MakeToZip` | `true` 時，匯出後把該目錄的 CSV 壓進 ZIP，並刪除已壓入的 CSV |

`DeIdentification` 會被命令列讀入。匯出時只寫一筆日誌，CSV 仍是原值。網頁匯出固定關閉這個開關。

## 連線字串

命令列與網頁的「連線字串」模式直接使用下列格式。網頁的「表單填寫」會依欄位組成同等內容。

<details>
<summary>SQLite</summary>

```text
Data Source=c:\temp\database.db;
```

</details>

<details>
<summary>SQL Server</summary>

SQL 驗證：

```text
Data Source=localhost,1433;Initial Catalog=database;User ID=user;Password=password;Trust Server Certificate=True
```

Windows 驗證把伺服器寫在 `Data Source`。具名執行個體例如 `localhost\SQLEXPRESS`，連接埠留空。

</details>

<details>
<summary>MySQL</summary>

```text
Server=localhost;Port=3306;Database=database;User ID=user;Password=password
```

</details>

<details>
<summary>Oracle（服務名稱）</summary>

```text
Data Source=localhost:1521/ORCL;User ID=user;Password=password
```

</details>

<details>
<summary>PostgreSQL</summary>

```text
Host=localhost;Port=5432;Database=database;Username=user;Password=password
```

</details>

## 專案配置

```text
ExportDataProject/
├── ExportData/                 命令列、目錄 SQL、CSV 匯出
├── ExportData.Tests/           連線組成、PostgreSQL 目錄 SQL、驅動探測
├── ExportData.Core/            解析專案文件模型與儲存
├── ExportData.Core.Tests/      解析專案盤點模型測試
├── ExportDataWeb/              析庫工作台（Razor Pages）
└── ExportDataProjects.sln      命令列、核心與測試
```

## 接下來

工作台下一步是把解析專案接上畫面：唯讀盤點留在本機檔案、逐欄寫下用途並標成已確認或略過，再把看過的範圍匯出成分析包（封面、資料字典、實體關係圖、未確認清單）。命令列的 CSV 匯出維持獨立，不讀寫解析專案。
