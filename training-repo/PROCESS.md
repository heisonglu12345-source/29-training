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

## 活動 2／練習 4 — 會修改資料的 `cancel_order`

### 工具與 annotations

- `cancel_order` 只轉接 `OrderService.CancelOrderAsync`，沒有在 MCP 層重複實作狀態檢查或庫存回補規則。
- MCP Inspector 2.1.0 的 `tools/list` 顯示：`get_order`、`low_stock`、`customer_orders` 都是 `readOnlyHint=true`；`cancel_order` 是 `destructiveHint=true`、`idempotentHint=false`。
- Codex 專案設定將 `cancel_order` 的 `approval_mode` 設為 `prompt`，讓 client 在執行資料異動前要求確認。

### 權限確認與資料驗證

- 測試訂單：`#209`，狀態為 Pending，包含 SKU-1002「極光 機械鍵盤」2 件；取消前商品庫存為 100。
- 未允許時，Codex 顯示 `user cancelled MCP tool call`。回到訂單與商品頁確認，訂單仍是「待處理」，庫存仍為 100，資料沒有改動。
- 允許後，agent 只呼叫一次 `mcp__orderhub__cancel_order(id=209)`，回傳「訂單 209 已取消，庫存已回補」。
- 回到 `/Orders/Details/209` 與 `/Products` 核對：訂單已是「已取消」，SKU-1002 庫存由 100 回補為 102。
- 對同一筆訂單再次呼叫，得到「取消失敗：狀態為 Cancelled 的訂單不可取消」，沒有 exception dump，庫存也不會重複回補。

### 自動驗證

- `dotnet build src/OrderHub.Mcp -m:1`：成功，0 warning、0 error。
- `dotnet test --no-restore -m:1`：34/34 通過；既有取消訂單測試涵蓋 Pending／Confirmed 成功、庫存回補、Shipped／Cancelled 拒絕與不存在訂單。

## 活動 2／練習 5 — Resources 與 Prompts

### Inspector 與 Codex 等效驗證

- MCP Inspector 2.1.0 的 `resources/list` 列出「會員折扣規則」，URI 為 `orderhub://discount-rules`，MIME type 為 `text/markdown`；`resources/read` 可讀到 Standard、Silver、Gold 三種折扣規則。
- 將 Inspector 讀出的 Resource 內容交給全新 Codex session，再問「Gold 會員買 1000 元商品應付多少？」；agent 沒有讀程式碼或呼叫工具，回答 9 折、應付 900 元。
- `prompts/list` 列出 `low_stock_report`，`threshold` 是非必填參數；`prompts/get(threshold=5)` 正確將門檻展開到訊息中。
- 將展開後的 Prompt 交給全新 Codex session，agent 呼叫一次 `mcp__orderhub__low_stock(threshold=5)`，並對 5 筆商品產出 SKU、名稱、現有庫存、建議補貨量與理由表格。
- 現有工具無法依商品或 SKU 查近期訂單，agent 因此明確標示資料限制，只提出補至門檻 5 的最低安全補貨量，沒有捏造近期銷量。

### Tool、Resource、Prompt 的分工思考

- Tool 是動作：需要參數並執行查詢、計算或資料異動，例如 `low_stock`、`cancel_order`。
- Resource 是可放入 context 的資料。相較於讓 agent 自行讀 `OrderService.cs`，`discount-rules` 不要求 agent 理解專案路徑與程式結構，也能給非程式碼 client 使用；內容可隨 server 一起版本控制。不過靜態規則會和 service 形成兩份真相，折扣邏輯改版時必須同步更新，較好的長期作法是從同一規則來源動態產生內容。
- Prompt 是替使用者表達任務的共用範本。放在 server 後，團隊共用相同流程與參數，規則改版只需更新一處並留下版本紀錄；若每個人各自輸入，容易漏步驟或產生不同版本。
- Prompt 只能編排既有能力，不能補出 server 沒提供的資料。本次報告暴露「缺少依商品查近期訂單」的能力缺口，這比讓 agent 猜測資料更安全，也能作為未來工具設計的依據。

### 自動驗證

- `dotnet build src/OrderHub.Mcp -m:1`：成功，0 warning、0 error。
- `dotnet test --no-restore -m:1`：34/34 通過。
