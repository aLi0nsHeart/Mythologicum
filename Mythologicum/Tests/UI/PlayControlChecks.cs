using System;
using Mythologicum;
using UnityEngine.InputSystem;

public static class PlayControlChecks
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Equal(float expected, float actual, string message)
        => Check(Math.Abs(expected - actual) < .001f && !float.IsNaN(actual), message + ": " + actual);
    public static void Main()
    {
        Equal(1, SimulationSpeed.Sanitize(float.NaN), "Invalid simulation speed has a safe default");
        Equal(6, SimulationSpeed.Sanitize(20), "Simulation speed is bounded to the supported range");
        Equal(.1f, SimulationSpeed.Sanitize(-1), "Simulation speed stays positive");
        Equal(2, SimulationSpeed.Sanitize(2), "Normal speed choices remain exact");
        Equal(.75f, GameSettings.SanitizeUIScale(.2f, .75f, 1.5f), "Small interface scale clamps to its designer minimum");
        Equal(1.5f, GameSettings.SanitizeUIScale(3f, .75f, 1.5f), "Large interface scale clamps to its designer maximum");
        Equal(1, GameSettings.SanitizeUIScale(float.PositiveInfinity, .75f, 1.5f), "Invalid interface scale resets to readable default");
        Equal(1.25f, GameSettings.SanitizeUIScale(1.25f, .75f, 1.5f), "Intermediate interface scale remains adjustable");
        Check(GameInput.DefaultKey(GameplayAction.Buildings) == Key.B && GameInput.DefaultKey(GameplayAction.MoveForward) == Key.W,
            "Default construction and movement shortcuts preserve current controls");
        Check(GameInput.DefaultKey(GameplayAction.ToggleSimulationPause) == Key.Space && GameInput.DefaultKey(GameplayAction.TripleSpeed) == Key.Digit3,
            "Simulation speed shortcuts have distinct defaults");
        Check(!GameInput.AllowedKey(Key.Escape, GameplayAction.Buildings) && GameInput.AllowedKey(Key.Escape, GameplayAction.Pause),
            "Escape remains the universal close and pause shortcut");
        Check(!GameInput.AllowedKey(Key.LeftCtrl, GameplayAction.Buildings) && !GameInput.AllowedKey(Key.LeftArrow, GameplayAction.Buildings),
            "Modifiers and alternate camera arrows cannot be stolen by construction bindings");
        Check(GameInput.AllowedKey(Key.Y, GameplayAction.Buildings) && !GameInput.AllowedKey(Key.None, GameplayAction.Buildings),
            "Letter bindings are accepted and missing keys rejected");
        Check(GameInput.Caption(Key.Digit2) == "2" && GameInput.Caption(Key.Escape) == "Esc", "Shortcut captions are readable");
        Console.WriteLine("PASS: " + checks + " play-control, UI scale and shortcut policy checks.");
    }
}
