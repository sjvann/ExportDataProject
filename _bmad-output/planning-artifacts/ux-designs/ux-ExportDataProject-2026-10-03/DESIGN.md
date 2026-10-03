---
name: ExportDataProject
description: 賣給客戶自己接整合專案的本機 Web 產品。客戶沿用賣方的做法：看懂舊系統資料庫，確認欄位用途，交出分析包。
status: final
sources:
  - "{planning_artifacts}/prds/prd-ExportDataProject-2026-10-03/prd.md"
updated: 2026-10-03
colors:
  # [ASSUMPTION] 繼承 ExportDataWeb 已搭載的 Bootstrap 5.3.3。下列為契約要鎖定的值，未列出的語意色沿用 Bootstrap。
  body: "#212529"
  surface: "#FFFFFF"
  canvas: "#F4F7FB"
  next-action: "#0D6EFD"
  status-unread: "#495057"
  status-draft: "#997404"
  status-confirmed: "#146C43"
  status-skipped: "#495057"
  danger: "#DC3545"
  on-action: "#FFFFFF"
  focus: "#258CFB"
  border: "#DEE2E6"
typography:
  body:
    fontFamily: "var(--bs-font-sans-serif)"
    fontSize: 16px
    fontWeight: "400"
    lineHeight: "1.5"
  label:
    fontFamily: "var(--bs-font-sans-serif)"
    fontSize: 14px
    fontWeight: "600"
    lineHeight: "1.4"
  identifier:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"
    fontSize: 14px
    fontWeight: "600"
    lineHeight: "1.4"
rounded:
  sm: 0.25rem
  md: 0.375rem
  lg: 0.5rem
spacing:
  page: 1.5rem
  region-gap: 1rem
  home-max: 960px
  workbench-min: 1200px
components:
  next-step-bar:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    accent: "{colors.next-action}"
    border: "{colors.border}"
    radius: "{rounded.md}"
  connect-form:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    accent: "{colors.next-action}"
    danger: "{colors.danger}"
    radius: "{rounded.md}"
  connection-failure:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    danger: "{colors.danger}"
    border: "{colors.danger}"
    radius: "{rounded.md}"
  table-list:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    accent: "{colors.next-action}"
    border: "{colors.border}"
    radius: "{rounded.md}"
  status-badge:
    unread: "{colors.status-unread}"
    draft: "{colors.status-draft}"
    confirmed: "{colors.status-confirmed}"
    skipped: "{colors.status-skipped}"
    surface: "{colors.surface}"
    radius: "{rounded.sm}"
  workbench:
    background: "{colors.canvas}"
    region: "{colors.surface}"
    foreground: "{colors.body}"
    border: "{colors.border}"
    radius: "{rounded.md}"
    gap: "{spacing.region-gap}"
  purpose-editor:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    border: "{colors.border}"
    radius: "{rounded.md}"
  sample-region:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    border: "{colors.border}"
    radius: "{rounded.md}"
  neighbor-graph:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    edge: "{colors.body}"
    current: "{colors.next-action}"
    radius: "{rounded.lg}"
  partial-failure:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    danger: "{colors.danger}"
    border: "{colors.border}"
    radius: "{rounded.md}"
  export-panel:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    accent: "{colors.next-action}"
    danger: "{colors.danger}"
    border: "{colors.border}"
    radius: "{rounded.md}"
  product-promise:
    background: "{colors.surface}"
    foreground: "{colors.body}"
    accent: "{colors.next-action}"
    radius: "{rounded.md}"
---

# ExportDataProject — Design Spine

視覺識別。行為、資訊架構與流程在 `EXPERIENCE.md`。四個表面都沒有 mock，實作以這兩份契約為準；契約與畫面稿衝突時，以契約為準。

## Brand & Style

這是賣給客戶的產品。客戶用它自己去接整合專案，做法與賣方今天接案相同。前身是內部自己用的工具；PRD 仍寫「內部工具」。體驗以這次的決定為準。

畫面上的人是系統分析師林安。她是買下產品、自己接案的人。她接的案子就是這三步：唯讀連上舊系統資料庫，在同一頁看懂表並自己確認用途，交出一份別人讀得懂的分析包。競爭力就是這套做法可以交給客戶獨立完成。唯讀在填密碼之前就說清楚。確認狀態由她本人推進。分析包讓沒有在場的人分得清已確認與未確認。

