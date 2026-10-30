# Pure Volumetric Fog in Mythologicum

Source: **Pure Volumetric Fog 1.0.0**, BK. The original README and package metadata
are retained alongside this document. Its Unity 6 / URP 17 runtime, renderer feature,
Inspector, two shaders, shared HLSL and three compute shaders live in
`Assets/Mythologicum/ThirdParty/PureVolumetricFog`. Their original GUIDs and assembly
definitions are preserved. The unused example scene and Unity Terrain were removed
from `Assets/BK` after checking their GUID references.

## Artist controls

Open `Assets/Mythologicum/Resources/Rendering/ParkAtmosphere.prefab` and select its
**Pure Volumetric Fog** component. Changes here apply to new and loaded parks:

- **Density** controls how thick the mist looks.
- **Ground Fog Height** controls how high it reaches above the terrain.
- **Near Fade Distance** keeps construction close to the camera clear.
- **Turbulence** and **Wind Speed** control the drifting patches.
- **Lighting & Scattering** controls tint and sunlight.

Enter Play mode, move and zoom the camera, and compare nearby construction with the
distant forest. Changes made to the runtime object during Play mode are temporary;
edit the prefab outside Play mode to keep them. The preset starts with subtle
blue-green mist, a 12 m layer and slow wind rather than the package's landscape-scale
defaults. Terrain and simulation data do not depend on these artistic settings.

The **Park Volumetric Fog** renderer feature is installed on PC and Mobile renderer
assets alongside their existing features. It raymarches at half resolution and uses
depth-aware upsampling. Quarter resolution is available on the feature for lower
rendering cost. The package targets perspective cameras, compute shaders and URP;
its original README documents platform limitations. This integration is GPU-tested
on the project's Windows Direct3D 11 configuration, not a mobile platform claim.

## Mesh terrain integration

`BuildModeController` instantiates the resource prefab when it creates a park.
`ParkAtmosphere` supplies a normalized floating-point height texture and world
bounds sampled from the same `GridHeightField` used by construction and movement.
It uploads only when the terrain revision changes, including landscaping,
foundation flattening and restored saves. Flat and negative terrain heights are
supported. There is no second Unity Terrain or extra ground collider.

The retained component adds a caller-owned `SetHeightSource(Texture, Bounds, float)`
API, `ClearHeightSource()`, `UsesExternalHeightSource`, `IsReady` and
`ApplyParkPreset()`. Native Unity Terrain support remains available. The adapter
owns and releases the uploaded texture; the fog component owns its generated GPU
resources. Scene transitions unregister the volume and release both.

The old scene distance fog is disabled while the new volume is ready. It remains
available as a fallback on unsupported rendering hardware. The existing daylight
scene and sky settings are retained. This is atmospheric fog; it does not control
discovery or hide unexplored gameplay.

Realtime shadows use the existing directional light's URP shadow atlas. A separate
`bakeTerrainHorizonShadows` switch keeps the optional landscape horizon bake off in
the park preset, avoiding a costly rebake during terrain brushes. Mesh shadows
remain enabled. The package's shadow shaders and native terrain height compositor
are retained because the runtime loads them and they support its optional modes.

`Mythologicum/Lighting/Set Up Volumetric Fog` can reinstall missing renderer features
or recreate a missing prefab. It preserves an existing artist-edited prefab and
updates URP's renderer feature reference map. No scene regeneration is needed.

## Validation

`Tests/Rendering/Unity/FogIntegrationValidation.cs` runs in an isolated project with
a real graphics device. It checks shader availability, height reconstruction,
terrain editing, save loading, map sizes, volume cleanup and near/far image
comparisons against the original linear haze. Captures and results stay in the
ignored validation workspace.
