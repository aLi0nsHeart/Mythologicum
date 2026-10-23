using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Mythologicum
{
    public sealed class EnclosurePresentation
    {
        private readonly EnclosureMap map;
        private readonly SquareBuildGrid grid;
        private readonly GameObject root, panel, overlay;
        private readonly Text information, warning;
        private readonly Material material;
        private Mesh mesh;
        private Vector2Int? selectedCell;
        private EnclosureRecord selected;
        private int lastRevision = -1;
        private bool inspecting = true;

        public EnclosurePresentation(EnclosureMap map, SquareBuildGrid grid, Canvas canvas, Font font, Material groundMaterial)
        {
            this.map = map;
            this.grid = grid;
            root = new GameObject("Enclosure inspection", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel = new GameObject("Enclosure information", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(15f, -170f);
            panelRect.sizeDelta = new Vector2(340f, 270f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.09f, 0.95f);
            information = Label(panel.transform, font, 20);
            information.alignment = TextAnchor.UpperLeft;
            information.rectTransform.anchorMin = Vector2.zero; information.rectTransform.anchorMax = Vector2.one;
            information.rectTransform.offsetMin = new Vector2(15f, 12f);
            information.rectTransform.offsetMax = new Vector2(-15f, -12f);
            warning = Label(root.transform, font, 21);
            warning.color = new Color(1f, 0.35f, 0.22f);
            warning.rectTransform.anchorMin = warning.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            warning.rectTransform.anchoredPosition = new Vector2(0f, -135f);
            warning.rectTransform.sizeDelta = new Vector2(1000f, 42f);
            material = new Material(groundMaterial);
            overlay = new GameObject("Selected enclosure area", typeof(MeshFilter), typeof(MeshRenderer));
            overlay.GetComponent<MeshRenderer>().sharedMaterial = material;
            overlay.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            panel.SetActive(false);
            overlay.SetActive(false);
        }

        private static Text Label(Transform parent, Font font, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font; text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.94f, 0.86f, 0.66f);
            text.raycastTarget = false;
            return text;
        }

        public void SetInspecting(bool value)
        {
            inspecting = value;
            if (panel != null) panel.SetActive(value && selected != null);
            if (overlay != null) overlay.SetActive(value && selected != null);
        }

        public void Select(Vector2Int? cell) { selectedCell = cell; lastRevision = -1; }

        public void Refresh()
        {
            int unsecured = 0;
            foreach (EnclosureRecord record in map.Records) if (!record.IsSecured) unsecured++;
            warning.gameObject.SetActive(unsecured > 0);
            warning.text = $"Warning: {unsecured} enclosure(s) unsecured. Repair the fence gaps.";
            EnclosureRecord current = selectedCell.HasValue ? map.At(selectedCell.Value) : null;
            if (current != selected || map.Revision != lastRevision)
            {
                selected = current;
                lastRevision = map.Revision;
                RebuildHighlight();
            }
            SetInspecting(inspecting);
            if (selected == null) return;
            EnvironmentStats stats = selected.Environment;
            information.text = $"Enclosure #{selected.Id}\n"
                + (selected.IsSecured ? "Secured perimeter\n" : "UNSECURED: fence gap\n")
                + $"Area: {stats.Area:0.#} m² ({stats.Tiles} tiles)\n"
                + $"Forest coverage: {stats.ForestPercent:0.#}%\n"
                + $"Gates: {selected.GateCount}\n"
                + (selected.HasStaffAccess ? "Staff access: connected\n" : "Staff access: no park path\n")
                + "\nClick elsewhere to change selection.";
        }

        private void RebuildHighlight()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (selected == null) return;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector2Int cell in selected.Cells)
            {
                Vector3 center = grid.CellCenter(cell) + Vector3.up * 0.115f;
                float half = grid.CellSize * 0.48f;
                int i = vertices.Count;
                vertices.Add(center + new Vector3(-half, 0f, -half));
                vertices.Add(center + new Vector3(-half, 0f, half));
                vertices.Add(center + new Vector3(half, 0f, half));
                vertices.Add(center + new Vector3(half, 0f, -half));
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            mesh = new Mesh { name = "Enclosure area", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            overlay.GetComponent<MeshFilter>().sharedMesh = mesh;
            material.color = selected.IsSecured ? new Color(0.25f, 0.55f, 0.35f) : new Color(0.7f, 0.18f, 0.12f);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (overlay != null) Object.Destroy(overlay);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
