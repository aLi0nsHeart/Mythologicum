# Yokai monster art

The Chupacabra currently uses **Hitotume** from tt-3d's Low Poly Japanese Yokai Stater pack (Asset Store product 386276). Its name, diet, prey tags, needs, movement, climbing and sleep rules remain unchanged. Only `visualPrefab` and `visualScale` were added to the Chupacabra definition.

All three supplied creatures are retained in species folders under `Prefabs/Creatures`, `Models/Creatures`, `Materials/Creatures` and `Textures/Creatures`:

- **Hitotume**: assigned to `Resources/Creatures/Chupacabra.asset`, scaled to approximately the existing 1.7-metre body height.
- **Oni** and **Kuchisakeonna**: prefabs reserved for future species. They have no gameplay definitions and do not appear in the creature placement catalog.

The imported assets retain their original GUIDs. Original textures/colors are preserved while materials are converted from Standard to URP Lit. Publisher documentation remains in `Documentation/ThirdParty/tt-3d`. The original pack folder and demonstration scene were removed after dependency checks.

The pack contains humanoid rigs but no animation clips. `Animations/Creatures/Yokai/Yokai.controller` is a separate copy of the civilian humanoid locomotion controller, sharing the existing idle/walk/run clips in `Animations/People`. All three monster prefabs use it with root motion disabled. Chupacabra AI still controls movement and chooses idle/walk/run as appropriate. Dedicated attack, eating, sleep and climbing clips are not supplied; unsupported actions use the existing idle fallback. Death uses the existing stopped-animation/lying-down fallback.

`Tests/Creatures/Unity/MonsterAssetValidation.cs` passes 89 isolated Unity 6000.0.74f1 Play-mode checks. It checks the three retained prefabs, URP textures, humanoid rigs, actual retargeted bone movement, Chupacabra profile identity and spawning, movement-state animation selection, safe action fallbacks, scale/ground alignment, colliders and exclusion of future species from gameplay. The headless harness disables animation culling on its temporary instances to evaluate bone motion without a rendered camera. Run it using the established isolated-project workflow with `-executeMethod MonsterAssetValidation.Start` and without `-quit`.