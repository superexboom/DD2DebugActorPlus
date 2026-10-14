using System;
using System.Text.RegularExpressions;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Dot;
using Assets.Code.Item;
using Assets.Code.Locale;
using Assets.Code.Quirk;
using Assets.Code.Token;
using Assets.Code.UI;
using Assets.Code.Utils;

namespace DD2DebugActorPlus
{
    /// <summary>Localized names and descriptions taken from the game's own description helpers.</summary>
    internal static class GameText
    {
        private static readonly Regex RichTextTagRegex = new Regex("<.*?>");

        internal static string Localized(string fallback, params string[] keys)
        {
            if (!Singleton<Localization>.HasInstance()) return Plain(fallback);
            try
            {
                foreach (string key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    string text = Plain(Singleton<Localization>.Instance.TryGetString(key, false));
                    if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return text;
                    }
                }
            }
            catch
            {
            }

            return Plain(fallback);
        }

        /// <summary>Single-line text without rich-text tags (names, ids, labels).</summary>
        internal static string Plain(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = RichTextTagRegex.Replace(StripMissingKeys(text), "");
            cleaned = cleaned.Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "" : cleaned;
        }

        /// <summary>Rich text kept for TextMeshPro (colors, inline sprites, line breaks).</summary>
        internal static string Rich(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = StripMissingKeys(text).Replace("\r", "").Trim();
            bool empty = string.IsNullOrWhiteSpace(RichTextTagRegex.Replace(cleaned, "")) &&
                cleaned.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) < 0;
            return empty ? "" : cleaned;
        }

        // Localization.GetString returns "<color=#00FFFF>key</color>" for keys the game lacks.
        private static readonly Regex MissingKeyRegex = new Regex("<color=#00FFFF>[^<]*</color>", RegexOptions.IgnoreCase);

        /// <summary>Drops the game's highlighted missing-key placeholders (e.g. token_anti_block_description).</summary>
        internal static string StripMissingKeys(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("#00FFFF", StringComparison.OrdinalIgnoreCase) < 0) return text ?? "";
            return MissingKeyRegex.Replace(text, "");
        }

        private static readonly Regex SpriteTagRegex = new Regex("<sprite(?<attrs>[^>]*)>", RegexOptions.IgnoreCase);
        private static readonly Regex SpriteNameRegex = new Regex("name\\s*=\\s*[\"']?(?<name>[^\"'\\s>]+)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Replaces inline icon tags the sprite asset cannot draw with the token / DOT name, so
        /// descriptions never show raw &lt;sprite&gt; markup.
        /// </summary>
        internal static string SpriteTagsToNames(string rich, Func<string, bool> keepIcon = null)
        {
            if (string.IsNullOrEmpty(rich) || rich.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) < 0) return rich;
            return SpriteTagRegex.Replace(rich, match =>
            {
                Match nameMatch = SpriteNameRegex.Match(match.Groups["attrs"].Value);
                string sprite = nameMatch.Success ? nameMatch.Groups["name"].Value : "";
                if (string.IsNullOrWhiteSpace(sprite)) return "";
                if (keepIcon != null && keepIcon(sprite)) return match.Value;
                string name = "";
                if (sprite.StartsWith("token_", StringComparison.OrdinalIgnoreCase))
                {
                    name = Localized("", "token_name_" + sprite.Substring("token_".Length));
                }
                else if (sprite.StartsWith("dot_", StringComparison.OrdinalIgnoreCase))
                {
                    name = Localized("", "dot_name_" + sprite.Substring("dot_".Length));
                }

                if (string.IsNullOrWhiteSpace(name)) name = Localized(sprite, sprite);
                return "<color=#D6B066>[" + name + "]</color>";
            });
        }

        internal static string ActorName(ActorInstance actor)
        {
            if (actor == null) return "";
            string name = Plain(actor.ActorName);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            string dataId = Plain(actor.ActorDataId);
            return Localized(dataId, dataId, "hero_name_" + dataId, "hero_select_" + dataId);
        }

        internal static string TokenName(TokenDefinition token)
        {
            if (token == null) return "";
            // About a third of the tokens have no "token_name_" key; the game's helper then returns a
            // highlighted missing-key placeholder, so look the key up directly and fall back to the id.
            // ("token_<id>" is only the inline icon glyph, not a name.)
            return Localized(token.Id, "token_name_" + token.Id);
        }

        internal static string TokenDetail(TokenDefinition token)
        {
            if (token == null) return "";
            try
            {
                return Rich(TokenDescription.GetDetailString(token));
            }
            catch
            {
                return "";
            }
        }

        internal static string BuffName(BuffDefinition buff)
        {
            if (buff == null) return "";
            string id = buff.Id;
            return Localized(id, "buff_name_" + id, "buff_" + id, id);
        }

        internal static string BuffDetail(BuffDefinition buff)
        {
            if (buff == null) return "";
            try
            {
                return Rich(BuffDescription.GetDescription(buff, true));
            }
            catch
            {
                return "";
            }
        }

        internal static string DotType(DotDefinition dot)
        {
            if (dot == null) return "";
            return string.IsNullOrWhiteSpace(dot.m_Type) ? dot.Id : dot.m_Type;
        }

        internal static string DotTypeName(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "";
            try
            {
                string name = Plain(DotDescription.GetUnglyphedName(type));
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("dot_name_" + type)) return name;
            }
            catch
            {
            }

            return Localized(type, "dot_name_" + type, "dot_" + type);
        }

        internal static string DotName(DotDefinition dot)
        {
            if (dot == null) return "";
            string type = DotType(dot);
            string typeName = DotTypeName(type);
            return string.IsNullOrWhiteSpace(typeName) || typeName == type ? Localized(dot.Id, "dot_name_" + type, dot.Id) : typeName;
        }

        internal static string DotDetail(DotDefinition dot)
        {
            if (dot == null) return "";
            try
            {
                int magnitude = DotDescription.GetDotMagnitude(dot);
                return Rich(DotDescription.GetDescription(DotType(dot), magnitude, false));
            }
            catch
            {
                return "";
            }
        }

        internal static string QuirkName(QuirkDefinition quirk)
        {
            if (quirk == null) return "";
            try
            {
                string name = Plain(QuirkDescription.GetNameString(quirk, (string)null, false));
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains(quirk.Id)) return name;
            }
            catch
            {
            }

            return Localized(quirk.Id, quirk.Id, "quirk_name_" + quirk.Id, "quirk_" + quirk.Id);
        }

        internal static string QuirkDetail(QuirkDefinition quirk)
        {
            if (quirk == null) return "";
            try
            {
                string description = QuirkDescription.GetDescriptionString(quirk, (string)null);
                if (!string.IsNullOrWhiteSpace(description)) return Rich(description);
            }
            catch
            {
            }

            return QuirkKindLabel(quirk);
        }

        internal static string QuirkKindLabel(QuirkDefinition quirk)
        {
            if (quirk == null) return "";
            if (quirk.IsDisease) return L.T("Disease", "疾病");
            if (quirk.IsCurse) return L.T("Curse", "诅咒");
            return quirk.IsNegative ? L.T("Negative quirk", "负面特质") : L.T("Positive quirk", "正面特质");
        }

        internal static string ItemName(ItemDefinition item)
        {
            if (item == null) return "";
            try
            {
                string title = Plain(ItemDescription.GetTitle(item, 1));
                if (!string.IsNullOrWhiteSpace(title)) return title;
            }
            catch
            {
            }

            return Localized(item.m_id, "item_name_" + item.m_id);
        }

        internal static string ItemDetail(ItemDefinition item)
        {
            if (item == null) return "";
            try
            {
                return Rich(ItemDescription.GetDescription(item, -1, false, 0, false, false, false, false, true, null));
            }
            catch
            {
                return "";
            }
        }
    }
}
