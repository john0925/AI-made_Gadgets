# 資安測試文件

開啟此文件時不應出現任何對話框、不應自動開啟瀏覽器，也不應載入任何遠端內容。下列 `{...}` 與連結都只應以純文字或一般連結顯示。

## 自訂屬性（應顯示為純文字）

滑鼠移過此標題不應有反應 {onmouseover="alert('generic attribute')"}

此段落不應有背景圖 {style="background:url(https://example.com/track.gif)"}

[點擊只應開啟瀏覽器，不應跳出對話框](https://example.com){onclick="alert('onclick')"}

## 影片嵌入（應顯示為「圖片未載入」）

![YouTube](https://www.youtube.com/watch?v=dQw4w9WgXcQ)

![遠端影片](https://example.com/movie.mp4)

## 其他

![遠端圖片](https://example.com/image.png)

![跳脫資料夾的圖片](../secret.png)

<img src="x" onerror="alert('raw html')">

[危險連結](javascript:alert('javascript uri'))

[本機檔案連結](file:///C:/Windows/win.ini)