美學姿勢是文件感、低裝飾、高方位感。沒有獨立的行銷首頁。繁體中文是介面與分析包固定標題的語言。資料表名與欄位名維持資料庫原文。實體關係圖只用 crow's foot。

[ASSUMPTION] 產品名稱尚未指定。沒有專案時，首頁標題用工作描述，不用倉庫名稱 ExportDataWeb。不另引字體、不引入新的元件庫。既有畫面使用 Bootstrap 5.3.3；本文件只鎖定與「看懂、確認、交出」直接相關的差異。淺色介面。沒有深色模式。

## Colors

| Token | 角色 | 不用在 |
|---|---|---|
| `{colors.body}` | 內文、表名以外的說明。對 `{colors.surface}` 目標至少 7:1 | 狀態的唯一線索 |
| `{colors.surface}` | 表單、清單、工作台分區、圖面 | 整頁大底（工作台大底用 canvas） |
| `{colors.canvas}` | 工作台頁面底，讓三個分區讀成同一個工具 | 按鈕填色 |
| `{colors.next-action}` | 唯一的主行動：繼續下一張未看、測試連線、產生分析包。繼承 Bootstrap primary `#0D6EFD` | 裝飾連結、多個並列主按鈕 |
| `{colors.on-action}` | 主行動按鈕上的文字 | 內文 |
| `{colors.status-unread}` | 「未看」文字與圖示 | 單獨靠灰色傳遞狀態 |
| `{colors.status-draft}` | 「草稿」文字。不用 Bootstrap warning `#FFC107` 當小字，黃底對白字對比不足 | 已確認項目 |
| `{colors.status-confirmed}` | 「已確認」文字與圖示。比 Bootstrap success 略深，小字對白底目標至少 4.5:1 | 未確認清單 |
| `{colors.status-skipped}` | 「略過」文字。與未看同色階，必須同時有文字「略過」 | 只用刪除線代替文字 |
| `{colors.danger}` | 連線失敗、部分失敗、未遮罩範例的警示 | 一般次要按鈕 |
| `{colors.focus}` | 鍵盤焦點環。沿用現有 `site.css` 的 `#258CFB` | 滑鼠 hover 的唯一回饋 |
| `{colors.border}` | 分區與清單分隔 | 表達確認狀態 |

狀態色一律搭配文字：未看、草稿、已確認、略過。資料表層級另用進行中；進行中用 `{colors.next-action}` 的外框加上文字「進行中」，不另造色。

## Typography

介面內文用 `{typography.body}`，繼承 Bootstrap 無襯線字族。標籤與區塊標題用 `{typography.label}`。資料表名、欄位名、鍵名用 `{typography.identifier}`，維持原文大小寫。

不把資料庫識別名排成行銷標題。分析包封面可以有專案名稱，識別名仍是等寬字。繁體中文與英文、數字混排時，行高維持 `{typography.body.lineHeight}`，不為了塞更多欄而降到 1.2 以下。

## Layout & Spacing

首頁（解析專案首頁）內容寬度上限 `{spacing.home-max}`，讓連線與清單讀起來像一件事，而不是一片後台。工作台與匯出使用全寬，頁面內距 `{spacing.page}`。工作台在寬度至少 `{spacing.workbench-min}` 時，三個分區同時可見：欄位結構、範例資料、鄰居圖。分區間距 `{spacing.region-gap}`。

[ASSUMPTION] 窄於 `{spacing.workbench-min}` 時改為直向堆疊，順序固定為結構、範例、鄰居圖。下一步列保持釘選，不跟著捲走。手機不是目標表面。

首頁的主視覺不是「匯出全部」。主行動留在 `{components.next-step-bar}`。

## Elevation & Depth

分區靠 `{colors.border}` 與 `{colors.canvas}` 區分，不靠厚重浮卡。唯一允許的陰影是釘選的下一步列：`0 1px 2px rgba(33, 37, 41, 0.08)`。[ASSUMPTION] 不用 Bootstrap 預設的大範圍 `box-shadow` 當卡片語言。圖與表格不抬升。

## Shapes

圓角沿用 Bootstrap：控制項 `{rounded.md}`，狀態標章 `{rounded.sm}`，鄰居圖上的表節點 `{rounded.lg}`。不使用膠囊形當確認狀態，避免「已確認」看起來像可關閉的標籤。

## Components

### next-step-bar

