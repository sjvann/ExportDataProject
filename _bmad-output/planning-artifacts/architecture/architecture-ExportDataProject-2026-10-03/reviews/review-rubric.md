# 架構骨架檢核 — rubric walker

- 對象：`ARCHITECTURE-SPINE.md`（未修改）
- 高度：feature（下一層是史詩）
- 棕地：`ExportData`（`net9.0-windows`）、`ExportDataWeb`（`net9.0`，Razor Pages，專案參考 `ExportData`）
- 父骨架：無。此項不評分。
- 日期：2026-10-03

## Verdict

**needs-fixes**

確認狀態、單一快照、單一遮罩、單一組裝器、localhost 單行程這些不變量釘得住。下一層仍會在目錄 SQL 的包裝範圍、四種資料庫的識別字規則，以及目標框架／`System.Data.SqlClient` 的實際釘法上做出不相容選擇。

## 檢核摘要

| 檢核 | 結果 |
| --- | --- |
| 釘住史詩會分歧的點，且沒漏掉不相容選擇 | 未過。見 F1、F2、F5 |
| 每條 AD 的 Rule 可執行，且擋得住 Prevents | 未過。見 F1。其餘 AD 的 Prevents 擋得住 |
| Deferred 沒有「兩個單元仍會各做各的」 | 過。數字契約的分類問題見 F6，不構成各做各的 |
| 點名技術有版本且與棕地一致 | 未過。見 F3、F4、F7。Dapper 2.1.35、System.Data.SQLite 1.0.118、MySql.Data 8.3.0、Oracle.ManagedDataAccess.Core 3.21.140 與兩個 csproj 一致 |
| 父骨架繼承 | 不適用，不扣分 |
| feature 高度的結構維度皆已決定、已延後或是開放問題 | 過。部署與執行環境、基礎設施、營運不是整塊沉默 |
| 骨架短；整段抄 PRD 且沒有新不變量才標 medium | 過。沒有整段抄入。不要求再抄 FR |

## 已涵蓋、不列為發現

- 範式是 ports and adapters。`ExportData.Core` 擁有確認狀態、基數、遮罩、投影、分析包與專案檔；`ExportDataWeb` 是唯一驅動轉接器；命令列不引用核心。這擋住「頁面、命令列、各產生器各存一份結論」。
- AD-2、AD-3、AD-4、AD-6、AD-7、AD-9、AD-10、AD-11 的 Rule 可對著專案參考、命令清單、單一函式、固定檔名與詞彙對照執行，且對得上各自的 Prevents。允許轉換、字典欄、FR-12／FR-14 細節用 PRD 引用，沒有把 FR 再抄進來。
- AD-8 擋得住「命令列另存一份解析專案」與「聽在區網」。Kestrel 只繫結 localhost、沒有帳號、沒有第二環境。
- 執行環境是分析師自己的機器；沒有雲端供應商；日誌在本機行程且受 AD-6 約束；不做排程備份，專案檔由分析師複製。部署、基礎設施、營運都有決定，不是沉默。
- Deferred 的佈局程式庫、起草廠商與離線、文書處理檔、外鍵密度、驅動升級時機、不讀原始碼或預存程序、PNG、跨工作階段座標、改名配對、中間表折疊、多人編輯，都有不變契約或明確非目標，不會讓兩個史詩各做各的。
- 棕地確認：四種 `ISqlGenerater`（SQLite、SQL Server、MySQL、Oracle）、`MapRazorPages`、`SqlAllTable`／`SqlOneTable`／`Size` 只存在命令列設定與 `GetSqlRecords`。

## 發現

### F1 — 目錄連接埠的包裝範圍擋不住 AD-5 的 Prevents

- **嚴重性：** high
- **檢核：** Rule 必須擋得住 Prevents；史詩不得做出不相容選擇
- **處置：** discuss（寫進 AD-1 或 AD-5，不要留成讀法）

AD-1 寫目錄讀取「包裝既有 `ISqlGenerater`」。該介面的 `GetSqlRecords` 就是 `SELECT … config.Size`（SQL Server 為 `TOP`，Oracle 為 `rownum`）。`DbService` 在設定檔的 `SqlAllTable`／`SqlOneTable` 非空時，會用這兩段任意 SQL 覆蓋產生器。

AD-5 的 Prevents 是「工作台接受任意 SQL，或範例列數沿用匯出的 Size」。Rule 後段雖寫這三項只留在命令列，但沒有禁止轉接器呼叫 `GetSqlRecords`，也沒有禁止沿用 `DbService` 的覆蓋。盤點史詩可以把整個介面交給工作台；匯出史詩可以堅持 Size 只留在命令列。兩條 Rule 同時成立時，Prevents 擋不住。

### F2 — 四種資料庫的識別字規則沒有內容

- **嚴重性：** high
- **檢核：** 漏掉會讓兩個史詩不相容的決定
- **處置：** discuss

