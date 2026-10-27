# Creature art

Reusable art lives under `Assets/Mythologicum` by asset type:

- `Models/Creatures/<Species>`: source meshes and rigs.
- `Prefabs/Creatures/<Species>`: visual prefabs referenced by creature definitions.
- `Materials/Creatures/<Species>` and `Textures/Creatures/<Species>`: rendering dependencies.
- `Animations/Creatures/<Species>`: animator controller, with source clips in `Clips`.
- `Resources/Creatures`: gameplay species definitions; only their visual prefab reference pulls in the art.
- `Documentation/ThirdParty`: publisher documentation and provenance.

The Goat definition references `Prefabs/Creatures/Goat/Goat.prefab`. Its model uses a foot-level origin, a unit scale, and Unity's positive Z forward direction. CreatureWorld offsets it below the simulation origin and preserves the existing trigger collider and AI movement. Root motion and visual colliders are disabled. The original capsule remains the fallback for species without a visual prefab. `bodyScale` continues to define simulation bounds; `visualScale` controls only the art.

The goat controller contains idle, walk, run, eating, sitting/sleeping, attack and death states. CreatureWorld selects the state from the creature's activity; the non-looping sitting and death clips hold their last pose. Model materials remain unchanged by placeholder agitation tinting; mood is still shown by the existing labels.

## Asset source

Goat assets are from **Animated Goat and Sheep- 3D low poly-FREE**, UrsaAnimations, version 1.0.1, Unity Asset Store product 251910. The original AssetOrigin metadata and publisher PDF are retained. Source meshes, textures, clips and the URP prefab/material retain their imported GUIDs. The shared source FBX and texture also contain sheep data; they remain intact because the goat depends on them.

Removed content includes sample scenes and terrain, Standard/HDRP variants, unused goat/sheep prefabs, the keyboard demo controller, and unused animation clips. The downloaded Asset Store cache remains available for reimporting other variants later.

## Validation

Run the existing `Tests/Creatures/RunChecks.ps1` for compilation and simulation regressions. `Tests/Creatures/Unity/GoatAssetValidation.cs` is a separate Play-mode harness: copy it into an isolated project's `Assets/Editor` folder alongside the project assets and packages, then launch the matching Unity editor with `-batchmode -nographics -projectPath <isolated-project> -executeMethod GoatAssetValidation.Start -logFile <log-path>`. Do not pass `-quit`: the harness enters Play mode and exits with its result.

In the game, check manual goat placement and dispenser output, walking/fleeing, grazing, sleep/waking, and death. Other species should retain their placeholder visuals.