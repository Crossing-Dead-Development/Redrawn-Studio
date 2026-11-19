# Redrawn Studio 1.0.0
Evans Goethe · 獨立高解析度立繪管理器

Redrawn Studio 提供遊戲原生模組載入、獨立 Canvas 管理介面、即時重載與多立繪來源選擇。不需 198、963 或其他前置模組。介面及角色目錄支援繁體中文與英文。1.0.0 為指定的正式版本號；本發行包為原始碼，尚未通過實機驗收。

## 安裝或升級
1. 關閉遊戲並備份工程，複製或解壓本原始碼倉庫。如需立繪，請在本機加入有權使用的素材；此倉庫不含原創 PNG。
2. 安裝 .NET SDK 6 或更新版本。在工程根目錄執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -GameDir 'C:\Games\Probably Stolen' -Install -Language zh-TW
```

`-Install` 自動尋找遊戲 Mods 資料夾內相同 ID 的舊版，備份至遊戲根目錄的 `RedrawnStudio_Backups`，更名為 `RedrawnStudio`，只更新 DLL、manifest 及角色目錄。PNG、Sources、角色 CSV 不覆蓋。若舊版不在預設目錄，額外指定 `-InstalledModDir '實際模組資料夾路徑'`。不加 `-Install` 則只編譯及更新開發工程內的模組資料夾。編譯失敗不更改素材或已安裝模組。請勿同時啟用兩份相同模組 ID 的副本。首次安裝會複製完整素材工程。

模組顯示名稱、輸出 DLL (`RedrawnStudio.dll`)、工程檔 (`RedrawnStudio.csproj`)、產品及資料夾名稱統一為 Redrawn Studio。穩定內部 ID `evansgoethe.redrawn`、設定鍵與 CSV 中的內部來源值 `Redrawn` / `ProviderDefault` 保留相容；不要手動更名這些值。編譯成功後會移除舊 `.csproj`，請先備份工程。更新到新目錄後，SAI 的輸出路徑請改為新目錄內的 Portraits。

## 語言
進入遊戲，按 F9。標題旁的 English / 繁體中文 按鈕切換語言，保留頁面、搜尋和未存的編輯預覽。設定依遊戲原生模組設定儲存。原生設定 `studio_language`：0 = 自動（作業系統語言，中文環境使用繁體），1 = English，2 = 繁體中文。原生設定列表的群組以雙語顯示，名稱與提示依語言產生；若沒有立即刷新，重新開啟設定頁。角色圖庫 410 筆名稱均有中英文欄位，搜尋比對兩欄及 ID。第三方模組名稱、角色 ID、路徑及底層例外不翻譯。中文顯示使用系統字型（Windows 微軟正黑體優先），不內附字型。

## 日常使用
- F9 開啟／關閉；角色圖庫、來源管理、目前事件、原始 Sprite 可切換。原版預覽需等角色實際來訪。
- PNG 放入已安裝的 `RedrawnStudio/Portraits`，檔名與角色 ID 一致可自動註冊。別名用 `portrait-map.csv`（key,portrait，不含副檔名）。重載每 1.5 秒檢查，等待連續兩次穩定檔案狀態；也可手動刷新。
- 全局預設 88%，個別倍率與全局相乘；底部置中。拖曳、滾輪、數字欄位可校準個別人物，儲存寫入 `portrait-layout.csv`（key,percent,x,y）。未存的個別調整切換角色或關閉面板時捨棄。
- 使用原版永久存入 `portrait-original.csv`；暫時比較原版不存檔。`vanilla_client_louOperativeIntro` 不再使用錯誤的 wanted5 別名。特殊客人優先匹配獨立事件 ID（非改造人原版角色）。
- 夜景與通緝照片替換保留，與一般人物比例無關，尚未支援即時重載。

## 多來源管理
自動偵測由 `ModHook.OnModAssetsLoaded` 通知的已啟用模組。支援根目錄 `portrait-map.csv` + `Portraits/<asset>.png`（198 格式），已知角色 ID 同名的 `Portraits` / `Textures` PNG，及每個角色子資料夾只有一張 PNG 的簡單包。原生來源只讀，不複製其美術，不自行執行其 DLL。未啟用模組不會自動列出。也可用 `ImportPortraitSource.ps1` 將相容包或 963 靜態 `pack.json` 匯入本機 `Sources`：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ImportPortraitSource.ps1 -Source '完整來源資料夾或 pack.json 路徑' -SourceId RetroAnime -Language zh-TW
```

在 F9 來源管理批量套用，或在角色頁單獨切換。使用原版優先；跟隨來源模組會停止接管；使用選定素材則由 Studio 靜態渲染。來源選擇在 `portrait-source.csv`；失效時回退至自己的重繪或遊戲／來源提供的立繪。批量套用保留永久原版選擇。選擇跟隨來源時不套用 Studio 的縮放與位置。

## 相容性及驗收
保留 v0.0.7 的 Harmony 早／晚期查詢優先級；不能保證其他 DLL 提前改寫字典或後續覆蓋時仍能得到真正的原圖，動畫、表情差分、隨機池及額外場景來源亦未保證相容。初次測試請停用 963（另一管理器），先測自身 PNG、四商人、原版切換、熱重載，再啟用 198 測試個別與批量來源、停用來源回退。最後切換兩種語言、重啟確認語言及配置保留，以及檢查中文是否正確顯示。

本包沒有遊戲 DLL、第三方素材或編譯後 DLL。本倉庫刻意排除 25 張原創 PNG；請在本機自行放入有權使用的素材。已通過靜態檢查與 Build.ps1 的 Release 編譯（0 警告、0 錯誤）；尚未執行匯入／安裝流程或 Unity 實機測試；1.0.0 不代表測試已經通過。


## 致謝與權利
RetroAnime／Portrait Studio 的技術參考及 AI vibe coding 揭露請見 [README.md](README.md)。尚未選定開源授權。驗證限制請見 [VALIDATION.md](VALIDATION.md)。
