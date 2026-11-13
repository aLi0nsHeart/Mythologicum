using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>Run in the disposable validation project; CPU maintenance only, excluding rendering and simulation.</summary>
[InitializeOnLoad]
public static class UIPerformanceValidation
{
    private const string Running = "UIPerformanceValidation.running";
    private static bool pending;
    private static bool allocationCounterAvailable;
    private static int settledFrames;
    private static double began;

    [Serializable] private sealed class Profile
    {
        public string name;
        public int totalGraphics, visibleGraphics, activeLayoutCallbacks, activeOverflowCallbacks, activeIconCallbacks, activeFrameCallbacks;
        public int samples;
        public double medianMs, p95Ms, maximumMs, bytesPerUpdate;
    }
    [Serializable] private sealed class Report
    {
        public string utcTime, unityVersion, processor;
        public bool allocationCounterAvailable;
        public string workload = "Actual generated GameScene UI. CPU maintenance callbacks are invoked in a fixed order for 120 synthetic 60 Hz updates, with a discovery scan every 12 updates. Includes active layout, overflow, icon and frame callbacks, but excludes simulation, rendering, canvas mesh rebuilding, input and dynamic report generation. Not game FPS. Allocation bytes are -1 when the runtime fails a known-allocation counter probe; zero from an unsupported counter is not an allocation-free result.";
        public List<Profile> profiles = new List<Profile>();
    }

    static UIPerformanceValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); pending = true; began = EditorApplication.timeSinceStartup; settledFrames = 0;
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "UIPerformanceValidation";
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!pending || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 240) throw new Exception("UI benchmark preparation timed out.");
            if (SceneTransition.HasFailed) throw new Exception("GameScene initialization failed.");
            var park = Object.FindFirstObjectByType<BuildModeController>();
            if (park == null || !park.IsReady || SceneTransition.IsLoading || ++settledFrames < 3) return;
            pending = false;
            park.enabled = false; Time.timeScale = 0;
            var canvas = SceneTransition.GameplayCanvas;
            allocationCounterAvailable = CalibrateAllocationCounter();
            var report = new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                processor = SystemInfo.processorType, allocationCounterAvailable = allocationCounterAvailable };
            report.profiles.Add(Measure(canvas, "Closed menus"));
            canvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "Building").onClick.Invoke();
            canvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "Buildings (B)").onClick.Invoke();
            report.profiles.Add(Measure(canvas, "Building catalogue"));
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "Performance");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "UIBenchmark-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(directory, "LatestUIBenchmark.txt"), path);
            File.WriteAllText("UIPerformanceResults.txt", "PASS: Actual generated UI, closed and open construction menus.\nReport: " + path);
            Debug.Log("UI PERFORMANCE PASS: " + path); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            pending = false; File.WriteAllText("UIPerformanceResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static Profile Measure(Canvas canvas, string name)
    {
        UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
        var applicator = canvas.GetComponent<UIThemeApplicator>();
        var maintain = Callback(applicator);
        var nextScan = typeof(UIThemeApplicator).GetField("nextScan", BindingFlags.Instance | BindingFlags.NonPublic);
        var layouts = canvas.GetComponentsInChildren<UILayoutElement>().Where(item => item.isActiveAndEnabled).ToArray();
        var overflow = canvas.GetComponentsInChildren<UIOverflowScroll>().Where(item => item.isActiveAndEnabled).ToArray();
        var icons = canvas.GetComponentsInChildren<UIIcon>().Where(item => item.isActiveAndEnabled).ToArray();
        var frames = canvas.GetComponentsInChildren<UIFramedPanel>().Where(item => item.isActiveAndEnabled).ToArray();
        var callbacks = layouts.Select(Callback).Concat(overflow.Select(Callback)).Concat(icons.Select(Callback)).Concat(frames.Select(Callback)).ToArray();
        Action update = () => { maintain(); foreach (var callback in callbacks) callback(); };
        for (int index = 0; index < 20; index++) { if (index % 12 == 0) nextScan.SetValue(applicator, -1f); update(); }
        var times = new List<double>(120); long allocated = 0;
        var stopwatch = new Stopwatch();
        for (int index = 0; index < 120; index++)
        {
            // Reflection and reporting allocations are deliberately outside each sample.
            if (index % 12 == 0) nextScan.SetValue(applicator, -1f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart(); update(); stopwatch.Stop();
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        var profile = new Profile { name = name, totalGraphics = canvas.GetComponentsInChildren<Graphic>(true).Length,
            visibleGraphics = canvas.GetComponentsInChildren<Graphic>().Count(item => item.isActiveAndEnabled),
            activeLayoutCallbacks = layouts.Length, activeOverflowCallbacks = overflow.Length, activeIconCallbacks = icons.Length,
            activeFrameCallbacks = frames.Length, samples = times.Count, medianMs = times[59], p95Ms = times[113], maximumMs = times[119],
            bytesPerUpdate = allocationCounterAvailable ? (double)allocated / times.Count : -1 };
        if (profile.totalGraphics < profile.visibleGraphics || profile.samples != 120 || profile.medianMs < 0)
            throw new Exception("Invalid UI profile: " + name);
        return profile;
    }
    private static Action Callback(MonoBehaviour target)
        => (Action)Delegate.CreateDelegate(typeof(Action), target, target.GetType().GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance));
    private static bool CalibrateAllocationCounter()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var probe = new byte[8192];
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(probe);
        return allocated >= probe.Length;
    }
}
