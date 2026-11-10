using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mythologicum
{
    public enum GameplayAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, RotateLeft, RotateRight,
        Buildings, Paths, Fences, Creatures, Enclosures, Terrain, Demolish, Gate, RotateBuilding,
        Pause, ToggleSimulationPause, NormalSpeed, DoubleSpeed, TripleSpeed
    }

    /// <summary>Keyboard preferences shared by camera, construction and play controls.</summary>
    public static class GameInput
    {
        private const string Prefix = "Mythologicum.Controls.";
        private static readonly Key[] defaults = { Key.W, Key.S, Key.A, Key.D, Key.Q, Key.E,
            Key.B, Key.P, Key.F, Key.C, Key.H, Key.T, Key.X, Key.G, Key.R,
            Key.Escape, Key.Space, Key.Digit1, Key.Digit2, Key.Digit3 };
        private static readonly string[] names = { "ui.input.action.MoveForward", "ui.input.action.MoveBack", "ui.input.action.MoveLeft", "ui.input.action.MoveRight", "ui.input.action.RotateLeft", "ui.input.action.RotateRight",
            "ui.input.action.Buildings", "ui.input.action.Paths", "ui.input.action.Fences", "ui.input.action.Creatures", "ui.input.action.Enclosures", "ui.input.action.Terrain", "ui.input.action.Demolish", "ui.input.action.Gate", "ui.input.action.RotateBuilding",
            "ui.input.action.Pause", "ui.input.action.ToggleSimulationPause", "ui.input.action.NormalSpeed", "ui.input.action.DoubleSpeed", "ui.input.action.TripleSpeed" };
        private static Key[] bindings;
        private static GameplayAction capturing;
        private static Action<bool, string> completion;
        private static int suppressedFrame = -1;
        public static bool IsCapturing => completion != null;
        public static bool BlocksGameplayInput => SceneTransition.IsLoading || IsCapturing || suppressedFrame == Time.frameCount;
        public static bool CommandModifierHeld => Keyboard.current != null && (Keyboard.current.ctrlKey.isPressed
            || Keyboard.current.altKey.isPressed || Keyboard.current.leftMetaKey.isPressed || Keyboard.current.rightMetaKey.isPressed);
        public static int Revision { get; private set; }

        public static string NameId(GameplayAction action) => names[(int)action];
        public static string Name(GameplayAction action) => Loc.Text(NameId(action));
        public static Key DefaultKey(GameplayAction action) => defaults[(int)action];
        public static Key GetKey(GameplayAction action) { EnsureLoaded(); return bindings[(int)action]; }
        public static string KeyCaption(GameplayAction action) => Caption(GetKey(action));
        public static string Caption(Key key) => key >= Key.Digit1 && key <= Key.Digit0 ? key.ToString().Replace("Digit", "")
            : key == Key.Space ? Loc.Text("ui.input.key.space") : key == Key.Escape ? Loc.Text("ui.input.key.escape") : Loc.Enum(key);

        public static bool Pressed(GameplayAction action)
            => !BlocksGameplayInput && !CommandModifierHeld && Keyboard.current != null && Keyboard.current[GetKey(action)].wasPressedThisFrame;

        public static bool Held(GameplayAction action)
        {
            if (BlocksGameplayInput || CommandModifierHeld || Keyboard.current == null) return false;
            if (Keyboard.current[GetKey(action)].isPressed) return true;
            // Arrow keys remain alternate camera controls, independent of the configurable primary keys.
            return action == GameplayAction.MoveForward ? Keyboard.current.upArrowKey.isPressed
                : action == GameplayAction.MoveBack ? Keyboard.current.downArrowKey.isPressed
                : action == GameplayAction.MoveLeft ? Keyboard.current.leftArrowKey.isPressed
                : action == GameplayAction.MoveRight && Keyboard.current.rightArrowKey.isPressed;
        }

        public static bool TrySetKey(GameplayAction action, Key key, out string error)
        {
            EnsureLoaded();
            if (!AllowedKey(key, action)) { error = Loc.Text("ui.input.invalid_key"); return false; }
            for (int i = 0; i < bindings.Length; i++)
                if (i != (int)action && bindings[i] == key)
                { error = Loc.Format("ui.input.key_conflict", Caption(key), Name((GameplayAction)i)); return false; }
            bindings[(int)action] = key;
            PlayerPrefs.SetInt(Prefix + action, (int)key); PlayerPrefs.Save(); Revision++;
            error = ""; return true;
        }

        public static bool AllowedKey(Key key, GameplayAction action)
        {
            if (!Enum.IsDefined(typeof(Key), key) || key == Key.None || key.ToString() == "IMESelected") return false;
            if (key == Key.Escape) return action == GameplayAction.Pause;
            // Modifiers are reserved for combinations such as construction undo/redo.
            return key != Key.LeftCtrl && key != Key.RightCtrl && key != Key.LeftAlt && key != Key.RightAlt
                && key != Key.LeftShift && key != Key.RightShift && key != Key.LeftMeta && key != Key.RightMeta
                && key != Key.UpArrow && key != Key.DownArrow && key != Key.LeftArrow && key != Key.RightArrow;
        }

        public static void BeginCapture(GameplayAction action, Action<bool, string> completed)
        {
            CancelCapture(); capturing = action; completion = completed; suppressedFrame = Time.frameCount;
        }
        public static void CancelCapture()
        {
            if (completion == null) return;
            var callback = completion; completion = null; suppressedFrame = Time.frameCount;
            callback(false, Loc.Text("ui.input.capture_cancelled"));
        }
        public static void UpdateCapture()
        {
            if (!IsCapturing || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) { CancelCapture(); return; }
            if (CommandModifierHeld) return;
            foreach (var key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                suppressedFrame = Time.frameCount;
                if (!TrySetKey(capturing, key.keyCode, out string error))
                { completion(false, Loc.Format("ui.input.retry_capture", error)); return; }
                var callback = completion; completion = null;
                callback(true, Loc.Format("ui.input.capture_success", Name(capturing), KeyCaption(capturing))); return;
            }
        }

        public static void ResetDefaults()
        {
            CancelCapture(); bindings = (Key[])defaults.Clone();
            for (int i = 0; i < bindings.Length; i++) PlayerPrefs.DeleteKey(Prefix + (GameplayAction)i);
            PlayerPrefs.Save(); Revision++;
        }
        private static void EnsureLoaded()
        {
            if (bindings != null) return;
            bindings = (Key[])defaults.Clone();
            for (int i = 0; i < bindings.Length; i++)
            {
                var key = (Key)PlayerPrefs.GetInt(Prefix + (GameplayAction)i, (int)defaults[i]);
                if (AllowedKey(key, (GameplayAction)i)) bindings[i] = key;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { bindings = null; completion = null; suppressedFrame = -1; Revision = 0; }
    }
}
