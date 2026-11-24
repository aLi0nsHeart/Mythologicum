using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Destroyed UI selection and real keyboard Undo, in a prepared disposable Play-mode park.</summary>
public static class ConstructionUndoInputNativeChecks
{
    public static IEnumerator Run(BuildModeController controller, Action<bool, string> check)
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        T Field<T>(string name) => (T)typeof(BuildModeController).GetField(name, PrivateInstance).GetValue(controller);
        object Call(string name, params object[] values)
            => typeof(BuildModeController).GetMethod(name, PrivateInstance).Invoke(controller, values);
        void Check(bool value, string message) => check(value, "Construction undo input: " + message);
        var events = EventSystem.current;
        Check(events != null && !controller.enabled && !GameInput.BlocksGameplayInput,
            "the prepared fixture has an event system and unblocked manual input");
        var previousSelection = events.currentSelectedGameObject;
        var originalKeyboard = Keyboard.current;
        var originalSettings = InputSystem.settings;
        var settings = Object.Instantiate(originalSettings);
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        bool originalBackground = Application.runInBackground;
        var update = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null);
        Keyboard keyboard = null;
        GameObject root = null, selectedButton = null;
        var grid = Field<SquareBuildGrid>("grid");
        var paths = Field<PathConstruction>("paths");
        var creatures = Field<CreatureWorld>("creatureWorld");
        Vector2Int cell = default;
        bool placed = false;
        int originalHistory = controller.UndoCount;
        void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            update.Invoke(null, new object[] { InputUpdateType.Dynamic });
            keyboard.MakeCurrent();
        }
        try
        {
            InputSystem.settings = settings; Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>("Construction undo lifetime validation"); keyboard.MakeCurrent();
            Keys(); _ = keyboard.zKey.wasPressedThisFrame;
            bool found = false;
            for (int y = grid.Depth - 4; y >= 1 && !found; y--)
            for (int x = grid.Width - 4; x >= 1 && !found; x--)
            {
                var candidate = new Vector2Int(x, y);
                if (!grid.IsAvailable(candidate) || !grid.GroundWalkable(candidate, true)
                    || creatures.Agents.Any(actor => actor.Alive && (actor.Cell - candidate).sqrMagnitude < 25)) continue;
                cell = candidate; found = true;
            }
            Check(found, "there is a clear patch for a real reversible construction action");
            Call("BeginConstructionAction", "park.build_mode_controller.build_path_route");
            placed = paths.TryPlace(cell);
            Check(placed, "the fixture places a new path tile");
            Call("CommitConstructionAction", 0f);
            Check(controller.UndoCount == originalHistory + 1, "placement records exactly one action");

            root = new GameObject("Undo input lifetime fixture", typeof(RectTransform));
            root.transform.SetParent(SceneTransition.GameplayCanvas.transform, false);
            selectedButton = new GameObject("Temporary selected menu row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            selectedButton.transform.SetParent(root.transform, false);
            events.SetSelectedGameObject(selectedButton);
            Check(ReferenceEquals(events.currentSelectedGameObject, selectedButton), "a live menu row is genuinely selected");
            Object.Destroy(selectedButton);
            yield return null;
            var staleSelection = events.currentSelectedGameObject;
            Check(!ReferenceEquals(staleSelection, null) && staleSelection == null,
                "destroying the selected row leaves the actual Unity destroyed-object reference");
            Check(ReferenceEquals(staleSelection, selectedButton), "the event system still holds that destroyed row");
            for (int frame = 0; frame < 4; frame++)
            {
                Keys();
                bool consumed = false;
                for (int i = 0; i < 64; i++) consumed |= (bool)Call("HandleConstructionUndoInput");
                Check(!consumed && controller.UndoCount == originalHistory + 1 && grid.ContentAt(cell) == BuildCellContent.Path,
                    "ordinary keyboard updates tolerate destroyed selection without exception or Undo (frame " + frame + ")");
                yield return null;
            }
            Keys(Key.LeftCtrl, Key.Z);
            Check(keyboard.zKey.wasPressedThisFrame, "the injected Ctrl+Z has a real key-down edge");
            Check((bool)Call("HandleConstructionUndoInput"), "Ctrl+Z still reaches construction Undo after its selected UI row was destroyed");
            placed = grid.ContentAt(cell) == BuildCellContent.Path;
            Check(!placed && controller.UndoCount == originalHistory, "the shortcut reverses exactly the fixture's latest placement");
            Keys();
            Check(!(bool)Call("HandleConstructionUndoInput") && controller.UndoCount == originalHistory,
                "releasing the shortcut does not undo another action");

            Call("BeginConstructionAction", "park.build_mode_controller.build_path_route");
            placed = paths.TryPlace(cell);
            Check(placed, "a second real placement is available for the text-entry guard");
            Call("CommitConstructionAction", 0f);
            var inputObject = new GameObject("Focused price input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(root.transform, false);
            var input = inputObject.GetComponent<InputField>();
            var captionObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            captionObject.transform.SetParent(inputObject.transform, false);
            var caption = captionObject.GetComponent<Text>(); caption.font = UITheme.BodyFont;
            caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
            input.textComponent = caption; input.targetGraphic = inputObject.GetComponent<Image>(); input.text = "15";
            events.SetSelectedGameObject(inputObject); input.ActivateInputField();
            yield return null;
            Check(input.isFocused && ReferenceEquals(events.currentSelectedGameObject, inputObject),
                "a real UI input field owns keyboard focus");
            Keys(Key.LeftCtrl, Key.Z);
            Check(keyboard.zKey.wasPressedThisFrame, "the focused input also receives a genuine Ctrl+Z edge");
            Check(!(bool)Call("HandleConstructionUndoInput") && controller.UndoCount == originalHistory + 1
                && grid.ContentAt(cell) == BuildCellContent.Path,
                "Ctrl+Z while typing never undoes park construction");
            Keys(); input.DeactivateInputField(); events.SetSelectedGameObject(null);
            Check(controller.UndoLastConstruction() && controller.UndoCount == originalHistory,
                "normal Undo works again when text entry ends");
            placed = false;
        }
        finally
        {
            if (keyboard != null && keyboard.added)
            {
                Keys(); InputSystem.RemoveDevice(keyboard);
            }
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            InputSystem.settings = originalSettings; Application.runInBackground = originalBackground;
            if (events != null) events.SetSelectedGameObject(previousSelection != null ? previousSelection : null);
            if (root != null) Object.Destroy(root);
            if (selectedButton != null) Object.Destroy(selectedButton);
            if (placed)
            {
                // Best-effort cleanup if a regression assertion interrupted the fixture.
                if (controller.UndoCount == originalHistory + 1) controller.UndoLastConstruction();
                else paths.TryRemove(cell);
            }
            Object.Destroy(settings);
        }
    }
}
