using System;
using Assets.Code.UI.Managers;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DD2DebugActorPlus
{
    internal sealed class PluginSettings
    {
        public float UiScale = 1f;
        public string Frames = "";
        public bool DumpUiSprites;
        public bool SpriteGallery;
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dd2.debugactorplus";
        public const string PluginName = "DD2 Debug Actor Plus";
        public const string PluginVersion = "0.3.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;
        private EditorScreen _screen;
        private int _lastOfficialToggleFrame = -1;
        private int _lastHotkeyToggleFrame = -1;

        private ConfigEntry<string> _language;
        private ConfigEntry<KeyboardShortcut> _toggleKey;
        private ConfigEntry<float> _uiScale;
        private ConfigEntry<string> _frames;
        private ConfigEntry<bool> _dumpUiSprites;
        private ConfigEntry<bool> _spriteGallery;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            _language = Config.Bind("General", "Language", "auto",
                new ConfigDescription("UI language: auto (follow the game), zh, en.", new AcceptableValueList<string>("auto", "zh", "en")));
            _toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                "Opens / closes the editor (the game's own Alt+J debug entry is redirected here as well).");
            _uiScale = Config.Bind("Appearance", "UiScale", 1f,
                new ConfigDescription("Editor scale relative to a 1600x900 layout (1 = fills the screen at any resolution; text stays readable at 1280x720).", new AcceptableValueRange<float>(0.6f, 1.6f)));
            _frames = Config.Bind("Appearance", "Frames", "",
                "Optional frame overrides, e.g. \"Panel=ui_tooltip@5; Button=ui_button_black@5\". Roles: Window, Panel, Card, CardSelected, " +
                "Button, ButtonPrimary, ButtonDanger, Chip, ChipSelected, Field, ScrollHandle, Divider. Value = loaded game sprite name " +
                "@ border shrink factor, or none for the drawn frame. Empty = built-in native frames. Press Reload in the editor to apply.");
            _spriteGallery = Config.Bind("Diagnostics", "SpriteGallery", false,
                "Adds a tab that shows every loaded 9-slice game sprite as a frame (to choose Appearance overrides).");
            _dumpUiSprites = Config.Bind("Diagnostics", "DumpUiSprites", false,
                "Log every loaded 9-slice game sprite the first time the editor opens (to pick Appearance overrides).");

            L.SetMode(_language.Value);
            _screen = new EditorScreen(ReadSettings());
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. {_toggleKey.Value} opens the editor; the official Alt+J actor editor is replaced.");
        }

        private PluginSettings ReadSettings()
        {
            return new PluginSettings
            {
                UiScale = _uiScale.Value,
                Frames = _frames.Value,
                DumpUiSprites = _dumpUiSprites.Value,
                SpriteGallery = _spriteGallery.Value,
            };
        }

        internal void SetLanguage(string language)
        {
            _language.Value = language;
            L.SetMode(language);
            _screen.Rebuild();
        }

        internal void ReloadEditor()
        {
            try
            {
                Config.Reload();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Config reload failed: " + ex.Message);
            }

            L.SetMode(_language.Value);
            _screen.ApplySettings(ReadSettings());
            _screen.Rebuild();
        }

        internal void ToggleFromOfficialEntry()
        {
            if (Time.frameCount == _lastHotkeyToggleFrame) return;
            _lastOfficialToggleFrame = Time.frameCount;
            _screen.Toggle();
        }

        private void Update()
        {
            try
            {
                if (_toggleKey.Value.IsDown() && Time.frameCount != _lastOfficialToggleFrame)
                {
                    _lastHotkeyToggleFrame = Time.frameCount;
                    _screen.Toggle();
                    return;
                }

                if (_screen.Visible && UnityInput.Current.GetKeyDown(KeyCode.Escape))
                {
                    if (_screen.IsTyping || _screen.WasTyping) _screen.LeaveTextField();
                    else _screen.Close();
                    return;
                }

                _screen.Tick();
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Update skipped: {ex.Message}");
            }
        }

        private void LateUpdate()
        {
            try
            {
                _screen.LateTick();
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"LateUpdate skipped: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            _screen?.Destroy();
            _harmony?.UnpatchSelf();
            InputGuard.Active = false;
            if (Instance == this) Instance = null;
            Logger.LogInfo($"{PluginName} unloaded.");
        }

        [HarmonyPatch(typeof(CommonUiBhv), nameof(CommonUiBhv.ShowDebugCombatActorEditor))]
        private static class CommonUiDebugActorEditorPatch
        {
            private static bool Prefix()
            {
                try
                {
                    if (Instance == null) return true;
                    Instance.ToggleFromOfficialEntry();
                    return false;
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"Failed to open Debug Actor Plus: {ex.Message}");
                    return true;
                }
            }
        }
    }
}
