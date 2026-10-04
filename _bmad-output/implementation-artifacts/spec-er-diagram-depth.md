---
title: '資料表明細的可調層數實體關係圖'
type: 'feature'
created: '2026-10-04'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 從資料表清單打開 `TableDetail` 時，網址只有表名，連線留在工作台那次送出裡。明細頁因此出現「請提供完整的資料表名稱和連線資訊」，而且「關聯」是清單，不是實體關係圖。使用者要在這一頁看到 ER 圖，層數可設成一層或 n 層。

**Approach:** 工作台連線成功後，把資料庫類型與連線字串放進伺服器工作階段。打開資料表時用這份連線畫 crow's foot 實體關係圖，預設一層，可用數字改成 n 層。結構與範例仍留在同一頁，但進來先看到圖。

## Boundaries & Constraints

**Always:**
- 層數是 1 以上的整數，預設 1。1 層只含目前表與宣告外鍵直接相連的父表、子表。n 層沿宣告外鍵再走 n 步。已走過的表不重複展開。
- 只畫目錄裡的宣告關聯。線兩端都在選中的表集合裡才畫。沒有關聯時圖上只有目前表，並寫「目錄中沒有宣告關聯」。
- 記法是 crow's foot。節點顯示表名，以及主鍵與外鍵欄位。父端基數維持 `1`。推不出子端時標「未明」，不畫成多對多，也不標 `*`。
- 點圖上其他表名會打開該表，並沿用目前層數。
- 連線字串只放工作階段，不進網址，也不寫進頁面 HTML。
- 圖、結構、範例任一塊失敗，另外的塊仍可用，並在失敗的那一塊寫原因。

**Never:**
- 不改 `TableRelation` 五欄，不改五種 SqlGen 的關聯查詢，不改 `ExportData.Core`。
- 不畫推測關聯，不做「全部欄位」切換，不做拖曳存座標，不做模組圖或 UML。
- 不把密碼或完整連線字串放進 query string 或隱藏欄位。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 一層 | 中心 `Orders`；`Customers←Orders←OrderLines` | 節點只有 Orders、Customers、OrderLines | N/A |
| n 層 | 同上，層數 2 | 再納入與這三張直接相連的表；不會無限擴到無關的表 | N/A |
| 環 | A→B→A，層數 5 | 只有 A 與 B | N/A |
| 無宣告關聯 | 中心表沒有任何外鍵連到選中集合 | 只有中心表，文案「目錄中沒有宣告關聯」 | N/A |
| 非法層數 | 0、負數、空白、非整數 | 改回 1 再畫 | 不查庫失敗 |
| 無工作階段連線 | 直接打開明細網址 | 圖區說明要先回工作台連線；不出現「請提供完整的資料表名稱和連線資訊」 | 不連資料庫 |
| 關聯讀取失敗 | 連線在，關聯查詢丟出例外 | 圖區寫失敗原因；結構與範例仍可載入 | 記下日誌，頁面不整頁失敗 |
| 鄰表結構讀取失敗 | 關聯在，某一張表的結構失敗 | 該節點仍在，欄位標成讀不到；線的子端基數為未明 | 其他節點照常 |

</frozen-after-approval>

## Open Questions

- 子端基數的唯一性 — 現有目錄查詢沒有填 `IndexInfo`，唯一索引讀不到。選項：只依主鍵與可否空值（非主鍵的唯一外鍵會畫成 `0..*` 或 `1..*`）／這次一併讓五種資料庫讀出唯一索引（一對一外鍵的基數才符合 FR-12，範圍會擴到每個 SqlGen）。

## Code Map

