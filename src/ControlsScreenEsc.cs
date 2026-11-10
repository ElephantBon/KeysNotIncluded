using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KeysNotIncluded
{
    /// <summary>
    /// Options > Controls: while a binding is waiting for a key ("Press a key..."), Esc clears that
    /// binding to None instead of just cancelling. Works for every hotkey, the game's and mods'.
    ///
    /// How: the screen's own Bind(key, modifier) is run with F15 (a key no keyboard sends, so it
    /// never conflicts). That makes the screen do all its usual bookkeeping for the right entry.
    /// The entry that changed to F15 is then set to None, saved and re-applied. If that last step
    /// can't be done in some game version, the action is left on F15, which behaves as unbound.
    /// </summary>
    internal static class ControlsScreenEsc
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const KKeyCode Placeholder = (KKeyCode)KeyCode.F15;

        public static readonly Type ScreenType = AccessTools.TypeByName("InputBindingsScreen");

        private static int handledFrame = -10;

        /// <summary>True if the screen is waiting for a key press.</summary>
        public static bool IsWaiting(object screen)
        {
            var f = AccessTools.Field(ScreenType, "waitingForKeyPress");
            if (f == null)
                f = ScreenType.GetFields(AnyInstance).FirstOrDefault(x => x.FieldType == typeof(bool)
                    && x.Name.IndexOf("waiting", StringComparison.OrdinalIgnoreCase) >= 0);
            return f != null && f.GetValue(screen) is bool b && b;
        }

        /// <summary>Esc was pressed this frame or the last one and already handled by us.</summary>
        public static bool JustHandled
        {
            get { return Time.frameCount - handledFrame <= 1; }
        }

        /// <summary>Clear the binding being edited. False if this game version can't be handled (vanilla Esc then applies).</summary>
        public static bool ClearActiveBinding(object screen)
        {
            if (!KeysOptions.Current.EscClearsBinding || !IsWaiting(screen)) return false;

            var bind = AccessTools.Method(ScreenType, "Bind", new[] { typeof(KKeyCode), typeof(Modifier) });
            var bindings = KeyBindings();
            if (bind == null || bindings == null)
            {
                Debug.LogWarning("[KeysNotIncluded] Controls screen: Bind or KeyBindings not found; Esc keeps its normal behaviour.");
                return false;
            }

            var before = Snapshot(bindings);
            bind.Invoke(screen, new object[] { Placeholder, Modifier.None });
            handledFrame = Time.frameCount;

            bindings = KeyBindings(); // Bind may have replaced the array
            if (bindings == null) return true;
            bool changed = false;
            for (int i = 0; i < bindings.Length && i < before.Length; i++)
            {
                object entry = bindings.GetValue(i);
                if (!Equals(GetField(entry, "mKeyCode"), Placeholder) || Equals(before[i], Placeholder)) continue;
                SetField(entry, "mKeyCode", KKeyCode.None);
                SetField(entry, "mModifier", Modifier.None);
                bindings.SetValue(entry, i); // BindingEntry is a struct: write the copy back
                changed = true;
            }
            if (changed) Apply(screen);
            return true;
        }

        private static Array KeyBindings()
        {
            var t = AccessTools.TypeByName("GameInputMapping");
            if (t == null) return null;
            var f = t.GetField("KeyBindings", AnyStatic);
            if (f != null) return f.GetValue(null) as Array;
            var p = t.GetProperty("KeyBindings", AnyStatic);
            return p != null ? p.GetValue(null, null) as Array : null;
        }

        private static object[] Snapshot(Array bindings)
        {
            var keys = new object[bindings.Length];
            for (int i = 0; i < bindings.Length; i++) keys[i] = GetField(bindings.GetValue(i), "mKeyCode");
            return keys;
        }

        /// <summary>Save to disk, push to the live input controller, redraw the list. Each step is best effort.</summary>
        private static void Apply(object screen)
        {
            var mapping = AccessTools.TypeByName("GameInputMapping");
            Call(mapping, null, "SaveBindings");

            var global = AccessTools.TypeByName("Global");
            var getMgr = global != null ? global.GetMethod("GetInputManager", AnyStatic | AnyInstance, null, Type.EmptyTypes, null) : null;
            object mgr = null;
            if (getMgr != null)
            {
                object instance = getMgr.IsStatic ? null : global.GetProperty("Instance", AnyStatic)?.GetValue(null, null);
                if (getMgr.IsStatic || instance != null) mgr = getMgr.Invoke(instance, null);
            }
            if (mgr != null) Call(mgr.GetType(), mgr, "RebindControls");

            Call(ScreenType, screen, "BuildDisplay");
        }

        private static void Call(Type t, object instance, string name)
        {
            if (t == null) return;
            var m = AccessTools.Method(t, name, Type.EmptyTypes);
            if (m == null || (instance == null && !m.IsStatic)) return;
            try { m.Invoke(m.IsStatic ? null : instance, null); }
            catch (Exception ex) { Debug.LogWarning("[KeysNotIncluded] Controls screen " + name + ": " + ex.Message); }
        }

        private static object GetField(object o, string name)
        {
            var f = o != null ? o.GetType().GetField(name, AnyInstance) : null;
            return f != null ? f.GetValue(o) : null;
        }

        private static void SetField(object o, string name, object value)
        {
            var f = o != null ? o.GetType().GetField(name, AnyInstance) : null;
            if (f != null) f.SetValue(o, value);
        }
    }

    /// <summary>Esc delivered as a key event (KScreen.OnKeyDown) on the Controls screen.</summary>
    [HarmonyPatch]
    internal static class InputBindingsScreen_OnKeyDown_Patch
    {
        private static MethodBase Target()
        {
            var t = ControlsScreenEsc.ScreenType;
            // Only the screen's own override: patching the inherited KScreen method would affect every screen.
            return t != null ? AccessTools.DeclaredMethod(t, "OnKeyDown", new[] { typeof(KButtonEvent) }) : null;
        }

        private static bool Prepare() { return Target() != null; }
        private static MethodBase TargetMethod() { return Target(); }

        private static bool Prefix(object __instance, KButtonEvent e)
        {
            try
            {
                if (!e.IsAction(Action.Escape)) return true;
                if (!ControlsScreenEsc.JustHandled) KeysOptions.Reload(); // may have just been changed in the Mods screen
                if (!KeysOptions.Current.EscClearsBinding) return true;
                if (ControlsScreenEsc.JustHandled) { e.Consumed = true; return false; } // already cleared in Update
                if (!ControlsScreenEsc.IsWaiting(__instance)) return true;               // normal Esc: close the screen
                if (!ControlsScreenEsc.ClearActiveBinding(__instance)) return true;
                e.Consumed = true;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KeysNotIncluded] Controls screen Esc: " + ex);
                return true;
            }
        }
    }

    /// <summary>Esc read directly from Unity input in the screen's Update (where it listens for the new key).</summary>
    [HarmonyPatch]
    internal static class InputBindingsScreen_Update_Patch
    {
        private static MethodBase Target()
        {
            var t = ControlsScreenEsc.ScreenType;
            return t != null ? AccessTools.DeclaredMethod(t, "Update", Type.EmptyTypes) : null;
        }

        private static bool Prepare() { return Target() != null; }
        private static MethodBase TargetMethod() { return Target(); }

        private static bool Prefix(object __instance)
        {
            try
            {
                if (!Input.GetKeyDown(KeyCode.Escape)) return true;
                if (ControlsScreenEsc.JustHandled) return false;
                KeysOptions.Reload(); // may have just been changed in the Mods screen
                if (!KeysOptions.Current.EscClearsBinding) return true;
                return !ControlsScreenEsc.ClearActiveBinding(__instance);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KeysNotIncluded] Controls screen Esc: " + ex);
                return true;
            }
        }
    }
}
