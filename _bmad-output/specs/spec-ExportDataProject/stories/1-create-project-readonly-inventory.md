---
title: '建立解析專案並唯讀盤點'
type: 'feature'
created: '2026-10-03'
status: 'done'
route: 'dispatch'
baseline_commit: '45ea42e9ef65e8cefede52392b53cdeff99a6c45'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-ExportDataProject/SPEC.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-ExportDataProject-2026-10-03/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 還沒有解析專案這份文件，後續盤點與首頁無法把結論留在同一處。

**Approach:** 新增 `ExportData.Core`，只實作解析專案文件與原子寫入。呼叫端傳入資料夾。目錄讀取、秘密庫與首頁留到後續批次。

## Boundaries & Constraints

**Always:**
- 文件是一份 JSON，暫存檔再改名。欄位只有顯示名稱、資料庫類型、識別字規則、是否包含檢視表、資料表快照、宣告關聯，以及空的推測關聯、模組、結構變更。
- 資料表快照含綱要、表名、約略筆數、宣告關聯數、欄位數、確認狀態。新快照的確認狀態皆為未看。檢視表預設不進入快照。
- 約略筆數失敗的列仍在，筆數為未知。呼叫端給定的資料夾內，同一顯示名稱已有檔案則拒絕寫入。
- 核心不引用 ASP.NET Core、Dapper、資料庫驅動或 DPAPI。

**Never:**
- 文件型別沒有連線字串、密碼或儲存格。這一則不寫 `.secret`，不連資料庫，不改 `ExportData` 或 `ExportDataWeb`。
- 不實作目錄轉接、四種連線錯誤、首頁、下一張未看、工作台或分析包。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 新專案 | 空資料夾、顯示名稱 | 寫出 `.analysis.json`，集合為空 | N/A |
| 不含檢視表 | 一張表與一張檢視表，開關關 | 快照只有那張表 | N/A |
| 包含檢視表 | 同上，開關開 | 兩者都在；檢視表確認狀態不適用 | N/A |
| 筆數未知 | 某一列筆數失敗 | 該列留下，筆數為未知 | 不丟掉該列 |
| 名稱重複 | 同資料夾已有同名檔 | 不覆寫既有檔 | 拒絕並說明 |
| 讀回 | 剛寫入的檔 | 與寫入內容一致 | 檔案不存在則失敗，不產生空快照 |

</frozen-after-approval>

## Code Map

- `ExportData/Models/Database/DatabaseModels.cs` -- `TableSchema`、`ColumnInfo`、`TableRelation` 是目錄形狀。核心快照另定型別，不引用這個專案。
- `ExportData/ExportData.csproj` -- 維持現狀。這一則不改目標框架，也不加入對核心的參考。
- `ExportDataWeb/` -- 這一則不改。

## Tasks & Acceptance

**Execution:**
- [x] `ExportData.Core/ExportData.Core.csproj` -- 新增 `net10.0` 類別庫與解析專案型別、由目錄列組成快照的函式、原子寫入的儲存 -- 讓後續批次依賴同一份文件
- [x] `ExportData.Core.Tests/ExportData.Core.Tests.csproj` -- 用 xUnit 覆蓋 I/O 矩陣 -- 先紅後綠
- [x] `ExportDataProjects.sln` -- 納入上述兩個專案 -- 方案目前只有命令列

