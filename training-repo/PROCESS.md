# 練習紀錄

## 活動 2／練習 0 — 使用 Playwright MCP 操作 OrderHub

- Codex 已從專案層級的 `.codex/config.toml` 載入 Playwright MCP。
- Agent 自行開啟 `http://localhost:5150`，從導覽列進入建立訂單頁，選擇客戶與商品、輸入數量並送出表單。
- 實測建立訂單 `#209`：陳志明（金卡會員）購買 `SKU-1002 極光 機械鍵盤` 2 件；結果頁顯示小計 NT$4,640、會員折扣 NT$464、應付 NT$4,176。
- 結果頁截圖位於 `artifacts/exercise-0-order.png`。

### 與活動 1／練習 2 的對比

活動 1 排查 bug 時，需要人工逐頁操作：先記錄商品價格或庫存、建立指定條件的訂單、記下訂單編號與頁面數字，再返回訂單或商品頁核對結果，最後把具體現象交給 agent 分析。這些步驟容易因漏記數字、選錯測試資料或操作順序不一致而難以重現。

接上 Playwright MCP 後，agent 能直接讀取頁面結構並完成點擊、選項、輸入、送出、結果核對與截圖。人仍需決定測試情境並檢查證據，但重複的瀏覽器操作可交由 agent 執行，重現步驟也更容易固定與重跑。

## 活動 2／練習 3 — OrderHub MCP before/after 對照

問題：`哪些商品庫存低於 5？`

### Before：未註冊 OrderHub MCP

- 禁用 Playwright 與其他 MCP，只保留唯讀 shell／HTTP。
- Agent 對 `/Products` 發出一次 HTTP GET，收到完整商品頁 HTML，內容包含 50 筆商品與頁面標記。
- Agent 必須理解表格欄位、從 HTML 擷取庫存數字，再自行套用 `< 5` 條件。
- 工具呼叫雖然也是 1 次，但傳回的是為人類頁面設計的大量資料；若頁面結構改版，解析方式也可能失效。

### After：啟用 OrderHub MCP

- Agent 只呼叫一次 `mcp__orderhub__low_stock`，參數為 `threshold=5`。
- Server 直接回傳 5 筆符合條件的結構化商品資料，不需讀頁面、原始碼或自行判斷 HTML 結構。
- 結果與 before 一致：SKU-1048（2）、SKU-1005（3）、SKU-1023（3）、SKU-1014（4）、SKU-1032（4）。

### 結論

差異不只在工具呼叫次數，而在介面的語意與資料量。沒有專用 MCP 時，agent 取得的是通用網頁資料，必須自行推斷如何解析；有 MCP 時，工具名稱、description、參數 schema 與精簡回傳已把任務意圖說清楚，流程較穩定，也更容易在不同 agent 之間重用。
