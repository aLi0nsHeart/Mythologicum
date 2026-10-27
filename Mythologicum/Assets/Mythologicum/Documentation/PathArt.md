# Path art

Paths use only the `block2` square tile from StarsandShellsStudio's road pack. Required assets retain their imported Unity GUIDs:

- `Prefabs/Paths/block2.prefab`
- `Models/Paths/block2.fbx`
- `Materials/Paths`: the tile's three referenced materials.
- `Documentation/ThirdParty/StarsandShellsStudio`: publisher PDF.

The other tiles, road segments, demo scene and unused materials were removed with the original pack folder. The retained materials already use URP Lit.

The game scene's BuildModeController assigns `pathTilePrefab` to block2. PathConstruction fits the visual bounds to one grid cell horizontally and the existing 0.08-metre path height vertically, correcting the imported pivot so adjoining tiles meet exactly. A separate cell-sized trigger handles selection and demolition; imported colliders are disabled. Grid occupancy, routing, costs and ground-cover clearing use the existing systems. Unconfigured callers retain the cube fallback.

The thirteen existing logic suites pass. `Tests/Creatures/Unity/PathAssetValidation.cs` passes 117 Unity Play-mode checks, covering the actual scene assignment, retained dependencies/materials, tile sizing and ground alignment at cell sizes 1, 2 and 3.5 metres, adjoining edges, placement rejection, demolition, rebuilding, vegetation clearing/restoration and the fallback. Run it in an isolated Unity project with `-executeMethod PathAssetValidation.Start` and without `-quit`; it writes `PathValidationResult.txt` and exits with its result.