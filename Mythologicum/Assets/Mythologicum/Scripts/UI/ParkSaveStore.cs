using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Mythologicum
{
    [Serializable] public sealed class ParkSaveData
    {
        public int version = 3, width, depth, departedVisitors, lostVisitors;
        public SavedTerrain terrain;
        public List<SavedTreeVisual> treeVisuals;
        public MapGenerationOptions generation;
        public List<Vector2Int> rocks;
        public ProgressionSave progression;
        public FinanceSnapshot finance;
        public ParkReputationSnapshot reputation;
        public int rejectedAdmissions, expensiveServiceRefusals;
        public float cellSize, balance, income, expenses;
        public bool unlimited;
        public string savedAt;
        public string parkName;
        public Vector3 focus;
        public float yaw, zoom;
        public List<Vector2Int> paths = new List<Vector2Int>(), trees = new List<Vector2Int>(), graves = new List<Vector2Int>();
        public List<Vector2Int> scarecrows = new List<Vector2Int>();
        public List<SavedBuilding> buildings = new List<SavedBuilding>();
        public List<SavedFence> fences = new List<SavedFence>();
        public List<SavedActor> creatures = new List<SavedActor>(), visitors = new List<SavedActor>(), staff = new List<SavedActor>();
        public List<SavedResearch> research = new List<SavedResearch>();
    }
    [Serializable] public sealed class SavedBuilding
    {
        public BuildingKind kind;
        public Vector2Int cell;
        public int rotation, width, depth, door, stock;
        public float height, price, revenue, expenses;
        public string research;
    }
    [Serializable] public sealed class SavedFence { public Vector2Int cell; public bool alongX, gate; public FenceKind kind; public float health; }
    [Serializable] public sealed class SavedResearch { public string species; public float progress; }
    [Serializable] public sealed class SavedActor
    {
        public string species;
        public Vector2Int cell;
        public float hunger, fear, energy, health, happiness, agitation, sedation, age, satisfaction, patience, explored;
        public float escapeCooldown, fenceRetryCooldown, scareCooldown;
        public int building = -1;
        public bool retire, sedate, leaving;
    }
    public static class ParkSaveStore
    {
        public static ParkSaveData Pending;
        internal static string LoadError;
        public static string ActiveParkName { get; private set; } = Loc.Text("ui.save.untitled_park");
        private static ParkSaveRepository Repository => new ParkSaveRepository(Application.persistentDataPath);
        public static string SavePath => Path.Combine(Application.persistentDataPath, "park-save.json");
        public static bool Exists => Repository.HasSaves;
        public static IReadOnlyList<SaveSlotInfo> ListSlots() => Repository.ListSlots();
        public static string NewManualSlotId() => "manual-" + Guid.NewGuid().ToString("N");
        public static void BeginNewPark() => ActiveParkName = Loc.Text("ui.save.untitled_park");
        public static void WriteSlot(ParkSaveData data, string id, string name)
        {
            data.parkName = CleanName(name);
            Repository.WriteManual(data, id);
            ActiveParkName = data.parkName;
        }
        public static ParkSaveData ReadSlot(string id, bool backup = false)
            => Repository.Read(id, backup);
        public static void AdoptParkName(ParkSaveData data) => ActiveParkName = CleanName(data?.parkName);
        public static SaveSlotInfo WriteAutosave(ParkSaveData data, int count)
        {
            data.parkName = CleanName(ActiveParkName);
            return Repository.WriteAutosave(data, count);
        }
        public static string CleanName(string name)
        {
            string value = (name ?? "").Trim();
            if (value.Length == 0) return Loc.Text("ui.save.untitled_park");
            value = value.Replace('\r', ' ').Replace('\n', ' ');
            return value.Length > 64 ? value.Substring(0, 64) : value;
        }
        public static void Write(ParkSaveData data)
        {
            data.parkName = CleanName(string.IsNullOrWhiteSpace(data.parkName) ? ActiveParkName : data.parkName);
            WriteFile(data, SavePath);
        }
        internal static void WriteFile(ParkSaveData data, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            // Replace atomically, retaining the previous successful save as a recovery copy.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        public static ParkSaveData Read()
        {
            ParkSaveData data = ReadFile(SavePath);
            return data;
        }
        internal static ParkSaveData ReadFile(string path)
        {
            var data = JsonUtility.FromJson<ParkSaveData>(File.ReadAllText(path));
            if (data == null || data.version<1 || data.version>3 || data.width < 1 || data.depth < 1 || data.width>128||data.depth>128
                ||float.IsNaN(data.cellSize)||float.IsInfinity(data.cellSize)||data.cellSize<.25f||data.cellSize>16f
                || data.buildings == null || data.paths == null || data.fences == null || data.creatures == null
                || data.trees == null || data.graves == null || data.staff == null || data.visitors == null || data.research == null
                || float.IsNaN(data.balance) || float.IsInfinity(data.balance) || float.IsNaN(data.income) || float.IsInfinity(data.income)
                || float.IsNaN(data.expenses) || float.IsInfinity(data.expenses))
                throw new InvalidDataException(Loc.Text("ui.save.invalid_save"));
            if(data.version>=2 && data.progression==null)throw new InvalidDataException(Loc.Text("ui.save.missing_progression"));
            if(data.version>=3 && data.terrain==null)throw new InvalidDataException(Loc.Text("ui.save.missing_terrain"));
            // Additive enrichment data was absent in older version-three saves.
            data.scarecrows ??= new List<Vector2Int>();
            return data;
        }
    }

    public sealed class SaveSlotInfo
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public DateTime SavedUtc { get; internal set; }
        public bool IsAutosave { get; internal set; }
        public bool IsLegacy => Id == SaveSlotPaths.Legacy;
        public bool IsValid { get; internal set; }
        public bool HasBackup { get; internal set; }
        public bool BackupIsValid { get; internal set; }
        public string Error { get; internal set; }
        public float Funds { get; internal set; }
        public int Width { get; internal set; }
        public int Depth { get; internal set; }
    }

    /// <summary>Opaque slot IDs are separate from player-facing names; names never become paths.</summary>
    public static class SaveSlotPaths
    {
        public const string Legacy = "legacy";
        public static bool IsManual(string id)
        {
            if (id == null || !id.StartsWith("manual-", StringComparison.Ordinal) || id.Length != 39) return false;
            for (int i = 7; i < id.Length; i++)
                if (!((id[i] >= '0' && id[i] <= '9') || (id[i] >= 'a' && id[i] <= 'f'))) return false;
            return true;
        }
        public static bool IsAutomatic(string id)
        {
            if (id == null || !id.StartsWith("auto-", StringComparison.Ordinal) || id.Length != 7) return false;
            return id[5] >= '0' && id[5] <= '9' && id[6] >= '0' && id[6] <= '9';
        }
        public static string Resolve(string directory, string id)
        {
            if (id == Legacy) return Path.Combine(Path.GetFullPath(directory), "park-save.json");
            if (!IsManual(id) && !IsAutomatic(id)) throw new ArgumentException(Loc.Text("ui.save.unknown_slot"));
            return Path.Combine(Path.GetFullPath(directory), "Saves", id + ".json");
        }
    }

    /// <summary>File storage also accepts an isolated directory for native integration checks.</summary>
    public sealed class ParkSaveRepository
    {
        private readonly string directory;
        public ParkSaveRepository(string directory) => this.directory = Path.GetFullPath(directory);
        private string SlotsDirectory => Path.Combine(directory, "Saves");
        public bool HasSaves
        {
            get
            {
                if (File.Exists(SaveSlotPaths.Resolve(directory, SaveSlotPaths.Legacy))
                    || File.Exists(SaveSlotPaths.Resolve(directory, SaveSlotPaths.Legacy) + ".bak")) return true;
                if (!Directory.Exists(SlotsDirectory)) return false;
                foreach (string file in Directory.GetFiles(SlotsDirectory, "*.json*"))
                    if (IdFromFile(file) != null) return true;
                return false;
            }
        }
        public IReadOnlyList<SaveSlotInfo> ListSlots()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            string legacy = SaveSlotPaths.Resolve(directory, SaveSlotPaths.Legacy);
            if (File.Exists(legacy) || File.Exists(legacy + ".bak")) ids.Add(SaveSlotPaths.Legacy);
            if (Directory.Exists(SlotsDirectory))
                foreach (string file in Directory.GetFiles(SlotsDirectory, "*.json*"))
                {
                    string id = IdFromFile(file);
                    if (id != null) ids.Add(id);
                }
            var result = new List<SaveSlotInfo>();
            foreach (string id in ids) result.Add(Inspect(id));
            result.Sort((a, b) => b.SavedUtc.CompareTo(a.SavedUtc));
            return result;
        }
        private static string IdFromFile(string path)
        {
            string name = Path.GetFileName(path);
            if (name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return null;
            string id = name.Substring(0, name.Length - 5);
            return SaveSlotPaths.IsManual(id) || SaveSlotPaths.IsAutomatic(id) ? id : null;
        }
        public SaveSlotInfo Inspect(string id)
        {
            string path = SaveSlotPaths.Resolve(directory, id);
            var result = new SaveSlotInfo { Id = id, Name = id == SaveSlotPaths.Legacy ? Loc.Text("ui.save.previous_park_save") : Loc.Text("ui.save.unreadable_save"),
                IsAutosave = SaveSlotPaths.IsAutomatic(id), HasBackup = File.Exists(path + ".bak"),
                SavedUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path + ".bak") };
            try
            {
                ParkSaveData data = ParkSaveStore.ReadFile(path);
                result.Name = ParkSaveStore.CleanName(data.parkName);
                result.Width = data.width; result.Depth = data.depth; result.Funds = data.balance;
                result.IsValid = true;
                if (DateTime.TryParse(data.savedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime saved))
                    result.SavedUtc = saved.ToUniversalTime();
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            { result.Error = Loc.Format("ui.save.cannot_read", error.Message); }
            if (result.HasBackup)
            {
                try { ParkSaveStore.ReadFile(path + ".bak"); result.BackupIsValid = true; }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { }
            }
            return result;
        }
        public ParkSaveData Read(string id, bool backup = false)
            => ParkSaveStore.ReadFile(SaveSlotPaths.Resolve(directory, id) + (backup ? ".bak" : ""));
        public void WriteManual(ParkSaveData data, string id)
        {
            if (id != SaveSlotPaths.Legacy && !SaveSlotPaths.IsManual(id)) throw new ArgumentException(Loc.Text("ui.save.choose_manual_slot"));
            ParkSaveStore.WriteFile(data, SaveSlotPaths.Resolve(directory, id));
        }
        public SaveSlotInfo WriteAutosave(ParkSaveData data, int count)
        {
            count = Math.Max(1, Math.Min(99, count));
            string chosen = null;
            DateTime oldest = DateTime.MaxValue;
            for (int index = 0; index < count; index++)
            {
                string id = "auto-" + index.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                string path = SaveSlotPaths.Resolve(directory, id);
                if (!File.Exists(path)) { chosen = id; break; }
                DateTime written = File.GetLastWriteTimeUtc(path);
                if (written < oldest) { oldest = written; chosen = id; }
            }
            ParkSaveStore.WriteFile(data, SaveSlotPaths.Resolve(directory, chosen));
            // Retention changes affect only reserved automatic files, after a successful new write.
            foreach (string file in Directory.GetFiles(SlotsDirectory, "auto-*.json*"))
            {
                string id = IdFromFile(file);
                if (id == null || !SaveSlotPaths.IsAutomatic(id)) continue;
                int index = (id[5] - '0') * 10 + id[6] - '0';
                if (index >= count) File.Delete(file);
            }
            return Inspect(chosen);
        }
    }
}
