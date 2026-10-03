# 既有程式邊界

這些是起點。目標結構、連接埠與檔案格式以架構骨架為準，不從現有頁面推論產品行為。

- `ExportData` 命令列與四種 `ISqlGenerater` 只做既有的大量 CSV 匯出。該專案不引用 `ExportData.Core`。目錄 SQL 可以被 Web 的目錄轉接器使用，但目錄讀取不轉呼叫 `GetSqlRecords`，也不讀 `SqlAllTable` 或 `SqlOneTable`。
- 目錄與外鍵的現有形狀在 `ExportData/Models/Database/DatabaseModels.cs`（`TableSchema`、`ColumnInfo`、`TableRelation`），讀取在 `DbService.GetTableSchemaAsync`、`GetTableRelationsAsync`，四種資料庫的 SQL 在 `ExportData/SqlGen/`。
- 現有範例列走 `GetDataSetAsync` 與設定裡的 `Size`。工作台的 20／100 列是新的上限，不沿用匯出的 `Size`。
- `ExportDataWeb/Pages/Index.cshtml` 現在以匯出為主行動。`TableDetail.cshtml` 把結構、範例、關聯拆成三次送出，關聯是表而不是鄰居圖。資料表工作台取代這個流程。
- 解析專案的持久化尚不存在。用途與確認狀態不能只放在頁面模型裡。
- `DeIdentificationService` 是空的。遮罩不以它為起點，也不把它擴成依個資類別的去識別化。
- 倉庫目標框架現為 `net9.0`。實作時改為架構棧的 `net10.0`。Web 與命令列都不用 `net10.0-windows`。驅動版本先維持倉庫釘選，直到建置證明不相容。
