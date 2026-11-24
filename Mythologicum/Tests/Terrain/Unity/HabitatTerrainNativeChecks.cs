using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Called by a prepared Play-mode integration fixture in the disposable editor.</summary>
public static class HabitatTerrainNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception("Habitat terrain: " + message); checks++; }
        Check(Application.isPlaying, "native terrain mesh lifetime uses normal Play mode");
        var art = Resources.Load<TerrainArtDefinition>("Terrain/TerrainArt");
        Check(art != null && art.grass != null, "organized terrain art still supplies the ground textures");
        var field = new GridHeightField(24, 24, 2);
        var grid = new SquareBuildGrid(new Vector3(60000, 0, 60000), 2, 24, 24, field);
        var parent = new GameObject("Habitat terrain validation");
        ParkTerrainSurface ground = null; HabitatTerrainSurface habitats = null; TerrainEditingPresentation ui = null;
        try
        {
            ground = new ParkTerrainSurface(grid, art.grass, art.cliff, art.soil);
            habitats = new HabitatTerrainSurface(grid, art, parent.transform);
            var originalColliderMesh = ground.Root.GetComponentInChildren<MeshCollider>().sharedMesh;
            int column = 2;
            foreach (HabitatBiome biome in Enum.GetValues(typeof(HabitatBiome)))
            {
                Check(grid.Habitat.PaintBiome(new Vector2(column + .5f, 3.5f), 1.1f, biome, cell => true, out _), "the real painter changes " + biome);
                habitats.Refresh();
                var painted = habitats.Root.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.name.StartsWith("Painted " + biome + " [")).ToArray();
                Check(painted.Length > 0 && painted.All(renderer => renderer.sharedMaterial != null && renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                    "painted " + biome + " has its own visible material and ground-conforming mesh");
                column += 4;
            }
            Check(ReferenceEquals(originalColliderMesh, ground.Root.GetComponentInChildren<MeshCollider>().sharedMesh), "biome painting does not rebuild or recook the physical terrain collider");
            grid.Habitat.PaintBiome(new Vector2(2.5f, 19.5f), .4f, HabitatBiome.Grass, cell => true, out _);
            grid.Habitat.PaintBiome(new Vector2(20.5f, 19.5f), .4f, HabitatBiome.Sand, cell => true, out _);
            habitats.Refresh();
            Check(habitats.Root.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.name == "Painted Grass [0, 16]")
                && habitats.Root.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.name == "Painted Sand [16, 16]"),
                "batched habitat edits refresh every affected chunk rather than losing an earlier edit");
            Check(habitats.Root.GetComponentsInChildren<Collider>(true).Length == 0, "habitat overlays never become placement or navigation obstacles");
            Check(grid.Habitat.AddWater(new Vector2(10.5f, 11.5f), 3, 0, 1.2f, cell => true, out _), "the player water brush carves a pond using the actual terrain sampler");
            habitats.Refresh();
            var water = habitats.Root.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.name.StartsWith("Pond water [")).ToArray();
            Check(water.Length > 0 && water.All(renderer => renderer.sharedMaterial.renderQueue >= 3000), "pond surfaces use transparent water art");
            foreach (var renderer in water)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.vertexCount > 0 && mesh.triangles.Length > 0, "a pond creates actual clipped shoreline geometry");
                Check(mesh.vertices.All(vertex => Mathf.Abs(renderer.transform.TransformPoint(vertex).y - .018f) < .002f), "the water surface remains level across the excavated bed");
                Check(mesh.vertices.All(vertex => grid.GroundHeight(renderer.transform.TransformPoint(vertex)) <= .08f), "water geometry is clipped against dry banks instead of covering raised ground");
                Check(mesh.normals.All(normal => normal.y > .99f), "water triangles face upward");
            }
            var beforeHeight = grid.CellCenter(new Vector2Int(10, 11)).y;
            Check(beforeHeight < -.5f && !grid.GroundWalkable(new Vector2Int(10, 11)), "the water visual and land traversal share the same real pond bed");
            Check(grid.Habitat.RemoveWater(new Vector2(10.5f, 11.5f), 4, null, out _), "the drain tool removes the surface");
            habitats.Refresh();
            Check(!habitats.Root.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.name.StartsWith("Pond water [")), "drained pond surfaces are no longer rendered");
            Check(Mathf.Abs(grid.CellCenter(new Vector2Int(10, 11)).y - beforeHeight) < .001f, "draining does not secretly refill the basin with soil");

            var canvas = SceneTransition.GameplayCanvas;
            Check(canvas != null, "the prepared game supplies a real UI canvas");
            TerrainBrushMode selected = TerrainBrushMode.Raise;
            ui = new TerrainEditingPresentation(canvas, mode => selected = mode, () => { }); ui.SetActive(true); ui.SetMode(TerrainBrushMode.Water);
            Canvas.ForceUpdateCanvases();
            var uiRoot = (GameObject)typeof(TerrainEditingPresentation).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
            var buttons = uiRoot.GetComponentsInChildren<Button>();
            foreach (TerrainBrushMode mode in Enum.GetValues(typeof(TerrainBrushMode)))
            {
                var button = buttons.Single(item => item.GetComponentInChildren<Text>().text == Loc.Enum(mode));
                button.onClick.Invoke(); Check(selected == mode, "the real terrain toolbar exposes " + mode);
            }
            float depth = ui.WaterDepth;
            buttons.Single(item => item.GetComponentInChildren<Text>().text == Loc.Text("habitat.terrain.depth_more")).onClick.Invoke();
            Check(ui.WaterDepth > depth, "pond depth is adjustable through the actual terrain controls");
            var frame = uiRoot.GetComponent<UIFramedPanel>(); frame.Refresh(); Canvas.ForceUpdateCanvases();
            var safe = frame.Content; var corners = new Vector3[4];
            foreach (var button in buttons)
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Check(corners.All(point => safe.rect.Contains(safe.InverseTransformPoint(point))), "every terrain button stays inside the unstretched illustrated frame");
            }
        }
        finally
        {
            ui?.Dispose(); habitats?.Dispose(); ground?.Dispose(); parent.SetActive(false); Object.Destroy(parent);
        }
        return checks;
    }
}
