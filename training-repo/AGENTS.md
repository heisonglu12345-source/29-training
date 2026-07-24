# OrderHub — 專案指引

## 專案簡介

OrderHub 是公司內部使用的訂單管理培訓系統，提供訂單、商品及客戶查詢與管理功能。
系統使用單一 SQL Server 資料庫，不需要加入多租戶、微服務或高併發架構。

## 技術棧

- .NET 8 / ASP.NET Core MVC / Razor Views / Bootstrap 5
- EF Core 8.0.11 + SQL Server
- xUnit 2.5.3
- 測試使用 EF Core InMemory，不連接本機 SQL Server

## 專案結構與依賴

- `src/OrderHub.Web`：Controllers、ViewModels、Views，以及 DI 組裝
- `src/OrderHub.Core`：Domain models、service、repository interfaces 與商業邏輯
- `src/OrderHub.Infrastructure`：EF Core DbContext、repository implementations、migrations 與種子資料
- `tests/OrderHub.Tests`：xUnit 測試
- Core 不依賴 Web 或 Infrastructure
- Infrastructure 引用 Core；Web 引用 Core 與 Infrastructure

## 分層與程式慣例

- Controller 保持薄，只處理輸入、ModelState、service 呼叫、ViewModel mapping 與導頁
- 商業邏輯放在 Core service
- 只有 Infrastructure repository 可以直接使用 `OrderHubDbContext`
- Service 與 Controller 不可直接查詢 EF Core
- 預期內的業務失敗使用 `ServiceResult<T>` 回傳，不要用例外控制流程
- View 一律綁定 ViewModel，不直接綁定 domain model
- 使用者輸入使用 DataAnnotations + ModelState 驗證，輸入錯誤不可造成 500
- 金額一律使用 `decimal`
- 折扣計算集中在 `OrderService.CalculateTotal`，不要在其他位置重複套用
- Controller 寫法參考 `ProductsController.cs`
- Service 寫法參考 `ProductService.cs`
- Repository 寫法參考 `ProductRepository.cs`

## 常用指令

- `dotnet build`：建置整個 solution
- `dotnet test`：執行全部測試
- `dotnet run --project src/OrderHub.Web`：啟動網站，預設為 `http://localhost:5150`

## 驗證要求

- 修復 bug 時，先重現並記錄具體現象，再定位根因
- 每個 bug 都要補回歸測試並回到頁面實測
- 多檔案功能或重構必須先提出計畫，經使用者確認後才實作
- 完成改動後執行 `dotnet test`，並從 code review 角度檢查 diff
- 不可為了讓測試通過而任意放寬或刪除既有測試

## 重要與危險檔案

- `src/OrderHub.Infrastructure/Migrations/**` 是資料庫歷史紀錄，不要手動修改
- 修改 `src/OrderHub.Web/appsettings*.json` 前先取得使用者同意
- 不要讀取或寫入 `*.pfx`、`appsettings.Production.json` 或 user-secrets

## 不要做的事

- 不要未經同意新增 NuGet 套件
- 不要在 Controller 或 Service 直接使用 DbContext
- 不要順手重構與目前任務無關的程式碼
- 不要執行 `git push --force` 或 `git reset --hard`
- 不要在未取得明確同意時刪除或重置資料庫
- 不要在使用者確認前建立 commit 或 push
