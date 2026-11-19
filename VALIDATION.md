# Validation / 驗證紀錄

Checked on 2026-11-19 for Redrawn Studio 1.0.0 by Evans Goethe.

## Passed / 已通過

- All 9 C# source files are byte-for-byte identical to the user-selected project folder. / 9 個 C# 檔案與指定工程逐位元組一致。
- Both manifests match the latest supplied 1.0.0 credits patch, including author, version, bilingual technical references and AI disclosure. / 兩份 manifest 與最新致謝補丁一致。
- All 410 catalog rows contain an ID, English name and Traditional Chinese name. / 410 筆目錄均有 ID、英文與繁體中文名稱。
- Both PowerShell scripts parse without errors. / 兩份 PowerShell 腳本語法解析通過。
- Clean Release compilation through Build.ps1 passed with .NET SDK 10.0.401 targeting netstandard2.1, using locally installed game Managed references: 0 warnings, 0 errors. Build output stayed in a separate temporary copy. / 使用本機遊戲參考組件，在獨立暫存副本執行 Build.ps1，Release 編譯 0 警告、0 錯誤。
- Upload inventory excludes 25 original PNGs, compiled DLL/PDB files, bin/obj output and third-party binaries. Pattern scans found no credential markers or user-specific absolute paths in upload files. This is a limited scan, not a security audit. / 上傳清單排除 25 張原創 PNG、DLL/PDB、bin/obj 及第三方二進位檔；模式掃描未發現憑證標記或使用者專屬絕對路徑，並非完整安全稽核。

## Packaging changes / 倉庫整理

Added .gitignore, .gitattributes, a bilingual entry README and this verification record. Removed hardcoded GameDir defaults; callers must supply their installation location. Updated language-specific READMEs to describe the source-only contents and actual verification status. Original project files were not overwritten. No license was selected.

新增忽略規則、文字格式規則、雙語入口 README 與驗證紀錄；移除 GameDir 固定預設路徑，改為由使用者指定。更新雙語文件的素材範圍與驗證狀態；未覆蓋原工程，也未選定授權。

## Not verified / 未驗證

- No game launch, F9 UI check, Chinese glyph check, language persistence test or live reload test. / 未啟動遊戲，未測 F9、中文字形、語言保存及即時重載。
- No compatibility test with RetroAnime, Portrait Studio or other packs. / 未與其他立繪模組或素材包實測。
- No execution of the import script or the -Install branch. / 未執行匯入腳本或 -Install 安裝分支。
- Original, night and wanted artwork is absent. Related rendering paths require authorized local images; missing images may produce warnings and retain game/provider artwork. / 未附一般、夜景及通緝立繪；相關功能需本機素材，缺圖可能警告並保留遊戲或來源圖像。
- The build verifies only the locally available game assembly version; it does not establish compatibility with every game version or minimum SDK version. / 編譯僅驗證當前本機遊戲組件，不代表支援所有遊戲或最低 SDK 版本。
