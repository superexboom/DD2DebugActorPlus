using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Code.UI.Managers;
using Assets.Code.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DD2DebugActorPlus
{
    /// <summary>
    /// Fonts, colours and frame sprites. Text uses the game's own TextMeshPro fonts; frames are
    /// procedural placeholders unless a game sprite name is configured (Appearance.*Sprite).
    /// </summary>
    internal sealed class UiTheme
    {
        // Palette (dark wood / parchment / gold, close to the game's UI).
        internal static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.62f);
        internal static readonly Color Text = new Color(0.91f, 0.86f, 0.77f);
        internal static readonly Color TextMuted = new Color(0.60f, 0.55f, 0.48f);
        internal static readonly Color TextDim = new Color(0.44f, 0.40f, 0.35f);
        internal static readonly Color Gold = new Color(0.84f, 0.69f, 0.40f);
        internal static readonly Color Danger = new Color(0.86f, 0.40f, 0.32f);
        internal static readonly Color Success = new Color(0.66f, 0.80f, 0.52f);
        internal static readonly Color Positive = new Color(0.90f, 0.76f, 0.38f);
        internal static readonly Color Negative = new Color(0.52f, 0.58f, 0.92f);
        internal static readonly Color Neutral = new Color(0.66f, 0.63f, 0.58f);
        internal static readonly Color HealthFill = new Color(0.62f, 0.12f, 0.11f);
        internal static readonly Color StressFill = new Color(0.82f, 0.79f, 0.72f);
        internal static readonly Color BarBack = new Color(0.07f, 0.06f, 0.05f, 1f);

        internal TMP_FontAsset Body;
        internal TMP_FontAsset Title;

        internal Sprite Window;
        internal Sprite Panel;
        internal Sprite Card;
        internal Sprite CardSelected;
        internal Sprite Button;
        internal Sprite ButtonPrimary;
        internal Sprite ButtonDanger;
        internal Sprite Chip;
        internal Sprite ChipSelected;
        internal Sprite Field;
        internal Sprite Solid;
        internal Sprite Frame;

        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private static readonly List<TMP_FontAsset> AddedGlobalFallbacks = new List<TMP_FontAsset>();
        private static bool _resolversInstalled;

        internal static UiTheme Build(PluginSettings settings)
        {
            UiTheme theme = new UiTheme();
            theme.LoadFonts();
            theme.BuildSprites(settings);
            InstallTmpResolvers();
            return theme;
        }

        internal void Destroy()
        {
            foreach (UnityEngine.Object obj in _owned)
            {
                if (obj != null) UnityEngine.Object.Destroy(obj);
            }

            _owned.Clear();
        }

        private void LoadFonts()
        {
            try
            {
                if (SingletonMonoBehaviour<CommonUiBhv>.HasInstance(false))
                {
                    CommonUiBhv common = SingletonMonoBehaviour<CommonUiBhv>.Instance;
                    Body = ReadField<TMP_FontAsset>(common, "m_descFont");
                    Title = ReadField<TMP_FontAsset>(common, "m_titleFont");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Could not read the game's UI fonts: " + ex.Message);
            }

            if (Body == null) Body = TMP_Settings.defaultFontAsset;
            if (Body == null) Body = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            if (Title == null) Title = Body;

            if (L.IsChinese && !L.GameIsChinese())
            {
                EnsureChineseFallbacks();
            }

            Plugin.Log?.LogInfo($"UI fonts: body={Body?.name ?? "<none>"}, title={Title?.name ?? "<none>"}");
        }

        /// <summary>
        /// The game only loads its CJK fallback fonts when the game language is Chinese. When the
        /// plugin is set to Chinese while the game is not, register those fonts as global TMP
        /// fallbacks (only used for glyphs the primary font lacks).
        /// </summary>
        private static void EnsureChineseFallbacks()
        {
            try
            {
                if (!SingletonMonoBehaviour<CommonUiBhv>.HasInstance(false)) return;
                object list = ReadField<object>(SingletonMonoBehaviour<CommonUiBhv>.Instance, "m_ChineseFallbackList");
                if (list == null) return;
                List<TMP_FontAsset> fonts = new List<TMP_FontAsset>();
                foreach (string fieldName in new[] { "m_DescFallbackFontAssetTable", "m_TitleFallbackFontAssetTable" })
                {
                    if (!(ReadField<object>(list, fieldName) is IEnumerable references)) continue;
                    foreach (object reference in references)
                    {
                        if (!(reference is AssetReference assetReference) || !assetReference.RuntimeKeyIsValid()) continue;
                        TMP_FontAsset font = Addressables.LoadAssetAsync<TMP_FontAsset>(assetReference.RuntimeKey).WaitForCompletion();
                        if (font != null && !fonts.Contains(font)) fonts.Add(font);
                    }
                }

                if (fonts.Count == 0) return;
                List<TMP_FontAsset> global = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();
                foreach (TMP_FontAsset font in fonts)
                {
                    if (global.Contains(font)) continue;
                    global.Add(font);
                    AddedGlobalFallbacks.Add(font);
                }

                TMP_Settings.fallbackFontAssets = global;
                Plugin.Log?.LogInfo("Registered Chinese fallback fonts for the editor: " + string.Join(", ", fonts.Select(font => font.name).ToArray()));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Chinese fallback fonts unavailable: " + ex.Message);
            }
        }

        /// <summary>
        /// Lets inline &lt;sprite="asset"&gt; / &lt;font="name"&gt; tags in game descriptions resolve
        /// against assets the game has already loaded (TMP otherwise only looks in Resources).
        /// </summary>
        private static void InstallTmpResolvers()
        {
            if (_resolversInstalled) return;
            _resolversInstalled = true;
            try
            {
                TMP_Text.OnSpriteAssetRequest += ResolveSpriteAsset;
                TMP_Text.OnFontAssetRequest += ResolveFontAsset;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("TMP resolvers not installed: " + ex.Message);
            }
        }

        private static readonly Dictionary<string, TMP_SpriteAsset> SpriteAssetCache = new Dictionary<string, TMP_SpriteAsset>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, TMP_FontAsset> FontAssetCache = new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);

        private static TMP_SpriteAsset ResolveSpriteAsset(int hashCode, string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;
            if (SpriteAssetCache.TryGetValue(assetName, out TMP_SpriteAsset cached) && cached != null) return cached;
            TMP_SpriteAsset found = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>()
                .FirstOrDefault(asset => asset != null && string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase));
            if (found != null) SpriteAssetCache[assetName] = found;
            return found;
        }

        private static TMP_FontAsset ResolveFontAsset(int hashCode, string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;
            if (FontAssetCache.TryGetValue(assetName, out TMP_FontAsset cached) && cached != null) return cached;
            TMP_FontAsset found = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(asset => asset != null && string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase));
            if (found != null) FontAssetCache[assetName] = found;
            return found;
        }

        private void BuildSprites(PluginSettings settings)
        {
            Window = Box(new Color(0.063f, 0.051f, 0.043f, 0.98f), new Color(0.42f, 0.33f, 0.22f), 2, 4);
            Panel = Box(new Color(0.095f, 0.078f, 0.066f, 0.96f), new Color(0.24f, 0.19f, 0.14f), 1, 3);
            Card = Box(new Color(0.135f, 0.11f, 0.09f, 1f), new Color(0.26f, 0.21f, 0.16f), 1, 3);
            CardSelected = Box(new Color(0.22f, 0.16f, 0.08f, 1f), Gold, 2, 3);
            Button = Box(new Color(0.17f, 0.14f, 0.11f, 1f), new Color(0.42f, 0.33f, 0.22f), 1, 3);
            ButtonPrimary = Box(new Color(0.36f, 0.25f, 0.09f, 1f), new Color(0.83f, 0.65f, 0.35f), 1, 3);
            ButtonDanger = Box(new Color(0.30f, 0.09f, 0.07f, 1f), new Color(0.64f, 0.22f, 0.17f), 1, 3);
            Chip = Box(new Color(0.12f, 0.10f, 0.08f, 1f), new Color(0.29f, 0.23f, 0.17f), 1, 8);
            ChipSelected = Box(new Color(0.30f, 0.22f, 0.09f, 1f), Gold, 1, 8);
            Field = Box(new Color(0.04f, 0.035f, 0.03f, 1f), new Color(0.30f, 0.24f, 0.17f), 1, 3);
            Solid = Box(Color.white, Color.white, 0, 0);
            Frame = Box(new Color(1f, 1f, 1f, 0f), Color.white, 2, 3);
            QuirkPositive = Badge(new Color(0.55f, 0.43f, 0.16f), new Color(1f, 0.93f, 0.72f), 0);
            QuirkNegative = Badge(new Color(0.50f, 0.14f, 0.12f), new Color(1f, 0.85f, 0.80f), 1);
            QuirkDisease = Badge(new Color(0.30f, 0.22f, 0.42f), new Color(0.90f, 0.84f, 1f), 2);
            ScrollHandle = Solid;
            Divider = Solid;
            // Bars keep the drawn dark trough: a game input frame would cover the fill.
            BarTrough = Field;

            ApplyGameFrames(settings?.Frames);
        }

        internal Sprite ScrollHandle;
        internal Sprite Divider;
        internal Sprite BarTrough;

        /// <summary>Sprites that are game frames: how much to shrink their (large) 9-slice borders.</summary>
        private readonly Dictionary<Sprite, float> _multipliers = new Dictionary<Sprite, float>();
        private readonly HashSet<Sprite> _gameFrames = new HashSet<Sprite>();

        internal float MultiplierOf(Sprite sprite)
        {
            return sprite != null && _multipliers.TryGetValue(sprite, out float value) ? value : 1f;
        }

        internal bool IsGameFrame(Sprite sprite)
        {
            return sprite != null && _gameFrames.Contains(sprite);
        }

        /// <summary>
        /// Native frames, chosen from sprites the game keeps loaded from the main menu on (so they
        /// exist in every scene). "role=sprite@multiplier"; the multiplier shrinks the sprite's
        /// 9-slice border to fit the editor's small controls. Missing sprites keep the drawn frame.
        /// </summary>
        internal const string DefaultFrames =
            "Window=ui_popup@3; Panel=ui_selectionbox@4; Card=ui_button_black@5; CardSelected=ui_button_positive@5; " +
            "Button=ui_button_black@5; ButtonPrimary=ui_button_positive_pressed@5; ButtonDanger=ui_button_menu_pressed@3; " +
            "Chip=ui_button_black@6; ChipSelected=ui_button_positive_pressed@6; Field=ui_inputbg@1.5; " +
            "ScrollHandle=ui_scrollhandle@5; Divider=ui_double_rule@2";

        private void ApplyGameFrames(string overrides)
        {
            Dictionary<string, string> specs = ParseFrames(DefaultFrames);
            foreach (KeyValuePair<string, string> entry in ParseFrames(overrides)) specs[entry.Key] = entry.Value;

            Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sprite == null || sprite.border == Vector4.zero) continue;
                    if (!loaded.ContainsKey(sprite.name)) loaded[sprite.name] = sprite;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Could not list game sprites: " + ex.Message);
                return;
            }

            List<string> applied = new List<string>();
            foreach (KeyValuePair<string, string> spec in specs)
            {
                string value = spec.Value.Trim();
                if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase)) continue;
                string name = value;
                float multiplier = 1f;
                int at = value.LastIndexOf('@');
                if (at > 0)
                {
                    name = value.Substring(0, at).Trim();
                    float.TryParse(value.Substring(at + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out multiplier);
                    if (multiplier <= 0f) multiplier = 1f;
                }

                if (!loaded.TryGetValue(name, out Sprite sprite))
                {
                    Plugin.Log?.LogInfo($"Frame {spec.Key}: game sprite '{name}' not loaded, using the drawn frame.");
                    continue;
                }

                if (!AssignRole(spec.Key, sprite)) continue;
                _multipliers[sprite] = multiplier;
                _gameFrames.Add(sprite);
                applied.Add(spec.Key + "=" + name + "@" + multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            Plugin.Log?.LogInfo("Game frames: " + string.Join(", ", applied.ToArray()));
        }

        private bool AssignRole(string role, Sprite sprite)
        {
            switch (role.ToLowerInvariant())
            {
                case "window": Window = sprite; return true;
                case "panel": Panel = sprite; return true;
                case "card": Card = sprite; return true;
                case "cardselected": CardSelected = sprite; return true;
                case "button": Button = sprite; return true;
                case "buttonprimary": ButtonPrimary = sprite; return true;
                case "buttondanger": ButtonDanger = sprite; return true;
                case "chip": Chip = sprite; return true;
                case "chipselected": ChipSelected = sprite; return true;
                case "field": Field = sprite; return true;
                case "scrollhandle": ScrollHandle = sprite; return true;
                case "divider": Divider = sprite; return true;
                default:
                    Plugin.Log?.LogWarning("Unknown frame role: " + role);
                    return false;
            }
        }

        private static Dictionary<string, string> ParseFrames(string text)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (string part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                result[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
            }

            return result;
        }

        private static TMP_SpriteAsset _gameSprites;
        private static HashSet<string> _gameSpriteNames;
        private static float _gameSpritesRetryAt = -100f;
        private static bool _loggedSpriteCandidates;

        /// <summary>True when the game's icon sprite asset (or its fallbacks) has a sprite with this name.</summary>
        internal static bool HasGameSprite(string spriteName)
        {
            return _gameSpriteNames != null && !string.IsNullOrEmpty(spriteName) && _gameSpriteNames.Contains(spriteName);
        }

        private static HashSet<string> CollectSpriteNames(TMP_SpriteAsset root)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<TMP_SpriteAsset> visited = new HashSet<TMP_SpriteAsset>();
            Stack<TMP_SpriteAsset> pending = new Stack<TMP_SpriteAsset>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                TMP_SpriteAsset asset = pending.Pop();
                if (asset == null || !visited.Add(asset)) continue;
                if (asset.spriteCharacterTable != null)
                {
                    foreach (TMP_SpriteCharacter character in asset.spriteCharacterTable)
                    {
                        if (character != null && !string.IsNullOrEmpty(character.name)) names.Add(character.name);
                    }
                }

                if (asset.fallbackSpriteAssets != null)
                {
                    foreach (TMP_SpriteAsset fallback in asset.fallbackSpriteAssets) pending.Push(fallback);
                }
            }

            return names;
        }

        /// <summary>
        /// The game's TextMeshPro sprite asset holding the inline description icons (token_block,
        /// token_stress, ...). Game text components reference it explicitly; ours must too.
        /// </summary>
        internal static TMP_SpriteAsset GameSpriteAsset()
        {
            if (_gameSprites != null) return _gameSprites;
            if (Time.unscaledTime < _gameSpritesRetryAt) return null;
            _gameSpritesRetryAt = Time.unscaledTime + 5f;
            try
            {
                // Candidates: every loaded sprite asset, the ones game texts actually reference, and
                // the TMP default. Sprites may live in an asset's fallback chain, so search with fallbacks.
                HashSet<TMP_SpriteAsset> candidates = new HashSet<TMP_SpriteAsset>();
                foreach (TMP_SpriteAsset asset in Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>())
                {
                    if (asset != null) candidates.Add(asset);
                }

                foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (text != null && text.spriteAsset != null) candidates.Add(text.spriteAsset);
                }

                if (TMP_Settings.defaultSpriteAsset != null) candidates.Add(TMP_Settings.defaultSpriteAsset);

                // Compare sprite names directly. This TMP build hashes names one way in its lookup table
                // (TMP_TextUtilities.GetHashCode) and another in GetSpriteIndexFromName (GetSimpleHashCode),
                // so name/hash lookups from code miss sprites that the text parser itself finds.
                TMP_SpriteAsset best = null;
                HashSet<string> bestNames = null;
                int bestScore = 0;
                foreach (TMP_SpriteAsset asset in candidates)
                {
                    HashSet<string> names = CollectSpriteNames(asset);
                    int score = new[] { "token_block", "token_stress", "token_dodge", "token_horror" }.Count(names.Contains);
                    if (score > bestScore)
                    {
                        best = asset;
                        bestNames = names;
                        bestScore = score;
                    }
                }

                if (best != null)
                {
                    _gameSprites = best;
                    _gameSpriteNames = bestNames;
                    Plugin.Log?.LogInfo($"Inline description icons: sprite asset '{best.name}' ({bestNames.Count} sprites incl. fallbacks)");
                }
                else if (!_loggedSpriteCandidates)
                {
                    _loggedSpriteCandidates = true;
                    Plugin.Log?.LogInfo("No TMP sprite asset with token icons is loaded; descriptions show names instead. Candidates: " +
                        string.Join(" | ", candidates.Select(asset => asset.name + ": " + string.Join(",", CollectSpriteNames(asset).Take(25).ToArray())).ToArray()));
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("Sprite asset lookup failed: " + ex.Message);
            }

            return _gameSprites;
        }

        internal Sprite QuirkBadge(QuirkIconKind kind)
        {
            switch (kind)
            {
                case QuirkIconKind.Negative: return QuirkNegative;
                case QuirkIconKind.Disease: return QuirkDisease;
                default: return QuirkPositive;
            }
        }

        internal Sprite QuirkPositive;
        internal Sprite QuirkNegative;
        internal Sprite QuirkDisease;

        /// <summary>Round badge with a plus / minus / cross mark (quirks have no per-quirk icon in the game).</summary>
        private Sprite Badge(Color fill, Color mark, int symbol)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Vector2.Distance(p, center);
                    float circle = Mathf.Clamp01(28.5f - d);
                    if (circle <= 0f)
                    {
                        pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    float dx = Mathf.Abs(p.x - center.x), dy = Mathf.Abs(p.y - center.y);
                    bool horizontal = dx <= 15f && dy <= 3.5f;
                    bool vertical = dy <= 15f && dx <= 3.5f;
                    bool diagonal = Mathf.Abs((p.x - center.x) - (p.y - center.y)) <= 5f && d <= 17f ||
                                    Mathf.Abs((p.x - center.x) + (p.y - center.y)) <= 5f && d <= 17f;
                    bool onMark = symbol == 0 ? horizontal || vertical : symbol == 1 ? horizontal : diagonal;
                    bool rim = d >= 25.5f;
                    Color color = onMark ? mark : rim ? Color.Lerp(fill, Color.black, 0.45f) : fill;
                    color.a *= circle;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        /// <summary>Rounded box with a baked border, 9-sliced so it stretches cleanly.</summary>
        private Sprite Box(Color fill, Color border, int borderPx, int radius)
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = RoundedCoverage(x, y, size, radius);
                    if (coverage <= 0f)
                    {
                        pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    float edge = DistanceToEdge(x, y, size, radius);
                    Color color = borderPx > 0 && edge < borderPx ? border : fill;
                    color.a *= coverage;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            float slice = Mathf.Max(radius, borderPx) + 2f;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        private static float DistanceToEdge(int x, int y, int size, int radius)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float cx = Mathf.Clamp(px, radius, size - radius);
            float cy = Mathf.Clamp(py, radius, size - radius);
            if (radius > 0 && (px < radius || px > size - radius) && (py < radius || py > size - radius))
            {
                return radius - Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
            }

            return Mathf.Min(Mathf.Min(px, size - px), Mathf.Min(py, size - py));
        }

        private static float RoundedCoverage(int x, int y, int size, int radius)
        {
            if (radius <= 0) return 1f;
            return Mathf.Clamp01(DistanceToEdge(x, y, size, radius) + 0.5f);
        }

        private static T ReadField<T>(object instance, string fieldName) where T : class
        {
            if (instance == null) return null;
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(instance) as T;
        }

        /// <summary>Writes every loaded sprite with a 9-slice border to the log, to pick theme overrides later.</summary>
        internal static void DumpCandidateSprites()
        {
            try
            {
                IEnumerable<Sprite> sprites = Resources.FindObjectsOfTypeAll<Sprite>()
                    .Where(sprite => sprite != null && sprite.border != Vector4.zero)
                    .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase);
                foreach (Sprite sprite in sprites)
                {
                    Plugin.Log?.LogInfo($"[sprite] {sprite.name}  size={sprite.rect.width}x{sprite.rect.height}  border={sprite.border}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Sprite dump failed: " + ex.Message);
            }
        }
    }
}
