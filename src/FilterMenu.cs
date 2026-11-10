using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KeysNotIncluded
{
    /// <summary>
    /// Reads and changes the filter menu of the active filtered tool (the "All / Wires / Buildings ..."
    /// list at the top right while Deconstruct, Cancel, Prioritize etc. are selected).
    ///
    /// Klei has changed the menu's internals between game versions (a Dictionary of toggle states in
    /// older builds, an array of ToggleData in newer ones), so nothing here depends on that layout.
    /// A filter is selected the same way a mouse click selects it: by invoking the onClick of that
    /// filter's toggle widget. That keeps the tool, the menu and the menu's visuals in sync no matter
    /// how the game stores the state.
    /// </summary>
    internal static class FilterMenu
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Possible game keys for each layer, best guess first. The game's own constants in
        /// ToolParameterMenu.FILTERLAYERS are checked first (see <see cref="FieldNames"/>).
        /// </summary>
        private static readonly Dictionary<FilterLayer, string[]> KeyGuesses = new Dictionary<FilterLayer, string[]>
        {
            { FilterLayer.All,           new[] { "ALL" } },
            { FilterLayer.Buildings,     new[] { "BUILDINGS", "BUILDING" } },
            { FilterLayer.Wires,         new[] { "WIRES", "WIRE", "POWER" } },
            { FilterLayer.LiquidPipes,   new[] { "LIQUIDPIPES", "LIQUIDCONDUIT", "LIQUIDCONDUITS", "LIQUIDPIPE" } },
            { FilterLayer.GasPipes,      new[] { "GASPIPES", "GASCONDUIT", "GASCONDUITS", "GASPIPE" } },
            { FilterLayer.ConveyorRails, new[] { "SOLIDCONDUIT", "SOLIDCONDUITS", "CONVEYOR", "CONVEYORRAILS", "SOLIDPIPES" } },
            { FilterLayer.Automation,    new[] { "LOGIC", "AUTOMATION" } },
            { FilterLayer.Backwall,      new[] { "BACKWALL", "BACKWALLS", "BACKGROUND" } },
            { FilterLayer.Tiles,         new[] { "TILES", "TILE" } },
            { FilterLayer.DigOrders,     new[] { "DIGPLACER", "DIG", "DIGGING" } },
            { FilterLayer.Cleaning,      new[] { "CLEANANDCLEAR", "CLEAN", "CLEANING" } },
            { FilterLayer.Construction,  new[] { "CONSTRUCTION", "CONSTRUCT" } },
        };

        /// <summary>Field names in ToolParameterMenu.FILTERLAYERS holding each layer's key.</summary>
        private static readonly Dictionary<FilterLayer, string[]> FieldNames = new Dictionary<FilterLayer, string[]>
        {
            { FilterLayer.All,           new[] { "ALL" } },
            { FilterLayer.Buildings,     new[] { "BUILDINGS" } },
            { FilterLayer.Wires,         new[] { "WIRES" } },
            { FilterLayer.LiquidPipes,   new[] { "LIQUIDCONDUIT" } },
            { FilterLayer.GasPipes,      new[] { "GASCONDUIT" } },
            { FilterLayer.ConveyorRails, new[] { "SOLIDCONDUIT" } },
            { FilterLayer.Automation,    new[] { "LOGIC" } },
            { FilterLayer.Backwall,      new[] { "BACKWALL" } },
            { FilterLayer.Tiles,         new[] { "TILES" } },
            { FilterLayer.DigOrders,     new[] { "DIGPLACER" } },
            { FilterLayer.Cleaning,      new[] { "CLEANANDCLEAR" } },
            { FilterLayer.Construction,  new[] { "CONSTRUCTION" } },
        };

        private static bool warnedNoWidgets;

        // ------------------------------------------------------------------ queries

        /// <summary>The filter keys currently in the menu, in display order (empty if none).</summary>
        public static List<string> Keys()
        {
            var keys = new List<string>();
            var widgets = Widgets(Menu());
            if (widgets == null) return keys;
            foreach (DictionaryEntry kv in widgets)
            {
                var go = kv.Value as GameObject;
                if (kv.Key is string key && go != null) keys.Add(key);
            }
            return keys;
        }

        /// <summary>The key of the filter that is on, or null if it can't be read.</summary>
        public static string SelectedKey()
        {
            var menu = Menu();
            if (menu == null) return null;
            foreach (var kv in States(menu))
                if (kv.Value == "On") return kv.Key;
            return null;
        }

        /// <summary>The game key for a layer in the current menu, or null if this tool has no such filter.</summary>
        public static string KeyFor(FilterLayer layer, List<string> keys)
        {
            if (layer == FilterLayer.GameDefault || keys == null || keys.Count == 0) return null;

            var wanted = new List<string>();
            var layers = typeof(ToolParameterMenu).GetNestedType("FILTERLAYERS", BindingFlags.Public | BindingFlags.NonPublic);
            if (layers != null && FieldNames.TryGetValue(layer, out var fields))
            {
                foreach (var name in fields)
                {
                    var f = layers.GetField(name, AnyStatic);
                    if (f != null && f.GetValue(null) is string value) wanted.Add(value);
                }
            }
            if (KeyGuesses.TryGetValue(layer, out var guesses)) wanted.AddRange(guesses);

            foreach (var w in wanted)
                foreach (var k in keys)
                    if (Normalize(k) == Normalize(w)) return k;
            return null;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Select one layer in the active tool's menu. False if the tool has no such filter.</summary>
        public static bool Select(FilterLayer layer)
        {
            var key = KeyFor(layer, Keys());
            return key != null && Select(key);
        }

        /// <summary>Select a filter by its game key, exactly as if it were clicked.</summary>
        public static bool Select(string key)
        {
            var menu = Menu();
            if (menu == null || key == null) return false;

            var widgets = Widgets(menu);
            if (widgets != null && widgets.Contains(key) && widgets[key] is GameObject go)
            {
                var toggle = go.GetComponentInChildren<MultiToggle>(true);
                if (toggle != null && toggle.onClick != null)
                {
                    toggle.onClick();
                    return true;
                }
            }

            // Fallback: the menu's own click handler (ChangeToSetting + OnChange in the builds we know of).
            var change = AccessTools.Method(typeof(ToolParameterMenu), "ChangeToSetting", new[] { typeof(string) });
            if (change == null) return false;
            change.Invoke(menu, new object[] { key });
            var onChange = AccessTools.Method(typeof(ToolParameterMenu), "OnChange", Type.EmptyTypes);
            if (onChange != null) onChange.Invoke(menu, null);
            return true;
        }

        // ------------------------------------------------------------------ reflection

        private static ToolParameterMenu Menu()
        {
            var tm = ToolMenu.Instance;
            if (tm != null)
            {
                var f = AccessTools.Field(typeof(ToolMenu), "toolParameterMenu");
                if (f != null && f.GetValue(tm) is ToolParameterMenu menu && menu != null) return menu;
            }
            return UnityEngine.Object.FindObjectOfType<ToolParameterMenu>();
        }

        /// <summary>The menu's key -> widget GameObject map (field "widgets", or any field of that shape).</summary>
        private static IDictionary Widgets(ToolParameterMenu menu)
        {
            if (menu == null) return null;
            var named = AccessTools.Field(typeof(ToolParameterMenu), "widgets");
            if (named != null && named.GetValue(menu) is IDictionary d) return d;

            foreach (var f in typeof(ToolParameterMenu).GetFields(AnyInstance))
            {
                var t = f.FieldType;
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                    && t.GetGenericArguments()[0] == typeof(string)
                    && t.GetGenericArguments()[1] == typeof(GameObject))
                    return f.GetValue(menu) as IDictionary;
            }
            if (!warnedNoWidgets)
            {
                warnedNoWidgets = true;
                Debug.LogWarning("[KeysNotIncluded] could not find the filter menu widgets; filter hotkeys disabled.");
            }
            return null;
        }

        /// <summary>
        /// Filter key -> "On" / "Off" / "Disabled", read from whichever field holds the menu's current
        /// parameters (a Dictionary&lt;string, ToggleState&gt; or a collection of ToggleData objects).
        /// </summary>
        private static Dictionary<string, string> States(ToolParameterMenu menu)
        {
            var result = new Dictionary<string, string>();
            var stateType = typeof(ToolParameterMenu.ToggleState);

            foreach (var f in typeof(ToolParameterMenu).GetFields(AnyInstance))
            {
                object value;
                try { value = f.GetValue(menu); } catch { continue; }
                if (value == null || value is string) continue;

                if (value is IDictionary dict)
                {
                    foreach (DictionaryEntry kv in dict)
                        if (kv.Key is string k && kv.Value != null && kv.Value.GetType() == stateType)
                            result[k] = kv.Value.ToString();
                }
                else if (value is IEnumerable list)
                {
                    foreach (var item in list)
                    {
                        if (item == null) continue;
                        string key = null, state = null;
                        foreach (var m in item.GetType().GetFields(AnyInstance))
                        {
                            var v = m.GetValue(item);
                            if (key == null && v is string s) key = s;
                            else if (state == null && v != null && v.GetType() == stateType) state = v.ToString();
                        }
                        foreach (var p in item.GetType().GetProperties(AnyInstance))
                        {
                            if (p.GetIndexParameters().Length != 0) continue;
                            object v;
                            try { v = p.GetValue(item); } catch { continue; }
                            if (key == null && v is string s) key = s;
                            else if (state == null && v != null && v.GetType() == stateType) state = v.ToString();
                        }
                        if (key != null && state != null) result[key] = state;
                    }
                }
                if (result.Count > 0) return result;
            }
            return result;
        }

        private static string Normalize(string s)
        {
            return (s ?? "").Replace(" ", "").Replace("_", "").ToUpperInvariant();
        }
    }
}
