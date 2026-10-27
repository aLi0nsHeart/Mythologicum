using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Mythologicum
{
    [RequireComponent(typeof(Camera))]
    public sealed class BuildModeController : MonoBehaviour
    {
        [SerializeField] private Collider ground;
        [SerializeField] private GameObject pathTilePrefab;
        [SerializeField, Min(0.25f)] private float cellSize = 2f;
        [SerializeField, Min(0.25f)] private float buildingHeight = 2f;
        [SerializeField] private BuildingDefinition[] buildingCatalog = BuildingDefinition.Defaults();
        [SerializeField] private VisitorDefinition visitorSettings;
        [SerializeField, Min(0f)] private float startingFunds = 5000f;
        private SquareBuildGrid grid;
        private Camera view;
        private GameObject gridObject;
        private GameObject preview;
        private GameObject buildings;
        private ConstructionPalette palette;
        private BuildingConstruction buildingConstruction;
        private BuildingDefinition selectedBuilding;
        private CreatureDefinition[] creatureCatalog;
        private CreatureDefinition selectedCreature;
        private CreatureWorld creatureWorld;
        private TreeConstruction trees;
        private GroundVegetation groundVegetation;
        private CreatureInspection creatureInspection;
        private VisitorWorld visitorWorld;
        private VisitorPresentation visitorPresentation;
        private ParkEconomy economy;
        private bool ownsVisitorSettings;
        public VisitorWorld Visitors => visitorWorld;
        public ParkEconomy Economy => economy;
        private int buildingRotation;
        private GameObject entrancePreview;
        private Text status;
        private Mesh gridMesh;
        private Material gridMaterial;
        private Material previewMaterial;
        private Material buildingMaterial;
        private Material pathMaterial;
        private Material fenceMaterial;
        private Material entranceMaterial;
        private PathConstruction paths;
        private FenceConstruction fences;
        private FenceKind selectedFenceKind = FenceKind.Wooden;
        private EnclosureMap enclosureMap;
        private EnclosurePresentation enclosurePresentation;
        public EnvironmentGrid Environment { get; private set; }
        public EnclosureMap Enclosures => enclosureMap;
        public FenceLayout Barriers => fences?.Layout;
        private enum Tool { None, Building, Path, Fence, Gate, Demolish, Creature, Tree }
        private Tool tool;
        private bool dragging;
        private readonly ConstructionRouteSelection<Vector2Int> pathSelection = new ConstructionRouteSelection<Vector2Int>();
        private readonly ConstructionRouteSelection<FenceEdge> fenceSelection = new ConstructionRouteSelection<FenceEdge>();
        private readonly List<GameObject> routePreviews = new List<GameObject>();
        private int visiblePreviews;
        private Vector2 previousPointer;

        private void Start()
        {
            if (ground == null)
            {
                Debug.LogError("Build mode requires a ground collider.", this);
                enabled = false;
                return;
            }
            view = GetComponent<Camera>();
            // Add new catalogue entries without replacing the user's existing building settings.
            var expanded = new List<BuildingDefinition>(buildingCatalog);
            foreach (BuildingDefinition addition in BuildingDefinition.Defaults())
            {
                bool found = false;
                foreach (BuildingDefinition existing in expanded) if (existing.kind == addition.kind) { found = true; break; }
                if (!found) expanded.Add(addition);
            }
            buildingCatalog = expanded.ToArray();
            var buildingVisuals = Resources.Load<BuildingVisualCatalog>("Buildings/BuildingVisuals");
            buildingVisuals?.ApplyTo(buildingCatalog);
            creatureCatalog = Resources.LoadAll<CreatureDefinition>("Creatures");
            Array.Sort(creatureCatalog, (a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            Bounds bounds = ground.bounds;
            var origin = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
            grid = new SquareBuildGrid(origin, cellSize,
                Mathf.FloorToInt(bounds.size.x / cellSize), Mathf.FloorToInt(bounds.size.z / cellSize));
            Material groundMaterial = ground.GetComponent<Renderer>().sharedMaterial;
            gridMaterial = new Material(groundMaterial) { color = new Color(0.2f, 0.3f, 0.35f) };
            previewMaterial = new Material(groundMaterial);
            buildingMaterial = new Material(groundMaterial) { color = new Color(0.65f, 0.48f, 0.25f) };
            pathMaterial = new Material(groundMaterial) { color = new Color(0.73f, 0.68f, 0.53f) };
            fenceMaterial = new Material(groundMaterial) { color = new Color(0.32f, 0.21f, 0.12f) };
            entranceMaterial = new Material(groundMaterial) { color = new Color(0.1f, 0.85f, 0.95f) };
            gridMesh = BuildGridVisual.CreateMesh(grid);
            gridObject = new GameObject("Build Grid", typeof(MeshFilter), typeof(MeshRenderer));
            gridObject.transform.position = origin + Vector3.up * 0.1f;
            gridObject.GetComponent<MeshFilter>().sharedMesh = gridMesh;
            gridObject.GetComponent<MeshRenderer>().sharedMaterial = gridMaterial;
            gridObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "Build Preview";
            preview.GetComponent<Collider>().enabled = false;
            preview.GetComponent<MeshRenderer>().sharedMaterial = previewMaterial;
            preview.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            preview.transform.localScale = new Vector3(cellSize * 0.9f, buildingHeight, cellSize * 0.9f);
            buildings = new GameObject("Placed Buildings");
            var pathRoot = new GameObject("Paths");
            pathRoot.transform.SetParent(buildings.transform);
            paths = new PathConstruction(grid, pathRoot.transform, pathMaterial, pathTilePrefab);
            var fenceRoot = new GameObject("Fences");
            fenceRoot.transform.SetParent(buildings.transform);
            fences = new FenceConstruction(grid, fenceRoot.transform, fenceMaterial, entranceMaterial,
                Resources.Load<FenceVisualCatalog>("Fences/FenceVisuals"));
            buildingConstruction = new BuildingConstruction(grid, fences.Layout, buildings.transform, buildingMaterial, entranceMaterial);
            Environment = new EnvironmentGrid(grid);
            creatureWorld = new CreatureWorld(grid, fences.Layout, buildingConstruction.Network, view, buildingMaterial, creatureCatalog, Environment);
            if (visitorSettings == null) visitorSettings = Resources.Load<VisitorDefinition>("Visitors/DefaultVisitors");
            if (visitorSettings == null)
            { visitorSettings = ScriptableObject.CreateInstance<VisitorDefinition>(); ownsVisitorSettings = true; }
            economy = new ParkEconomy(startingFunds);
            visitorWorld = new VisitorWorld(grid, fences.Layout, buildingConstruction.Network, creatureWorld, visitorSettings, economy);
            var treeRoot = new GameObject("Enrichment trees"); treeRoot.transform.SetParent(buildings.transform);
            var vegetation = Resources.Load<VegetationDefinition>("Environment/Vegetation");
            trees = new TreeConstruction(grid, Environment, treeRoot.transform, buildingMaterial, vegetation);
            groundVegetation = new GroundVegetation(grid, Environment, vegetation);
            entrancePreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            entrancePreview.name = "Building Entrance Preview";
            entrancePreview.GetComponent<Collider>().enabled = false;
            entrancePreview.GetComponent<MeshRenderer>().sharedMaterial = entranceMaterial;
            entrancePreview.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            entrancePreview.SetActive(false);
            CreateUI();
            creatureInspection = new CreatureInspection(FindFirstObjectByType<Canvas>(), status.font);
            visitorPresentation = new VisitorPresentation(FindFirstObjectByType<Canvas>(), status.font, visitorWorld, economy);
            enclosureMap = new EnclosureMap(grid, fences.Layout, Environment);
            enclosurePresentation = new EnclosurePresentation(enclosureMap, grid, FindFirstObjectByType<Canvas>(), status.font, groundMaterial);
            SelectTool(Tool.None);
            foreach (BuildingDefinition definition in buildingCatalog)
                if (definition.kind == BuildingKind.ParkEntrance) { SelectBuilding(definition); break; }
        }

        private void CreateUI()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            Button template = canvas.GetComponentInChildren<Button>();
            palette = new ConstructionPalette(canvas, template, buildingCatalog, SelectBuilding,
                () => SelectTool(Tool.Path), SelectFence,
                () => SelectTool(Tool.Gate),
                () => SelectTool(tool == Tool.Demolish ? Tool.None : Tool.Demolish), () => SelectTool(Tool.None),
                creatureCatalog, SelectCreature, () => SelectTool(Tool.Tree));
            status = palette.Status;
            var creditsObject = new GameObject("Vegetation art credit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            creditsObject.transform.SetParent(palette.Root.transform, false);
            var credits = creditsObject.GetComponent<Text>();
            credits.text = "Vegetation art: LUX ART STUDIOS";
            credits.font = status.font;
            credits.fontSize = 16;
            credits.color = new Color(0.94f, 0.94f, 0.88f);
            credits.alignment = TextAnchor.UpperRight;
            credits.raycastTarget = false;
            credits.rectTransform.anchorMin = credits.rectTransform.anchorMax = new Vector2(1f, 1f);
            credits.rectTransform.pivot = new Vector2(1f, 1f);
            credits.rectTransform.anchoredPosition = new Vector2(-18f, -65f);
            credits.rectTransform.sizeDelta = new Vector2(330f, 24f);
            foreach (Text label in canvas.GetComponentsInChildren<Text>())
                if (label.text.Contains("WASD"))
                {
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    label.rectTransform.anchoredPosition = new Vector2(0f, -90f);
                }
        }

        private void SelectBuilding(BuildingDefinition definition)
        {
            if ((definition.kind == BuildingKind.ParkEntrance) == buildingConstruction.Network.HasEntrance) return;
            selectedBuilding = definition;
            buildingRotation = 0;
            SelectTool(Tool.Building);
        }

        private void SelectCreature(CreatureDefinition definition)
        {
            selectedCreature = definition;
            SelectTool(Tool.Creature);
        }

        private void SelectFence(FenceKind kind)
        {
            selectedFenceKind = kind;
            SelectTool(Tool.Fence);
        }

        private void SelectTool(Tool selected)
        {
            if ((selected == Tool.Path || selected == Tool.Fence || selected == Tool.Gate || selected == Tool.Creature || selected == Tool.Tree)
                && !buildingConstruction.Network.HasEntrance) return;
            tool = selected;
            palette?.Close();
            ResetStroke();
            pathSelection.Reset();
            fenceSelection.Reset();
            HideRoutePreviews();
            if (gridObject != null) gridObject.SetActive(tool != Tool.None && tool != Tool.Creature);
            if (preview != null) preview.SetActive(false);
            if (entrancePreview != null) entrancePreview.SetActive(false);
            enclosurePresentation?.SetInspecting(tool == Tool.None);
            creatureInspection?.SetInspecting(tool == Tool.None);
            visitorPresentation?.SetInspecting(tool == Tool.None);
        }

        private void ResetStroke()
        {
            dragging = false;
        }

        // After camera movement, so the preview follows the final camera pose.
        private void LateUpdate()
        {
            groundVegetation?.Refresh();
            buildingConstruction.Refresh(view);
            creatureWorld.Tick(Time.deltaTime);
            visitorWorld.Tick(Time.deltaTime);
            float operatingCost = 0f;
            foreach (ParkBuildingRecord building in buildingConstruction.Network.Records)
                operatingCost += Mathf.Max(0f, building.Footprint.Definition.operatingCostPerSecond);
            economy.Tick(Time.deltaTime, operatingCost);
            creatureInspection.Refresh();
            visitorPresentation.Refresh();
            palette.Refresh(buildingConstruction.Network.HasEntrance);
            enclosureMap.Refresh(buildingConstruction.Network.IsPathConnected);
            enclosurePresentation.Refresh();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { SelectTool(Tool.None); return; }
                if (keyboard.bKey.wasPressedThisFrame) palette.Toggle(ConstructionCategory.Buildings);
                if (keyboard.pKey.wasPressedThisFrame) palette.Toggle(ConstructionCategory.Paths);
                if (keyboard.fKey.wasPressedThisFrame) palette.Toggle(ConstructionCategory.Fences);
                if (keyboard.cKey.wasPressedThisFrame) palette.Toggle(ConstructionCategory.Creatures);
                if (keyboard.hKey.wasPressedThisFrame) palette.Toggle(ConstructionCategory.Enclosures);
                if (keyboard.xKey.wasPressedThisFrame) SelectTool(tool == Tool.Demolish ? Tool.None : Tool.Demolish);
                if (keyboard.gKey.wasPressedThisFrame) SelectTool(tool == Tool.Gate ? Tool.None : Tool.Gate);
                if (tool == Tool.Building && keyboard.rKey.wasPressedThisFrame) buildingRotation = (buildingRotation + 1) % 4;
            }
            if (tool == Tool.None)
            {
                status.text = buildingConstruction.Network.HasEntrance ? "Choose a construction category below."
                    : "Place the park entrance first: Buildings > Park entrance.";
                InspectEnclosure();
                return;
            }
            preview.SetActive(false);
            entrancePreview.SetActive(false);
            HideRoutePreviews();
            status.text = tool == Tool.Building
                ? "Click: place building | R: rotate 90 degrees | Cyan tile: path entrance | Esc: cancel"
                : tool == Tool.Creature ? "Click: place creature anywhere on clear ground | Esc: cancel"
                : tool == Tool.Tree ? "Click: plant tree | X tool: remove trees | Esc: finish"
                : tool == Tool.Path
                    ? "Startpunkt anklicken | Esc: Abbrechen"
                    : tool == Tool.Fence
                        ? "Startkante anklicken | Esc: Abbrechen"
                        : tool == Tool.Gate ? "Click an existing wooden fence segment to replace it with a staff gate. | Esc: cancel"
                        : "Linksklick / Ziehen: Gebautes abreissen | Esc: Fertig";
            Mouse mouse = Mouse.current;
            if (mouse == null || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            { ResetStroke(); return; }
            Vector2 pointer = mouse.position.ReadValue();
            if (!view.pixelRect.Contains(pointer)) { ResetStroke(); return; }
            if (tool == Tool.Demolish) { ApplyDemolition(mouse, pointer); return; }
            if (mouse.rightButton.wasPressedThisFrame)
            { pathSelection.Reset(); fenceSelection.Reset(); return; }
            Ray ray = view.ScreenPointToRay(pointer);
            // Hit only the ground: buildings must not shift the cursor onto their roofs.
            if (!ground.Raycast(ray, out RaycastHit hit, view.farClipPlane)) { ResetStroke(); return; }
            if (tool == Tool.Fence) { ApplyFenceRoute(mouse, hit.point); return; }
            if (tool == Tool.Gate) { ApplyGate(mouse, hit.point); return; }
            Vector2Int cell = grid.WorldToCell(hit.point);
            if (!grid.Contains(cell)) { ResetStroke(); return; }
            if (tool == Tool.Path) { ApplyPathRoute(mouse, cell); return; }
            if (tool == Tool.Creature) { ApplyCreature(mouse, cell); return; }
            if (tool == Tool.Tree) { ApplyTree(mouse, cell); return; }
            ApplyBuilding(mouse, cell);
        }

        private void ApplyBuilding(Mouse mouse, Vector2Int cell)
        {
            var footprint = new BuildingFootprint(selectedBuilding, cell, buildingRotation);
            bool valid = buildingConstruction.CanPlace(footprint) && !creatureWorld.Occupies(footprint);
            foreach (Vector2Int occupied in footprint.Cells()) if (trees.Occupies(occupied)) valid = false;
            bool affordable = economy.CanAfford(selectedBuilding.constructionCost);
            preview.transform.position = footprint.Center(grid) + Vector3.up * selectedBuilding.height / 2f;
            preview.transform.localScale = new Vector3(footprint.Width * cellSize - 0.1f,
                selectedBuilding.height, footprint.Depth * cellSize - 0.1f);
            previewMaterial.color = valid && affordable ? new Color(0.15f, 0.85f, 0.3f) : new Color(0.95f, 0.15f, 0.12f);
            preview.SetActive(true);
            entrancePreview.transform.position = grid.CellCenter(footprint.AccessCell) + Vector3.up * 0.14f;
            entrancePreview.transform.localScale = new Vector3(cellSize * 0.65f, 0.08f, cellSize * 0.65f);
            entrancePreview.SetActive(true);
            if (valid && !selectedBuilding.RequiresPathConnection)
                status.text = "Click: place dispenser | R: rotate | Cyan tile: animal release point | No path required";
            if (!valid) status.text = "Cannot place here: footprint and entrance tile must be clear and inside the map. | R: rotate";
            if (valid) status.text += $" | Cost: {selectedBuilding.constructionCost:0}";
            if (valid && !affordable) status.text = $"Not enough funds: need {selectedBuilding.constructionCost:0}. Enable Infinite funds to build freely.";
            if (valid && affordable && mouse.leftButton.wasPressedThisFrame && buildingConstruction.TryPlace(footprint))
            { economy.TrySpend(selectedBuilding.constructionCost); SelectTool(Tool.None); }
        }

        private void ApplyTree(Mouse mouse, Vector2Int cell)
        {
            bool valid = trees.CanPlace(cell);
            preview.transform.position = grid.CellCenter(cell) + Vector3.up * 1.525f;
            preview.transform.localScale = new Vector3(1.6f, 3.05f, 1.6f);
            previewMaterial.color = valid ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.95f, 0.15f, 0.12f);
            preview.SetActive(true);
            if (!valid) status.text = "Choose clear natural ground without a tree, path or building.";
            if (valid && mouse.leftButton.wasPressedThisFrame) trees.TryPlace(cell);
        }

        private void ApplyCreature(Mouse mouse, Vector2Int cell)
        {
            bool valid = creatureWorld.CanSpawn(cell);
            preview.transform.position = grid.CellCenter(cell) + Vector3.up * selectedCreature.bodyScale.y;
            preview.transform.localScale = selectedCreature.bodyScale;
            previewMaterial.color = valid ? selectedCreature.color : new Color(0.95f, 0.15f, 0.12f);
            preview.SetActive(true);
            if (!valid) status.text = "Choose clear ground away from another creature (or population limit reached).";
            if (valid && mouse.leftButton.wasPressedThisFrame && creatureWorld.TrySpawn(selectedCreature, cell)) SelectTool(Tool.None);
        }

        private void InspectEnclosure()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (!view.pixelRect.Contains(pointer)) return;
            if (Physics.Raycast(view.ScreenPointToRay(pointer), out RaycastHit creatureHit, view.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                CreatureAgent creature = creatureHit.collider.GetComponentInParent<CreatureAgent>();
                if (creature != null && creature.Alive)
                {
                    VisitorAgent visitor = creature.GetComponent<VisitorAgent>();
                    visitorPresentation.Select(visitor);
                    creatureInspection.Select(visitor == null ? creature : null);
                    enclosurePresentation.Select(null); return;
                }
            }
            creatureInspection.Select(null);
            visitorPresentation.Select(null);
            if (ground.Raycast(view.ScreenPointToRay(pointer), out RaycastHit hit, view.farClipPlane))
                enclosurePresentation.Select(grid.WorldToCell(hit.point));
            else enclosurePresentation.Select(null);
        }

        private void ApplyGate(Mouse mouse, Vector3 point)
        {
            FenceEdge edge = fences.Layout.NearestEdge(point);
            if (Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit,
                view.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                FenceSegment segment = hit.collider.GetComponentInParent<FenceSegment>();
                if (segment != null) edge = segment.Edge;
            }
            bool valid = fences.CanPlaceGate(edge);
            previewMaterial.color = valid ? new Color(0.15f, 0.85f, 0.3f) : new Color(0.95f, 0.15f, 0.12f);
            ShowFencePreview(edge);
            if (!valid) status.text = "Choose a wooden fence without a gate. Spiked fencing cannot become a gate. | Esc: cancel";
            if (valid && mouse.leftButton.wasPressedThisFrame && fences.TryPlaceGate(edge)) SelectTool(Tool.None);
        }

        private bool CanPlace(Vector2Int cell)
        {
            if (trees.Occupies(cell)) return false;
            Vector3 center = grid.CellCenter(cell) + Vector3.up * (buildingHeight / 2f);
            Vector3 halfExtents = new Vector3(cellSize / 2f - 0.01f, buildingHeight / 2f - 0.01f, cellSize / 2f - 0.01f);
            return grid.IsAvailable(cell) && !Physics.CheckBox(center, halfExtents,
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void ApplyPathRoute(Mouse mouse, Vector2Int cell)
        {
            ApplyRoute(mouse, cell, pathSelection,
                node => ConstructionRouteGraph.PathNeighbors(node, grid, fences.Layout),
                node => grid.ContentAt(node) == BuildCellContent.Path || CanPlace(node),
                ConstructionRouteGraph.CellDistance, ShowPathPreview,
                node => grid.ContentAt(node) == BuildCellContent.Path || paths.TryPlace(node));
        }

        private void ApplyFenceRoute(Mouse mouse, Vector3 point)
        {
            FenceEdge edge = fences.Layout.NearestEdge(point);
            ApplyRoute(mouse, edge, fenceSelection,
                node => ConstructionRouteGraph.FenceNeighbors(node, fences.Layout),
                node => fences.CanPlace(node, selectedFenceKind), ConstructionRouteGraph.EdgeDistance, ShowFencePreview,
                node => fences.TryPlace(node, selectedFenceKind));
        }

        private void ApplyRoute<T>(Mouse mouse, T destination, ConstructionRouteSelection<T> selection,
            Func<T, IEnumerable<T>> neighbors, Predicate<T> canBuild, Func<T, T, int> estimate,
            Action<T> showPreview, Func<T, bool> place)
        {
            if (!selection.HasStart)
            {
                bool valid = canBuild(destination);
                previewMaterial.color = valid ? new Color(0.15f, 0.85f, 0.3f) : new Color(0.95f, 0.15f, 0.12f);
                showPreview(destination);
                if (!valid) status.text = "Startpunkt blockiert | Anderen Start waehlen | Esc: Abbrechen";
                if (valid && mouse.leftButton.wasPressedThisFrame) selection.Begin(destination);
                return;
            }
            selection.Update(destination, neighbors, canBuild, estimate);
            bool found = selection.Route.Count > 0;
            previewMaterial.color = found ? new Color(0.15f, 0.85f, 0.3f) : new Color(0.95f, 0.15f, 0.12f);
            if (found)
                foreach (T step in selection.Route) showPreview(step);
            else
            { showPreview(selection.Start); showPreview(destination); }
            status.text = found
                ? "Endpunkt anklicken: Strecke bauen | Rechtsklick: Neuer Start | Esc: Abbrechen"
                : "Keine freie Strecke | Anderes Ziel waehlen | Rechtsklick: Neuer Start";
            if (!found || !mouse.leftButton.wasPressedThisFrame) return;

            // Revalidate the complete route before creating any objects.
            Physics.SyncTransforms();
            foreach (T step in selection.Route)
                if (!canBuild(step))
                { selection.Invalidate(); status.text = "Strecke inzwischen blockiert | Ziel neu waehlen"; return; }
            foreach (T step in selection.Route)
                if (!place(step))
                { selection.Invalidate(); Debug.LogError("A validated construction route could not be placed.", this); return; }
            SelectTool(Tool.None);
        }

        private void HideRoutePreviews()
        {
            foreach (GameObject item in routePreviews)
                if (item != null) item.SetActive(false);
            visiblePreviews = 0;
        }

        private void ShowRoutePreview(Vector3 position, Vector3 scale)
        {
            if (visiblePreviews == routePreviews.Count)
            {
                GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                item.name = "Route Preview";
                item.GetComponent<Collider>().enabled = false;
                var renderer = item.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = previewMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                routePreviews.Add(item);
            }
            GameObject visual = routePreviews[visiblePreviews++];
            visual.transform.position = position;
            visual.transform.localScale = scale;
            visual.SetActive(true);
        }

        private void ShowPathPreview(Vector2Int cell) => ShowRoutePreview(
            grid.CellCenter(cell) + Vector3.up * 0.15f, new Vector3(cellSize * 0.95f, 0.12f, cellSize * 0.95f));

        private void ShowFencePreview(FenceEdge edge)
        {
            float height = tool == Tool.Fence && selectedFenceKind == FenceKind.Spiked
                ? FenceConstruction.SpikedHeight : FenceConstruction.Height;
            ShowRoutePreview(fences.Layout.Center(edge) + Vector3.up * height / 2f,
                edge.AlongX ? new Vector3(cellSize, height, FenceConstruction.Thickness)
                    : new Vector3(FenceConstruction.Thickness, height, cellSize));
        }

        private DemolitionTarget TargetAt(Vector2 pointer)
        {
            if (!Physics.Raycast(view.ScreenPointToRay(pointer), out RaycastHit hit, view.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return null;
            return hit.collider.GetComponentInParent<DemolitionTarget>();
        }

        private void ApplyDemolition(Mouse mouse, Vector2 pointer)
        {
            Physics.SyncTransforms();
            DemolitionTarget target = TargetAt(pointer);
            if (target != null)
            {
                Bounds bounds = target.GetComponent<Collider>().bounds;
                preview.transform.position = bounds.center;
                preview.transform.localScale = bounds.size + Vector3.one * 0.04f;
                previewMaterial.color = new Color(0.95f, 0.15f, 0.12f);
                preview.SetActive(true);
            }
            else status.text = "Weg, Zaun oder gebautes Gebaeude waehlen | Esc: Fertig";
            if (!mouse.leftButton.isPressed) { ResetStroke(); return; }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                previousPointer = pointer;
                if (target != null) target.Demolish();
                preview.SetActive(false);
                return;
            }
            if (!dragging) return;
            if (Vector2.Distance(previousPointer, pointer) < 0.5f) return;
            // Sample screen positions to catch narrow fences and paths during fast drags.
            int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(previousPointer, pointer) / 3f), 1, 512);
            for (int i = 1; i <= samples; i++)
            {
                DemolitionTarget selected = TargetAt(Vector2.Lerp(previousPointer, pointer, (float)i / samples));
                if (selected != null) selected.Demolish();
            }
            previousPointer = pointer;
            preview.SetActive(false);
        }

        private void OnDisable()
        {
            if (gridObject != null) SelectTool(Tool.None);
        }

        private void OnDestroy()
        {
            visitorPresentation?.Dispose();
            visitorWorld?.Dispose();
            creatureInspection?.Dispose();
            groundVegetation?.Dispose();
            trees?.Dispose();
            creatureWorld?.Dispose();
            enclosurePresentation?.Dispose();
            foreach (GameObject item in routePreviews)
                if (item != null) Destroy(item);
            if (gridObject != null) Destroy(gridObject);
            if (preview != null) Destroy(preview);
            if (palette != null && palette.Root != null) Destroy(palette.Root);
            if (entrancePreview != null) Destroy(entrancePreview);
            if (buildings != null) Destroy(buildings);
            if (gridMesh != null) Destroy(gridMesh);
            if (gridMaterial != null) Destroy(gridMaterial);
            if (previewMaterial != null) Destroy(previewMaterial);
            if (buildingMaterial != null) Destroy(buildingMaterial);
            if (pathMaterial != null) Destroy(pathMaterial);
            if (fenceMaterial != null) Destroy(fenceMaterial);
            if (entranceMaterial != null) Destroy(entranceMaterial);
            if (ownsVisitorSettings && visitorSettings != null) Destroy(visitorSettings);
        }
    }
}