AD-5 要求連線成功時把「識別字規則」寫進解析專案，前綴比較用它；AD-9 的重新讀取對齊「比較規則同 AD-5」。規則本體沒寫，也不在 Deferred。

棕地已經分叉：`GenSqlForOracle` 查綱要時對表名 `ToUpper()`；`GenSqlForSqlServer.GetSqlAllTableNameList` 把前綴直接拼進 `LIKE`。前綴篩選史詩與重新讀取史詩可以各存一套（摺大小寫、定序名稱、或 Oracle 全大寫），同一欄的用途在重新讀取後會被當成消失。引號同樣沒按方言釘（`[]`、`"`、`` ` ``），範例查詢史詩與盤點史詩會各加一層。

### F3 — 目標框架與 windows TFM 和棕地相反，DPAPI 沒有套件版本

- **嚴重性：** high
- **檢核：** 點名技術有版本且與棕地一致
- **處置：** discuss

倉庫現況：`ExportData.csproj` 為 `net9.0-windows`，`ExportDataWeb.csproj` 為 `net9.0`。骨架 Stack 釘 .NET 10.0.12，AD-8 規定 Web 與命令列都用 `net10.0`，且都不用 `net10.0-windows`。AD-6 同時要求 Windows DPAPI 附檔，沒有點名 `System.Security.Cryptography.ProtectedData` 或版本。

遵守 AD-8 的史詩會拿掉 `-windows` 並另找 DPAPI 套件；修命令列編譯的史詩會沿用 `net9.0-windows` 或改回 `net10.0-windows`。這是骨架內部與棕地 csproj 的衝突，不是兩個史詩可以各自合法的開放問題。

### F4 — System.Data.SqlClient 被點名但沒有版本，Deferred 的「倉庫釘選」不存在

- **嚴重性：** high
- **檢核：** 點名技術有版本且與棕地一致
- **處置：** autofix

`GenSqlForSqlServer` 與 `DbService` 使用 `System.Data.SqlClient`。兩個 csproj 都沒有對應的 `PackageReference`。骨架 Stack 如實寫了「無 PackageReference 釘選」，卻沒有版本號。Deferred 又寫「驅動仍用倉庫釘選，直到建置證明不相容」，並把是否換成 `Microsoft.Data.SqlClient` 延後。

倉庫沒有可沿用的版本。目標框架若升到 `net10.0`，這個參考必須變成明確套件。一個史詩會加某個 `System.Data.SqlClient` 版本，另一個會提前換成 `Microsoft.Data.SqlClient`。Deferred 這條目前讓兩個單元各做各的，因為「釘選」沒有對象。

其餘已點名的驅動版本與 csproj 一致，這條不牽連它們。

### F5 — v1 的 UML 產出檔名既未決定也未延後

- **嚴重性：** medium
- **檢核：** 結構決定要嘛已決定、已延後、或是開放問題；漏掉的分歧點
- **處置：** defer

AD-7 把分析包檔名固定為 `cover.md`、`dictionary.csv`、`relations.csv`、`open-items.csv`、`er.svg`，並寫 MVP 不產出 UML。AD-4 與 AD-11 仍要求 UML 與實體關係圖同一投影。FR-16 史詩與分析包史詩沒有共同檔名（例如是否為 `uml.svg`），Deferred 也沒寫。MVP 可以不動檔；v1 接縫上兩個史詩會各取檔名。

### F6 — 已是契約的數字放在 Deferred

- **嚴重性：** low
- **檢核：** Deferred 的分類
- **處置：** autofix

20／100 列、逾時 30 秒、全庫圖 40 張、清單 500 張、鄰居圖逾時行為，正文寫明已是契約、不得另開第二套上限。把它們放在 Deferred，下一層會讀成尚未決定。建議只留在 AD-5 與慣例；Deferred 若要保留，只留「第一座真實舊庫再覆核」這個條件，並註明覆核前不得改數字。

此條本身沒有讓兩個單元各做各的，因為同段已禁止第二套上限。

### F7 — 命令列既有套件沒進 Stack

- **嚴重性：** low
- **檢核：** 點名技術與棕地一致
- **處置：** discuss

`ExportData.csproj` 另釘 `Microsoft.Extensions.Hosting` 8.0.0、`Microsoft.Extensions.Logging` 8.0.0、`Microsoft.Extensions.Logging.Console` 8.0.0、`System.CommandLine` 2.0.0-beta4.22272.1。骨架的 Stack 沒有這些名字。AD-8 把命令列一起升到 `net10.0` 時，8.0.0 的 Extensions 套件與新目標框架可能不合，碰到命令列的史詩會各自升級。若命令列依賴維持不動，應在 Stack 補上版本，並寫明隨 TFM 遷移時的條件。

## 不要求的事

不要把 FR-9、FR-12、FR-14、FR-20 的條文再抄進骨架。現有的「以該 FR 為準」加上單一命令／單一函式／不增刪欄，已經是不變量。缺的是 F1、F2、F3、F4 那些 PRD 沒寫、史詩卻會分叉的實作邊界。
