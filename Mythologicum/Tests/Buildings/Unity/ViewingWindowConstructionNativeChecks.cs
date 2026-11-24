using System;
using System.Linq;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Call from a prepared Play-mode fixture; generated meshes use normal delayed destruction.</summary>
public static class ViewingWindowConstructionNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Viewing window construction: " + reason); checks++; }
        Check(Application.isPlaying, "the fixture runs through normal Play-mode construction lifetime");
        Check(Resources.Load<ViewingWindowDefinition>("Attractions/ViewingWindow") != null, "authored window settings resolve in the organized resource folder");
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        int index = 0;
        try
        {
            foreach (bool sloped in new[] { false, true })
            foreach (FenceKind kind in Enum.GetValues(typeof(FenceKind)))
            {
                var terrain = sloped ? new GridHeightField(8, 8, 2) : null;
                var grid = new SquareBuildGrid(new Vector3(22000 + index++ * 30, 0, 22000), 2, 8, 8, terrain);
                var root = new GameObject("Window construction fixture");
                var construction = new FenceConstruction(grid, root.transform, material);
                try
                {
                    var edge = new FenceEdge(new Vector2Int(3, 3), true);
                    Check(construction.TryPlace(edge, kind), "a base fence can be built for " + kind);
                    float health = construction.Layout.HealthAt(edge);
                    Check(construction.TryPlaceWindow(edge, 5) && construction.Layout.HasWindow(edge), "the window construction tool installs glass on " + kind);
                    Check(!construction.CanPlaceGate(edge) && !construction.CanPlaceWindow(edge), "gate and duplicate-window tools reject upgraded glass");
                    var segment = root.GetComponentsInChildren<FenceSegment>().Single(item => item.Edge.Equals(edge));
                    Check(segment.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.gameObject.name == "Viewing glass"), "the glass has a distinct rendered visual");
                    Check(segment.transform.Find("Closed barrier") != null && construction.Layout.BlocksPath(edge)
                        && construction.Layout.HealthAt(edge) == health, "rendering a window preserves the solid barrier and live health");
                    if (sloped)
                    {
                        var visual = segment.GetComponentInChildren<GroundConformingVisual>();
                        Check(visual != null && visual.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "a framed window uses the terrain-following mesh pipeline");
                        var pane = visual.transform.Find("Viewing glass");
                        var before = pane.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => pane.TransformPoint(p).y).ToArray();
                        Check(terrain.TryEdit(grid, new Vector2(3, 3), 2f, .2f, TerrainBrushMode.Raise, 0, out _), "window fixture ground can be raised");
                        construction.RefreshTerrain();
                        var after = pane.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => pane.TransformPoint(p).y).ToArray();
                        Check(before.Zip(after, (a, b) => Mathf.Abs(a - b)).Any(delta => delta > .001f), "the glass refits after a real terrain edit");
                    }
                }
                finally { root.SetActive(false); Object.Destroy(root); construction.Dispose(); }
            }

            var portalGrid = new SquareBuildGrid(new Vector3(23000, 0, 23000), 2, 8, 8);
            var portalRoot = new GameObject("Petting entrance visual fixture");
            var portalConstruction = new FenceConstruction(portalGrid, portalRoot.transform, material);
            try
            {
                var network = new ParkBuildingNetwork(portalGrid, portalConstruction.Layout);
                network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 1), 0));
                var host = network.TryPlace(new BuildingFootprint(AttractionDefinition.Building(BuildingKind.PettingZoo), new Vector2Int(3, 3), 0));
                Check(host != null && portalConstruction.TryPlace(host.Footprint.RearEdge), "a petting host can border a real fence");
                portalConstruction.ConfigurePettingEntrances(() => network.Records); portalConstruction.Refresh();
                Check(!portalConstruction.Layout.IsGate(host.Footprint.RearEdge) && portalConstruction.Layout.BlocksPath(host.Footprint.RearEdge), "a petting entrance's gate appearance never opens an animal route");
                Check(!portalConstruction.CanPlaceGate(host.Footprint.RearEdge) && !portalConstruction.CanPlaceWindow(host.Footprint.RearEdge), "the dedicated petting portal cannot be replaced by another gate or window");
                Check(portalRoot.GetComponentsInChildren<FenceSegment>().Single().name.Contains("gate"), "the petting entrance visibly uses a gate instead of an ordinary fence");
                var formerRear = host.Footprint.RearEdge; network.Remove(host); portalConstruction.Refresh();
                Check(portalConstruction.Layout.HasFence(formerRear) && portalConstruction.CanPlaceWindow(formerRear)
                    && portalRoot.GetComponentsInChildren<FenceSegment>().Single().name.Contains("fence"), "demolishing the host restores the original closed-fence appearance");
            }
            finally { portalRoot.SetActive(false); Object.Destroy(portalRoot); portalConstruction.Dispose(); }
        }
        finally { Object.Destroy(material); }
        return checks;
    }
}
