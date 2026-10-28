# Building art

Buildings use the eight requested models from **Pandazole - City Town Lowpoly Pack**, version 1.0.0, Asset Store product 205787. Imported assets retain their original Unity GUIDs and AssetOrigin metadata.

| Game role | Source model | Grid footprint |
| --- | --- | --- |
| Rest area | Env_CommercialBuilding_04 | 4 x 3 |
| Kiosk | Env_CommercialBuilding_03 | 4 x 3 |
| Park entrance | Env_CommercialBuilding_02 | 5 x 5 |
| Expedition centre | Env_CommercialBuilding_01 | 8 x 6 |
| Goat dispenser | Prop_RoofWindow_07 | 1 x 1 |
| Research centre | Env_Motel_04 | 12 x 10 |
| Mercenary headquarters | Env_ResidentBuilding_02 | 10 x 8 |
| Zookeeper station | Env_ResidentBuilding_05 | 4 x 2 |

## Assets and configuration

- `Prefabs/Buildings`: the five original building prefabs; the legacy `Future` folder contains the three employee-building prefabs, which are now active.
- `Models/Buildings`: the eight source FBX meshes.
- `Materials/Buildings` and `Textures/Buildings`: the shared palette material/texture. The material is converted from Standard to URP Lit without changing its texture or colors.
- `Resources/Buildings/BuildingVisuals.asset`: the original five model assignments, footprints, heights, rotations and front-door columns, plus the research/mercenary/zookeeper model references.

The original source folder, unused models/props and demo scenes were removed after checking retained dependencies. No publisher documentation was included in this import; its origin metadata is retained on the assets.

## Integration

BuildModeController applies the visual catalog before creating construction UI. It updates appearance and footprint fields while preserving each building's existing economic and service tuning. The original five BuildingKind values remain unchanged; ResearchCentre, MercenaryHQ and ZookeeperHut append three active employee-building kinds. BuildingVisualCatalog uses the existing employee-model references for those definitions without rewriting the original five visual entries.

BuildingConstruction uniformly scales each visual inside its footprint, corrects its pivot to ground level and rotates it with the building. The storefronts were inspected from both sides: active commercial models rotate 180 degrees to face the existing negative-Z access side. The rest area's access column is moved to its visible side door. The dispenser is a small ground-level hatch with one grid cell and no path requirement.

A single solid collider represents each building; imported mesh colliders are disabled. Path access, placement checks, grid occupancy, creature avoidance, demolition and ground-cover clearing use the resulting footprint. Model materials remain unchanged when a building opens/closes; the access marker and status label communicate connection state. The legacy cube/bench visuals remain available for definitions without a visual prefab.

Placed buildings have a selection component connected to their individual accounts. Outside construction mode, clicking a building opens upkeep, prices, profit and personnel controls. Employee workplaces require paths and support up to five staff each. See [BuildingManagement.md](BuildingManagement.md) for hiring, research, care and testing steps.

## Validation

The previous art-import validation, before employee buildings were activated, passed thirteen logic suites and 495 isolated Unity 6000.0.74f1 Play-mode checks. That earlier run covered five placeable buildings and three reserved models.

`Tests/Creatures/Unity/BuildingAssetValidation.cs` now passes **818 checks** in an isolated Unity 6000.0.74f1 Play-mode run. It checks all eight active model mappings, employee-model assignment, economic tuning preservation, every building in all four rotations, selection/account links, proportional fit and grounding, solid/disabled colliders, entrance-marker alignment, overlap rejection, demolition and full-footprint release. Its actual-game-scene fixture places all eight buildings, connects paths, admits a visitor, releases a goat from the one-cell dispenser and verifies vegetation restoration after demolition. Manual building management and personnel tests are described in [BuildingManagement.md](BuildingManagement.md).

Run the harness using the established isolated-project workflow with `-executeMethod BuildingAssetValidation.Start` and without `-quit`. It writes `BuildingValidationResult.txt`. `Temp/GoatAssetValidation/BuildingsFront.png` and `BuildingsBack.png` are inspection renders of the original models; they are not game assets.
