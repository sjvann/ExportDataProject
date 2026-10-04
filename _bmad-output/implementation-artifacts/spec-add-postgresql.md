---
title: '匯出工具支援 PostgreSQL'
type: 'feature'
created: '2026-10-04'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'ad554b82dbd3fa6787ded36629eae217be6f6c22'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `EnumDbType.PostgreSql` 已存在，但 `DbChoicer` 回傳 null。命令列與網頁只列四種資料庫，匯出工具讀不了 PostgreSQL。

**Approach:** 用 Npgsql 10.0.3 補上與另外四種相同的唯讀讀取面，並讓命令列與網頁能選 PostgreSQL。

## Boundaries & Constraints

**Always:**
- 只發目錄查詢與 `SELECT … LIMIT {Size}`。`Size` 沿用既有設定。
- 表清單是單一文字欄，值為 `綱要.表名`。後續查詢拆開後以雙引號括起。
- 排除 `pg_catalog`、`information_schema`，以及名稱以 `pg_toast` 開頭的綱要。
- 前綴只比對表名，小寫比較，單引號跳脫成兩個單引號。
- Npgsql 10.0.3 加在 `ExportData` 與 `ExportDataWeb`。

**Never:**
- 不改 `ExportData.Core` 及其測試。不改另外四種產生器，不改 `SqlAllTable`／`SqlOneTable` 覆寫，不改去識別化。
- 不改 `ExportDataWeb/Program.cs` 現有未提交內容，也不改已定稿的 PRD、架構、UX。
- 不產生寫入或 DDL。沒有可連的 PostgreSQL 時，不把建置成功說成已讀真實庫。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 選到 PostgreSQL | `DbType` 為 `PostgreSql` | 回傳 `GenSqlForPostgreSql` | 連線失敗沿用既有例外，不以空清單冒充沒有表 |
| 表清單 | 有使用者表、檢視與系統綱要 | `Table` 只列基礎表，`View` 只列檢視；名稱 `綱要.表名`；系統綱要不出現 | 失敗時 `DbService` 仍回傳 null |
| 匯出列 | `public.orders`，`Size` 20 | `SELECT * FROM "public"."orders" LIMIT 20` | 缺綱要或空段時不發出語句 |
| 結構與外鍵 | 欄含型別、長度或精度、預設值、主鍵、外鍵 | 填入既有 `ColumnInfo` 與 `TableRelation`；兩端同為 `綱要.表名` | 無預設值或外鍵時該格為空 |

</frozen-after-approval>

## Code Map

- `ExportData/SqlGen/GenSqlForMySql.cs` — 複用方法形狀與 `LIMIT`。不要複用反引號或 `REFERENCED_TABLE_NAME`。
- `ExportData/SqlGen/DbChoicer.cs` — `default` 回傳 null。`EnumDbType.PostgreSql` 已存在，不要改順序。
- `ExportData/DbService.cs` — 清單必須是單一文字欄。`PromptForDatabaseType` 與 `Index.cshtml` 只有四種。
- `ExportData.Core/**` — 不要改。其測試禁止參考 `ExportData`。

## Tasks & Acceptance

**Execution:**
- [x] `ExportData/ExportData.csproj`、`ExportDataWeb/ExportDataWeb.csproj` — 加入 `Npgsql` 10.0.3
- [x] `ExportData/SqlGen/GenSqlForPostgreSql.cs` — 實作 `ISqlGenerater`。外鍵經 `referential_constraints` 連到 `constraint_column_usage`。主鍵用 `constraint_type = 'PRIMARY KEY'`
- [x] `ExportData/SqlGen/DbChoicer.cs` — `PostgreSql` 建立該產生器
- [x] `ExportData/Services/InteractiveCliService.cs` — 選項 5
- [x] `ExportDataWeb/Pages/Index.cshtml` — 選項值 `PostgreSql`
- [x] `README.md` — 補類型與 `Host=localhost;Port=5432;Database=database;Username=user;Password=password`
- [x] `ExportData.Tests` 與 `ExportDataProjects.sln` — 新測試專案參考 `ExportData`，不連線即鎖住清單 SQL、`LIMIT` 引號與缺綱要

**Acceptance Criteria:**
- Given `DbType` 為 `PostgreSql`，when 建立產生器，then 型別是 `GenSqlForPostgreSql`。
- Given 命令列與網頁，when 查看類型，then 看得到 PostgreSQL，值為 `PostgreSql`。

## Implementation Notes

- 清單、`LIMIT` 引號、缺綱要與連線失敗都由 `ExportData.Tests` 鎖定，沒有連上 PostgreSQL。格式錯誤的連線字串在 `NpgsqlConnection` 建構時丟出 `ArgumentException`，`DbService.GetTableNamesAsync` 回傳 null。
- 結構與資料庫資訊只查 `information_schema`。不對使用者表發 `COUNT(*)`，`TableSchema.RowCount` 維持 0。
- 複合外鍵另以 `position_in_unique_constraint` 對齊 `constraint_column_usage`，避免笛卡兒積。子欄仍經 `key_column_usage`。
- 匯出名稱以每個 `.` 拆段，空段不發語句。表名本身若含句點，會被拆成多個識別字。結構查詢只接受恰好兩段。

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet test ExportDataProjects.sln` -- expected: 既有測試與新的 SQL 字串測試通過
- `dotnet build ExportDataProjects.sln` -- expected: 建置成功

**Manual checks (if no CLI):**
- 網頁類型選單出現 PostgreSQL。沒有伺服器時，不宣稱已讀到真實目錄。
