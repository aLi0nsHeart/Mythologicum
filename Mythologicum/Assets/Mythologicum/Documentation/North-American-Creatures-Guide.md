# North American creatures, aviaries and aquariums

North America now offers **Wendigo, Thunderbird and Kalupelik**, alongside the
existing Chupacabra. The game has **24 mythological species plus the domestic
goat**, across the same [23 permanent regions](World-Regions-Guide.md).
Bigfoot, Yeti and the Loch Ness Monster remain reserved for DLC.

## Starting creature designs

These are editable gameplay profiles. Diets, ratings, combat values, grouping and
enclosure needs are game design choices. Origin labels identify the game's
cultural inspiration independently of the expedition region.

| Resource asset | Origin label | Rating | Health / damage / threat | Starting care and behaviour |
| --- | --- | --- | --- | --- |
| Wendigo | Algonquian | 5 | 240 / 55 / 85 | Solitary forest carnivore; 400 space, four trees, cave shelter. Uses thicket or nearby trees for a short stalk before rushing prey; can climb wooden fences. |
| Thunderbird | Indigenous North America | 5 | 360 / 75 / 100 | Solitary carnivore; 600 space, two trees, tree-canopy sleep. Flies, animates its wings and shows eye lightning during ordinary attacks. |
| Kalupelik | Inuit | 3 | 160 / 35 / 60 | Carnivore alone or paired; 180 space, water shelter and 60 preferred water area. Swims, wades, lures children toward a reachable shore and uses the existing capture-and-drag hunt. |

All three eat suitable ordinary prey, including goats, and need drinking water.
Their hunger grows at 0.8, 0.4 and 0.35 per second respectively. Kalupelik's lure
does not replace ordinary hunting; an inaccessible or fenced-off shore cannot
be reached by a visitor simply because it was selected as a lure target.
The animals remain free-roaming agents; they are not assigned to an enclosure.

## Designer controls

Stop Play mode. In the Project window, open **Assets > Mythologicum > Resources
> Creatures**, then select **Wendigo**, **Thunderbird** or **Kalupelik**.

- Change health, damage, threat, visitor appeal, hunger, thirst, space, biome
  preferences, trees, movement and shelter directly in the Inspector.
- **Minimum/Maximum Companions** excludes the animal itself. `0 / 0` means
  solitary; Kalupelik's `0 / 1` allows one or two animals in total.
- **Stalking** controls cover search, approach speed, waiting and ambush range.
  It is enabled only for Wendigo in these profiles; the stalking time is bounded.
- **Flight** controls cruise/max height, speeds, roof clearance, wing motion and
  the optional lightning effect. Thunderbird starts at a 4 m cruise height and
  a 14 m maximum height. Lightning uses normal attack damage, without a separate
  area attack or weather system.
- **Lure** controls eligible visitor types, range, search limits, duration and
  cooldown. Kalupelik starts with **Children** only, 14 m range, a 20-second
  duration and a 30-second cooldown. **Aquatic** controls swimming, preferred
  water, body offsets and dragging prey.

Flight, stalking and lure are shared optional settings; future species can use
them without another species-specific AI. They are disabled by default for
existing creatures. The composed placeholder bodies use the normal simulation,
selection and animation. **Visual Prefab** remains available for later artwork.
Research progressively reveals the new ability and shelter guidance. Name,
origin and guidance text use localization IDs with English fallback.

## Building the new enclosure types

Research **Aviaries** or **Aquariums** at the research centre, then choose the
unlocked type under **Building > Fences**. Place it using the normal start/end
fence tool. Both technologies start at 450 cost and 60 seconds of research;
these values and the region's species pools are editable in
**Resources > Progression > DefaultProgression**.

The special effect activates only when the entire boundary closes with the
**same special fence type**. A gap or an ordinary wooden segment does not finish
an aviary or aquarium. Ordinary partitions can still stand inside a complete
outer loop. Irregular grid-aligned loops work as well as rectangles.

