using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KeysNotIncluded
{
    /// <summary>
    /// When a build menu category is opened (by its hotkey, e.g. 2 for Oxygen, or by clicking it),
    /// switches to its overlay: Oxygen, Power, Plumbing, Ventilation, Automation, Shipping and
    /// Radiation, each switchable in Options. When the build menu closes, or a category without an
    /// overlay is opened, an overlay the mod turned on is turned off again, unless
    /// the player has changed overlay in the meantime.
    ///
    /// Polled once per frame from <see cref="HotkeyScreen"/>, so it works with however the game opens
    /// the category.
    /// </summary>
    internal static class BuildMenuOverlay
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Plan category IDs the game uses (TUNING.BUILDINGS.PLANORDER).</summary>
        public static readonly string[] Categories =
        {
            "Base", "Oxygen", "Power", "Food", "Plumbing", "HVAC", "Refining", "Medical",
            "Furniture", "Equipment", "Utilities", "Automation", "Conveyance", "Rocketry", "HEP",
        };

        /// <summary>
        /// Category -> OverlayModes nested class (each has a static HashedString ID). Only these
        /// categories have an overlay; opening any other one turns off an overlay the mod turned on.
        /// </summary>
        private static readonly Dictionary<string, string> ModeClass = new Dictionary<string, string>
        {
            { "Oxygen", "Oxygen" },
            { "Power", "Power" },
            { "Plumbing", "LiquidConduits" },
            { "HVAC", "GasConduits" },          // Ventilation
            { "Automation", "Logic" },
            { "Conveyance", "SolidConveyor" },  // Shipping
            { "HEP", "Radiation" },             // Radiation (Spaced Out)
        };

        private static string lastCategory;
        private static HashedString? ourOverlay; // overlay the mod switched on, if still in effect
        private static bool warned;

        /// <summary>Called when a save finishes loading.</summary>
        public static void Reset()
        {
            lastCategory = null;
            ourOverlay = null;
        }

        public static void Poll()
        {
            var category = ActiveCategory();
            if (category == lastCategory) return;
            lastCategory = category;

            var opts = KeysOptions.Current;
            var os = OverlayScreen.Instance;
            if (os == null) return;

            HashedString? target = category != null && opts.OverlayEnabled(category) ? OverlayId(category) : null;
            HashedString current = os.GetMode();

            // The player changed overlay themselves since we last switched: it's theirs now.
            if (ourOverlay.HasValue && current != ourOverlay.Value) ourOverlay = null;

            if (target.HasValue)
            {
                if (current != target.Value) os.ToggleOverlay(target.Value, true);
                // Only count it as ours if we turned it on (or replaced one we turned on).
                if (current != target.Value || ourOverlay.HasValue) ourOverlay = target;
            }
            else if (ourOverlay.HasValue && (category != null || opts.LeaveOverlayOnClose))
            {
                os.ToggleOverlay(OverlayModes.None.ID, true);
                ourOverlay = null;
            }
        }

        private static HashedString? OverlayId(string category)
        {
            if (!ModeClass.TryGetValue(category, out var cls)) return null;
            var t = typeof(OverlayModes).GetNestedType(cls, BindingFlags.Public | BindingFlags.NonPublic);
            var f = t != null ? t.GetField("ID", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
            if (f != null && f.GetValue(null) is HashedString id) return id;
            return null; // e.g. Radiation in the base game
        }

        /// <summary>ID of the open build menu category ("Oxygen", ...), or null when the build menu is closed.</summary>
        private static string ActiveCategory()
        {
            var ps = PlanScreen.Instance;
            if (ps == null || !ps.isActiveAndEnabled) return null;

            object info = null;
            var prop = AccessTools.Property(typeof(PlanScreen), "ActiveCategoryToggleInfo");
            if (prop != null) info = prop.GetValue(ps, null);
            else
            {
                var field = AccessTools.Field(typeof(PlanScreen), "activeCategoryInfo");
                if (field != null) info = field.GetValue(ps);
                else if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[KeysNotIncluded] could not find the open build menu category; build menu overlays disabled.");
                }
            }
            if (info == null) return null;

            var userDataField = info.GetType().GetField("userData", AnyInstance);
            object data = userDataField != null ? userDataField.GetValue(info) : null;
            if (data == null) return null;

            HashedString? hash = null;
            if (data is HashedString h) hash = h;
            else
            {
                // Some versions wrap the category in a small object; find its HashedString / string member.
                foreach (var f in data.GetType().GetFields(AnyInstance))
                {
                    var v = f.GetValue(data);
                    if (v is HashedString fh) { hash = fh; break; }
                    if (v is string s) return Match(s);
                }
            }
            if (!hash.HasValue) return Match(data.ToString());
            foreach (var c in Categories)
                if (new HashedString(c) == hash.Value) return c;
            return hash.Value.ToString(); // unknown (modded) category: tracked, but no overlay configured
        }

        private static string Match(string s)
        {
            foreach (var c in Categories)
                if (string.Equals(c, s, StringComparison.OrdinalIgnoreCase)) return c;
            return s;
        }
    }
}
