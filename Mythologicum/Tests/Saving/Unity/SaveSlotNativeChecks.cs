using System;
using System.IO;
using System.Linq;
using Mythologicum;
using UnityEngine;

/// <summary>Called from the disposable native validation project; never uses the player's save directory.</summary>
public static class SaveSlotNativeChecks
{
    public static int Run(string parentDirectory)
    {
        int checks = 0;
        void Check(bool value, string message)
        { checks++; if (!value) throw new Exception(message); Debug.Log("SAVE SLOT PASS: " + message); }
        string directory = Path.Combine(Path.GetFullPath(parentDirectory), "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var repository = new ParkSaveRepository(directory);
        ParkSaveData Data(string name, float money) => new ParkSaveData { version = 1, width = 8, depth = 8,
            cellSize = 2, balance = money, parkName = name, savedAt = DateTime.UtcNow.ToString("O") };
        string a = ParkSaveStore.NewManualSlotId(), b = ParkSaveStore.NewManualSlotId();
        Check(!repository.HasSaves && repository.ListSlots().Count == 0, "A fresh repository has no loadable slots");
        repository.WriteManual(Data("Alpha", 100), a);
        repository.WriteManual(Data("Beta", 200), b);
        Check(repository.HasSaves && repository.ListSlots().Count == 2, "Two independent manual save slots are listed");
        Check(repository.Read(a).balance == 100 && repository.Read(b).balance == 200, "Each named park loads its own state");
        Check(repository.Inspect(a).Name == "Alpha" && repository.Inspect(a).Width == 8 && repository.Inspect(a).IsValid,
            "The browser receives actual park names, dates, dimensions and validity");
        repository.WriteManual(Data("Alpha", 125), a);
        Check(repository.Read(a).balance == 125 && repository.Read(a, true).balance == 100,
            "Overwriting one manual slot retains its previous version as a readable backup");
        string legacy = SaveSlotPaths.Resolve(directory, SaveSlotPaths.Legacy);
        File.WriteAllText(legacy, JsonUtility.ToJson(Data("Previous park", 75)));
        Check(repository.ListSlots().Any(slot => slot.IsLegacy && slot.IsValid)
            && repository.Read(SaveSlotPaths.Legacy).balance == 75, "An existing park-save.json is discoverable without migration or deletion");

        string pathA = SaveSlotPaths.Resolve(directory, a);
        File.WriteAllText(pathA, "{ invalid JSON");
        SaveSlotInfo broken = repository.Inspect(a);
        Check(!broken.IsValid && broken.BackupIsValid, "A corrupt primary save leaves its readable backup available");
        Check(repository.ListSlots().Count == 3 && repository.Inspect(b).IsValid,
            "A corrupt slot cannot hide or invalidate the other parks");
        byte[] beforeA = File.ReadAllBytes(pathA), beforeB = File.ReadAllBytes(SaveSlotPaths.Resolve(directory, b));
        SaveSlotInfo first = repository.WriteAutosave(Data("Automatic park", 300), 2);
        File.SetLastWriteTimeUtc(SaveSlotPaths.Resolve(directory, first.Id), DateTime.UtcNow.AddMinutes(-2));
        SaveSlotInfo second = repository.WriteAutosave(Data("Automatic park", 400), 2);
        File.SetLastWriteTimeUtc(SaveSlotPaths.Resolve(directory, second.Id), DateTime.UtcNow.AddMinutes(-1));
        Check(first.IsAutosave && second.IsAutosave && first.Id != second.Id, "Autosaves first fill their separate rotation slots");
        SaveSlotInfo third = repository.WriteAutosave(Data("Automatic park", 500), 2);
        Check(third.Id == first.Id && repository.Read(third.Id).balance == 500 && repository.Read(third.Id, true).balance == 300,
            "Autosave rotation replaces the oldest automatic slot and preserves its previous backup");
        Check(repository.ListSlots().Count(slot => slot.IsAutosave) == 2, "Configured rotation does not grow a new file every interval");
        Check(File.ReadAllBytes(pathA).SequenceEqual(beforeA)
            && File.ReadAllBytes(SaveSlotPaths.Resolve(directory, b)).SequenceEqual(beforeB)
            && repository.Read(SaveSlotPaths.Legacy).balance == 75, "Autosaves never overwrite named manual slots or the previous legacy save");

        bool rejected = false;
        try { repository.WriteManual(Data("Invalid", 1), "auto-00"); } catch (ArgumentException) { rejected = true; }
        Check(rejected && repository.Read("auto-00").balance == 500, "Manual writes cannot claim reserved automatic slot IDs");
        rejected = false;
        try { repository.Read("../park-save"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A supplied slot identifier cannot traverse outside the save directory");
        File.WriteAllText(Path.Combine(directory, "Saves", "unrelated.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "Saves", "manual-not-an-id.json"), "{}");
        Check(repository.ListSlots().Count == 5, "Unrelated JSON files are ignored by the slot browser");
        File.Delete(pathA);
        SaveSlotInfo backupOnly = repository.Inspect(a);
        Check(!backupOnly.IsValid && backupOnly.BackupIsValid && repository.ListSlots().Any(slot => slot.Id == a),
            "A backup remains recoverable even when its primary save is missing");
        Check(repository.Read(a, true).balance == 100, "Recovering a backup reads its own retained park state");
        repository.WriteAutosave(Data("Automatic park", 600), 1);
        Check(repository.ListSlots().Count(slot => slot.IsAutosave) == 1
            && repository.Read(b).balance == 200 && repository.Read(a, true).balance == 100,
            "Reducing retention removes only obsolete autosaves after a new successful write");
        return checks;
    }
}
