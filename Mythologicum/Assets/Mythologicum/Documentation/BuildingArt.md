# Building art

Buildings use the eight requested models from **Pandazole - City Town Lowpoly Pack**, version 1.0.0, Asset Store product 205787. Imported assets retain their original Unity GUIDs and AssetOrigin metadata.

| Game role | Source model | Grid footprint |
| --- | --- | --- |
| Rest area | Env_CommercialBuilding_04 | 4 x 3 |
| Kiosk | Env_CommercialBuilding_03 | 4 x 3 |
| Park entrance | Env_CommercialBuilding_02 | 5 x 5 |
| Expedition centre | Env_CommercialBuilding_01 | 8 x 6 |
| Goat dispenser | Prop_RoofWindow_07 | 1 x 1 |
| Research centre (future) | Env_Motel_04 | Not active |
| Mercenary (future) | Env_ResidentBuilding_02 | Not active |
| Zookeeper (future) | Env_ResidentBuilding_05 | Not active |

## Assets and configuration

- `Prefabs/Buildings`: five active building prefabs; `Future` contains the three reserved prefabs.
- `Models/Buildings`: the eight source FBX meshes.
- `Materials/Buildings` and `Textures/Buildings`: the shared palette material/texture. The material is converted from Standard to URP Lit without changing its texture or colors.
- `Resources/Buildings/BuildingVisuals.asset`: active model assignments, footprints, heights, rotations and front-door columns; separate future-role references.

The original source folder, unused models/props and demo scenes were removed after checking retained dependencies. No publisher documentation was included in this import; its origin metadata is retained on the assets.

## Integration

BuildModeController applies the visual catalog before creating construction UI. It updates appearance and footprint fields while preserving each building's existing economic and service tuning. The existing five BuildingKind values remain unchanged; future buildings have no gameplay entries or placement buttons yet.

BuildingConstruction uniformly scales each visual inside its footprint, corrects its pivot to ground level and rotates it with the building. The storefronts were inspected from both sides: active commercial models rotate 180 degrees to face the existing negative-Z access side. The rest area's access column is moved to its visible side door. The dispenser is a small ground-level hatch with one grid cell and no path requirement.

A single solid collider represents each building; imported mesh colliders are disabled. Path access, placement checks, grid occupancy, creature avoidance, demolition and ground-cover clearing use the resulting footprint. Model materials remain unchanged when a building opens/closes; the access marker and status label communicate connection state. The legacy cube/bench visuals remain available for definitions without a visual prefab.

## Validation

All thirteen logic suites pass. `Tests/Creatures/Unity/BuildingAssetValidation.cs` passes 495 checks in an isolated Unity 6000.0.74f1 Play-mode run. It checks all eight requested model mappings, future-role isolation, economic tuning preservation, every active building in all four rotations, proportional fit and grounding, solid/disabled colliders, entrance-marker alignment, overlap rejection, demolition and full-footprint release. In the actual game scene it places all five buildings, connects paths, admits a visitor through the entrance, releases a goat from the one-cell dispenser and verifies vegetation restoration after demolition.

Run the harness using the established isolated-project workflow with `-executeMethod BuildingAssetValidation.Start` and without `-quit`. It writes `BuildingValidationResult.txt`. `Temp/GoatAssetValidation/BuildingsFront.png` and `BuildingsBack.png` are inspection renders of the original models; they are not game assets.