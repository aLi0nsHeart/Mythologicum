using System;
using System.Collections;
using System.IO;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Play-mode regression for destroyed UI selection and construction shortcuts. Disposable project only.</summary>
[InitializeOnLoad]
public static class ConstructionUndoInputValidation
{
    private const string Running = "ConstructionUndoInputValidation.running";
    private static IEnumerator steps;
    private static double began;
    private static int checks;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "ConstructionUndoInputResults.txt");

    static ConstructionUndoInputValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            began = EditorApplication.timeSinceStartup; checks = 0; steps = Run();
        };
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "ConstructionUndoInputValidation";
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (steps == null || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 120 || SceneTransition.HasFailed)
                throw new Exception("Construction Undo input validation timed out or scene preparation failed.");
            if (steps.MoveNext()) return;
            steps = null;
            File.WriteAllText(Result, "PASS: " + checks + " construction Undo, destroyed UI selection and text focus checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            steps = null;
            Debug.LogException(error); File.WriteAllText(Result, "FAIL: " + error); EditorApplication.Exit(1);
        }
    }

    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 32, depth = 32, seed = 81325, relief = 0, featureSize = 28, forestDensity = 0 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var controller = Object.FindFirstObjectByType<BuildModeController>();
        controller.enabled = false; Time.timeScale = 0;
        var regression = ConstructionUndoInputNativeChecks.Run(controller, (passed, reason) =>
        {
            if (!passed) throw new Exception(reason);
            checks++; Debug.Log("UNDO INPUT PASS: " + reason);
        });
        try { while (regression.MoveNext()) yield return regression.Current; }
        finally { (regression as IDisposable)?.Dispose(); }
    }
}
