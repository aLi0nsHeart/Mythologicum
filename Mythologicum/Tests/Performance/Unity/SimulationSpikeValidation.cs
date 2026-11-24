using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs targeted creature search regressions in the disposable Play-mode project.</summary>
[InitializeOnLoad]
public static class SimulationSpikeValidation
{
    private const string Running = "SimulationSpikeValidation.running";
    private static bool pending;
    static SimulationSpikeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); pending = true;
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "SimulationSpikeValidation";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!pending || !Application.isPlaying) return;
        pending = false;
        try
        {
            int creatures = CreatureSpikeNativeChecks.RunChecks();
            string result = $"PASS: {creatures} creature search, cache invalidation, route and habitat checks.";
            File.WriteAllText("SimulationSpikeResults.txt", result);
            Debug.Log(result); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("SimulationSpikeResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
