using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace KeysNotIncluded
{
    /// <summary>
    /// Shown as "Options" next to the mod in the main-menu Mods screen (via PLib).
    /// Saved to config.json in the mod's folder and re-read each time a save is loaded.
    /// Key bindings are not here: they're in the game's own Options > Controls screen.
    /// </summary>
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public sealed class KeysOptions
    {
        private const string Filters = "Default tool filters";
        private const string Overlays = "Build menu overlays";

        // ------------------------------------------------------------------ tool filters

        [Option("Deconstruct", "Filter the Deconstruct tool starts on after loading a save.", Filters)]
        [JsonProperty]
        public FilterLayer DeconstructDefault { get; set; }

        [Option("Cancel", "Filter the Cancel tool starts on after loading a save.", Filters)]
        [JsonProperty]
        public FilterLayer CancelDefault { get; set; }

        [Option("Prioritize", "Filter the Prioritize tool starts on after loading a save.", Filters)]
        [JsonProperty]
        public FilterLayer PrioritizeDefault { get; set; }

        [Option("Apply every time a tool is picked",
            "Off: the default is applied the first time you pick the tool after loading a save, then your own choice sticks.\n" +
            "On: the tool goes back to its default every time you pick it.", Filters)]
        [JsonProperty]
        public bool ApplyEveryTime { get; set; }

        // ------------------------------------------------------------------ controls screen

        [Option("Esc clears a key binding",
            "In Options > Controls, pressing Esc while a binding says \"Press a key\" sets it to None (unbound)\n" +
            "instead of just cancelling. Works for every hotkey.", "Controls screen")]
        [JsonProperty]
        public bool EscClearsBinding { get; set; }

        // ------------------------------------------------------------------ build menu overlays

        [Option("Leave overlay when the build menu closes",
            "Turn the overlay off again when the build menu closes, unless you changed overlay yourself.", Overlays)]
        [JsonProperty]
        public bool LeaveOverlayOnClose { get; set; }

        [Option("Oxygen → Oxygen overlay", "Opening the Oxygen build menu switches to the Oxygen (breathability) overlay.", Overlays)]
        [JsonProperty] public bool OverlayOxygen { get; set; }

        [Option("Power → Power overlay", "Opening the Power build menu switches to the Power overlay.", Overlays)]
        [JsonProperty] public bool OverlayPower { get; set; }

        [Option("Plumbing → Plumbing overlay", "Opening the Plumbing build menu switches to the Plumbing overlay.", Overlays)]
        [JsonProperty] public bool OverlayPlumbing { get; set; }

        [Option("Ventilation → Ventilation overlay", "Opening the Ventilation build menu switches to the Ventilation overlay.", Overlays)]
        [JsonProperty] public bool OverlayVentilation { get; set; }

        [Option("Automation → Automation overlay", "Opening the Automation build menu switches to the Automation overlay.", Overlays)]
        [JsonProperty] public bool OverlayAutomation { get; set; }

        [Option("Shipping → Shipping overlay", "Opening the Shipping build menu switches to the Shipping (conveyor) overlay.", Overlays)]
        [JsonProperty] public bool OverlayShipping { get; set; }

        [Option("Radiation → Radiation overlay", "Opening the Radiation build menu switches to the Radiation overlay (Spaced Out).", Overlays)]
        [JsonProperty] public bool OverlayRadiation { get; set; }

        public KeysOptions()
        {
            DeconstructDefault = FilterLayer.Buildings;
            CancelDefault = FilterLayer.GameDefault;
            PrioritizeDefault = FilterLayer.GameDefault;
            ApplyEveryTime = false;

            EscClearsBinding = true;

            LeaveOverlayOnClose = true;
            OverlayOxygen = true;
            OverlayPower = true;
            OverlayPlumbing = true;
            OverlayVentilation = true;
            OverlayAutomation = true;
            OverlayShipping = true;
            OverlayRadiation = true;
        }

        private static KeysOptions current;

        /// <summary>Settings as of the last <see cref="Reload"/> (defaults before that).</summary>
        public static KeysOptions Current
        {
            get { return current ?? (current = Read()); }
        }

        /// <summary>Re-read config.json. Called when a save is loaded.</summary>
        public static void Reload()
        {
            current = Read();
        }

        private static KeysOptions Read()
        {
            KeysOptions opts;
            try { opts = POptions.ReadSettings<KeysOptions>(); }
            catch { opts = null; }
            return opts ?? new KeysOptions();
        }

        /// <summary>Configured default for a tool, by its class name (GameDefault if none).</summary>
        public FilterLayer DefaultFor(InterfaceTool tool)
        {
            switch (tool != null ? tool.GetType().Name : "")
            {
                case "DeconstructTool": return DeconstructDefault;
                case "CancelTool": return CancelDefault;
                case "PrioritizeTool": return PrioritizeDefault;
                default: return FilterLayer.GameDefault;
            }
        }

        /// <summary>Whether opening this build menu category (game ID, see BuildMenuOverlay) enters its overlay.</summary>
        public bool OverlayEnabled(string category)
        {
            switch (category)
            {
                case "Oxygen": return OverlayOxygen;
                case "Power": return OverlayPower;
                case "Plumbing": return OverlayPlumbing;
                case "HVAC": return OverlayVentilation;
                case "Automation": return OverlayAutomation;
                case "Conveyance": return OverlayShipping;
                case "HEP": return OverlayRadiation;
                default: return false;
            }
        }
    }
}
