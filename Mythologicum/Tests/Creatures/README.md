# Creature logic checks

Run from the Unity project folder in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File Tests/Creatures/RunChecks.ps1
```

For a different Unity installation, add `-UnityEditorData 'C:\path\to\Unity\Editor\Data'`.

The runner uses Unity's cached compiler references and current runtime source files. Open the project in Unity and let it compile once before running it. Every suite compiles the current runtime code, then checks pure logic using Unity's bundled Mono runtime. The tests stay outside Assets, and all generated executables and response files go in Temp/CreatureChecks. A compilation or test failure exits with a nonzero code.

- `CreatureModelChecks.cs`: hunger, feeding, fear, recovery, limits and time-step independence.
- `CreatureNavigationTests.cs`: free roaming, routes, fences, gates, boundaries, visibility and building obstacles.
- `GrazingFieldChecks.cs`: grass depletion, regrowth and excluding paths/buildings.
- `BuildingOperatingChecks.cs`: dispensers work without paths while ordinary buildings still require connections.
- `CreatureWellbeingChecks.cs`: environment, space, herd and hunger/fear contributions, smooth happiness and agitation.
- `EnvironmentTreeChecks.cs`: tree counts, reversible forest coverage and environment queries.

These checks do not run Unity's scene, physics or UI. Confirm placement, animal movement, predator reactions and dispenser output separately in Play mode.

`Unity/CreatureIntegrationValidation.cs` preserves the separate headless Play-mode harness. It is kept outside Assets and was run in an isolated project under Temp, with 50 checks for actual creature behaviour, animations, needs and earlier regressions. It is not invoked by `RunChecks.ps1`.
