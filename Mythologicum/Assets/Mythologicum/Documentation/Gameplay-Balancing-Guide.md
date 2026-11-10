# Gameplay balancing guide

Open **Mythologicum > Gameplay > Balancing Tools** in Unity's top menu. Select a
category on the left, edit its real settings asset on the right, then click
**Save asset**. **Select in Project** opens the same asset in the normal Inspector.
Hover over a field for its explanation. No C# edits or scene rebuilds are needed.

The settings live under `Assets/Mythologicum/Resources`:

| Category | Asset | Main controls |
| --- | --- | --- |
| Park and economy | `Management/DefaultParkBalance` | Starting cash, initial funds mode, star-rating formula, fair prices, demand, review weights, accidents and recovery |
| Buildings | `Buildings/DefaultBuildings` | Footprints, entrances, construction/upkeep, default admission/meal/rest prices, service capacity, staff capacity/salaries, feeder stock and dispensing |
| Fences | `Fences/DefaultFences` | Health, climbing, phasing protection and resistance to creature damage, separately per fence type |
| Visitors | `Visitors/DefaultVisitors` | Arrivals, capacity, needs, health/threat, movement, fear, patience, satisfaction, complaints and viewing choices |
| Staff and creature research | `Staff/DefaultStaff` | Perception, travel, shooting, sedation, healing/repairs, observation speed and discovery thresholds |
| Expeditions and technology | `Progression/DefaultProgression` | Teams, locations, mission costs/time/chances/rewards, initial stock/unlocks, technologies, prerequisites and upgrades |
| Shared creature simulation | `Creatures/SimulationSettings` | Shared population cap, placement spacing, grazing regrowth, transport distance and corpse removal |
| Individual species | `Creatures/Chupacabra`, `Goat`, `Hitotsume`, `Oni`, `Yurei` | Combat, diet, needs, habitat/space/group requirements, sleep, abilities, happiness weights and escape behaviour |
| Terrain and brush | `Terrain/DefaultTerrain` | Generation defaults, slopes, uphill movement cost, foundation earthwork and brush ranges |
| Vegetation and habitat | `Environment/Vegetation` | Forest contribution per tree and existing vegetation presentation settings |
| Alerts | `Management/DefaultAlerts` | Safety/operations alert switches, injury thresholds, grace periods, delays and recurrence cooldown |
| Autosaves | `Saving/DefaultSaveSettings` | Enable switch, active-play interval, rotating slot count, failure retry and status duration |
| Overlays & history | `Management/DefaultOperations` | Map overlay colours, refresh interval, ground offset and construction Undo capacity |
| Play controls | `Interface/DefaultPlayControls` | Three simulation speed values, player UI-scale range and speed-control dock geometry |

The [park operations guide](Park-Operations-Guide.md) explains these tools in play,
named save slots, construction Undo, data validation and repeatable performance
measurements. **Validate gameplay data** in Balancing Tools checks the assets and
links each finding to its source; it does not change the values.

## A useful testing workflow

1. Stop Play mode and open Balancing Tools.
2. Change a small number of related values and save the asset.
3. Start Play mode. Use a new park when comparing starting funds, starting unlocks
   or map generation. Use Park Management and object information panels to inspect
   the result.
4. Repeat with another small change. Unity Undo is available for Inspector edits.

Asset edits are project design settings. **Save game** stores the park's current
state; it does not create a separate copy of your balancing assets.

Some fields update during play, but restarting Play mode gives a consistent test.
Building definitions are copied at park start, and creature/visitor initial values
apply when they spawn. Shared population and grazing settings apply when a world
starts. Existing saves keep their funds, creature needs, building footprints,
player-set prices and terrain. They use current behaviour and economic rules
where those rules are not part of the saved state. Lowering a staff/team hiring
limit keeps already hired employees/teams; it prevents further purchases until
the count is below the new limit.

## Money and star rating

**Park and economy > Economy** controls initial money. Building costs and salaries
are under **Buildings**. Rates ending in **Per Second** use simulation seconds:
`0.10` upkeep means `6` money per minute; five employees earning `0.20` each cost
`60` per minute in total.

Each placed revenue building keeps its own price. Its catalogue entry supplies
the initial price for a new placement. **Park and economy > Reputation > Fair
service prices** controls what visitors consider reasonable, including how stars
increase that value. Setting an entrance's initial price and setting its fair
price are separate decisions.

Reputation also exposes specimen appeal, extra-specimen limits, variety bonuses,
domestic-animal appeal limits, guest review weight, safety penalties and recovery.
The overall meter still uses five stars and percentage scores from 0 to 100.

Paths, fences, habitat objects and terrain editing remain free, matching the
existing game. Successfully undoing paid building construction reverses its
original purchase expense; this is separate from demolition.

## Creature needs and behaviour

Each species keeps its existing settings and adds a **Behaviour** foldout inside
Happiness. This contains the happiness weights and less visible AI decisions,
such as urgent hunger, dangerous exhaustion, social distance, escape pressure,
search ranges, scare strength and fence attack effort. Happiness weights are
normalized, so they do not have to add up to one.

- Hunger and fear run from 0 to 100. Energy runs from 0 (exhausted) to 100.
- Health, damage and threat use their own numeric scales.
- Space is accessible area in square metres; distances are metres unless a field
  explicitly says grid tiles or steps.
- Companion counts exclude the creature itself. Minimum `1`, maximum `2` means a
  preferred total group of 2–3. A maximum of `-1` means no upper limit.
- Escape probabilities use 0–1 **per minute**: `0.01` means a 1% chance per minute
  while that cause applies, before its pressure modifiers.

The shared actor limit includes ordinary creatures, visitors and staff. Coordinate
it with the visitor population limit when building a large park.

## Research, expeditions and identities

Creature discovery milestones are in **Staff > Discoveries**. Food, habitat,
social and containment notes use those percentages; the information panels and
encyclopedia use the same thresholds.

**Progression** contains the starting teams, region permits, technology unlocks,
specimen stock and basic fences, followed by location, mission and technology
lists. Costs use park currency, durations use simulation seconds, and chances use
0–1. Technology research time is researcher work: multiple employees contribute.
For example, an expedition duration multiplier of `0.75` means 25% less time.

Keep existing region/mission/technology IDs and species asset filenames stable
when balancing: saved parks use them to identify existing content. Display names
and descriptions can be edited separately. Technology descriptions are authored
text; update a description if you change the percentage it describes.

## Layout and implementation details

The building catalogue owns physical footprints, height and entrance column.
Building visual bindings supply fallback models and orientation; they do not
silently replace those construction settings. Saved parks retain their placed
footprints. Existing UI appearance controls remain in the separate
[UI designer guide](UI-Designer-Guide.md).

Code still contains mathematical constants, percentage bounds, unit conversions,
save-file limits, grid rotation steps and numerical tolerances. Designer-facing
simulation rates, thresholds, costs and formula weights are supplied by the
assets above. C# default values provide fallback data when creating new assets;
use the Project assets for balancing the game.
