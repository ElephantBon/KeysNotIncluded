using System;
using HarmonyLib;
using UnityEngine;

namespace KeysNotIncluded
{
    /// <summary>
    /// Hotkey versions of buttons in the selected object's info panel.
    /// </summary>
    internal static class SelectedObject
    {
        /// <summary>The object shown in the info panel, or null.</summary>
        private static GameObject Selected()
        {
            var tool = SelectTool.Instance;
            var sel = tool != null ? tool.selected : null;
            return sel != null ? sel.gameObject : null;
        }

        /// <summary>
        /// Rebuilds the info panel's buttons so e.g. "Deconstruct" turns into "Cancel Deconstruct".
        /// The button handlers in the game do the same after toggling.
        /// </summary>
        private static void RefreshStatusPanel(GameObject go)
        {
            if (go == null || Game.Instance == null || Game.Instance.userMenu == null) return;
            Game.Instance.userMenu.Refresh(go);
        }

        // ------------------------------------------------------------------ deconstruct

        private static Deconstructable SelectedDeconstructable()
        {
            var go = Selected();
            var d = go != null ? go.GetComponent<Deconstructable>() : null;
            if (d == null) return null;
            // Some buildings (e.g. the Printing Pod) hide the button; respect that.
            var allow = AccessTools.Field(typeof(Deconstructable), "allowDeconstruction");
            if (allow != null && allow.GetValue(d) is bool b && !b) return null;
            return d;
        }

        public static bool CanToggleDeconstruct()
        {
            return SelectedDeconstructable() != null;
        }

        /// <summary>
        /// Same as the Deconstruct button: marks the building for deconstruction, or cancels the
        /// order if it's already marked.
        /// </summary>
        public static bool ToggleDeconstruct()
        {
            var d = SelectedDeconstructable();
            if (d == null) return false;

            // The info panel button calls OnDeconstruct(), which toggles. Prefer it so we behave exactly the same.
            var onDeconstruct = AccessTools.Method(typeof(Deconstructable), "OnDeconstruct", Type.EmptyTypes);
            if (onDeconstruct != null)
            {
                onDeconstruct.Invoke(d, null);
                RefreshStatusPanel(d.gameObject);
                return true;
            }

            var isMarked = AccessTools.Method(typeof(Deconstructable), "IsMarkedForDeconstruction", Type.EmptyTypes);
            bool marked = isMarked != null && isMarked.Invoke(d, null) is bool m && m;
            if (marked)
            {
                var cancel = AccessTools.Method(typeof(Deconstructable), "CancelDeconstruction", Type.EmptyTypes);
                if (cancel == null) return false;
                cancel.Invoke(d, null);
                RefreshStatusPanel(d.gameObject);
                return true;
            }
            var queueUser = AccessTools.Method(typeof(Deconstructable), "QueueDeconstruction", new[] { typeof(bool) });
            if (queueUser != null)
            {
                queueUser.Invoke(d, new object[] { true });
                RefreshStatusPanel(d.gameObject);
                return true;
            }
            var queue = AccessTools.Method(typeof(Deconstructable), "QueueDeconstruction", Type.EmptyTypes);
            if (queue != null)
            {
                queue.Invoke(d, null);
                RefreshStatusPanel(d.gameObject);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ cancel construction

        public static bool CanCancelConstruction()
        {
            var go = Selected();
            return go != null && go.GetComponent<Constructable>() != null;
        }

        /// <summary>
        /// Same as the Cancel button on a planned / under-construction building: the order is removed
        /// and any delivered materials are dropped.
        /// </summary>
        public static bool CancelConstruction()
        {
            var go = Selected();
            if (go == null || go.GetComponent<Constructable>() == null) return false;
            go.Trigger((int)GameHashes.Cancel, null);
            return true;
        }
    }
}