- `ExportData/Models/Database/DatabaseModels.cs` — `TableRelation`（35–42 行）五欄契約不可改。`ColumnInfo.IsPrimaryKey` / `IsNullable` / `IsForeignKey` 供節點與基數。`IndexInfo` 模型在，但 SqlGen 沒填。
- `ExportData/DbService.cs` — `GetTableRelationsAsync`（96–112）整庫外鍵；`GetTableSchemaAsync`（78–94）單表。圖的層數在記憶體裡切，不新增 SQL。
- `ExportData/SqlGen/GenSqlForPostgreSql.cs` — `ToTableRelation`（309–324）是五欄對應的既有測試錨點。其他四種產生器回傳同一形狀。不要改它們。
- `ExportDataWeb/Pages/Index.cshtml` — 約 366 行，`/TableDetail?tableName=` 只帶表名。維持這樣。
- `ExportDataWeb/Pages/Index.cshtml.cs` — 工作階段目前只有 `exportdata.connection.password` 與 `exportdata.connection.identity`（52–53）。`OpenConnectionAsync` 成功時才可多記類型與連線字串。
- `ExportDataWeb/Pages/TableDetail.cshtml.cs` — `TableName` 可從 query 綁定（21–22）。`ConnectionString` 沒有 `SupportsGet`（24–28）。「請提供完整的資料表名稱和連線資訊」在 `OnPostAsync`（48–50）。關聯目前是整庫後再一層過濾（150–157）。
- `ExportDataWeb/Pages/TableDetail.cshtml` — 關聯區是清單（186–239），隱藏欄位會把連線字串送回頁面（37–38）。改成圖與層數欄，拿掉連線隱藏欄。
- `ExportDataWeb/wwwroot/css/site.css` — `.detail-bar` 起於 625 行。沒有 ER 圖樣式，沿用現有色彩變數。
- `ExportData.Tests/` — 新純函式測試放這裡。`PostgreSqlGeneratorTests.ToTableRelation` 不要改斷。

## Tasks & Acceptance

**Execution:**
- [ ] `ExportData/ErNeighborhood.cs` -- 新增純函式：依中心表與層數從 `TableRelation` 收斂節點與邊，並依父端 `1`、子端主鍵／可空／未知算出基數 -- 層數與環不要散落在頁面
- [ ] `ExportData.Tests/ErNeighborhoodTests.cs` -- 覆蓋 I/O 矩陣的一層、n 層、環、無關聯、非法層數，以及子端未明 -- 不連資料庫
- [ ] `ExportDataWeb/Pages/Index.cshtml.cs` -- 連線成功後把資料庫類型與連線字串寫入工作階段 -- 明細頁才連得到同一座庫
- [ ] `ExportDataWeb/Pages/TableDetail.cshtml.cs` -- 開啟時讀工作階段、載入關聯與選中表的結構、套用層數；缺連線或讀取失敗只影響對應區塊 -- 清單點進來就能畫圖
- [ ] `ExportDataWeb/Pages/TableDetail.cshtml` -- 預設顯示實體關係圖、層數輸入、crow's foot 與可點的表名；結構與範例保留 -- 這一頁進來先看到圖
- [ ] `ExportDataWeb/wwwroot/css/site.css` -- 圖面、目前表節點、線與基數標籤 -- 跟現有工作台同一套色彩

**Acceptance Criteria:**
- Given 工作台已連線並列出資料表，when 點「開啟」，then 明細頁直接畫出該表的一層實體關係圖，且網址與 HTML 都沒有連線字串。
- Given 圖已顯示，when 把層數改成 n 並送出，then 圖含 n 步內的宣告鄰居，不含更遠的表。
- Given 尚未連線就打開明細，when 頁面載入，then 圖區說明先回工作台連線，不出現「請提供完整的資料表名稱和連線資訊」。

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

層數是從中心表出發的步數，不是畫面上的列數。`Orders` 連到 `Customers` 與 `OrderLines`，`OrderLines` 再連到 `Products`：層數 1 沒有 `Products`，層數 2 才有。兩張已選入的表之間若另有宣告外鍵，那條線也畫。同一約束的多欄合成一條線；子端基數不一致時整條標未明。

沒有座標就自動排：中心在左，步數往右排。不寫回任何檔案。

## Verification

**Commands:**
- `dotnet test ExportData.Tests/ExportData.Tests.csproj --filter ErNeighborhoodTests` -- expected: 通過

**Manual checks (if no CLI):**
- 工作台分析後從清單打開一張有外鍵的表，確認先看到圖、層數 1 與改成 2 的差異，以及點鄰表會帶著同一層數打開。
