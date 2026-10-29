# Runtime terrain

The imported **3D Square Tile Terrain Generator** supplies a useful four-corner heightmap deformation algorithm. Its original implementation is an editor-only prefab generator. Mythologicum now uses an adapted runtime height sampler with the existing construction grid, procedural ground meshes, foundation planning and terrain-aware navigation.

## Try it in Unity

1. Stop Play mode, allow scripts to compile, and start a **new game**. Saves from before terrain was introduced retain their original flat ground.
2. Move and zoom the camera across the rolling hills. The camera follows terrain height and keeps clearance above the surface.
3. Place a park entrance or another building on a gentle hill. Its preview shows the planned foundation elevation; placement levels the entire footprint and its dedicated access tile.
4. Place paths and fences across a slope. Their generated geometry follows the ground. Paths refuse slopes above their configured limit. Staff gates retain their fence type's health and protection.
5. Place a building beside an existing path, tree or gravestone. Nearby ground attachments follow the terrain change. Existing foundations remain fixed, and changes that would make a neighboring path too steep are rejected.
6. Test creature movement, visitors and hired staff. Their feet follow the terrain between cells, including the tops of paths.
7. Save and load. Hills and flattened foundations should return exactly. Demolishing a building leaves its landscaped plateau in place and releases its foundation reservations.

## Designer settings

`Resources/Terrain/TerrainArt.asset` controls the imported grass and cliff materials and the new-game forest border. Its forest settings set the border width (3 cells), density (88%), pine height (5-7 metres), and deterministic scatter seed. Border trees are ordinary enclosure trees: they count toward habitat, stay walkable, and can be removed with Demolish. The central map remains clear. Loading restores saved trees and does not regenerate cleared forest. Model, height and rotation are saved with newly placed trees.

The imported pine is also part of `Resources/Environment/Vegetation.asset`, so the regular tree placer can use it. Grass uses the retained standalone texture. The cliff material samples one tile of the retained atlas for the map skirt. Materials use URP Lit; pine foliage uses alpha clipping and double-sided rendering.

Select `Assets/Mythologicum/Resources/Terrain/DefaultTerrain.asset` in the Project window and expand **Settings**:

- **Generate Hills**: enables the new landscape for new games. Disable it for a flat new map.
- **Seed**: changes the reproducible hill layout.
- **Relief**: hill height scale, initially 7 metres.
- **Feature Size**: horizontal hill scale, initially 28 metres. Larger values make broader hills.
- **Maximum Walk Slope**: creatures and staff can normally cross slopes up to 45 degrees.
- **Maximum Path Slope**: paths and ordinary visitors allow up to 28 degrees.
- **Maximum Foundation Earthwork**: maximum cut/fill at a foundation corner, initially 4 metres.
- **Heightmap**: optional readable grayscale texture instead of seeded hills. Black is low ground and white is high ground. The retained `Textures/Terrain/SquareTileTerrain/ExampleHeightmap.png` is an available example. Leave the field empty to use generated hills.

Terrain settings affect new parks. Loading uses the saved elevations and movement limits. Foundations blend into a shoulder around their footprint. Placement can fail near incompatible foundations, steep ground, or a path that cannot retain its allowed slope. No manual terrain brush, water, bridge or tunnel tools are introduced in this version.

## Implementation

`GridHeightField` owns shared corner elevations, terrain revision, maximum slopes, protected foundations and deterministic generation. `SquareBuildGrid` remains the shared X/Z occupancy grid and supplies surface positions, movement costs and terrain sight checks. Construction, creatures, visitors and staff use those same height samples.

`SquareTileHeightSampler` adapts the imported generator's interpolation. Ground is rendered as mesh chunks of 16 by 16 grid cells, with matching MeshColliders and skirts at the map edge. Each tile is subdivided twice; the sampler uses the exact rendered triangles, so colliders, paths and actor feet agree between tile centres. Ground colliders use Ignore Raycast layer 2 and are queried directly for building cursors; obstacle queries can ignore the ground itself.

Foundations are planned before applying changes. The full rotated footprint and its access tile become level. Existing foundation vertices stay fixed. Existing walkable ground and paths are protected against new excessive slopes. Previewing or rejecting a building does not alter terrain. Placement applies the plan and reserves the foundation; demolition releases the reservation.

Paths, fences and closed barriers use generated meshes when attached to runtime terrain. The imported building and creature models remain in use. The previous path/fence model rendering remains available to grids without a terrain field. Terrain changes update grid overlays, path/fence meshes, vegetation, actors and navigation caches.

Shared A* supports optional weighted edge costs, with uphill travel costing more than flat travel. Costs of at least one retain the existing Manhattan heuristic. Terrain sight checks prevent staff shooting through a ridge. Future terrain types and movement preferences can extend these shared traversal queries rather than adding separate pathfinders.

Save format 3 records every terrain corner plus terrain limits and seed. It restores elevations before placing saved construction and registers existing foundations without landscaping them a second time. Version 1 and 2 saves are restored on flat ground and retain the earlier progression migration behavior.

## Import cleanup and validation

The adapted sampler, optional heightmap, original documentation, grass texture, terrain atlas, and the pine model/prefab/texture/material were retained. See `ThirdParty/SquareTileTerrain/Integration.md`. GUID references outside the imported directory were checked before removing the remaining example scenes, editor tools, rule sprites/configurations, unused meshes/materials and tiles.

- `Tests/Terrain/TerrainChecks.cs`: interpolation, foundations, slope limits, weighted A*, terrain visibility and saved heights.
- `Tests/Terrain/Unity/TerrainIntegrationValidation.cs`: actual mesh/collider agreement, building flattening, paths/fences/gates, nearby vegetation, actor movement and save restoration in an isolated Unity project.
- `Tests/Terrain/TerrainArtChecks.cs` and `Unity/TerrainArtIntegrationValidation.cs`: outward beam/barrier winding, spiked gates, imported material bindings, forest habitat/removal and appearance persistence.
