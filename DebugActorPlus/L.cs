using System;
using Assets.Code.Locale;
using Assets.Code.Utils;
using UnityEngine;

namespace DD2DebugActorPlus
{
    /// <summary>
    /// Plugin UI language. "auto" follows the game's language so the plugin text and the
    /// game's own names (tokens, quirks, heroes) are always in the same language and the
    /// game's CJK font fallbacks are already loaded.
    /// </summary>
    internal static class L
    {
        private static string _mode = "auto";

        internal static string Mode => _mode;

        internal static void SetMode(string mode)
        {
            string value = string.IsNullOrWhiteSpace(mode) ? "auto" : mode.Trim().ToLowerInvariant();
            _mode = value == "zh" || value == "en" ? value : "auto";
        }

        internal static bool IsChinese
        {
            get
            {
                if (_mode == "zh") return true;
                if (_mode == "en") return false;
                return GameIsChinese();
            }
        }

        internal static bool GameIsChinese()
        {
            try
            {
                if (Singleton<Localization>.HasInstance())
                {
                    LanguageDefinition language = Singleton<Localization>.Instance.GetLanguage();
                    if (language != null) return language.IsChinese;
                }
            }
            catch
            {
            }

            return Application.systemLanguage == SystemLanguage.Chinese ||
                Application.systemLanguage == SystemLanguage.ChineseSimplified ||
                Application.systemLanguage == SystemLanguage.ChineseTraditional;
        }

        internal static string T(string english, string chinese)
        {
            return IsChinese ? chinese : english;
        }

        internal static string F(string english, string chinese, params object[] args)
        {
            try
            {
                return string.Format(T(english, chinese), args);
            }
            catch (FormatException)
            {
                return T(english, chinese);
            }
        }

        /// <summary>Label shown on the header language button: the language you switch to.</summary>
        internal static string NextModeLabel()
        {
            return IsChinese ? "EN" : "中文";
        }

        internal static string NextMode()
        {
            return IsChinese ? "en" : "zh";
        }
    }
}
