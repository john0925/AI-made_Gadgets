# MinimalMarkdownReader

MinimalMarkdownReader 是 Windows 10/11 x64 的唯讀 Markdown 閱讀器，可用分頁同時開啟多份文件。它以 WPF、WebView2 與 Markdig 顯示 UTF-8 Markdown，介面採繁體中文。

## 功能

- 開啟 `.md`／`.markdown` 文件，支援 `Ctrl+O`（可多選）、拖放（可一次拖入多個檔案）與檔案關聯雙擊；每份文件開在自己的分頁，已開啟的文件會直接切換到該分頁。
- 分頁：點擊切換、✕ 或滑鼠中鍵關閉，右鍵可關閉其他分頁或複製路徑；`Ctrl+Tab`／`Ctrl+Shift+Tab` 切換、`Ctrl+W` 關閉。切換分頁時會記住並還原各文件的捲動位置，並重新讀取檔案以顯示最新內容。
- 並排顯示：工具列「畫面」可切換單一畫面、左右並排或上下並排。點擊分頁會把文件顯示在目前焦點所在的一側（點一下文件內容即可切換焦點，焦點側上緣有深藍色線），分頁上的「左／右」或「上／下」標記表示文件顯示在哪一側；分頁右鍵「在另一側顯示」可把文件移到另一側。拖曳中間的分隔線可調整比例（每側至少 15%），按兩下分隔線恢復各半。並排方式與比例會保存。
- 調整內文對齊、系統字型、字級、行距與柔和／護眼／明亮／深色主題。對齊、字型、字級與行距依分頁各自設定（分頁關閉後不保留）；新分頁套用預設版面，可用 ☰「將目前版面設為預設」更新。主題與顏色為所有分頁共用。
- 支援自訂背景色與本文色，並將主題、顏色、預設版面、並排方式與介面語言保存至 `%LOCALAPPDATA%\MinimalMarkdownReader\settings.json`。首次啟動時會讀取舊版 MarkdownReader 的設定檔。
- 顯示文件目錄內的相對 PNG、JPG、GIF、WebP、BMP 圖片；限制圖片不得跳脫文件目錄。
- 外部 HTTP(S) 連結會交給預設瀏覽器開啟。
- 點擊工具列的三橫線選單可開啟文件、切換繁體中文／English，以及查看關於資訊（依介面語言顯示繁體中文或英文，含可點擊的 GitHub 專案與下載連結）。語言選擇會保存並在下次啟動時套用。

## 需求與執行

- Windows 10 或 Windows 11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Microsoft Edge WebView2 Runtime（Windows 11 通常已包含；其他系統請依 Microsoft 指引安裝）

```powershell
dotnet restore MinimalMarkdownReader.sln
dotnet build MinimalMarkdownReader.sln --configuration Debug
dotnet run --project src/MinimalMarkdownReader/MinimalMarkdownReader.csproj
```

在 Visual Studio 中直接開啟 `MinimalMarkdownReader.sln`，選擇 `MinimalMarkdownReader` 作為啟始專案後按 F5。

## 發布

### 可攜式發佈

```powershell
dotnet publish src/MinimalMarkdownReader/MinimalMarkdownReader.csproj -c Release -r win-x64 --self-contained false
```

輸出位於 `src/MinimalMarkdownReader/bin/Release/net10.0-windows/win-x64/publish/`。此為 framework-dependent 發布，因此目標電腦仍需要 .NET 10 Desktop Runtime 與 WebView2 Runtime。

### 安裝檔

安裝檔不包含在原始碼中，會透過 [GitHub Releases](https://github.com/john0925/AI-made_Gadgets/releases) 發布（檔名格式為 `MinimalMarkdownReader-Setup-<版本>.exe`）。若 Releases 尚未提供，可依下方步驟自行建置。

> **下載安全：** 請只從上方的官方 GitHub Releases 頁面下載安裝檔，不要使用其他網站或他人轉傳的檔案。安裝前請比對 Release 說明中的 SHA-256：
>
> ```powershell
> Get-FileHash .\MinimalMarkdownReader-Setup-<版本>.exe -Algorithm SHA256
> ```
>
> 輸出的雜湊值與 Release 說明不一致時，請勿安裝。安裝檔目前未經程式碼簽章，Windows SmartScreen 可能顯示「未知的發行者」。

安裝檔為自帶 .NET 10 執行階段的 Windows x64 版本。執行後，程式會安裝至目前使用者的 AppData，並建立開始功能表捷徑；可在安裝過程選擇是否建立桌面捷徑。仍需要 Microsoft Edge WebView2 Runtime。

安裝程式會註冊 `.md`、`.markdown` 的檔案類型與 MinimalMarkdownReader 圖示。安裝後可將其設為預設開啟程式；雙擊檔案時，程式會載入 Windows 傳入的文件路徑。

若需自行建置安裝檔，先安裝 Inno Setup 6，然後在 `MinimalMarkdownReader` 資料夾執行下列指令；產出的安裝檔位於 `artifacts/installer/`（此資料夾已被 `.gitignore` 排除）：

```powershell
Remove-Item artifacts/publish -Recurse -Force -ErrorAction SilentlyContinue
dotnet restore MinimalMarkdownReader.sln --runtime win-x64
dotnet publish src/MinimalMarkdownReader/MinimalMarkdownReader.csproj -c Release -r win-x64 --self-contained true --no-restore --output artifacts/publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/MinimalMarkdownReader.iss
```

## 安全與限制

Markdown 原始 HTML 已停用，`{...}` 自訂屬性與影片／YouTube 嵌入擴充也未啟用；文件內的 JavaScript、事件屬性、iframe、影音、`javascript:` URI、遠端圖片與未允許的本機圖片都不會載入。閱讀頁面另有內容安全政策（CSP），並由程式攔截所有網路請求，只允許讀取文件資料夾內的圖片；下載與外部協定啟動一律封鎖。程式不提供編輯、目錄、搜尋、列印、PDF 匯出、雲端同步或 Mermaid。設定檔格式錯誤時會安全地改用預設值。

## 已驗證

2026-10-28 已在 Windows 上執行：`dotnet build MinimalMarkdownReader.sln --configuration Debug`、`dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug`；31 個單元測試全數通過，並以 `dotnet run` 手動驗證設定套用、資安測試文件與相對圖片。發布後的安裝檔尚未逐項手動驗證，請依 [測試計畫](docs/TEST_PLAN.md) 完成檢查。
