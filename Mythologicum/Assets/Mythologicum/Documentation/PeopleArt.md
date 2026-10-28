# People and visitor art

City People FREE Samples by Denys Almaral (Asset Store product 260446) provides the visitor and staff models. Original publisher documentation and AssetOrigin metadata are retained under `Documentation/ThirdParty/DenysAlmaral`.

## Structure

- `Prefabs/People/Visitors`: seven civilian models (four casual adults, an older woman, a young boy and a woman with a prosthetic leg).
- `Prefabs/People/Staff`: police, doctor and construction worker models, now used by mercenaries, researchers and zookeepers respectively.
- `Prefabs/People/Staff/Tools`: the worker's six tool prefabs.
- `Models/People`, `Materials/People`, `Textures/People`, `Animations/People`: required meshes, rigs, palettes, controllers and clips.
- `Resources/People/People.asset`: explicit civilian list and separate police/doctor/worker references.
- `Resources/Visitors/DefaultVisitors.asset`: links visitor spawning to the people catalog.

All retained imported assets keep their Unity GUIDs. Material palette overrides from the publisher's demo component are baked into the prefabs before that component is removed. Existing URP materials remain intact. Demo scenes, demo scripts, unrelated props, unused palettes, unused dances and conversion packages were removed with the original `Assets/DenysAlmaral` folder. Construction animations and tools remain available with the worker for later development.

## Runtime behavior

Each arrival chooses one model from `People.civilians`, using a separate appearance random generator. Staff fields never participate in ordinary visitor selection. Each guest owns an independent visual instance while keeping the same shared visitor gameplay profile, navigation, needs, ticket income and predator interactions.

Hiring personnel at a connected employee building spawns independent staff actors using the police, doctor or worker model. These are visual role placeholders; mercenary patrols and grouping, researcher observation, and zookeeper care/transport are controlled by StaffWorld. Staff runtime instances reuse an appropriate civilian idle/walk/run controller with root motion disabled, so their movement follows the staff simulation. The worker's demonstration tool animations and tools remain available as art assets; they do not drive the work assignments. Friendly staff do not frighten ordinary visitors. See [BuildingManagement.md](BuildingManagement.md) for hiring and job behaviour.

Civilian controllers expose `idle`, `walk_forward` and `run_forward` states. Male/female characters use the matching supplied locomotion clips. The prosthetic-leg character uses its own embedded idle/walking animations; its run state uses the supplied walk at increased speed because the pack has no separate compatible run clip for that rig. Root motion is disabled so simulation movement remains authoritative. Unsupported actions such as eating/resting use idle. Since the pack has no death animation, dead visitor visuals stop animating and lie down rather than continuing to walk. Profiles without a people catalog retain the capsule fallback.

## Validation

The previous visitor-art validation, before personnel gameplay was added, passed thirteen logic suites and 655 isolated Unity 6000.0.74f1 Play-mode checks. Thirty ordinary entrance arrivals sampled all seven civilian variants and no staff models. Those historical results covered imported dependencies, URP materials, absence of demo behaviours, rig and animation references, AI-driven walking/running/idle, ground alignment, independent instances, exclusion of staff from ordinary visitor selection, death fallback and unconfigured-species fallback.

`Tests/Visitors/Unity/PeopleAssetValidation.cs` retains the visitor-art checks. Personnel work and creature-handling validation are separate from that historical art-import result.

Launch the harness using the same isolated-project method as the other asset validation scripts, with `-executeMethod PeopleAssetValidation.Start`, without `-quit`. It writes `PeopleValidationResult.txt` and exits with its result. Inspect the visitors in the game for a final visual review.
