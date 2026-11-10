using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace KeysNotIncluded
{
    public sealed class KeysNotIncludedMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony); // applies every [HarmonyPatch] in this assembly
            PUtil.InitLibrary(false);
            new POptions().RegisterOptions(this, typeof(KeysOptions)); // "Options" button in the Mods screen
            Hotkeys.Register(); // must happen in OnLoad so the bindings exist before the game reads them
            Debug.Log("[KeysNotIncluded] loaded");
        }
    }

    /// <summary>A save finished loading: re-read options, re-arm the default filters, start listening for keys.</summary>
    [HarmonyPatch(typeof(Game), "OnSpawn")]
    internal static class Game_OnSpawn_Patch
    {
        private static void Postfix()
        {
            KeysOptions.Reload();
            DefaultFilters.Reset();
            BuildMenuOverlay.Reset();
            BackwallDeconstruct.Reset();
            HotkeyScreen.Create();
        }
    }

    /// <summary>Apply default filters when a tool is picked (by hotkey or by clicking its button).</summary>
    [HarmonyPatch(typeof(InterfaceTool), "ActivateTool")]
    internal static class InterfaceTool_ActivateTool_Patch
    {
        private static void Postfix(InterfaceTool __instance)
        {
            DefaultFilters.OnToolActivated(__instance);
        }
    }
}
