using System;
using System.Collections.Generic;

namespace KeysNotIncluded
{
    /// <summary>
    /// Applies the configured default filter (e.g. Deconstruct -> Buildings) when a filtered tool is
    /// picked. By default that happens once per tool after each save is loaded, so a filter you choose
    /// yourself sticks for the rest of the session.
    /// </summary>
    internal static class DefaultFilters
    {
        private static readonly HashSet<string> applied = new HashSet<string>();

        private static string pendingTool;

        /// <summary>Called when a save finishes loading.</summary>
        public static void Reset()
        {
            applied.Clear();
            pendingTool = null;
        }

        /// <summary>Called right after any tool is activated.</summary>
        public static void OnToolActivated(InterfaceTool tool)
        {
            if (!(tool is FilteredDragTool filtered)) return;
            BackwallDeconstruct.OnToolActivated(tool);
            TryApply(filtered);
        }

        /// <summary>Called every frame while gameplay is running.</summary>
        public static void Poll()
        {
            if (pendingTool == null) return;
            var active = PlayerController.Instance != null ? PlayerController.Instance.ActiveTool as FilteredDragTool : null;
            if (active == null || active.GetType().Name != pendingTool)
            {
                pendingTool = null;
                return;
            }
            TryApply(active);
        }

        /// <summary>Called when the tool filter menu is rebuilt/populated.</summary>
        public static void OnFilterMenuPopulated()
        {
            var active = PlayerController.Instance != null ? PlayerController.Instance.ActiveTool as FilteredDragTool : null;
            if (active != null) TryApply(active);
        }

        private static void TryApply(FilteredDragTool filtered)
        {
            var opts = KeysOptions.Current;
            var layer = opts.DefaultFor(filtered);
            string name = filtered.GetType().Name;

            if (layer == FilterLayer.GameDefault)
            {
                if (pendingTool == name) pendingTool = null;
                return;
            }

            if (!opts.ApplyEveryTime && applied.Contains(name))
            {
                if (pendingTool == name) pendingTool = null;
                return;
            }

            // In an overlay (Power, Plumbing, ...) the game narrows the filter to that overlay's
            // layer. Leave that alone and apply the default the next time the tool is picked outside it.
            if (InOverlay()) return;

            try
            {
                var keys = FilterMenu.Keys();
                if (keys.Count == 0)
                {
                    pendingTool = name;
                    return;
                }

                var key = FilterMenu.KeyFor(layer, keys);
                if (key == null)
                {
                    applied.Add(name); // this tool has no such filter; don't retry every time
                    pendingTool = null;
                    Debug.LogWarning("[KeysNotIncluded] " + name + " has no '" + layer + "' filter; keeping the game default.");
                    return;
                }

                if (FilterMenu.Select(key))
                {
                    var selected = FilterMenu.SelectedKey();
                    if (selected != null && string.Equals(selected, key, StringComparison.Ordinal))
                    {
                        applied.Add(name);
                        pendingTool = null;
                    }
                    else
                        pendingTool = name;
                }
                else
                    pendingTool = name;
            }
            catch (Exception ex)
            {
                pendingTool = null;
                applied.Add(name);
                Debug.LogWarning("[KeysNotIncluded] could not set default filter for " + name + ": " + ex.Message);
            }
        }

        private static bool InOverlay()
        {
            var os = OverlayScreen.Instance;
            return os != null && os.GetMode() != OverlayModes.None.ID;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(ToolParameterMenu), "PopulateMenu")]
    internal static class ToolParameterMenu_PopulateMenu_Patch
    {
        private static void Postfix()
        {
            DefaultFilters.OnFilterMenuPopulated();
        }
    }
}
