# Fence art

Fence models come from **Low Poly Fence Pack** by BrokenVector, version 1.1, Asset Store product 61661. Original asset GUIDs and origin metadata are retained.

| Role | Selected source |
| --- | --- |
| Normal/wooden fence | Fence Type1 01 |
| Spiked fence | Fence Type2 03 |
| Fence door/gate | Fence Type2 01 |
| Heavy/reinforced fence | Fence Type6 Big |
| Sanctified fence | Fence Type4 Small |

## Layout and configuration

- `Prefabs/Fences`: Normal, Spiked and Gate prefabs. Heavy and Sanctified still live in the `Future` subfolder, but are active gameplay assets selected through the catalog.
- `Models/Fences`: the five required source DAE meshes.
- `Materials/Fences` and `Textures/Fences`: the required wood palette material/texture, converted to URP Lit while preserving the source color.
- `Resources/Fences/FenceVisuals.asset`: explicit active assignments and alignment angles for all four fence types and the normal gate.

BuildModeController loads the fence catalog and passes it to FenceConstruction. Models are fitted to the grid-edge length, existing barrier thickness and gameplay heights: 1.6 metres for wood, 2.15 for spiked/sanctified, and 2.7 for reinforced fences. Source pivot offsets are corrected, and segment rotation follows X/Z edges. Imported mesh colliders are disabled; selection triggers, closed solid barriers, FenceSegment identity and demolition callbacks remain under the construction system. Unconfigured callers retain primitive fences.

All four fence kinds appear in the construction palette. Normal fences remain climbable by capable creatures; spiked, reinforced and sanctified fences block climbing. A sufficiently hungry Yurei can phase through every kind except sanctified fencing. These permissions are separate from physical damage: Oni can attack sanctified fencing, while reinforced fencing blocks physical destruction but does not block Yurei phasing.

Fence health is **120 wood, 220 spiked, 300 sanctified and 800 reinforced**. Creature strikes reduce health on the first three kinds; reinforced fences retain their health under creature attacks. At zero health the barrier is removed from navigation, and construction refresh removes its visual and colliders. Damaged segments display current/max health. Replacing a fence with a different kind restores the new kind's full health.

Normal, reinforced and sanctified fences can become staff gates; spiked fencing cannot. Gates retain their kind, health and corresponding protections, allow authorized path travel, and remain closed to ordinary creature and panic ground travel. Reinforced and sanctified gates use their matching protective fence model. The normal gate uses the **static Fence Type2 01 panel**, not the pack's separate animated Fence Type2 Door. No gate-opening animation is introduced.

Unused variants, animated doors, demo scripts/scenes and unused materials/textures were removed with the original source folder. This import had no publisher readme; its AssetOrigin records remain on the retained files.

## Validation

The original art import passed 248 checks in an isolated Unity 6000.0.74f1 Play-mode run; that historical result predates fence health and the newly active protective kinds. `Tests/Creatures/Unity/FenceAssetValidation.cs` has been updated to check all five model mappings, four active kinds, imported meshes/materials, X/Z alignment at three cell sizes, straight joins/corners, replacement, climbing and phasing permissions, protective gates, health behavior, old collider retirement, demolition/rebuilding, actual game-scene placement and primitive fallback. Do not treat the historical count as a result for this updated harness.

`FenceHealthChecks.cs` and `FenceCapabilityChecks.cs` add pure checks for damage, destruction, kind replacement and species-specific route permissions. The updated Play-mode harness must be run separately to verify rendering and scene integration.

Run it using the existing isolated-project workflow with `-executeMethod FenceAssetValidation.Start` and without `-quit`. It writes `FenceValidationResult.txt` and exits with its result.
