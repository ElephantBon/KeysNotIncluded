using System;
using System.Collections.Generic;
using PeterHan.PLib.Actions;
using UnityEngine;

namespace KeysNotIncluded
{
    /// <summary>
    /// The mod's key bindings. They're registered with PLib, which adds them to the game's own
    /// Options > Controls screen (in the "Mods" section), so players rebind or clear them like any
    /// other game hotkey and the game saves them with its own bindings.
    ///
    /// A key only does something (and is only consumed) when its action applies, e.g. object keys
    /// need a matching object selected.
    /// </summary>
    internal static class Hotkeys
    {
        private const string Prefix = "Keys Not Included: ";

        internal sealed class Binding
        {
            public PAction Action;
            public Func<bool> CanRun;
            public Func<bool> Run;
        }

        public static readonly List<Binding> All = new List<Binding>();

        /// <summary>Create the actions. PLib requires this during UserMod2.OnLoad.</summary>
        public static void Register()
        {
            var mgr = new PActionManager();

            void Add(string id, string title, KKeyCode key, Modifier mod, Func<bool> canRun, Func<bool> run)
            {
                var action = mgr.CreateAction("KeysNotIncluded." + id, new LocString(Prefix + title), new PKeyBinding(key, mod));
                All.Add(new Binding { Action = action, CanRun = canRun, Run = run });
            }

            // Selected object (same as the buttons in its info panel)
            Add("DeconstructSelected", "Deconstruct selected (press again to cancel)", KKeyCode.Delete, Modifier.None,
                SelectedObject.CanToggleDeconstruct, SelectedObject.ToggleDeconstruct);
            Add("CancelSelectedConstruction", "Cancel selected construction", KKeyCode.Backspace, Modifier.None,
                SelectedObject.CanCancelConstruction, SelectedObject.CancelConstruction);

            // Tools
            Add("DeconstructBackwall", "Deconstruct background buildings", KKeyCode.X, Modifier.Shift,
                BackwallDeconstruct.CanRun, BackwallDeconstruct.Run);
        }
    }

    /// <summary>
    /// Invisible screen that receives key presses during gameplay and drives per-frame features.
    /// Text boxes (renaming a duplicant, search fields, ...) sit on higher-priority screens and
    /// consume keys first, so typing never triggers these hotkeys.
    /// </summary>
    public sealed class HotkeyScreen : KScreen
    {
        public static HotkeyScreen Instance { get; private set; }

        private bool overlayFailed;

        public static void Create()
        {
            if (Instance != null) return;
            var gsm = GameScreenManager.Instance;
            if (gsm == null || gsm.ssOverlayCanvas == null) return;

            var go = new GameObject("KeysNotIncluded.Hotkeys", typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(gsm.ssOverlayCanvas.transform, false);
            go.AddComponent<HotkeyScreen>();
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Activate();
        }

        protected override void OnCleanUp()
        {
            if (Instance == this) Instance = null;
            base.OnCleanUp();
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (!e.Consumed)
            {
                foreach (var b in Hotkeys.All)
                {
                    bool applies;
                    try { applies = b.CanRun(); }
                    catch { applies = false; }
                    if (!applies || !e.TryConsume(b.Action.GetKAction())) continue;
                    try { b.Run(); }
                    catch (Exception ex) { Debug.LogWarning("[KeysNotIncluded] " + b.Action.Identifier + ": " + ex); }
                    return;
                }
            }
            base.OnKeyDown(e);
        }

        private void LateUpdate()
        {
            if (overlayFailed) return;
            try { BuildMenuOverlay.Poll(); }
            catch (Exception ex)
            {
                overlayFailed = true; // don't spam the log every frame
                Debug.LogWarning("[KeysNotIncluded] build menu overlays disabled: " + ex);
            }
        }
    }
}