- A completed **aviary** adds cage walls and a roof, limiting flight at the
  boundary and below its ceiling. Ordinary fences can still be flown over when
  the creature has enough clearance. Opening a gap in the special perimeter
  removes the completed cage's containment; completing it restores containment.
- A completed **aquarium** excavates and fills its empty interior. Build it
  before placing land animals or scenery there: occupied cells and reserved
  building access prevent automatic excavation. The system retries a blocked
  pool after temporary occupants leave. Existing landscaped water is preserved.
  The default minimum pool size is four cells; use a larger loop for swimming.

The pool becomes normal terrain and water. **Demolishing or breaking a wall
keeps its water and excavation**; only containment is lost. Repairing the loop
or loading the save does not excavate it again or overwrite later landscaping.
Save files retain the completed-pool records alongside ordinary terrain and
water data. Aviary cages are rebuilt from the surviving fence layout.

Select **Resources > Enclosures > DefaultSpecialEnclosures** to adjust cage
height, bar spacing, pool depth, water lift, minimum depth/size and retry timing.
Fence health and containment properties are in **Resources > Fences >
DefaultFences**. The default cage height is 8 m and pool excavation depth 1.2 m.

## Quick Play-mode check

1. Open an expedition centre and select North America. Send a suitable mission;
   all three new creatures have ratings within Dangerous missions. Successful
   rewards should add their specimens to the creature placement menu.
2. Give Wendigo a large forested habitat, a cave and goats. Watch its brief
   stalking approach and subsequent hunt. Check care and activity in its panel.
3. Research Aviaries. Draw all but one side of an aviary, then finish it: the
   cage should appear only when the loop closes. Add trees and Thunderbird;
   observe flight inside, then remove and replace a perimeter segment.
4. Research Aquariums. Close a generously sized aquarium on clear ground and
   check its excavation and water. Place Kalupelik with reachable water, prey
   and a dry bank. For a controlled lure test, child visitors need an actual
   accessible route to that shore; an intact visitor barrier should protect them.
5. Breach an aquarium wall. Check that water stays. Repair, save and reload;
   check water, terrain, creatures, specimen stock and research again. Also try
   saving while the wall is missing, then repairing it after loading.

The separate checks are described in `Tests/Creatures/README.md`. Compilation
and model checks cannot replace observing imported data, physics and visuals
in Unity; native validation results should be read from the current run's logs.

## Verified validation, 2026-11-24

The full model runner passed **68 suites and 204,620 assertions**:

```powershell
powershell -ExecutionPolicy Bypass -File Tests/Creatures/RunChecks.ps1
```

The focused Unity Play-mode run passed **350 checks** in the disposable copy.
It covered imported profile values and procedural bodies, flight and cage
containment, stalking, child luring, aquarium excavation and retained water,
breach/repair, construction Undo, specimen inventory, research and production
disk-save/scene-load continuity. Runtime and Resources file hashes matched the
working project; the successful run had no unexpected exceptions.

The existing `SteampunkUIIntegrationValidation.Start` run also passed **5,179
checks**, with 63 GPU captures covering menus, settings, layout, interactions
and save/load. Its result is recorded in
`LocalCache/SaveValidation/SteampunkUIValidationResults.txt`.

After preparing the disposable copy as described in `Tests/Creatures/README.md`,
run from that copy's project directory:

```powershell
& 'D:\MeineProgramme\Unity\6000.0.74f1\Editor\Unity.exe' `
  -batchmode -force-d3d11 -projectPath "$PWD" `
  -executeMethod NorthAmericanExpansionValidation.Start `
  -logFile "$PWD\NorthAmericanExpansionValidation.log"
```

The recorded result is
`LocalCache/SaveValidation/NorthAmericanExpansionValidationResults.txt`;
its matching detailed log is `NorthAmericanExpansionValidation.log` in the
same directory. Re-run after relevant code or data changes.
