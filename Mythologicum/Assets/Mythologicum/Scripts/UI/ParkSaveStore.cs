using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Mythologicum
{
    [Serializable] public sealed class ParkSaveData
    {
        public int version = 1, width, depth, departedVisitors, lostVisitors;
        public float cellSize, balance, income, expenses;
        public bool unlimited;
        public string savedAt;
        public Vector3 focus;
        public float yaw, zoom;
        public List<Vector2Int> paths = new List<Vector2Int>(), trees = new List<Vector2Int>(), graves = new List<Vector2Int>();
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
        public int building = -1;
        public bool retire, sedate, leaving;
    }
    public static class ParkSaveStore
    {
        public static ParkSaveData Pending;
        internal static string LoadError;
        public static string SavePath => Path.Combine(Application.persistentDataPath, "park-save.json");
        public static bool Exists => File.Exists(SavePath);
        public static void Write(ParkSaveData data)
            => WriteFile(data, SavePath);
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
            => ReadFile(SavePath);
        internal static ParkSaveData ReadFile(string path)
        {
            var data = JsonUtility.FromJson<ParkSaveData>(File.ReadAllText(path));
            if (data == null || data.version != 1 || data.width < 1 || data.depth < 1 || data.cellSize <= 0
                || data.buildings == null || data.paths == null || data.fences == null || data.creatures == null
                || data.trees == null || data.graves == null || data.staff == null || data.visitors == null || data.research == null)
                throw new InvalidDataException("This save is invalid or uses an unsupported version.");
            return data;
        }
    }
}
