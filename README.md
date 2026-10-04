# 析庫

析庫是跑在本機的舊系統資料庫工具。連上 SQLite、SQL Server、MySQL、Oracle 或 PostgreSQL 之後，可以查看資料表清單、欄位結構、少量範例與宣告關聯，並把資料表匯出成 CSV。畫面與命令列提示都是繁體中文。

目前有兩種用法：

- **網頁工作台**（`ExportDataWeb`）：用表單或連線字串連線、測試連線、列出資料庫、分析資料表，並匯出 CSV。
- **命令列**（`ExportData`）：用互動問答或 `appsettings.json` 做批量 CSV 匯出，可選擇壓成 ZIP。

`ExportData.Core` 已開始保存解析專案（盤點快照、欄位確認狀態、宣告關聯）。欄位用途確認與分析包匯出還沒接到工作台。

## 環境

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows
- 要連線的資料庫，以及該資料庫的讀取帳號

網頁內建 SQLite、SQL Server、MySQL、Oracle、PostgreSQL 的驅動。若執行時回報驅動尚未載入，依畫面連結安裝後重新啟動工具。

## 建置

在儲存庫根目錄執行：

```bash
dotnet build ExportDataProjects.sln
dotnet build ExportDataWeb/ExportDataWeb.csproj
```

`ExportDataProjects.sln` 含命令列、核心程式庫與測試。網頁專案請用第二行單獨建置。

測試：

```bash
dotnet test ExportDataProjects.sln
```

## 網頁工作台

```bash
dotnet run --project ExportDataWeb --launch-profile https
```

瀏覽器開啟：

- https://localhost:7046
- http://localhost:5107

只要 HTTP 時：

```bash
dotnet run --project ExportDataWeb --launch-profile http
```

工作台可以：

1. 選擇資料庫類型。SQL Server、MySQL、PostgreSQL 可載入伺服器上的資料庫清單。
2. 用表單填寫連線，或改貼完整連線字串。預覽裡的密碼以星號顯示。
3. **測試連線**，確認主機、連接埠與帳號。
4. **分析結構**，在右側列出資料表或檢視表。
5. **匯出 CSV**。可指定路徑、每表筆數（1–10000）、名稱前綴、資料表或檢視表，以及是否壓成 ZIP。

密碼在測試連線後不會留在輸入框。同一瀏覽器工作階段會記住，以便接著分析或匯出。從資料表清單按「開啟」時，網址只帶表名；詳細頁要再提供連線字串與資料庫類型，才能載入結構、範例（10 筆）或關聯。

## 命令列

在儲存庫根目錄執行。

互動模式會詢問資料庫類型、連線字串、匯出筆數、前綴、指定資料表、路徑與是否壓縮：

```bash
dotnet run --project ExportData -- --interactive
```

設定檔模式讀取目前目錄的 `appsettings.json`，或用 `--config` 指定路徑：

```bash
dotnet run --project ExportData -- --config appsettings.json
```

內建 SQLite 煙霧測試會在暫存目錄建立測試庫並匯出：

```bash
dotnet run --project ExportData -- --test
```

設定檔模式會讀取 `ExControl:ExportPath` 與 `ExControl:MakeToZip`。ZIP 檔名在這個模式下使用 `ExportZip`，再加上日期。互動模式可以自訂 ZIP 檔名。

## 設定檔

命令列設定檔的鍵名如下。請換成自己的連線，不要把含密碼的檔案提交進儲存庫。

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

SQLite：

```text
Data Source=c:\temp\database.db;
```

SQL Server（SQL 驗證）：

```text
Data Source=localhost,1433;Initial Catalog=database;User ID=user;Password=password;Trust Server Certificate=True
```

SQL Server（Windows 驗證）把伺服器寫在 `Data Source`。具名執行個體例如 `localhost\SQLEXPRESS`，連接埠留空。

MySQL：

```text
Server=localhost;Port=3306;Database=database;User ID=user;Password=password
```

Oracle（服務名稱）：

```text
Data Source=localhost:1521/ORCL;User ID=user;Password=password
```

PostgreSQL：

```text
Host=localhost;Port=5432;Database=database;Username=user;Password=password
```

## 專案

```text
ExportDataProject/
├── ExportData/                 命令列、目錄 SQL、CSV 匯出
├── ExportData.Tests/           連線組成、PostgreSQL 目錄 SQL、驅動探測
├── ExportData.Core/            解析專案文件模型與儲存
├── ExportData.Core.Tests/      解析專案盤點模型測試
├── ExportDataWeb/              析庫工作台（Razor Pages）
└── ExportDataProjects.sln      命令列、核心與測試
```

匯出檔名是 `{資料表名稱}_{yyyyMMdd}.csv`，編碼 UTF-8。ZIP 檔名是 `{名稱}_{yyyyMMdd}.zip`。

## 後續

工作台下一步是把解析專案接上畫面：唯讀盤點留在本機檔案、逐欄寫下用途並標成已確認或略過，再把看過的範圍匯出成分析包（封面、資料字典、實體關係圖、未確認清單）。命令列的 CSV 匯出維持獨立，不讀寫解析專案。
