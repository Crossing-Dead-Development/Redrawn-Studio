using UnityEngine;
namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry
    {
        internal const string DisplayName = "Redrawn Studio";
        internal const string ReleaseVersion = "1.0.0";
        private static ConfigEntry<int> language;
        // 0 = OS language; 1 = English; 2 = Traditional Chinese.
        private static bool Chinese => language != null && language.Value != 0
            ? language.Value == 2
            : Application.systemLanguage == SystemLanguage.Chinese || Application.systemLanguage == SystemLanguage.ChineseTraditional || Application.systemLanguage == SystemLanguage.ChineseSimplified;
        private static string L(string zh, string en) => Chinese ? zh : en;
        private static void InitLanguage(ModConfig config)
        {
            language = config.Define("studio_language", 0, "Language / 語言", () => "Language / 語言 (0 Auto / 1 English / 2 繁體中文)", min:0, max:2, tooltip:() => "0 = OS language / 系統語言; 1 = English; 2 = 繁體中文", requiresRestart:false);
        }
        private static void ToggleLanguage(){if(language!=null) language.Set(Chinese?1:2);}
    }
}
