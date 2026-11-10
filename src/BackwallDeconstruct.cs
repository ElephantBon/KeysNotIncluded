using System;

namespace KeysNotIncluded
{
    /// <summary>
    /// "Deconstruct background buildings" hotkey: picks the Deconstruct tool with its filter set to
    /// the background layer (drywall, tempshift plates, ...).
    ///
    /// The background filter is temporary. The next time Deconstruct is picked the normal way
    /// (its own hotkey or toolbar button), it goes back to the filter it had before, so the regular
    /// key keeps meaning "Buildings" (or whatever you use).
    /// </summary>
    internal static class BackwallDeconstruct
    {
        private static bool activating;      // true while we activate the tool ourselves
        private static bool active;          // the background filter is on because of us
        private static string filterBefore;  // filter key to restore afterwards

        /// <summary>Called when a save finishes loading.</summary>
        public static void Reset()
        {
            activating = false;
            active = false;
            filterBefore = null;
        }

        public static bool CanRun()
        {
            return DeconstructTool.Instance != null && PlayerController.Instance != null;
        }

        public static bool Run()
        {
            var tool = DeconstructTool.Instance;
            var pc = PlayerController.Instance;
            if (tool == null || pc == null) return false;

            if (pc.ActiveTool != tool)
            {
                activating = true;
                try { pc.ActivateTool(tool); } // the default filter (if any) is applied here first
                finally { activating = false; }
            }
            if (pc.ActiveTool != tool) return false;

            string before = FilterMenu.SelectedKey();
            if (!FilterMenu.Select(FilterLayer.Backwall))
            {
                // In an overlay (Power, Plumbing, ...) the menu only offers that overlay's layer.
                var os = OverlayScreen.Instance;
                if (os == null || os.GetMode() == OverlayModes.None.ID) return true;
                os.ToggleOverlay(OverlayModes.None.ID, true);
                before = FilterMenu.SelectedKey();
                if (!FilterMenu.Select(FilterLayer.Backwall))
                {
                    Debug.LogWarning("[KeysNotIncluded] the Deconstruct tool has no background buildings filter in this game version.");
                    return true;
                }
            }
            if (!active)
            {
                // Don't overwrite the remembered filter when pressed twice in a row.
                filterBefore = before;
                active = true;
            }
            return true;
        }

        /// <summary>Called right after any tool is activated, before default filters are applied.</summary>
        public static void OnToolActivated(InterfaceTool tool)
        {
            if (activating || !active || !(tool is DeconstructTool)) return;
            active = false;
            var restore = filterBefore;
            filterBefore = null;
            if (restore == null) return;
            try { FilterMenu.Select(restore); }
            catch (Exception ex) { Debug.LogWarning("[KeysNotIncluded] could not restore the Deconstruct filter: " + ex.Message); }
        }
    }
}
