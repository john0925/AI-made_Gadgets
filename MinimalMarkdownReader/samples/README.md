# MarkdownReader 範例文件

這是一段繁體中文長文字，用於檢查閱讀時的字型、字級、行距和文字對齊。**粗體**、*斜體* 與 `行內程式碼` 都應清楚顯示。

> 這是引用文字。切換置中或靠右時，引用內容應一起調整。

## 清單與表格

1. 第一個項目
   - 巢狀項目
   - 第二個巢狀項目
2. 第二個項目

| 靠左 | 置中 | 靠右 |
| :--- | :--: | ---: |
| A | B | 3 |

## 程式碼

```csharp
var title = "MarkdownReader";
Console.WriteLine(title);
```

`this_is_an_intentionally_long_code_token_for_horizontal_scrolling_validation_abcdefghijklmnopqrstuvwxyz_0123456789`

## 資源與連結

![不存在的測試圖片](missing-image.png)

[OpenAI](https://openai.com/)

下列文字應只以純文字顯示，絕不執行：

<script>alert('unsafe')</script>

[危險連結](javascript:alert('unsafe'))
