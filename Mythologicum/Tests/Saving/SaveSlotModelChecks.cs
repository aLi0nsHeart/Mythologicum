using System;
using System.IO;
using Mythologicum;

public static class SaveSlotModelChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Main()
    {
        string id = "manual-" + Guid.NewGuid().ToString("N");
        Check(SaveSlotPaths.IsManual(id), "Generated opaque manual slot ID is accepted");
        Check(SaveSlotPaths.IsAutomatic("auto-00") && SaveSlotPaths.IsAutomatic("auto-98"), "Rotation IDs stay separate from manual IDs");
        foreach (string invalid in new[] { null, "", "../park", "..\\park", "manual-../", "manual-" + new string('z', 32), "auto-1", "auto-../", "C:\\park" })
            Check(!SaveSlotPaths.IsManual(invalid) && !SaveSlotPaths.IsAutomatic(invalid), "Player text and traversal strings cannot become slot IDs");
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MythologicumSlotChecks"));
        string resolved = SaveSlotPaths.Resolve(root, id);
        Check(resolved.StartsWith(root + Path.DirectorySeparatorChar) && Path.GetFileName(resolved) == id + ".json", "A valid slot resolves only inside its save directory");
        Check(Path.GetFileName(SaveSlotPaths.Resolve(root, SaveSlotPaths.Legacy)) == "park-save.json", "Legacy save path remains supported");
        bool rejected = false; try { SaveSlotPaths.Resolve(root, "../park"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Resolving an untrusted slot ID fails before file access");
        Check(ParkSaveStore.CleanName("  My\nPark  ") == "My Park", "Player save names are trimmed and single-line");
        Check(ParkSaveStore.CleanName("") == "Untitled park" && ParkSaveStore.CleanName(new string('a', 90)).Length == 64, "Save names have a useful default and bounded display length");

        var schedule = new AutosaveSchedule();
        Check(!schedule.Advance(299, true, 300, 30), "Autosaving waits for the configured active interval");
        Check(!schedule.Advance(10000, false, 300, 30), "Paused time does not advance autosaving");
        Check(schedule.Advance(1, true, 300, 30), "The first autosave becomes due after active time reaches its interval");
        schedule.Completed(true);
        Check(!schedule.Advance(1, true, 300, 30), "Successful autosave starts a fresh interval instead of catching up");
        schedule.Completed(false);
        Check(!schedule.Advance(29, true, 300, 30) && schedule.Advance(1, true, 300, 30), "Failed autosaves retry after the configured delay");
        schedule.Restart();
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Check(!schedule.Advance(invalid, true, 300, 30), "Invalid elapsed time cannot force an autosave");
        Check(!schedule.Advance(299, true, 300, 30), "Resetting the schedule clears failed-save retry state");
        Console.WriteLine("PASS: " + checks + " save slot IDs, names and autosave schedule checks.");
    }
}
