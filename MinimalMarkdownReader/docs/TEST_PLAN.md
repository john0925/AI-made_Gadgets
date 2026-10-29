# MinimalMarkdownReader 測試計畫

## 自動化測試

`tests/MinimalMarkdownReader.Tests` 使用 xUnit，涵蓋：

- UTF-8 BOM 文件讀取與不支援副檔名拒絕。
- 相對圖片只允許文件資料夾內的指定點陣圖格式。
- 損壞的 `settings.json` 回復預設值。
- 非法對齊模式與 CSS 注入形式的色碼遭拒絕。
- 原始 HTML、`javascript:` URI 與不安全連結不會出現在轉換後 HTML；HTTP(S) 連結會保留。
- `{...}` 自訂屬性（如 `onload`、`style`）不會成為 HTML 屬性；YouTube／影片連結不會轉成 iframe 或 video。
- 閱讀頁面含限制性 CSP，內嵌腳本雜湊與 CSP 相符；資源請求只允許文件資料夾內的圖片，導覽只允許程式自己的文件載入。
- 語言資源可切換為繁體中文與英文，且語言偏好會保存再讀取。

執行：

```powershell
dotnet test MinimalMarkdownReader.sln --configuration Debug
```

## 手動檢查

1. 執行程式，確認歡迎畫面與「開啟文件」按鈕可見。
2. 使用按鈕、`Ctrl+O`、拖放分別開啟 `samples/README.md`；確認後兩種不會開啟新分頁。
3. 切換三種文字對齊，確認標題、段落、引用、清單（含項目符號與巢狀清單）、工作清單與表格都跟著靠左／置中／靠右；表格中以 `:---:` 等指定的欄位對齊不受影響；程式碼區塊維持靠左。
4. 調整字型、12–36 字級和 1.2–2.5 行距；確認目前文件立即更新，重啟後設定仍存在。
5. 切換四種主題、自訂背景／本文色及恢復預設。
6. 檢查相對圖片、缺少圖片、長程式碼、長文件捲動與繁體中文內容。
7. 點擊 HTTP(S) 連結，確認由預設瀏覽器開啟；確認惡意 HTML、`javascript:` 連結、遠端圖片與跳脫資料夾的圖片不會執行或載入。
8. 點擊三橫線選單，確認可開啟文件、切換繁體中文／English、查看關於資訊；切換後所有應用程式 UI 文字立即更新，重啟後維持選擇。「關於」視窗在繁體中文下顯示中文版、在 English 下顯示英文版，且皆含正確版本號；點擊兩個 GitHub 連結會以預設瀏覽器開啟，按「關閉」或 Esc 可關閉視窗。
9. 開啟 `samples/security-test.md`：確認沒有任何對話框、預設瀏覽器不會自動開啟、YouTube／影片沒有嵌入，`{...}` 只以純文字顯示。
10. 開啟文件後調整主題、字型、字級、行距與對齊，確認在 CSP 下設定仍會即時套用（`ExecuteScriptAsync` 呼叫 `window.reader.applySettings`）。

## 本次結果（2026-10-29，1.0.6）

- `dotnet build MinimalMarkdownReader.sln --no-restore --configuration Debug`：通過，0 警告 0 錯誤。
- `dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug`：通過，49/49。
- 以實際 WebView2 擷取三種對齊的畫面：清單、表格、引用皆跟隨對齊，程式碼維持靠左；CSP 下設定可正常套用。
- 手動檢查（使用者於 Visual Studio 執行）：「關於」視窗中英文內容與連結、三種對齊的版面，皆通過。
- 發布輸出不含獨立的 `about*.md`；內嵌的 about 版本為 1.0.6。

## 前次結果（2026-10-29，1.0.5）

- `dotnet build MinimalMarkdownReader.sln --no-restore --configuration Debug`：通過，0 警告 0 錯誤。
- `dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug`：通過，35/35。
- 發布輸出不含獨立的 `about*.md`；`MinimalMarkdownReader.dll` 內嵌 `about.md` 與 `about.en.md`。
- 「關於」在兩種語言下的顯示（第 8 項）：尚未進行互動式手動驗證。

## 前次結果（2026-10-28，資安修正）

- `dotnet build MinimalMarkdownReader.sln --configuration Debug`：通過，0 警告 0 錯誤。
- `dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug`：通過，31/31。
- `dotnet list MinimalMarkdownReader.sln package --vulnerable --include-transitive`：無已知弱點套件。
- 手動檢查（使用者於 Windows 以 `dotnet run` 執行）：設定即時套用、`samples/security-test.md` 無對話框／無自動開啟瀏覽器／無嵌入內容、相對圖片正常顯示，皆通過。

## 前次結果（2026-10-22）

- `dotnet restore MinimalMarkdownReader.sln --ignore-failed-sources`：通過；NuGet 弱點資料查詢因網路限制產生 NU1900 警告。
- `dotnet build MinimalMarkdownReader.sln --no-restore --configuration Debug`：通過，0 錯誤。
- `dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug`：通過，9/9。
- WebView2 視窗、拖放、連結與發布產物：尚未進行互動式手動驗證。
