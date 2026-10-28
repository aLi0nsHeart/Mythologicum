# Yokai monster art

The three supplied models from tt-3d's **Low Poly Japanese Yokai Stater pack**, Asset Store product 386276, now represent independent playable species:

| Gameplay species | Visual prefab | Gameplay definition |
| --- | --- | --- |
| Hitotsume | `Prefabs/Creatures/Hitotume/Hitotume.prefab` | `Resources/Creatures/Hitotsume.asset` |
| Yurei | `Prefabs/Creatures/Yurei/Yurei.prefab` | `Resources/Creatures/Yurei.asset` |
| Oni | `Prefabs/Creatures/Oni/Oni.prefab` | `Resources/Creatures/Oni.asset` |

Chupacabra has returned to the capsule placeholder: its species asset has no visual prefab. Its climbing, tree sleep and hunter identity remain independent of the humanoid models.

## Yurei rename and source assets

The former Kuchisakeonna folders and asset filenames under **Models**, **Prefabs**, **Materials** and **Textures** were renamed to **Yurei**. Prefab and material display names were updated. All original `.meta` GUIDs were preserved, keeping mesh, material, texture, avatar and prefab references intact.

Original source FBX embedded material identifiers and `AssetOrigin` publisher paths still contain the source name. They are retained for correct source material matching and provenance; changing them is not required to rename the playable species. Publisher documentation remains in `Documentation/ThirdParty/tt-3d`. The Hitotume source folder spelling is retained while the playable profile uses Hitotsume.

Original textures/colors remain intact. The imported materials use URP Lit, and imported visual colliders are disabled so gameplay owns movement, collisions and barrier permissions. Each species' **visualScale** controls art size independently of **bodyScale**, which describes the simulation body.

## Animation coverage

The source pack contains humanoid rigs but no dedicated animation clips. All three prefabs use `Animations/Creatures/Yokai/Yokai.controller`, sharing civilian idle/walk/run clips from `Animations/People`, with root motion disabled. Gameplay chooses the supported locomotion state.

Dedicated attack, scare, eating, grave sleep, phasing and fence-breaking character clips are not supplied. Unsupported actions therefore use safe animation fallbacks while the simulation performs its timed action and movement. Death stops unsupported animation and uses a lying-down fallback. These limitations affect visual polish, not species identity or fence permissions.

## Validation

`Tests/Creatures/RunChecks.ps1` compiles current runtime code and runs logic suites, including health checks. Separate Unity harnesses under `Tests/Creatures/Unity` exercise imported art and gameplay in an isolated project. Asset validations written for the previous Chupacabra-to-Hitotume mapping must use the current independent species and Yurei paths; older validation totals are not evidence for the new gameplay rules.

Check the game visually for individual model scale, ground alignment, locomotion, death fallback and preserved materials. Creature gameplay testing is described in [CreaturePrototypeTesting.md](CreaturePrototypeTesting.md).
