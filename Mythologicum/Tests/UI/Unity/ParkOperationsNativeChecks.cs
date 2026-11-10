using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mythologicum;
using Mythologicum.Editor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class ParkOperationsNativeChecks
{
    public static int Run(BuildModeController controller)
    {
        int checks = ConstructionUndoNativeChecks.Run(controller);
        void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception("Park operations: " + message); Debug.Log("PARK OPERATIONS PASS: " + message); }
        T Field<T>(string name) => (T)typeof(BuildModeController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var grid = Field<SquareBuildGrid>("grid"); var construction = Field<BuildingConstruction>("buildingConstruction");
        var creatures = Field<CreatureWorld>("creatureWorld"); var overlays = Field<ParkOverlayPresentation>("overlays");
        var enclosures = Field<EnclosureMap>("enclosureMap");
        var issues = GameplayDataValidator.ValidateAll();
        Check(!issues.Any(issue => issue.severity == UnityEditor.MessageType.Error), "Shipped gameplay data has no validation errors: "
            + string.Join("; ", issues.Where(issue => issue.severity == UnityEditor.MessageType.Error).Select(issue => issue.message)));
        var progression = UnityEngine.Object.Instantiate(Resources.Load<ProgressionDefinition>("Progression/DefaultProgression"));
        try
        {
            progression.rules.technologies = new[] { new TechnologyDefinition { id = "a", prerequisites = new[] { "b" } },
                new TechnologyDefinition { id = "b", prerequisites = new[] { "a", "missing" } } };
            progression.rules.startingTechnologies = Array.Empty<string>();
            var bad = GameplayDataValidator.ValidateProgression(progression, new HashSet<string>(Resources.LoadAll<CreatureDefinition>("Creatures").Select(item => item.name)));
            Check(bad.Any(issue => issue.message.Contains("cycle")) && bad.Any(issue => issue.message.Contains("unknown prerequisite")),
                "Data validation identifies both research cycles and missing prerequisite IDs");
        }
        finally { UnityEngine.Object.DestroyImmediate(progression); }

        int pathCount = 0;
        for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
            if (grid.ContentAt(new Vector2Int(x, y)) == BuildCellContent.Path) pathCount++;
        overlays.SetMode(ParkOverlayMode.PathConnections);
        Check(overlays.Mode == ParkOverlayMode.PathConnections && overlays.VisibleCellCount == pathCount && pathCount > 0,
            "The path overlay draws every actual path tile using the park network");
        overlays.SetMode(ParkOverlayMode.StaffAccess);
        Check(overlays.VisibleCellCount > 0, "The staff-access overlay draws navigation-reachable ground");
        overlays.SetMode(ParkOverlayMode.EnclosureSpace);
        Check(overlays.Mode == ParkOverlayMode.EnclosureSpace, "The enclosure-space overlay can be selected independently");
        overlays.SetMode(ParkOverlayMode.Off);
        Check(overlays.VisibleCellCount == 0 && !GameObject.Find("Map overlay legend"), "Hiding overlays removes their cells and temporary legend");

        var notices = new NotificationInbox(0);
        var rules = new ParkAlertRules { checkIntervalSeconds = .1f, recurrenceCooldownSeconds = 0,
            publicPathExposure = false, creatureEscapes = false, emptyFeederDelaySeconds = 0 };
        var monitor = new ParkAlertMonitor(grid, controller.Barriers, construction.Network, creatures, controller.Visitors,
            controller.Personnel, enclosures, notices, rules);
        var guest = controller.Visitors.Visitors.First();
        float health = guest.Actor.Health.Current;
        monitor.ResetBaseline();
        try
        {
            guest.Actor.Health.Damage(guest.Actor.Health.Maximum * .5f);
            monitor.Tick(.2f);
            Check(notices.Entries.Any(item => item.kind == ParkNoticeKind.VisitorInjured && item.hasLocation), "A real visitor injury creates a located warning letter");
            int count = notices.Entries.Count;
            monitor.Tick(.2f);
            Check(notices.Entries.Count == count, "An unresolved injury does not create repeated letters");
        }
        finally { guest.Actor.Health.Restore(Math.Max(0, health - guest.Actor.Health.Current)); }

        bool pauseBeforeAutosave = GameSaveMenu.IsOpen;
        float moneyBeforeAutosave = controller.Economy.Balance;
        Check(controller.GetComponent<ParkAutosave>().TrySave(), "The autosave component captures and validates an actual populated park");
        var automatic = ParkSaveStore.ListSlots().First(slot => slot.IsAutosave);
        var automaticData = ParkSaveStore.ReadSlot(automatic.Id);
        controller.ValidateSave(automaticData);
        Check(automatic.IsValid && Mathf.Approximately(automaticData.balance, moneyBeforeAutosave)
            && GameSaveMenu.IsOpen == pauseBeforeAutosave, "A real autosave can be read without charging money or opening a pause menu");

        float oldSpeed = SimulationSpeed.SelectedSpeed, oldScale = Time.timeScale, oldUIScale = GameSettings.UIScale;
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        Vector2 baseline = scaler.referenceResolution;
        try
        {
            SimulationSpeed.Select(2);
            Check(Mathf.Approximately(Time.timeScale, 2), "Selecting double speed advances the actual Unity simulation clock");
            GameSaveMenu.TogglePause();
            Check(GameSaveMenu.IsOpen && Time.timeScale == 0, "Opening Pause stops a sped-up simulation");
            GameSaveMenu.TogglePause();
            Check(!GameSaveMenu.IsOpen && Mathf.Approximately(Time.timeScale, 2), "Closing Pause restores the chosen simulation speed");
            SimulationSpeed.TogglePause(); Check(Time.timeScale == 0, "The compact hold control pauses without opening a menu");
            SimulationSpeed.TogglePause(); Check(Mathf.Approximately(Time.timeScale, 2), "The compact hold control resumes the selected speed");
            GameSettings.SetUIScale(1.25f);
            Check(Mathf.Approximately(GameSettings.UIScale, 1.25f) && scaler.referenceResolution.x < baseline.x,
                "The player UI scale setting updates the real canvas scaler");
        }
        finally
        {
            if (GameSaveMenu.IsOpen) GameSaveMenu.TogglePause();
            SimulationSpeed.Select(oldSpeed); Time.timeScale = oldScale; GameSettings.SetUIScale(oldUIScale);
        }

        var oldKeyboard = Keyboard.current;
        var oldInputSettings = InputSystem.settings;
        var testInputSettings = UnityEngine.Object.Instantiate(oldInputSettings);
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        bool oldRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        InputSystem.settings = testInputSettings;
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        Key originalKey = GameInput.GetKey(GameplayAction.Buildings);
        var suppression = typeof(GameInput).GetField("suppressedFrame", BindingFlags.Static | BindingFlags.NonPublic);
        int originalSuppression = (int)suppression.GetValue(null);
        var playerInputUpdate = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null);
        try
        {
            Check(GameInput.TrySetKey(GameplayAction.Buildings, Key.N, out _), "A construction shortcut can be rebound to an unused key");
            // A hidden batch Game view has no focus. The temporary settings route
            // injected events to its player buffer without editing the project's asset.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); playerInputUpdate.Invoke(null, new object[] { InputUpdateType.Dynamic });
            _ = keyboard.nKey.wasPressedThisFrame;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N)); playerInputUpdate.Invoke(null, new object[] { InputUpdateType.Dynamic }); keyboard.MakeCurrent();
            Check(GameInput.Pressed(GameplayAction.Buildings), "The remapped key drives the construction action"
                + $" (held={keyboard.nKey.isPressed}, edge={keyboard.nKey.wasPressedThisFrame}, blocked={GameInput.BlocksGameplayInput}, modifier={GameInput.CommandModifierHeld}, binding={GameInput.GetKey(GameplayAction.Buildings)})");
            Check(!GameInput.TrySetKey(GameplayAction.Paths, Key.N, out _), "Conflicting shortcuts are rejected with an explanation");
            bool cancelled = false;
            GameInput.BeginCapture(GameplayAction.Paths, (accepted, message) => cancelled = !accepted);
            Check(GameInput.BlocksGameplayInput && !GameInput.Pressed(GameplayAction.Buildings), "Shortcut capture prevents gameplay input from leaking through");
            GameInput.CancelCapture();
            Check(cancelled && GameInput.BlocksGameplayInput, "Cancelling a rebind also suppresses its completion frame");
        }
        finally
        {
            GameInput.CancelCapture(); GameInput.TrySetKey(GameplayAction.Buildings, originalKey, out _);
            suppression.SetValue(null, originalSuppression);
            InputSystem.RemoveDevice(keyboard); if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            InputSystem.settings = oldInputSettings; Application.runInBackground = oldRunInBackground;
            UnityEngine.Object.DestroyImmediate(testInputSettings);
        }
        return checks;
    }
}
