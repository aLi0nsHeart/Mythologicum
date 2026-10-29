# Saving and loading

Start a park as usual. The top-right **Pause** button (or Escape) freezes the simulation and opens:

- Resume
- Save game
- Load game
- Quit to main menu
- Quit game

There is one manual save slot. Overwriting it, loading during play, and quitting ask for confirmation. The main menu offers **Load game** beside Start; it is disabled until a save exists. Loading stays paused so you can inspect the restored park before resuming. In the Unity Editor, Quit game stops Play mode.

Saved data includes building footprints and rotation, prices and accounts, paths, fence types/gates/health, trees and gravestones, living creatures and their health/needs/sedation, visitors and their satisfaction/patience, hired staff and their home buildings, species research, finances, and camera framing. Creature assets are referenced by asset name; keep those names stable for this save format. Footprints are stored separately so later catalogue size adjustments do not resize existing saved buildings.

Staff and creatures choose fresh actions after loading. Carried creatures are placed on the ground at their carrier's location and retain their sedation. In-progress service, combat, climbing, sleep animation, grazing cooldowns, escape attempts, staff jobs, corpses and temporary effects are not serialized. These are resumed or reevaluated by the simulation. Technology project progress is saved, and researchers resume it after loading.

Format 2 also saves expedition teams and permits, in-flight missions and their outcomes, acquired creature stock, technology unlocks and partial projects, plus inbox letters and read state. Version 1 parks remain loadable and adopt unlocks needed for their existing buildings and fences. See `ProgressionPrototype.md` for the acquisition and research controls.

Format 3 adds terrain corner heights and movement limits, preserving hills and flattened building foundations exactly. Version 1 and 2 saves keep flat ground. Start a new park to use the generated landscape; see `TerrainPrototype.md` for terrain settings and testing.

New saves also retain each tree's model, height and rotation. Border forest is generated for new parks and then saved as ordinary trees; clearing it remains permanent after loading. Earlier saves without tree appearance data still load.

The file is `park-save.json` in Unity's `Application.persistentDataPath` (on Windows normally `%USERPROFILE%/AppData/LocalLow/<CompanyName>/<ProductName>`). A successful overwrite retains the previous file as `park-save.json.bak`; loading currently uses the primary file. Files are JSON with a format version and checked map dimensions. No save is stored inside the project or committed to Git.

## Check in Unity

1. Enter Play mode. Place an entrance, paths, another building and a gate. Acquire creatures through expeditions, then place them; set a building price and hire an employee.
2. Open Pause. Check that creatures and the camera stop responding. Save and resume.
3. Change the park, then Load game and confirm. Check that the saved layout, prices, money, and population return. Resume.
4. Save, quit to the main menu, and choose Load game. Check that the same park returns.
5. Try Cancel on overwrite/load/quit confirmations. Check that the current park stays intact.
6. In a standalone build, Quit game closes the application; in the Editor it ends Play mode.

Automated Play mode coverage is in `Tests/Saving/Unity/SaveIntegrationValidation.cs`. Run it in an isolated project with the harness copied to `Assets/Editor` and `-executeMethod SaveIntegrationValidation.Start`; it writes test saves inside that isolated project's Temp directory.
