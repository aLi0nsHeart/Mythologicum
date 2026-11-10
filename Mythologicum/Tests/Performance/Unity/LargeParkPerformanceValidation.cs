using System;
using System.IO;
using System.Linq;
using Mythologicum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Copy into Assets/Editor only in a disposable validation project.</summary>
public static class LargeParkPerformanceValidation
{
    public static void Run()
    {
        try
        {
            // A fresh batch editor starts with an untitled scene, which Unity cannot extend additively.
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            var originalScene = SceneManager.GetActiveScene();
            var originalScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index)).ToArray();
            var originalDirty = originalScenes.Select(scene => scene.isDirty).ToArray();
            int actorsBefore = Object.FindObjectsByType<Mythologicum.CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var report = LargeParkBenchmark.RunValidationBlocking();
            if (SceneManager.sceneCount != originalScenes.Length || SceneManager.GetActiveScene() != originalScene
                || originalScenes.Where((scene, index) => !scene.isLoaded || scene.isDirty != originalDirty[index]).Any()
                || Object.FindObjectsByType<Mythologicum.CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != actorsBefore)
                throw new Exception("The benchmark did not restore the original scene state and actor count.");
            if (report.profiles.Count != 3) throw new Exception("The benchmark did not complete all three populations.");
            foreach (var profile in report.profiles)
                if (profile.totalActors != profile.creatures + profile.visitors + profile.staff
                    || profile.endingCreatures != profile.creatures || profile.endingVisitors != profile.visitors || profile.endingStaff != profile.staff
                    || profile.totalTick.samples != report.measuredTicks || profile.totalTick.p95Ms < 0)
                    throw new Exception("Invalid benchmark results for " + profile.name);
            string path = LargeParkBenchmark.WriteReport(report);
            File.WriteAllText("LargeParkPerformanceResults.txt", "PASS: Three real simulation populations, exact actor counts, native timing samples and cleanup.\nReport: " + path);
            Debug.Log("PARK PERFORMANCE VALIDATION PASS: " + path); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("LargeParkPerformanceResults.txt", "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
