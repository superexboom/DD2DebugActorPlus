using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Dot;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Quirk;
using Assets.Code.Token;
using Assets.Code.UI;
using Assets.Code.UI.Data;
using Assets.Code.UI.RunLog;
using Assets.Code.Utils;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    internal enum QuirkIconKind
    {
        Positive,
        Negative,
        Disease,
    }

    /// <summary>Game sprites (icons and portraits) looked up from the game's resource databases.</summary>
    internal static class GameIcons
    {
        private const float MissingRetrySeconds = 5f;

        private static readonly Dictionary<string, Sprite> ItemSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> TokenSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> DotSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> BuffSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> PortraitSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<QuirkIconKind, Sprite> QuirkSprites = new Dictionary<QuirkIconKind, Sprite>();
        private static readonly Dictionary<string, float> MissingRetryAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        internal static void ClearMissing()
        {
            MissingRetryAt.Clear();
        }

        private static bool ShouldRetry(string key)
        {
            return !MissingRetryAt.TryGetValue(key, out float retryAt) || Time.unscaledTime >= retryAt;
        }

        private static void MarkMissing(string key)
        {
            MissingRetryAt[key] = Time.unscaledTime + MissingRetrySeconds;
        }

        internal static Sprite Item(ItemDefinition item)
        {
            if (item == null) return null;
            if (ItemSprites.TryGetValue(item.m_id, out Sprite cached)) return cached;

            Sprite sprite = null;
            try
            {
                if (Singleton<ResourceDatabaseItem>.HasInstance())
                {
                    ResourceItem resource = Singleton<ResourceDatabaseItem>.Instance.GetResource(item.m_id, true, null);
                    if (resource?.m_iconPrefab != null && resource.m_iconPrefab.RuntimeKeyIsValid())
                    {
                        AsyncOperationHandle<GameObject> handle = resource.m_iconPrefab.LoadAssetAsync();
                        GameObject prefab = handle.WaitForCompletion();
                        Image image = prefab != null ? prefab.GetComponentInChildren<Image>(true) : null;
                        sprite = image != null ? image.sprite : null;
                    }
                }
            }
            catch
            {
                sprite = null;
            }

            ItemSprites[item.m_id] = sprite;
            return sprite;
        }

        internal static Sprite Token(string tokenId)
        {
            if (string.IsNullOrWhiteSpace(tokenId)) return null;
            if (TokenSprites.TryGetValue(tokenId, out Sprite cached)) return cached;
            string key = "token:" + tokenId;
            if (!ShouldRetry(key)) return null;

            Sprite sprite = null;
            try
            {
                if (Singleton<ResourceDatabaseTokens>.HasInstance())
                {
                    ResourceToken resource = Singleton<ResourceDatabaseTokens>.Instance.GetResource(tokenId, true, null);
                    sprite = resource?.m_TokenSprite;
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null) TokenSprites[tokenId] = sprite;
            else MarkMissing(key);
            return sprite;
        }

        internal static Sprite Dot(string dotType)
        {
            if (string.IsNullOrWhiteSpace(dotType)) return null;
            if (DotSprites.TryGetValue(dotType, out Sprite cached)) return cached;
            string key = "dot:" + dotType;
            if (!ShouldRetry(key)) return null;

            Sprite sprite = null;
            try
            {
                if (Singleton<ResourceDatabaseDots>.HasInstance())
                {
                    ResourceDot resource = Singleton<ResourceDatabaseDots>.Instance.GetResource(dotType, true, true);
                    sprite = resource?.m_DotSprite;
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null) DotSprites[dotType] = sprite;
            else MarkMissing(key);
            return sprite;
        }

        internal static Sprite Buff(BuffDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) return null;
            if (BuffSprites.TryGetValue(definition.Id, out Sprite cached)) return cached;
            string key = "buff:" + definition.Id;
            if (!ShouldRetry(key)) return null;

            Sprite sprite = null;
            try
            {
                foreach (BuffIconBhv icon in BuffIcons())
                {
                    if (!BuffIconMatches(icon, definition)) continue;
                    BuffIconProperties properties = GetBuffIconProperties(icon);
                    if (properties?.Icon != null)
                    {
                        sprite = properties.Icon;
                        break;
                    }
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null) BuffSprites[definition.Id] = sprite;
            else MarkMissing(key);
            return sprite;
        }

        private static BuffIconBhv[] _buffIcons;
        private static float _buffIconsAt = -100f;

        // FindObjectsOfTypeAll is slow; list rows ask for many buff icons while scrolling.
        private static BuffIconBhv[] BuffIcons()
        {
            if (_buffIcons == null || Time.unscaledTime - _buffIconsAt > 10f)
            {
                _buffIcons = Resources.FindObjectsOfTypeAll<BuffIconBhv>();
                _buffIconsAt = Time.unscaledTime;
            }

            return _buffIcons;
        }

        private static bool BuffIconMatches(BuffIconBhv icon, BuffDefinition definition)
        {
            if (icon == null || definition == null || icon.RequiredTagCount <= 0 || definition.Tags == null || definition.Tags.Count == 0)
            {
                return false;
            }

            int matches = 0;
            foreach (string tag in definition.Tags)
            {
                if (icon.HasTagRequirement(tag)) matches++;
            }

            return matches == icon.RequiredTagCount;
        }

        private static BuffIconProperties GetBuffIconProperties(BuffIconBhv icon)
        {
            FieldInfo field = typeof(BuffIconBhv).GetField("m_iconProperties", BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(icon) as BuffIconProperties;
        }

        internal static QuirkIconKind QuirkKind(QuirkDefinition quirk)
        {
            if (quirk == null) return QuirkIconKind.Positive;
            if (quirk.IsDisease || quirk.IsCurse) return QuirkIconKind.Disease;
            return quirk.IsNegative ? QuirkIconKind.Negative : QuirkIconKind.Positive;
        }

        private static readonly Dictionary<string, Sprite> QuirkStatusSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Diseases and curses have their own status icon in the game; other quirks fall back to
        /// the run-log quirk icons, or to the editor's positive / negative / disease badge.
        /// </summary>
        internal static Sprite Quirk(QuirkDefinition quirk)
        {
            if (quirk == null) return null;
            if (!QuirkStatusSprites.TryGetValue(quirk.Id, out Sprite sprite))
            {
                sprite = null;
                try
                {
                    if (Singleton<ResourceDatabaseQuirk>.HasInstance())
                    {
                        ResourceQuirk resource = Singleton<ResourceDatabaseQuirk>.Instance.GetResource(quirk.Id, false, null);
                        sprite = resource != null ? resource.StatusSprite : null;
                    }
                }
                catch
                {
                    sprite = null;
                }

                QuirkStatusSprites[quirk.Id] = sprite;
            }

            if (sprite != null) return sprite;
            QuirkIconKind kind = QuirkKind(quirk);
            return Quirk(kind) ?? U.Theme?.QuirkBadge(kind);
        }

        internal static Sprite Quirk(QuirkIconKind kind)
        {
            if (QuirkSprites.TryGetValue(kind, out Sprite cached) && cached != null) return cached;
            string retryKey = "quirkkind:" + kind;
            if (!ShouldRetry(retryKey)) return null;
            MarkMissing(retryKey);

            Sprite sprite = null;
            try
            {
                string fieldName = kind == QuirkIconKind.Positive
                    ? "m_positiveSprite"
                    : kind == QuirkIconKind.Negative
                        ? "m_negativeSprite"
                        : "m_diseaseSprite";
                FieldInfo field = typeof(QuirkLogEntryBhv).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    foreach (QuirkLogEntryBhv entry in Resources.FindObjectsOfTypeAll<QuirkLogEntryBhv>())
                    {
                        sprite = field.GetValue(entry) as Sprite;
                        if (sprite != null) break;
                    }
                }
            }
            catch
            {
                sprite = null;
            }

            QuirkSprites[kind] = sprite;
            return sprite;
        }

        /// <summary>Hero colour portrait, or the turn-order portrait for monsters.</summary>
        internal static Sprite Portrait(ActorInstance actor)
        {
            if (actor == null) return null;
            string dataId = actor.ActorDataId;
            if (string.IsNullOrWhiteSpace(dataId)) return null;
            if (PortraitSprites.TryGetValue(dataId, out Sprite cached)) return cached;
            string key = "portrait:" + dataId;
            if (!ShouldRetry(key)) return null;

            Sprite sprite = null;
            try
            {
                if (Singleton<ResourceDatabaseActors>.HasInstance())
                {
                    ResourceActor resource = Singleton<ResourceDatabaseActors>.Instance.GetResource(dataId, true, null);
                    ResourceActor top = resource != null ? resource.GetTopMostParent() : null;
                    foreach (ResourceActor candidate in new[] { top, resource })
                    {
                        if (candidate == null) continue;
                        sprite = candidate.GetPortraitIconByType(ResourceActor.PortraitIconType.Color, null)
                            ?? candidate.GetPortraitIconByType(ResourceActor.PortraitIconType.TurnOrder, null)
                            ?? candidate.GetPortraitIconByType(ResourceActor.PortraitIconType.CombatBar, null);
                        if (sprite != null) break;
                    }
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null) PortraitSprites[dataId] = sprite;
            else MarkMissing(key);
            return sprite;
        }
    }
}
