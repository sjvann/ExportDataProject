---
title: '匯出工具支援 PostgreSQL'
type: 'feature'
created: '2026-10-04'
status: 'done'
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
- 審查後：前綴的 `\`、`%`、`_` 先跳脫再加 `ESCAPE '\'`；只有 `View` 列檢視；匯出也只接受恰好兩段；外鍵子查詢 `DISTINCT ON` 每個欄位一列；`Open()` 失敗先釋放連線。方案平台只留 Any CPU。

## Spec Change Log

## Review Triage Log

- `low` — `.ai_project/pids/ExportDataWeb.pid` 從基準起被刪。內容是已結束的行程編號。還原不會讓 PostgreSQL 匯出變正確，使用者日常也不會讀這個檔。
- `low` — `ExportDataProjects.sln` 新增 x86／x64，建置仍指向 Any CPU。預設 `dotnet build` 不走這兩個平台；刪掉才是直接修正，見 patch。
- `medium` — `BuildTableListSql` 只把單引號加倍。前綴裡的 `%`、`_`、`\` 仍是 LIKE 萬用字元，`a_` 會命中 `ab`。字面比對表名因此不成立。
- `low` — 前綴用 .NET `ToLowerInvariant()`，比對用 PostgreSQL `lower()`。土耳其文 İ 可能不一致。一般表名遇不到，另做文化轉換會加分支。
- `low` — 清單未排除 `pg_temp%`。新連線通常看不到別的工作階段暫存表，排除名單若要加長就得改已凍結的三個綱要。
- `false` — `FOREIGN TABLE` 與具體化檢視沒被 `Table`／`View` 列出。規格只要求基礎表與檢視，這是依規格的結果。
- `medium` — 分割子表在 `information_schema` 也是 `BASE TABLE`，匯出父表與子表會重複列。規格寫明 `Table` 列基礎表，排除子表等於改凍結規則，不在這一批改。
- `medium` — 欄位目錄的外鍵 LEFT JOIN 在同一欄有多個外鍵時會產生多列 `ColumnInfo`。`GetTableSchemaAsync` 對查詢結果逐列 `Add`，沒有合併。
- `low` — 沒有選 `datetime_precision` 與 `udt_name`。規格要填的是既有 `ColumnInfo` 的 `data_type`、長度與數值精度；列舉顯示成 `USER-DEFINED` 是目錄原值。
- `medium` — `TryQuoteQualifiedName` 接受三段以上，`GetTableSchemaAsync` 只接受兩段。`public.orders.extra` 會發出 `"public"."orders"."extra"`，結構卻是 null。規格的名稱是 `綱要.表名`。
- `false` — `Public.Orders` 對不到 `public.orders`。清單回傳的是目錄裡的儲存名，再依該字串加引號；目錄自己產生的 `public.orders` 會原樣匯出。
- `medium` — `TableType` 為 null 時編成 `VIEW`。省略設定會列出檢視，而不是資料表。
- `medium` — `RowCount` 維持 0，詳細頁把它顯示成記錄數。凍結規則禁止對使用者表 `COUNT(*)`；要顯示真實筆數就得改規格。
- `low` — 網頁 placeholder 與 README 的 JSON 範例仍是 SQL Server。PostgreSQL 連線字串已另有一節；共用範例不是這次選單壞掉。
- `low` — 命令列沒有 PostgreSQL 連線字串提示。它本來就只問一句通用連線字串。
- `medium` — `GetDatabaseInfoAsync` 的四段 SQL 與 `DatabaseType` 沒有測試。拿掉綱要排除，現有測試仍會過。
- `low` — 連線測試只覆蓋建構函式的 `ArgumentException`，沒有覆蓋 `Open()` 失敗。要模擬開線失敗就得有假連線，不是改一行斷言。
- `low` — 結構查詢的空名稱理論沒有 `null` 與 `""`。`TrySplitQualifiedName` 已把兩者當失敗；匯出理論已含這兩筆。
- `low` — 命令列與網頁測試用 `File.ReadAllText`。選項 5 的 `return` 與 `<option value="PostgreSql">` 都在對的位置；要拆掉仍讓字串測試通過，得刻意把標記留在別處。改成整段互動測試會新增測試架。
- `medium` — `GetConnection` 在 `Open()` 失敗時沒有釋放 `NpgsqlConnection`。
- `low` — `Size` 為負數會拼進 `LIMIT`。網頁 `min="1"`。多一個負數分支不是這次示範過的狀態。
- `low` — 名稱分段的前後空白不會被去掉。清單產生的 `綱要.表名` 沒有空白。
- `medium` — 兩段目錄 SQL 的測試沒有斷言 `position_in_unique_constraint`。刪掉對齊條件，現有 `Contains` 仍過，複合外鍵會笛卡兒積。
- `medium` — 沒有測試鎖住識別字裡的雙引號要變成兩個雙引號，也沒有鎖住三段名稱。
- `defer` — `GetTableSchemaAsync` 成功路徑沒有用假資料列跑過 Dapper 組裝。沒有真實 PostgreSQL 時不能說這條已在庫上跑過；SQL 文字與 `ToColumnInfo` 已分開斷言。
- `defer` — 首頁「查看詳細」只帶 `tableName`（`Index.cshtml` 約第 185 行）。`TableDetailModel.DbType` 預設仍是 `SqlServer`，而且這條連結在這次 diff 之前就是這樣，四種資料庫都不會把首頁的類型帶過去。

## Verification

**Commands:**
- `dotnet test ExportDataProjects.sln` -- expected: 既有測試與新的 SQL 字串測試通過
- `dotnet build ExportDataProjects.sln` -- expected: 建置成功

**Manual checks (if no CLI):**
- 網頁類型選單出現 PostgreSQL。沒有伺服器時，不宣稱已讀到真實目錄。