白底、底部分隔線 `{colors.border}`、圓角 `{rounded.md}`。一列只放一個填色按鈕，背景 `{colors.next-action}`，文字 `{colors.on-action}`。列上同時有解析專案名稱、目前位置、進度（已確認表數／總表數，以及進行中、未看）。沒有第二個同樣重量的按鈕。

### connect-form

白底卡片，圓角 `{rounded.md}`。欄位順序固定：專案名稱、資料庫類型、連線資訊、測試連線。測試連線使用 `{colors.next-action}`。類型只有 SQLite、SQL Server、MySQL、Oracle。密碼欄位不在任何成功或失敗畫面以明文複述。

### connection-failure

與表單同寬。左邊框與標題用 `{colors.danger}`。標題只用四種之一：連不上主機、認證失敗、沒有讀取目錄的權限、類型與連線內容不符。失敗時不渲染一張「0 張表」的清單。

### table-list

白底。表名用 `{typography.identifier}`。每一列都有 `{components.status-badge}` 與文字狀態。可搜尋、以前綴篩選、可排序。列的主動作是進入工作台，不是匯出。超過一屏時，下一步列仍在。

### status-badge

白底小標章，圓角 `{rounded.sm}`，字級 `{typography.label}`。未看、草稿、已確認、略過各用上表前景色，並永遠印出該狀態的詞。進行中只用在資料表，不用在單一欄位。標章不是唯一資訊：同一列的文字欄位重複狀態詞。

### workbench

頁面底 `{colors.canvas}`，三個分區白底、邊框 `{colors.border}`、圓角 `{rounded.md}`、間距 `{spacing.region-gap}`。分區標題用 `{typography.label}`：結構、範例資料、鄰居圖。表名用 `{typography.identifier}`。

### purpose-editor

位於結構分區內，白底、邊框 `{colors.border}`。表與欄各有用途文字。儲存後不自動把狀態改成已確認。空白用途可以存在；狀態仍由分析師切換。

### sample-region

白底表格。預設呈現足以判斷形態的少量列。空表仍保留欄位結構，並顯示「沒有列」。工作台上的範例是原始範例，不在此區假裝已經遮罩。

### neighbor-graph

白底圖面。目前這張表的節點外框 `{components.neighbor-graph.current}`，其餘表用 `{colors.body}` 外框。宣告關聯為實線。基數標在線上：`0..1`、`1`、`0..*`、`1..*`，或文字「未明」。沒有宣告關聯時，圖上只有目前這張表，並寫「目錄中沒有宣告關聯」。記法是 crow's foot。欄位顯示預設只畫鍵。

### partial-failure

放在失敗的那個分區內，不蓋住另外兩個分區。標題說明哪一塊失敗，內文給原因。其餘分區維持正常對比，不整頁灰掉。

### product-promise

只在還沒有解析專案時出現，放在連線表單之上。白底、圓角 `{rounded.md}`、內文 `{typography.body}`。第一句說明這是用來接一座舊系統的整合專案，第二句說明連線只讀取、不修改舊系統。沒有插圖、沒有功能清單牆。專案建立後這塊離開，改由 `{components.next-step-bar}` 承接。

### export-panel

白底。主按鈕「產生分析包」用 `{colors.next-action}`，文字 `{colors.on-action}`，一頁一個。未遮罩開關預設關閉。打開時，面板頂部出現整寬警示，背景淡紅、文字 `{colors.danger}`，文句為「本包含未遮罩範例資料」。範圍選擇（目前資料表及其鄰居、手動勾選）在按鈕之上，讓人先看到將交出什麼。

## Do's and Don'ts

- 還沒有專案時，先讓人讀到這個產品做什麼，再看到連線表單。
- 每一頁都讓人看得出：哪個解析專案、這頁做什麼、下一個動作是什麼。
- 畫面上的產品標題用工作描述，直到有指定的產品名稱。
- 狀態同時用詞與顏色。未看、草稿、已確認、略過缺一不可。
- 表名與欄位名用 `{typography.identifier}`，不翻譯。
- 主按鈕只留給當下那一步。
- 連線失敗不要畫成空的資料表清單。
- 不要把「匯出全部」做成首頁主按鈕。
- 不要用 `#FFC107` 當草稿小字。
- 不要為確認狀態做慶祝動畫或徽章牆。
- 不要在圖上改用陳氏記法。
- 不要把工作台上的原始範例畫成已經遮罩的樣子。
