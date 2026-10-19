using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MimesisMap
{
    /// <summary>
    /// Frees the cursor and pauses the player's controls while any of our UIs needs the mouse
    /// (settings window, interactive big map). Restores everything when the last one lets go.
    /// </summary>
    internal static class InputBlock
    {
        private static readonly HashSet<string> Holders = new HashSet<string>();
        private static readonly List<PlayerInput> Suspended = new List<PlayerInput>();
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;

        public static bool Active => Holders.Count > 0;

        public static void Set(string holder, bool on)
        {
            if (on) Acquire(holder); else Release(holder);
        }

        public static void Acquire(string holder)
        {
            if (!Holders.Add(holder) || Holders.Count > 1) return;
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            Suspended.Clear();
            try
            {
                foreach (var pi in Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
                {
                    if (pi == null || !pi.inputIsActive) continue;
                    pi.DeactivateInput();
                    Suspended.Add(pi);
                }
            }
            catch { }
        }

        public static void Release(string holder)
        {
            if (!Holders.Remove(holder) || Holders.Count > 0) return;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            foreach (var pi in Suspended)
                try { if (pi != null) pi.ActivateInput(); } catch { }
            Suspended.Clear();
        }

        /// <summary>The game re-locks the cursor on its own; keep it free every frame while blocked.</summary>
        public static void Tick()
        {
            if (Holders.Count == 0) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
