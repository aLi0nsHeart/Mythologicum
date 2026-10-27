# Vegetation art

Vegetation Stylized Kit by **LUX ART STUDIOS** supplies ten tree prefabs and eighteen ground-cover prefabs: five grasses, six ferns/clovers and seven flowers.

## Layout

- `Prefabs/Vegetation/Trees` and `Prefabs/Vegetation/GroundCover`: gameplay visual prefabs.
- `Models/Vegetation`: source meshes and the tree LOD meshes used by those prefabs.
- `Materials/Vegetation` and `Textures/Vegetation`: referenced materials and textures.
- `Resources/Environment/Vegetation.asset`: prefab lists, density, layout seed and target tree height.
- `Documentation/ThirdParty/LuxArtStudios`: original documentation and license.

The required imported assets retain their original GUIDs. Unused bushes/cattails, demonstration scenes, unused textures/materials and incompatible shader graphs were removed with the source pack folder. The imported materials were converted to double-sided URP Lit with alpha clipping for foliage, using their diffuse textures and original color tints. The trunk uses an opaque material. Foliage is static; the source shaders' wind effects are not retained. Material instancing is enabled.

`MeadowGround.mat` colors the game scene's map plane green. The shared prototype material is unchanged.

## Behavior and tuning

`BuildModeController` loads the vegetation catalog at startup. `GroundVegetation` scatters two small patches per 2-metre cell across the map, weighted toward grass (60%), then herbs (25%) and flowers (15%). Positions, yaw and height vary. A separate seeded random generator produces a repeatable layout without changing the simulation's random state. Set `patchesPerCell` to change density, or change `scatterSeed` for another layout.

Decorative plants have no enabled colliders and do not reserve grid cells. Ground cover is hidden in cells occupied by paths, buildings or placed trees, and returns when they are demolished. Visibility updates only when grid/environment revisions change.

Each placed tree chooses one of the ten catalog prefabs independently, with random yaw and a target height of 3.4 metres plus/minus 10%. Model pivots are corrected to place the mesh on the ground. Imported solid colliders are disabled; existing tree selection, demolition and environment enrichment remain under the construction system. Trees retain their LODs with distances adjusted to the park camera. Ground-cover prefabs disable their supplied empty distant LODs so flowers and grasses remain visible at the game camera height.

The game's upper-right vegetation credit names **LUX ART STUDIOS**, as requested by the included license.

## Validation

The thirteen existing simulation suites pass. The separate `Tests/Creatures/Unity/VegetationAssetValidation.cs` harness passes 1,458 checks in Unity 6000.0.74f1 Play mode, including the actual game scene, catalog references, URP materials, full-map scattering, random-state isolation, plant/tree ground alignment and scale, tree variation, construction clearing, demolition restoration and visibility settings.

Run the harness in an isolated project using the same approach as `GoatAssetValidation.cs`, with `-executeMethod VegetationAssetValidation.Start` and without `-quit`. The harness writes `VegetationValidationResult.txt` and exits with its result. Headless checks do not replace an in-editor visual review.