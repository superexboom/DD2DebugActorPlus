using System;
using Assets.Code.Game;
using Assets.Code.UI.Managers;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DD2DebugActorPlus
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dd2.debugactorplus";
        public const string PluginName = "DD2 Debug Actor Plus";
        public const string PluginVersion = "0.2.2";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;
        private DebugActorWindow _window;
        private int _lastOfficialToggleFrame = -1;
        private int _lastFallbackToggleFrame = -1;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            _window = new DebugActorWindow(Logger);
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. Official Alt+J actor editor is replaced.");
        }

        internal void ToggleWindowFromOfficialEntry()
        {
            if (Time.frameCount == _lastFallbackToggleFrame)
            {
                return;
            }

            _lastOfficialToggleFrame = Time.frameCount;
            _window.Toggle();
        }

        private void Update()
        {
            try
            {
                _window.Update();
                if (IsAltJPressed() && Time.frameCount != _lastOfficialToggleFrame)
                {
                    _lastFallbackToggleFrame = Time.frameCount;
                    _window.Toggle();
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Update skipped: {ex.Message}");
            }
        }

        private static bool IsAltJPressed()
        {
            IInputSystem input = BepInEx.UnityInput.Current;
            return input.GetKeyDown(KeyCode.J) &&
                (input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt));
        }

        private void OnGUI()
        {
            _window.Draw();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
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
                    Instance?.ToggleWindowFromOfficialEntry();
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
