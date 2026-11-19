# Redrawn Studio 1.0.0
Crossing Dead · Independent high-resolution portrait manager

Redrawn Studio provides native mod loading, a custom Canvas editor, live reloading and multiple portrait sources. No prerequisite mod is required. English and Traditional Chinese are available in the Studio and its character catalog. 1.0.0 is the designated release version; this is a source release pending in-game validation.

## Install or upgrade
1. Close the game and back up your project. Clone or extract this source repository. Add authorized artwork locally if desired; this checkout excludes all original PNGs.
2. Install .NET SDK 6 or later. Run from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -GameDir 'C:\Games\Probably Stolen' -Install
```

`-Install` looks for the same mod ID in the game's Mods folder, backs up the previous installation into `RedrawnStudio_Backups` outside Mods, renames the folder to `RedrawnStudio`, and updates only the DLL, manifest and catalog. Existing PNGs, Sources and character CSV files remain intact. Use `-InstalledModDir 'actual installed mod path'` for non-standard installations. Omit `-Install` to build and update the project asset folder only. A failed build does not change assets or the installed mod. Never enable two copies with the same mod ID. A fresh installation copies the complete built asset folder.

The display name, product, assembly (`RedrawnStudio.dll`), project (`RedrawnStudio.csproj`) and folder are consistently named Redrawn Studio. The stable mod ID `evansgoethe.redrawn`, setting keys and serialized source values `Redrawn` / `ProviderDefault` remain unchanged for compatibility. Do not rename those values. After a successful build the old project file is removed; back up the project first. Change your art application's export path to the new installed Portraits directory.

## Language
Press F9 in-game. Use the English / 繁體中文 button beside the title to switch language without losing the page, search or draft preview. The preference is stored by the native mod config. Setting `studio_language`: 0 = automatic (OS language; Chinese uses Traditional Chinese), 1 = English, 2 = Traditional Chinese. Native settings groups are bilingual; labels and tooltips are generated in the chosen language. Reopen the game's settings if it does not refresh immediately. All 410 catalog entries have English and Traditional Chinese display fields; search matches both and the ID. Third-party titles, character IDs, paths and raw exceptions are not translated. Chinese rendering uses system fonts (Microsoft JhengHei preferred on Windows); no fonts are bundled.

## Daily workflow
- F9 opens or closes Studio. Navigate the library, source manager, current event or native sprite. Original previews require a tracked character visit.
- Export PNGs to the installed `RedrawnStudio/Portraits` folder. An exact character-ID filename is registered automatically. Use `portrait-map.csv` (`key,portrait`, without extension) for aliases. Live reload checks every 1.5 seconds and waits for two stable file observations; manual reload is also available.
- Global scale defaults to 88%, multiplied by individual scale, bottom-centred. Drag, wheel or enter numeric values for layout. Save writes `portrait-layout.csv` (`key,percent,x,y`); unsaved edits are discarded on character changes or closing Studio.
- Use original saves a persistent choice in `portrait-original.csv`; Compare original is temporary. The erroneous `vanilla_client_louOperativeIntro` to wanted5 alias is ignored. Separate guests prefer event-specific IDs for non-augmented vanilla visitors.
- Night art and wanted photo replacements remain separate from counter scale and do not currently support live reload.

## Multiple portrait sources
Automatically discovers enabled mods reported by `ModHook.OnModAssetsLoaded`. Supports `portrait-map.csv` + `Portraits/<asset>.png` (198 format), known ID-named PNGs in `Portraits` / `Textures`, and simple character subfolders containing exactly one PNG. Native sources are read-only; Studio neither copies their artwork nor loads their DLLs itself. Disabled mods are not discovered. To import a compatible pack or a static Portrait Studio (963) `pack.json` into local `Sources`, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ImportPortraitSource.ps1 -Source 'path to source folder or pack.json' -SourceId RetroAnime
```

Open Sources in F9 to apply a whole pack, or switch sources per character. Use original has priority; Follow provider releases the character to other mods or game art; Use selected art lets Studio render the selected static PNG. Choices are saved in `portrait-source.csv`; unavailable sources fall back to own artwork or provider/game results. Bulk apply preserves persistent original choices. Studio layout does not apply when following the provider.

## Compatibility and validation
Retains v0.0.7 Harmony early/late lookup coordination. Other DLLs may rewrite original dictionaries or override the renderer later; true vanilla restoration, animated expressions, random portrait pools and other scene sources are not universally guaranteed. For the first test, disable 963 (a second manager), test own PNGs, four merchants, original restoration and live reload, then enable 198 and verify individual/bulk selection and disabled-source fallback. Test both languages, restart to check language/config persistence, and verify Chinese glyph rendering.

No game DLLs, third-party art or compiled DLL are bundled. The 25 original PNGs are intentionally excluded from this repository; supply authorized artwork locally. Static checks and a clean Release build using Build.ps1 passed (0 warnings, 0 errors). Import/install execution and Unity runtime testing have not been performed. The 1.0.0 version number does not imply these tests have passed.


## Credits and rights
See [README.md](README.md#credits-and-ai-disclosure--致謝與-ai-使用聲明) for RetroAnime / Portrait Studio technical references and the AI vibe coding disclosure. No open-source license has been selected. See [VALIDATION.md](VALIDATION.md) for verification limits.