**Acceptance Criteria:**
- Given 寫入後的 JSON，when 搜尋連線字串欄位，then 型別與檔案都沒有該欄。
- Given 寫入中斷於暫存檔尚未改名，when 讀取正式檔，then 仍是改名前的內容或檔案不存在。
- Given `ExportData.csproj`，when 這一則完成，then 其內容未被修改。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- low — `DisplayNameRules` 連前導空白也拒絕，訊息只寫結尾。`displayName != displayName.Trim()` 會擋下前導空白。直接改訊息。
- low — 保留裝置名與過長暫存檔名會丟出未包裝的 `IOException`。日常顯示名稱不會是 `CON`。修法要另加裝置名清單與長度分支，拒絕。
- low — 約束名稱只拒絕 null，空白仍可寫入。規格沒要求非空白約束名。修法是新的拒絕條件，拒絕。
- low — 約略筆數的負數可寫入。規格只把空值定義為未知。修法是新的範圍檢查，拒絕。
- low — 相同表名的目錄列可重複進快照。規格沒要求合併。修法要另做去重，拒絕。
- low — `Save` 不重算關聯數，集合在 `Create` 之後仍可改。日常路徑是寫入後立即儲存。修法要改成不可變集合，拒絕。
- low — `approximateRowCount` 與 `confirmationStatus` 省略時會變成 null。這和規格的未知筆數、檢視表空狀態是同一表示。不另加必填。
- false — `Load` 不核對檔內顯示名稱。讀回規格只要求與寫入內容一致；用原顯示名稱讀回時檔名與內容相同。
- low — 資料夾不存在時，`Save` 與 `Load` 的例外類型不同。兩者都失敗，且不會造出空快照。改訊息要加分支，拒絕。
- false — 名稱為「寫入中斷且正式檔已存在」的測試沒有呼叫中斷的 `Save`。`Save` 在正式檔已存在時於寫暫存檔前拒絕；`DuplicateDisplayName_RefusesWithoutOverwrite` 已斷言位元組不變。
- false — 這一則不能沿用「先寫暫存再取代既有正式檔」。規格要求同名檔拒絕覆寫，沒有取代路徑。
- low — 當機留下的 `.tmp` 下次成功寫入不會清掉。成功路徑用新的暫存檔名。另做清理會加行為，拒絕。
- false — 識別字規則存不下 SQL Server 定序。這一則的意圖排除目錄轉接；定序由呼叫端之後寫入，設計註記只列三種規則。
- medium — `OrdinalIgnoreCase` 的關聯計數沒有大小寫不同的斷言。把該分支改成區分大小寫，現有 11 項測試仍會通過。依驗證缺口層的 `patch` 補測試。
- low — 禁止秘密欄位的測試只做完整名稱相等，`DbPassword` 不會失敗。目前型別與 JSON 鍵沒有這些欄。擴充黑名單是開放清單，拒絕。
- false — 補上推測關聯等欄位後舊檔會失敗。`UnmappedMemberHandling.Disallow` 拒絕的是多出來的 JSON 成員；舊的空物件仍可對上後來新增的可選屬性。
- false — 快照沒有欄位型別、文件沒有版本、五個連接埠未出現。凍結的欄位清單沒有這些項目；修法是改規格或做出這一則禁止的目錄轉接。
- low — `deferred-work.md` 用絕對路徑，且仍寫約 2300 token。這是流程紀錄，不是分析師會讀的檔。拒絕。
- low — 未定義的資料庫類型、識別字規則、目錄種類可被轉成列舉後寫入。呼叫端要先做非法轉型。加 `Enum.IsDefined` 是新分支，拒絕。
- low — 只含空白的綱要可寫入，之後對不上關聯。SQLite 的空字串綱要是合法值。拒絕只含空白要加條件，拒絕。
- low — 目標路徑若是同名目錄，`File.Move` 會丟 `IOException`。顯示名稱與目錄同名不是日常路徑。拒絕。
- low — 刪除暫存檔若失敗，會蓋掉原本的寫入錯誤。`File.Delete` 失敗不是這次測試或日常寫入會碰到的情況。加 try 會多一層，拒絕。
- low — 無效 UTF-8 會被解碼後再解析。規格的讀回是對剛寫入的 UTF-8。改用拋錯的編碼器是新行為，拒絕。
- low — 讀回後不檢查確認狀態是否符合種類，也不檢查關閉檢視表時仍有檢視列。這些是手改 JSON。規格的讀回是自己寫出的檔。拒絕。

## Design Notes

檔名為 `{顯示名稱}.analysis.json`。約略筆數用可空整數，空值表示未知。確認狀態在檔內以 AD-10 的四個詞儲存：未看、草稿、已確認、略過。檢視表的確認狀態欄留空。寫入先寫同資料夾暫存檔，完成後再改成正式檔名。

## Verification

**Commands:**
- `dotnet test ExportData.Core.Tests/ExportData.Core.Tests.csproj` -- expected: 通過
- `dotnet build ExportData/ExportData.csproj` -- expected: 成功，且專案檔與這一則開始前相同
