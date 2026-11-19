# Redrawn Studio 1.0.0

**Crossing Dead** · Independent portrait manager for Probably Stolen / 獨立立繪管理器

[English documentation](README_en.md) · [繁體中文說明](README_zh-TW.md)

Native mod loading, an F9 portrait editor, live reload, layout adjustment and multiple portrait sources, with English and Traditional Chinese UI.

提供遊戲原生模組載入、F9 立繪編輯器、即時重載、位置與大小調整、多來源管理，以及英文／繁體中文介面。

## Source repository / 原始碼倉庫

This repository contains source code, build/import scripts, manifests and bilingual catalog data. Original artwork, third-party assets, game Managed assemblies and compiled output are excluded. Add artwork you are authorized to use locally. A source checkout is not the complete illustrated release package. `C:\Games\Probably Stolen` in documentation is an example, not a detected installation.

本倉庫包含原始碼、編譯／匯入腳本、manifest 及雙語目錄資料；不包含原創圖像、第三方素材、遊戲 Managed 程式集或編譯產物。請在本機加入有權使用的美術。原始碼倉庫並非附完整立繪的發行包。文件中的 `C:\Games\Probably Stolen` 為範例路徑。

Build with .NET SDK 6 or newer and your locally installed game assemblies. `Build.ps1` requires an explicit `-GameDir`; importing requires `-GameDir` or `-TargetModDir`. Do not commit local game files. Omit `-Install` to avoid changing the installed mod.

編譯需 .NET SDK 6 或更新版本及本機遊戲程式集。`Build.ps1` 需明確指定 `-GameDir`；匯入時需指定 `-GameDir` 或 `-TargetModDir`。不要提交本機遊戲檔案；不加 `-Install` 即不變更已安裝模組。

## Credits and AI disclosure / 致謝與 AI 使用聲明

Special thanks to **RetroAnime** (Workshop 3832229198) and **Portrait Studio by Flaming** (Workshop 3829932963). RetroAnime was a technical reference for native loading, high-resolution portrait replacement and bottom-centred scaling. Portrait Studio inspired separate special-guest portraits, live switching, layout controls and multi-source management. Development examined distributed mod structures, configuration formats and compiled assembly behaviour, with AI-assisted implementation. Neither reference mod's DLL or artwork is bundled, and neither is a required dependency.

特別感謝 **RetroAnime**（工作坊 3832229198）與 **Flaming 的 Portrait Studio**（工作坊 3829932963）。前者為原生載入、高解析度立繪替換及底部置中縮放的技術參考；後者啟發特殊客人獨立立繪、即時切換、版面調整及多來源管理。開發時分析已發行模組結構、配置格式與編譯後程式集行為，並以 AI 輔助實作；未內附兩者 DLL 或美術，亦無須以其為前置模組。

Developed using AI-assisted vibe coding with OpenAI ChatGPT under Crossing Dead's direction, including implementation, debugging, UI development, localization and compatibility work. Original Redrawn portrait artwork is drawn by Evans Goethe and is not included here. Full bilingual credits are retained in both manifests.

由 Crossing Dead 主導，以 OpenAI ChatGPT 輔助 vibe coding，涵蓋程式實作、除錯、介面、在地化與相容性工作。Redrawn 原創立繪由 Evans Goethe 親繪，本倉庫未收錄。兩份 manifest 均保留完整雙語致謝。

## Rights and validation / 權利與驗證

No open-source license has been selected or granted by this upload. Attribution does not grant rights to third-party assets. See [VALIDATION.md](VALIDATION.md) for the actual checks and remaining limitations; version 1.0.0 does not imply in-game acceptance.

此次上傳未選定或授予開源授權；致謝不代表取得第三方素材使用權。實際檢查與限制請見 [VALIDATION.md](VALIDATION.md)；1.0.0 版本號不表示已通過實機驗收。


## Developer / 開發者

Developed and maintained by **Crossing Dead**, under **Crossing Dead Development**. Artwork attribution remains **Evans Goethe**. Legacy technical identifiers (including the mod ID and C# namespace) are retained for compatibility and are not developer credits.

由 **Crossing Dead** 開發與維護，隸屬 **Crossing Dead Development**。美術作者仍署名 **Evans Goethe**。既有模組 ID 與 C# 命名空間保留相容性，不作為開發者署名。
