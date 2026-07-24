# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

- Agent：Codex CLI 0.145.0
- 模型：GPT-5.6 Sol（`gpt-5.6-sol`，high reasoning）

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

練習 1 的官方步驟只有三項：

1. 依 Codex 指南建立專案設定
2. 把設定檔 commit 到 Git
3. 填寫 `PROCESS.md` 自我驗證

實際執行時，我在第 1 項內加入檢查點：先確認環境，再分析專案，接著建立設定並驗證。

- 環境基準：.NET SDK 8.0.400；`dotnet test` 共 28 個測試，28 個通過、0 個失敗
- 網站基準：成功連接 SQL Server、建立並植入 `OrderHubTraining`，首頁回傳 HTTP 200
- 專案理解：核對三層依賴與建立訂單流程
- Agent 設定：建立 `AGENTS.md`、rules、hooks、兩個 subagent 與 `fix-bug` skill
- 設定驗證：rules、hooks、`AGENTS.md` 自動載入與 `test-runner` 均實測
- Git 提交：commit `5583a4b chore: configure Codex agent for OrderHub`

順序中有一次修正：Agent 一度把練習 1 說成五個步驟，我重新對照
`documents/activities/activity-guideline.md`，確認那五項只是執行檢查點，不是公司定義的五個任務，
最後仍以官方三步驟管理進度。

練習 2–4 則依「先取得證據，再做單一變更」拆解：

1. 練習 2：每個 bug 分別重現、定位、確認方案、修復、補回歸測試、頁面驗證及 commit
2. 練習 3：先唯讀分析分層並審計計畫，確認後才實作 Core、Infrastructure、Web 與測試
3. 練習 4：先盤點 `CreateOrderAsync` 的驗證順序與測試覆蓋，再只抽方法、不改行為

實際提交順序與範圍：

- `0ae494b`：訂單列表分頁 offset
- `00f7e9a`：Gold 折扣只套用一次
- `594b799`：取消訂單回補庫存
- `7212274`：低庫存警示頁與 3 個 service 測試
- `14e7483`：抽出建單驗證 helper

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

我使用的原始 prompt：

> 目前我公司要我做AI練習，幫我看這個folder全部的md file然後解析給我知道 具體要做什麽

這個問法有效，是因為同時說清楚了資料範圍（整個 folder 的 Markdown）與要的結果
（不是摘要，而是解析出具體工作）。Agent 找出 9 個 Markdown 檔案，區分主要練習文件、
Codex/Claude 設定指南、提示技巧、token 建議與第三方授權，最後整理成 4 個主要練習階段。

第二個有效 prompt：

> ok 開始執行，每執行一個就跟我確認一下

這句建立了人工檢查點。Agent 完成環境、分析、設定、驗證或 commit 後都先回報，
避免一次改完全部內容才發現方向不對。

練習 2 最有效的是提供可核對的數字，而不是轉貼客訴。例如庫存 bug 的原始回報：

> 商品 SKU 與初始庫存：SKU-1001 極光 無線滑鼠，庫存 22
> 建單數量及建單後庫存：1，21
> 訂單編號：#207
> 取消訂單後庫存：21

Agent 因此能把問題限制在取消流程，找到 `OrderService.CancelOrderAsync` 先把狀態改成
`Cancelled`、再判斷是否為 `Pending`／`Confirmed`，造成回補分支永遠不執行。

練習 3 的有效做法則是先要求計畫並逐層核對。Agent 在動手前列出 Core 查詢結果 model、
service、repository、Controller、ViewModel、View、導覽列與三個測試，並說明銷量會用
correlated aggregate 形成單一 SQL，避免 N+1；確認後才實作。

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

第一個問題是 Agent 把練習 1 的內部執行檢查點說成五個步驟，讓任務看起來比文件規定複雜。
我是重新查看 `activity-guideline.md` 的「練習 1」編號清單後發現：正式要求其實只有
「設定、commit、PROCESS 自我驗證」三步。我的修正原文是：

