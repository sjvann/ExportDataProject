---
title: '建立解析專案並唯讀盤點'
type: 'feature'
created: '2026-10-03'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-ExportDataProject/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-ExportDataProject/functional-requirements.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-ExportDataProject-2026-10-03/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-ExportDataProject-2026-10-03/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 分析師無法建立一份本機解析專案，並以唯讀連線盤點舊庫。現有網頁把匯出當主行動，連線失敗時也沒有留下可再開啟的專案。

**Approach:** 新增 `ExportData.Core` 持有解析專案與連接埠。`ExportDataWeb` 實作目錄讀取與 DPAPI 秘密庫，首頁在測試成功後顯示上次寫入的盤點快照。命令列繼續只做 CSV 匯出，不引用核心。

## Boundaries & Constraints

**Always:**
- 舊庫僅 SQLite、SQL Server、MySQL、Oracle。語句限於目錄查詢。目標框架 `net10.0`，不用 `net10.0-windows`。Kestrel 只聽 localhost。
- 解析專案是一份 JSON，暫存檔再改名。連線字串只進同目錄 DPAPI 附檔。文件含顯示名稱、資料庫類型、識別字規則、盤點快照，以及空的推測關聯、模組、結構變更。
- 測試成功才寫入快照。快照含表名、綱要、約略筆數、宣告關聯數、欄位數、資料表確認狀態（初始皆未看）。檢視表預設不進盤點，有獨立開關。單一表筆數失敗時該列為未知。
- 四種失敗分開，繁體中文，可附已去除連線字串的驅動原文：連不上主機、認證失敗、沒有讀取目錄的權限、類型與連線內容不符。失敗留下無快照的專案，清單不顯示成零張表。
- 再次打開顯示文件裡的快照，不重查筆數。空首頁標題用工作描述，不用 ExportDataWeb。

**Never:**
- 不實作搜尋、前綴篩選、排序、下一張未看、工作台、確認命令、分析包、推測關聯、模組、UML、起草頁。
- 目錄讀取不呼叫 `GetSqlRecords`、`GetDataSetAsync`，不讀 `SqlAllTable` 或 `SqlOneTable`。`ExportData` 不引用 `ExportData.Core`。
- 不把連線字串寫進 JSON、日誌、錯誤訊息或回應。不自動標成已確認。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 測試成功 | 合法唯讀連線 | 寫入快照與秘密附檔；清單出現資料表 | N/A |
| 認證失敗 | 錯誤密碼 | 留下無快照專案；訊息為認證失敗 | 不顯示清單 |
| 連不上主機 | 無法到達的主機 | 留下無快照專案；訊息為連不上主機 | 不顯示清單 |
| 沒有目錄權限 | 連上但讀不到目錄 | 留下無快照專案；訊息為沒有讀取目錄的權限 | 不顯示清單 |
| 類型不符 | 類型與內容不符 | 留下無快照專案；訊息為類型與連線內容不符 | 不顯示清單 |
| 再開啟 | 已有成功快照 | 清單與文件一致，不重查筆數 | 秘密讀失敗時不把清單清成零張表 |
| 筆數部分失敗 | 某一表筆數失敗 | 該列為未知，其他表仍在 | 不中止整份盤點 |
| 檢視表 | 開關預設關 | 清單不含檢視表 | 打開開關才出現，且確認狀態不適用 |

</frozen-after-approval>

## Open Questions

- 解析專案寫在哪裡、首頁如何開啟已有的一份 — options: 表單填本機資料夾，再開啟時指定同一路徑 (分析師自己決定位置，複製時找得到) / 固定寫入本機應用資料夾，首頁列出其中的專案 (不必選路徑，複製時要到該資料夾找)

## Code Map

