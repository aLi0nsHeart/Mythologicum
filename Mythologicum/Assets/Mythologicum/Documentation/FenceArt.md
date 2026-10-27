# Fence art

Fence models come from **Low Poly Fence Pack** by BrokenVector, version 1.1, Asset Store product 61661. Original asset GUIDs and origin metadata are retained.

| Role | Selected source |
| --- | --- |
| Normal/wooden fence | Fence Type1 01 |
| Spiked fence | Fence Type2 03 |
| Fence door/gate | Fence Type2 01 |
| Heavy fence (future) | Fence Type6 Big |
| Sanctified fence (future) | Fence Type4 Small |

## Layout and configuration

- `Prefabs/Fences`: Normal, Spiked and Gate prefabs; `Future` holds Heavy and Sanctified.
- `Models/Fences`: the five required source DAE meshes.
- `Materials/Fences` and `Textures/Fences`: the required wood palette material/texture, converted to URP Lit while preserving the source color.
- `Resources/Fences/FenceVisuals.asset`: explicit active assignments, alignment angles and future-role references.

BuildModeController loads the fence catalog and passes it to FenceConstruction. Models are fitted to the grid-edge length, existing barrier thickness and existing normal/spiked heights (1.6/2.15 metres). Source pivot offsets are corrected, and segment rotation follows X/Z edges. Imported mesh colliders are disabled; selection triggers, closed solid barriers, FenceSegment identity and demolition callbacks remain under the construction system. Unconfigured callers retain primitive fences.

Normal fences remain climbable by capable creatures. Spiked fences resist climbing. Gate replacement still applies only to normal fences, allows authorized path travel and remains closed to creatures/panic ground travel. The requested gate is the **static Fence Type2 01 panel**, not the pack's separate animated Fence Type2 Door. No gate-opening animation is introduced. Heavy and sanctified prefabs have no active FenceKind values, placement buttons or gameplay rules yet.

Unused variants, animated doors, demo scripts/scenes and unused materials/textures were removed with the original source folder. This import had no publisher readme; its AssetOrigin records remain on the retained files.

## Validation

All thirteen existing logic suites pass. `Tests/Creatures/Unity/FenceAssetValidation.cs` passes 248 checks in an isolated Unity 6000.0.74f1 Play-mode run. It validates all five mappings, reserved-role isolation, imported meshes/materials, X/Z alignment at three cell sizes, straight joins/corners, normal-to-spiked replacement, climbing behavior, gate conversion and permissions, old collider retirement, demolition/rebuilding, actual game-scene loading/placement and primitive fallback.

Run it using the existing isolated-project workflow with `-executeMethod FenceAssetValidation.Start` and without `-quit`. It writes `FenceValidationResult.txt` and exits with its result.