using System;
using Assets.Code.Inputs;
using HarmonyLib;

namespace DD2DebugActorPlus
{
    /// <summary>
    /// While the editor is open it is modal: the game's own input actions (hotkeys, Esc/pause,
    /// skill keys, camera) and polled mouse buttons are swallowed. Clicks that go through the
    /// EventSystem are already stopped by the editor canvas' full-screen backdrop.
    /// </summary>
    internal static class InputGuard
    {
        internal static bool Active;

        [HarmonyPatch(typeof(InputSource_Unity), "OnInput")]
        private static class InputSourceOnInputPatch
        {
            private static bool Prefix()
            {
                return !Active;
            }
        }

        [HarmonyPatch(typeof(InputSource_Unity), nameof(InputSource_Unity.GetPointerValues))]
        private static class InputSourcePointerPatch
        {
            private static void Postfix(ref InputPointerValues __result)
            {
                if (!Active)
                {
                    return;
                }

                try
                {
                    InputPointerValues.ButtonValues none = new InputPointerValues.ButtonValues(false, false, false);
                    __result = new InputPointerValues(__result.m_position, none, none, none);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug("Pointer guard skipped: " + ex.Message);
                }
            }
        }
    }
}
