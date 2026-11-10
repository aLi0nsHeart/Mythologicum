using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
namespace Mythologicum
{
    public enum ParkOverlayMode { Off, PathConnections, EnclosureSpace, StaffAccess }

    /// <summary>Diagnostic views use the same network and gate navigation as the simulation.</summary>
    public sealed class ParkOverlayPresentation : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly ParkBuildingNetwork buildings;
        private readonly EnclosureMap enclosures;
        private readonly StaffNavigation navigation;
        private readonly ParkOperationsDefinition settings;
        private readonly GameObject root, legend;
        private readonly Text caption;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private int gridRevision = -1, fenceRevision = -1, enclosureRevision = -1, employeeSignature;
        private float nextRefresh;
        public ParkOverlayMode Mode { get; private set; }
        public int VisibleCellCount { get; private set; }

        public ParkOverlayPresentation(Canvas canvas, SquareBuildGrid grid, FenceLayout fences,
            ParkBuildingNetwork buildings, EnclosureMap enclosures, Predicate<Vector2Int> clear)
        {
            this.grid = grid; this.fences = fences; this.buildings = buildings; this.enclosures = enclosures;
            navigation = new StaffNavigation(grid, fences, clear); settings = ParkOperationsDefinition.Current;
            root = new GameObject("Park diagnostic overlay");
            legend = ManagementWidgets.Rect(canvas.transform, "Map overlay legend", new Vector2(0, 0), new Vector2(570, 72)).gameObject;
            var rect = (RectTransform)legend.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0); rect.anchoredPosition = new Vector2(-24, 112);
            caption = ManagementWidgets.Label(legend.transform, "park.park_overlay_presentation.map_overlay_key", Vector2.zero, new Vector2(460, 72), 17);
            ManagementWidgets.Button(legend.transform, "park.park_overlay_presentation.hide_map_overlay", new Vector2(470, 10), new Vector2(96, 36), () => SetMode(ParkOverlayMode.Off), 16)
                .GetComponentInChildren<Text>().text = Loc.Text("park.park_overlay_presentation.hide");
            legend.SetActive(false);
        }

        public void SetMode(ParkOverlayMode mode)
        { Mode = mode; gridRevision = -1; nextRefresh = 0; legend.SetActive(mode != ParkOverlayMode.Off); Refresh(); }

        public void Refresh()
        {
            if (Mode == ParkOverlayMode.Off) { if (root.activeSelf) { Clear(); root.SetActive(false); } return; }
            int employees = 0;
            foreach (var building in buildings.Records)
                employees = unchecked(employees * 397 ^ building.RuntimeId ^ building.HiredCount ^ (building.IsOpen ? 1 : 0));
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + settings.overlayRefreshSeconds;
            if (gridRevision == grid.Revision && fenceRevision == fences.Revision && enclosureRevision == enclosures.Revision && employeeSignature == employees) return;
            gridRevision = grid.Revision; fenceRevision = fences.Revision; enclosureRevision = enclosures.Revision; employeeSignature = employees;
            Clear(); root.SetActive(true); buildings.Refresh(); enclosures.Refresh(buildings.IsPathConnected);
            var good = new HashSet<Vector2Int>(); var bad = new HashSet<Vector2Int>();
            if (Mode == ParkOverlayMode.PathConnections)
            {
                for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (grid.ContentAt(cell) == BuildCellContent.Path) (buildings.IsPathConnected(cell) ? good : bad).Add(cell);
                }
                caption.text = Loc.Text("park.park_overlay_presentation.path_connections_teal_connected_to_the_entrance_red_discon");
            }
            else if (Mode == ParkOverlayMode.EnclosureSpace)
            {
                foreach (var area in enclosures.Records)
                    foreach (var cell in area.Cells) (area.IsSecured ? good : bad).Add(cell);
                bad.ExceptWith(good);
                caption.text = Loc.Format("park.park_overlay_presentation.enclosure_space_blue_secured_area_red_breached_areas", enclosures.Records.Count);
            }
            else
            {
                var reached = new HashSet<Vector2Int>();
                foreach (var building in buildings.Records)
                    if (building.IsOpen && building.HiredCount > 0 && building.Footprint.Definition.IsEmployeeBuilding
                        && !reached.Contains(building.Footprint.AccessCell))
                        reached.UnionWith(navigation.Reachable(building.Footprint.AccessCell));
                for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (navigation.IsWalkable(cell)) (reached.Contains(cell) ? good : bad).Add(cell);
                }
                caption.text = Loc.Text("park.park_overlay_presentation.staff_access_teal_reachable_from_staffed_open_workplaces_r");
            }
            VisibleCellCount = good.Count + bad.Count;
            Draw(good, Mode == ParkOverlayMode.EnclosureSpace ? settings.securedColor : settings.connectedColor);
            Draw(bad, settings.disconnectedColor);
        }

        private void Draw(HashSet<Vector2Int> cells, Color color)
        {
            if (cells.Count == 0) return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent;
            var mesh = GroundMeshBuilder.Cells(grid, cells, settings.overlayGroundOffset);
            var child = new GameObject("Diagnostic cells", typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(root.transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            meshes.Add(mesh); materials.Add(material);
        }
        private void Clear()
        {
            if (root != null)
                foreach (Transform child in root.transform) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            foreach (var mesh in meshes) UnityEngine.Object.Destroy(mesh);
            foreach (var material in materials) UnityEngine.Object.Destroy(material);
            meshes.Clear(); materials.Clear(); VisibleCellCount = 0;
        }
        public void Dispose() { Clear(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(legend); }
    }
}
