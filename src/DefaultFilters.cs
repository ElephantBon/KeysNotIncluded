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

        /// <summary>Called when a save finishes loading.</summary>
        public static void Reset()
        {
            applied.Clear();
        }

        /// <summary>Called right after any tool is activated (the filter menu is already filled in).</summary>
        public static void OnToolActivated(InterfaceTool tool)
        {
            if (!(tool is FilteredDragTool filtered)) return;
            BackwallDeconstruct.OnToolActivated(tool);
            var opts = KeysOptions.Current;
            var layer = opts.DefaultFor(filtered);
            if (layer == FilterLayer.GameDefault) return;

            string name = filtered.GetType().Name;
            if (!opts.ApplyEveryTime && applied.Contains(name)) return;

            // In an overlay (Power, Plumbing, ...) the game narrows the filter to that overlay's
            // layer. Leave that alone and apply the default the next time the tool is picked outside it.
            if (InOverlay()) return;

            try
            {
                if (FilterMenu.Select(layer)) applied.Add(name);
                else
                {
                    applied.Add(name); // this tool has no such filter; don't retry every time
                    Debug.LogWarning("[KeysNotIncluded] " + name + " has no '" + layer + "' filter; keeping the game default.");
                }
            }
            catch (Exception ex)
            {
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
}
