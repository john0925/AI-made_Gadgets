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
2. 使用按鈕、`Ctrl+O`、拖放分別開啟 `samples/README.md`；確認只會有一個分頁（重複開啟會切換到既有分頁），且不會開啟新視窗。
3. 切換三種文字對齊，確認標題、段落、引用、清單（含項目符號與巢狀清單）、工作清單與表格都跟著靠左／置中／靠右；表格中以 `:---:` 等指定的欄位對齊不受影響；程式碼區塊維持靠左。
4. 調整字型、12–36 字級和 1.2–2.5 行距；確認目前文件立即更新（版面設定依分頁各自保存，詳見第 13 項）。
5. 切換四種主題、自訂背景／本文色及恢復預設。
6. 檢查相對圖片、缺少圖片、長程式碼、長文件捲動與繁體中文內容。
7. 點擊 HTTP(S) 連結，確認由預設瀏覽器開啟；確認惡意 HTML、`javascript:` 連結、遠端圖片與跳脫資料夾的圖片不會執行或載入。
8. 點擊三橫線選單，確認可開啟文件、切換繁體中文／English、查看關於資訊；切換後所有應用程式 UI 文字立即更新，重啟後維持選擇。「關於」視窗在繁體中文下顯示中文版、在 English 下顯示英文版，且皆含正確版本號；點擊兩個 GitHub 連結會以預設瀏覽器開啟，按「關閉」或 Esc 可關閉視窗。
9. 開啟 `samples/security-test.md`：確認沒有任何對話框、預設瀏覽器不會自動開啟、YouTube／影片沒有嵌入，`{...}` 只以純文字顯示。
10. 開啟文件後調整主題、字型、字級、行距與對齊，確認在 CSP 下設定仍會即時套用（`ExecuteScriptAsync` 呼叫 `window.reader.applySettings`）。
11. 分頁：以 `Ctrl+O` 多選、拖放多個檔案（分別拖到歡迎畫面與文件顯示區）開啟數份文件；點擊、`Ctrl+Tab`、`Ctrl+Shift+Tab` 切換；捲動一份長文件後切到別的分頁再切回，確認回到原捲動位置。
12. 分頁關閉：✕、滑鼠中鍵、`Ctrl+W`、右鍵「關閉其他分頁」；關閉目前分頁時改顯示右側（沒有則左側）分頁，全部關閉後回到歡迎畫面。分頁超出寬度時可用滑鼠滾輪或 ◀ ▶ 捲動，▾ 可列出全部分頁；右鍵「複製路徑」可貼上完整路徑。
13. 分頁版面：在分頁 A 改成靠右、字級 26，切到分頁 B 確認仍是預設版面且工具列顯示 B 的設定，切回 A 確認恢復靠右、26；主題與自訂顏色則所有分頁一起變更。在 A 使用 ☰「將目前版面設為預設」後，新開的分頁套用 A 的版面；關閉所有分頁時，工具列顯示並可直接修改預設版面；重啟程式後只保留預設版面與主題。
14. 並排顯示：開啟三份文件後按工具列「左右並排」，確認右側顯示另一份文件、分頁出現「左／右」標記且右側上緣有焦點線；點擊未顯示的分頁只替換焦點側；點擊另一側正在顯示的分頁只切換焦點；用滑鼠點一下左側文件內容，焦點線與工具列設定隨之切到左側。分頁右鍵「在另一側顯示」會交換兩側。拖曳分隔線調整比例，拖不到 15% 以下；按兩下分隔線恢復各半。切換「上下並排」後兩側改為上下且保留比例；切回「單一畫面」保留焦點側的文件。重啟程式後並排方式與比例仍在。把檔案拖放到右側（或其空白提示區）時在右側開啟。

## 開發中結果（2026-10-30，並排顯示第 2 階段）

- `dotnet build`：通過，0 警告 0 錯誤；`dotnet test`：通過，79/79。
- 以實際 `MainWindow`（兩個 WebView2）自動操作 16 個情境：左右並排後右側顯示下一份文件並取得焦點、字級只改焦點側、點擊分頁只替換焦點側、點擊另一側的分頁只切換焦點、在右側頁面按下滑鼠切換焦點、「在另一側顯示」交換兩側、比例 0.05 被限制為 0.15、0.7 時左右為 811／348px、上下並排保留比例（445／191px）、在右側開新檔、關閉焦點側分頁不會挑到另一側的文件、切回單一畫面保留焦點側文件、「關閉其他分頁」後另一側顯示空白提示，存檔包含 `SplitMode` 與 `SplitRatio`，皆符合預期。
- 初版以 WebView2 的 `GotFocus` 判斷焦點，測試發現頁面重新載入時也會觸發而讓焦點跳回另一側，已改為頁面送出固定的 `focus` 訊息。
- 第 14 項：尚未由使用者手動驗證（實際滑鼠拖曳分隔線、按兩下、點擊頁面切換焦點、拖放到右側）。

## 開發中結果（2026-10-29，分頁第 1 階段）

- `dotnet build`：通過，0 警告 0 錯誤；`dotnet test`：通過，60/60。
- 以實際 `MainWindow`（含 WebView2）自動操作：開啟三份文件、捲動位置 1500 切換後還原為 1500、大小寫不同的重複路徑沿用既有分頁、拖放產生的 `file:` 導覽改開新分頁、關閉目前分頁改顯示鄰近分頁、全部關閉回到歡迎畫面，皆符合預期。
- 分頁版面（同一天追加）：以實際 `MainWindow` 自動操作，A 分頁改為靠右／26px 不影響 B 分頁（左／18px），切回 A 後頁面與工具列皆恢復；「設為預設」後新分頁套用該版面；深色主題同時套用至所有分頁；存檔只含預設版面與主題。`dotnet test`：63/63。
- 第 11、12、13 項：尚未由使用者手動驗證。

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