> 練習1 不就是 3個小步驟吧了嗎？

第二個不精確處是把三層架構簡寫成 `Web → Core → Infrastructure`。
我對照三個 `.csproj` 後確認，這只能代表概念上的請求流程，不是實際專案引用方向：

- Core 不引用 Web 或 Infrastructure
- Infrastructure 引用 Core
- Web 同時引用 Core 與 Infrastructure，並在 `Program.cs` 完成 DI 組裝

另外，實際執行也抓到兩個環境問題，而不是把錯誤當成程式 Bug：

- `git` 回報 `detected dubious ownership`，處理方式是只把這個確切 repo 加入 `safe.directory`
- 第一次 commit 回報 `Author identity unknown`，處理方式是只在此 repo 設定 `user.name` 與 `user.email`

練習 3 有一次真實的驗證誤判。Agent 最初用 HTML 是否包含
`庫存門檻必須大於 0` 判斷錯誤訊息是否顯示，結果連預設門檻 10 與 `threshold=3`
都回報 `VALIDATION_ERROR=True`。原因是 DataAnnotations 也會把同一句文字放在
`data-val-range` 屬性，並不代表畫面真的顯示錯誤。

我是因為「有效門檻也回傳 True」這個矛盾抓到問題；重新檢查實際 validation span 後確認：

- 10、3：`field-validation-valid`
- 0、-1：`field-validation-error`，畫面顯示「庫存門檻必須大於 0」

另外，第一次完整 build 失敗不是程式錯誤，而是執行中的 `OrderHub.Web` 鎖住 DLL。
停止該確切程序後重新 build，結果為 0 warning、0 error。

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

我會使用「文件基準 → Agent 分析 → 證據核對 → 小步提交」：

1. 先找出需求文件中真正有編號的交付項目，寫成 checklist
2. 要求 Agent 先讀相關檔案並說明計畫，不立即修改
3. 對每個架構結論要求提供實際檔案或 `.csproj` 證據
4. 修改後執行測試、檢查 `git diff`，確認沒有混入無關檔案
5. 一個可獨立驗證的成果做一個 commit，再開始下一項

這比只說「幫我完成練習」更容易在 Agent 擴大範圍或理解錯誤時立即修正。

這次再加入一個可複製的 bug 修復流程：

1. 先記錄「輸入 → 中間狀態 → 錯誤結果」，例如庫存 `22 → 21 → 21`
2. 要求 Agent 先說明根因與最小修法，確認前不改檔
3. 回歸測試必須能在舊程式失敗，例如取消後庫存應從 7 回到 10
4. 自動測試後仍回頁面，用新資料驗證；舊資料不假設會被自動修復
5. 只 stage 本次檔案，核對 staged 清單後再做獨立 commit

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. ✅ 我能不看筆記說出三個專案各自的職責：
   - Web：Controller、ViewModel、Razor View、表單驗證及 DI 組裝
   - Core：Domain model、service、repository interface 與商業邏輯
   - Infrastructure：EF Core DbContext、repository 實作、migration 與 seed
2. ✅ 我核對過建單流程：`Create.cshtml` → `OrdersController.Create`
   → `OrderService.CreateOrderAsync` → repository → `OrderHubDbContext.SaveChangesAsync`。
   找到的過度簡化說法是 `Web → Core → Infrastructure` 並不等於實際專案引用方向。
3. ✅ 商業邏輯應放在 Core service。新增一個完整頁面通常需要檢查或修改：
   - Core：service/interface、domain 或查詢結果 model、repository interface
   - Infrastructure：repository 查詢實作
   - Web：Controller、ViewModel、View、導覽列；若新增服務還要設定 DI
   - Tests：service 層單元測試及必要的回歸測試

練習 2

1. ✅ 三個 bug 都先在頁面重現：
   - 分頁：新訂單 `#203` 不在第一頁；共 203 筆、11 頁，根因是第 1 頁先 `Skip(20)`
   - 折扣：Gold `#204` 原價 1,420，先存 1,278 又折 10%，錯誤總額 1,150.20；
     Silver `#205` 為 1,349，行為正常
   - 庫存：`SKU-1001` 為 `22 → 建單後 21 → 取消 #207 後仍 21`