- `ExportData/SqlGen/ISqlGenerater.cs` -- 目錄方法可重用：`GetTableSchemaAsync`、`GetTableRelationsAsync`、`GetDatabaseInfoAsync`、`GetSqlAllTableNameList`。`GetSqlRecords` 只留給匯出。
- `ExportData/DbService.cs` -- `GetTableSchemaAsync`、`GetTableRelationsAsync` 可被 Web 轉接器呼叫。不要走 `GetDataSetAsync`。
- `ExportData/Models/Database/DatabaseModels.cs` -- `TableSchema`、`ColumnInfo`、`TableRelation` 是目錄形狀。快照由核心另存，不把這些型別當解析專案。
- `ExportData/Program.cs`、`ExportData/ExportService` -- 命令列 CSV。不改匯出流程，不引用核心。
- `ExportDataWeb/Pages/Index.cshtml` 與 `Index.cshtml.cs` -- 現以匯出為主。改成解析專案首頁。
- `ExportDataWeb/Program.cs` -- 現無 localhost 硬綁。要改成只聽 localhost。
- `ExportDataWeb/Properties/launchSettings.json` -- 開發位址已是 localhost，不能改成對外位址。
- `ExportData/DeIdentificationService.cs` -- 空殼。遮罩不用它。

## Tasks & Acceptance

**Execution:**
- [ ] `ExportData.Core/ExportData.Core.csproj` -- 新增 `net10.0` 類別庫；定義目錄、儲存、秘密庫、分析包寫出、起草五個連接埠；實作 JSON 儲存與盤點快照 -- 核心不引用 ASP.NET、Dapper、驅動或 DPAPI
- [ ] `ExportData.Core.Tests/ExportData.Core.Tests.csproj` -- 為 I/O 矩陣的儲存與失敗留下行為寫測試：無快照、暫存改名、文件不含連線字串、檢視表預設排除、筆數未知 -- 先紅後綠
- [ ] `ExportData/ExportData.csproj` -- 目標框架改 `net10.0`，不加入對核心的參考 -- 與 AD-8 對齊且命令列維持獨立
- [ ] `ExportDataWeb/ExportDataWeb.csproj` 與 `ExportDataWeb/Catalog/` -- 目標框架改 `net10.0`；目錄轉接只呼叫代碼地圖中的目錄方法；秘密庫用 `System.Security.Cryptography.ProtectedData` 10.0.12 寫同目錄附檔 -- 網頁是唯一驅動轉接器
- [ ] `ExportDataWeb/Program.cs` 與 `ExportDataWeb/Pages/Index.cshtml` -- Kestrel 只聽 localhost；無專案時顯示工作描述與連線表單；成功顯示清單與進度初值；失敗顯示四種原因之一 -- 對應 UJ-1 的連線與盤點，不含下一張未看
- [ ] `ExportDataProjects.sln` -- 納入核心、核心測試與 `ExportDataWeb` -- 方案目前只有命令列專案

**Acceptance Criteria:**
- Given 命令列專案，when 建置，then 成功且不參考 `ExportData.Core`。
- Given 網頁行程，when 檢查監聽位址，then 只有 localhost。
- Given 測試失敗或成功，when 讀取解析專案 JSON，then 沒有連線字串或密碼。
- Given 已成功盤點後重新開啟，when 顯示清單，then 筆數與確認狀態來自文件，且不對舊庫重查筆數。

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

失敗仍建立 JSON（顯示名稱與資料庫類型、無快照）並寫入秘密附檔，讓同一份專案可再測。連線字串不進 JSON。無法歸類的連線錯誤呈「類型與連線內容不符」，並附去除連線字串的原文。檔名主檔與附檔同主檔名：`.analysis.json` 與 `.secret`。同一資料夾內顯示名稱重複則拒絕並說明。清單列在這一則不能進工作台。

## Verification

**Commands:**
- `dotnet test ExportData.Core.Tests/ExportData.Core.Tests.csproj` -- expected: 通過
- `dotnet build ExportData/ExportData.csproj` -- expected: 成功且專案檔無 `ExportData.Core` 參考
- `dotnet build ExportDataWeb/ExportDataWeb.csproj` -- expected: 成功
