# Park daylight

The original game scene already contained a directional light and Unity's default skybox, but the light had shadows disabled, the camera used a solid background and ambient lighting was flat. The daylight pass changes the scene's lighting rather than the authored model palettes.

## Scene setup

- `Sun - Warm Daylight`: realtime directional light, warm white at intensity 1.3, rotation 48/-35/0, soft shadows at strength 0.85. Bias 0.12 and normal bias 0.5 avoid self-shadow banding on the broad building roofs.
- `Materials/Lighting/DaylightSky.mat`: procedural sky with a sun disk and a pale blue lower hemisphere suited to the downward-looking park camera. The scene assigns its directional light as the sky's sun.
- RenderSettings: cooler sky/equator ambient light with a muted ground bounce, moderate reflection intensity and linear distance fog from 70 to 180 metres.
- `Park Daylight`: a global volume using `Settings/DaylightVolume.asset`. Neutral tonemapping, contrast 4, saturation 2, very low bloom (0.06) and a light vignette (0.06).
- Main camera: skybox clear mode, HDR, post processing, shadow rendering and high-quality SMAA. Overlay UI remains separate from the post-process image.
- PC URP asset: 4096 main-light shadow atlas, retaining its four cascades and existing ambient-occlusion renderer feature. Both PC and Mobile shadow distances are 110 metres to cover zoomed-out views. Mobile retains its existing 1024 atlas and hard-shadow capability.

Everything uses realtime lighting so newly placed buildings, trees, fences and moving characters receive the same light. No lightmap bake or day/night simulation is required.

## Editing

Adjust the sun in the hierarchy for direction, warmth or intensity. Edit the sky material and daylight volume profile for atmosphere and grading. `Mythologicum > Lighting > Apply Daylight to Game Scene` reapplies the preset while GameScene is open; it resets the preset's lighting values and saves that scene.

`Editor/DaylightSceneSetup.cs` owns this optional editor command. It does not run automatically on editor startup and adds no runtime lighting controller.

## Validation and previews

`Tests/Creatures/Unity/DaylightVisualValidation.cs` renders matching before/after sample parks using the actual PC URP configuration and a real graphics device. It checks assigned sun/soft shadows, visible sky, gradient ambient light, distant haze, post processing, SMAA, the global neutral-tonemapping volume and shadow range. Both render passes completed without shader errors. The test fixes the layout, tree choices and color output for a consistent comparison.

The sample layout is temporary: it includes the imported buildings, paths, fences, trees and characters and hides prototype landmarks/UI only for the capture. It is not saved into GameScene. Lighting changes preserve the existing gameplay scene records and camera controls. Local preview images are in `Artifacts/Lighting/Before.png` and `After.png`, outside Assets and ignored by Git.

To rerun the visual harness, copy the project assets/settings into an isolated Unity project, open with graphics enabled (omit `-nographics`), and use `-executeMethod DaylightVisualValidation.Start` without `-quit`. Start from the original lighting baseline when producing a comparison; the harness applies and saves the daylight preset between its Play-mode passes.