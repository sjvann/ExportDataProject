# 資料庫匯出工具 (Database Export Tool)

這是一個功能強大的資料庫匯出工具，支援多種資料庫類型，可以快速匯出資料庫中所有資料表的資料成 CSV 格式，並提供資料庫結構分析功能。

## 🚀 功能特色

- **多資料庫支援**: SQLite、SQL Server、MySQL、Oracle、PostgreSQL
- **兩種使用模式**: CLI 互動式介面 和 Web 介面
- **資料庫結構分析**: 取得資料表結構、關聯性、索引等資訊
- **範例資料預覽**: 可以查看每個資料表的範例資料
- **批量匯出**: 支援匯出所有資料表或指定資料表
- **資料壓縮**: 可選擇將匯出的 CSV 檔案壓縮成 ZIP
- **資料去識別化**: 支援敏感資料的去識別化處理 (開發中)

## 📦 專案結構

```
ExportDataProject/
├── ExportData/                 # 核心程式庫
│   ├── Interfaces/            # 介面定義
│   ├── Models/                # 資料模型
│   ├── Services/              # 服務類別
│   ├── SqlGen/                # SQL 產生器
│   └── Program.cs             # CLI 程式進入點
├── ExportDataWeb/             # Web 介面
│   ├── Pages/                 # Razor Pages
│   └── Program.cs             # Web 程式進入點
└── README.md
```

## 🛠️ 安裝與設定

### 前置需求
- .NET 9.0 或更高版本
- 對應的資料庫驅動程式

### 建置專案
```bash
# 建置 CLI 工具
cd ExportData
dotnet build

# 建置 Web 介面
cd ../ExportDataWeb
dotnet build
```

## 📖 使用方式

### 1. CLI 互動式模式

```bash
cd ExportData
dotnet run --interactive
```

這會啟動一問一答的互動式介面，引導您設定：
- 資料庫類型和連線字串
- 匯出參數 (路徑、檔案數量等)
- 是否壓縮檔案

### 2. CLI 設定檔模式

```bash
cd ExportData
dotnet run --config appsettings.json
```

使用預先設定好的 `appsettings.json` 檔案執行匯出。

### 3. Web 介面模式

```bash
cd ExportDataWeb
dotnet run
```

然後開啟瀏覽器訪問 `https://localhost:5001`

Web 介面提供：
- 視覺化的參數設定
- 資料庫結構分析
- 資料表詳細資訊查看
- 範例資料預覽

### 4. 測試模式

```bash
cd ExportData
dotnet run --test
```

執行內建的測試來驗證功能是否正常。

## ⚙️ 設定檔範例

### appsettings.json
```json
{
  "DbControl": {
    "ConnectionString": "Data Source=server;Initial Catalog=database;User Id=user;Password=password;",
    "DbType": "SqlServer",
    "TableType": "Table",
    "TableList": [],
    "Size": 100,
    "Prefix": ""
  },
  "ExControl": {
    "ExportPath": "c:\\temp",
    "MakeToZip": true,
    "ZipFileName": "ExportData"
  },
  "DeIdentification": {
    "DeIdentification": false,
    "PII": ["PatientName", "PatientID"],
    "PHI": false
  }
}
```

### 參數說明

#### DbControl (資料庫控制)
- `ConnectionString`: 資料庫連線字串
- `DbType`: 資料庫類型 (Sqlite, SqlServer, MySql, Oracle, PostgreSql)
- `TableType`: 匯出物件類型 (Table, View)
- `TableList`: 指定要匯出的資料表清單 (空陣列表示匯出所有)
- `Size`: 每個資料表匯出的記錄數量
- `Prefix`: 資料表名稱前綴過濾

#### ExControl (匯出控制)
- `ExportPath`: 匯出檔案的儲存路徑
- `MakeToZip`: 是否壓縮成 ZIP 檔案
- `ZipFileName`: ZIP 檔案名稱 (不含副檔名)

## 🔗 連線字串範例

### SQLite
```
Data Source=c:\temp\database.db;
```

### SQL Server
```
Data Source=server;Initial Catalog=database;User Id=user;Password=password;
```

### MySQL
```
Server=localhost;Database=database;Uid=user;Pwd=password;
```

### Oracle
```
Data Source=server:1521/service;User Id=user;Password=password;
```

### PostgreSQL
```
Host=localhost;Port=5432;Database=database;Username=user;Password=password
```

## 🎯 使用場景

1. **資料交換**: 快速了解新資料庫的結構和內容
2. **資料備份**: 將重要資料表匯出成 CSV 格式
3. **資料分析**: 取得範例資料進行初步分析
4. **系統遷移**: 了解來源系統的資料結構
5. **文件產生**: 自動產生資料庫結構文件

## 🔧 開發者資訊

### 架構設計
- **依賴注入**: 使用 Microsoft.Extensions.DependencyInjection
- **日誌記錄**: 使用 Microsoft.Extensions.Logging
- **非同步程式設計**: 全面使用 async/await
- **介面導向**: 透過介面實現鬆耦合設計

### 擴展性
- 可輕鬆新增其他資料庫類型的支援
- 可自訂匯出格式 (目前支援 CSV)
- 可擴展資料去識別化規則

## 📝 授權

此專案採用 MIT 授權條款。

## 🤝 貢獻

歡迎提交 Issue 和 Pull Request 來改善這個工具！
