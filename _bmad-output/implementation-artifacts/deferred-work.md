- source_spec: `E:\sjvann\ExportDataProject\_bmad-output\specs\spec-ExportDataProject\stories\1-create-project-readonly-inventory.md`
  summary: 以既有目錄 SQL 做唯讀盤點，把快照寫進解析專案，並把四種連線失敗分開。
  evidence: 故事 1 規格約 2300 token，使用者要求拆開分批。目錄讀取依賴既有 SqlGen 與驅動，可在文件模型之後獨立測試。
- source_spec: `E:\sjvann\ExportDataProject\_bmad-output\specs\spec-ExportDataProject\stories\1-create-project-readonly-inventory.md`
  summary: 本機首頁建立或開啟解析專案、DPAPI 秘密附檔、Kestrel 只聽 localhost，並把既有專案改為 net10.0。檔案放在分析師指定資料夾，或固定的本機應用資料夾，仍未決定。
  evidence: 故事 1 規格約 2300 token，使用者要求拆開分批。存放位置要等分析師看得到首頁才會影響結果，且會改既有 Web 與命令列的目標框架。
- source_spec: `E:\sjvann\ExportDataProject/_bmad-output/implementation-artifacts/spec-add-postgresql.md`
  summary: 解析專案能標成 PostgreSQL，並以未加引號識別字的小寫摺疊比較表名。
  evidence: 支援 PostgreSQL 的規格約 2217 token，使用者要求拆開分批。解析專案的型別與識別字規則不阻擋命令列與網頁先讀取 PostgreSQL。