2. ✅ 提供給 Agent 的是訂單編號、金額與庫存變化，不只貼客訴文字。
3. ✅ 修復後回頁面驗證：
   - `#203` 回到第一頁，最後一頁不再因錯誤 offset 空白
   - Gold `#206`：快照 1,420、折扣 142、應付 1,278
   - `#208` 已取消，`SKU-1001` 庫存恢復為 21
4. ✅ 每個 bug 各補一個回歸測試；練習 2 結束時 31/31 通過。
5. ✅ 三個獨立 commit：
   - `0ae494b fix: show newest orders and populate last page`
   - `00f7e9a fix: prevent Gold member discount from being applied twice`
   - `594b799 fix: restore product stock before cancelling orders`
6. ✅ 原本測試沒抓到的原因：
   - 分頁測試只驗證 `TotalCount`／`TotalPages`，沒有驗證每頁實際資料
   - 價格測試分開測快照與 `CalculateTotal`，沒有走 Gold「建單後再算總額」的完整流程
   - 取消測試只驗證狀態變成 `Cancelled`，沒有驗證商品庫存

練習 3

1. ✅ `/Products/LowStock` 不帶參數時門檻為 10，庫存依序為 2、3、3、4、4；
   `?threshold=3` 只剩 `SKU-1048`（庫存 2）。
2. ✅ `?threshold=0`、`?threshold=-1` 都回 HTTP 200，並顯示
   「庫存門檻必須大於 0」，不是 500。
3. ✅ 銷量測試建立近 30 天 Confirmed 數量 3、Cancelled 數量 7、31 天前數量 11，
   結果只計入 3。實際 SQL 條件包含 `Status <> Cancelled`。
4. ✅ service 測試確認停售且低庫存的商品不出現在結果。
5. ✅ 分層維持既有慣例：
   - Controller 只處理 ModelState、呼叫 service 與 mapping
   - Core service 決定 30 天起點
   - Infrastructure repository 執行單一 EF 聚合查詢
   - View 只綁 `LowStockViewModel`
6. ✅ 新增 3 個 service 測試；完整測試 34/34，build 0 warning、0 error。
   Commit：`7212274 feat: add low-stock alert page with recent sales`。

練習 4

1. ✅ 重構後建單測試 11/11、完整測試 34/34，build 0 warning、0 error。
2. ✅ 改善的是 `CreateOrderAsync` 只保留流程編排：
   - `ValidateOrderLines` 負責空明細、數量與重複商品
   - `AddValidatedOrderItemsAsync` 負責商品存在／停售／庫存、快照與扣庫存
   沒有改變驗證順序、錯誤訊息、錯誤累積、價格快照、庫存或儲存時機。
3. ✅ 從 code review 角度檢查 diff，確認只修改 `OrderService.cs`，
   公開 interface、Controller、Repository、資料庫與測試都未改。
   Commit：`14e7483 refactor: extract order creation validation helpers`。

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）

### 對話 1：先釐清完整任務

- 我的 prompt：`幫我看這個folder全部的md file然後解析給我知道 具體要做什麽`
- 回應摘要：Agent 讀完 9 個 Markdown，整理出環境準備、4 個主要練習、
  7 個實際工作項目、至少 6 個程式碼 commit，以及 `PROCESS.md` 的填寫要求。
- 值得保留的原因：先取得完整地圖，避免只看單一 README 就漏掉驗收條件。

### 對話 2：用人工檢查點控制 Agent

- 我的 prompt：`開始執行，每執行一個就跟我確認一下`
- 回應摘要：Agent 每完成一項就停下回報測試數字、檔案變更或 commit 狀態；
  當 Agent 把練習 1 拆得太細時，我再用原文件的三步驟把範圍校正回來。
- 值得保留的原因：檢查點讓我保有決策權，也留下可回溯的真實操作紀錄。
