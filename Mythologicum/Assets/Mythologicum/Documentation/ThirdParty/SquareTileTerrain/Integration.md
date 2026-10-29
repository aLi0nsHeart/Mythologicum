# 3D Square Tile Terrain Generator - retained material

Original imported folder: `Assets/SquareTileTerrain`.
Original code namespace: `SquareTileTerrainEditor`.
The supplied PDF lists Jacques Cartier in its Author metadata.

The package was inspected, cleaned up, and later imported again to retain its ground art and pine. Retained assets keep their original GUIDs:

- Four-corner heightmap deformation from `SquareTileEditor.SetHeightOnTile`, adapted into `Scripts/Terrain/SquareTileHeightSampler.cs`.
- Optional `Example/Heightmap0.png` at `Textures/Terrain/SquareTileTerrain/ExampleHeightmap.png`.
- Original `STTG_Doc.pdf`, preserved here.
- `Example/Tree/Tree.fbx`, moved to `Models/Environment/SquareTileTerrain`.
- Its `TreePrefab.prefab`, moved to `Prefabs/Environment/SquareTileTerrain`.
- `TreeTex.png` and the terrain `Textures.png` atlas, moved to `Textures/Terrain/SquareTileTerrain` (the atlas is named `TerrainAtlas.png`).
- `Example2/Other/Textures/TileGrass.png`, retained as `GroundGrass.png` in the same texture directory.
- The three corresponding materials, under `Materials/Terrain/SquareTileTerrain`, converted to URP Lit. Pine foliage uses alpha clipping and double-sided rendering. The cliff material samples a single atlas cell, excluding the black/magenta filler cells.

`Resources/Terrain/TerrainArt.asset` links the imported ground materials and pine to the runtime terrain and border forest. The pine is also appended to the existing vegetation catalogue; existing tree and plant variants are preserved.

The original EditorWindow, rule editor, color-rule sprites, demo scenes, text configurations, unused tile meshes/prefabs, road textures and remaining sample assets were removed after checking external GUID references. The original imported folder was then removed.

Mythologicum supplies the runtime height field, foundation planning, generated terrain-following construction, weighted navigation, seedable habitat forest and saved terrain/tree state.
