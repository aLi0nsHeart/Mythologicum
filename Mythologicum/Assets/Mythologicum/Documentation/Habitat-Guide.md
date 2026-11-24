# Water, ground types and creature shelters

Creatures evaluate the surroundings they can reach. Fences limit access, but a
creature does not belong to one enclosure. A suitable water source or shelter
outside its current pen can still matter when it becomes reachable.

## Give creatures water

Living species now have **Thirst**, from 0 (hydrated) to 100 (dehydrated).
The creature information panel also shows Hydration, which is the opposite
value. Thirst increases gradually and contributes to happiness. Yurei start
with no water requirement.

Creatures drink from reachable pond banks or water troughs. Keep a clear ground
tile beside the source. Fences can block drinking access; public visitor paths
are not required inside the enclosure.

Open **Building > Enclosures > Water trough** to place a trough. It starts full.
Click it to inspect its remaining stock, capacity and refill expenses. Drinking
uses water in proportion to the thirst actually relieved.

A critically thirsty sleeper leaves its tree, grave or burrow with the normal
exit animation before approaching water. It finishes its drink before returning
to sleep, and keeps its existing shelter home.

A hired zookeeper refills a low trough when it has physical access. Connect the
keeper's house to the park, and provide a staff gate into fenced enclosures.
Refilling takes time and charges supplies once when completed. A pond supplies
drinking water without keeper refills.

## Create and edit ponds

Open **Building > Terrain**, choose **Add water**, then hold the left mouse button
and drag over clear ground. The brush digs a bounded basin and places a flat
water surface. Adjust **Radius** and **Water depth** in the terrain controls.

The first click establishes the surface level for that stroke. Starting inside
an existing pond retains its surface level, so it can be extended. Water surfaces
are stored separately from the ground, and depth is derived from both. Kappa and
Nokk can swim where the water meets their minimum depth; ordinary land creatures
stay on the banks to drink.

Water placement protects buildings, paths, entrances, enclosure objects and
creatures. Nearby protected cells can also prevent excavation because terrain
corners are shared. Move the brush or reduce its radius if an edit is blocked.

Choose **Remove water** to drain the selected area. The excavated basin remains;
use **Raise**, **Smooth** or **Flatten** to reshape the dry ground afterwards.

## Paint ground types

The terrain tools include **Grass**, **Dirt**, **Thicket**, **Stone** and **Sand**.
Select a type, adjust the brush radius and paint the enclosure ground. Unpainted
tiles start as Grass. The biome brush sets the ground type used by creature
preferences.

Grass and Thicket can supply natural grazing. Dirt, Stone and Sand do not supply
grazing. Water and occupied construction tiles are excluded from ordinary ground
preferences. Creature information shows the nearby ground percentages, its
preferred minimums or maximums, and its current ground suitability.

Preferences sample local, reachable ground within the species' **Environment
Radius**. A large patch behind a closed fence does not satisfy an animal on the
other side. Ground suitability joins the existing trees, gravestones and other
environment requirements when happiness is evaluated.

## Provide shelter

| Species | Starting shelter | Starting ground preferences |
| --- | --- | --- |
| Chupacabra | Climbs into a tree to sleep or retreat. | At least 35% Thicket and 10% Dirt. |
| Goat | Can sleep on clear ground. | At least 40% Grass. |
| Hitotsume | Prefers tree shade; can sleep on clear ground. | At least 20% Thicket and 20% Grass. |
| Yurei | Uses a gravestone. | At least 25% Dirt and 20% Stone. |
| Oni | Requires a cave. | At least 25% Stone and 20% Dirt. |
| Unicorn | Requires a clear sleeping spot beside a reachable tree. | At least 60% Grass, at most 20% Stone. |
| Waldschrat | Requires tree shade. | At least 50% Thicket and 15% Grass. |
| Wolpertinger | Digs or reuses a burrow. | At least 40% Grass and 20% Dirt, at most 20% Stone. |
| Kappa | Requires reachable water. | No ground mix required; wants 40 m² of water at least 0.4 m deep. |
| Nokk | Requires reachable water. | No ground mix required; wants 96 m² of water at least 0.65 m deep. |

Place caves through **Building > Enclosures > Cave**. Caves start with room for
three compatible occupants. Give each species enough reachable shelter capacity
for its group, alongside its existing space and herd requirements.

Wolpertingers dig their own burrows on suitable natural ground. Digging leaves a
small visible dirt mound. They reuse reachable, available burrows for sleep and
retreat, and make a new one when they cannot find a suitable existing site.
Burrows start with one occupant each. Stone and water are unsuitable for digging.

Shelter can also be a retreat when a creature is frightened. Canopies, caves,
burrows and graves provide protection appropriate to that shelter type. Tree
shade remains open ground, so it does not make a creature invulnerable. Required
shelters must be usable before sleep can restore energy properly.

## Adjust settings in Unity

Stop Play mode before changing an asset so the edits persist. Select the asset
in the Project window and expand its fields in the Inspector.

| Asset | Editable settings |
| --- | --- |
| Species assets in `Assets/Mythologicum/Resources/Creatures` | **Thirst** rates, thresholds, drinking duration and relief; **Habitat > Preferences** ground minimums, maximums and weights; **Shelter** mode, required status, search radius, retreat rules, canopy height and fear recovery; **Aquatic** movement, depth, area, prey preference and water hunting settings. |
| `Assets/Mythologicum/Resources/Environment/WaterTrough.asset` | Purchase cost, capacity, water used per thirst point, refill threshold, refill duration, refill cost, height and colours. |
| `Assets/Mythologicum/Resources/Environment/Shelters.asset` | Cave cost, cave and burrow capacity, cave height, mound height and colours. |
| `Assets/Mythologicum/Resources/Terrain/DefaultTerrain.asset` | Terrain brush defaults and limits, including water depth. |
| `Assets/Mythologicum/Resources/Terrain/TerrainArt.asset` | Biome materials, tints, water appearance and Thicket ground-cover scale. |

Ground fractions use 0-1 values: 0.4 means 40%. Preference weights are relative;
they do not need to total one. **Behaviour > Thirst Weight** adjusts hydration's
contribution to happiness. Existing hunger, environment, space and herd weights
remain available.

Choose an explicit Shelter mode for a new species. **Legacy** supports the older
sleep flags on existing profiles. Current species use the new shelter settings.
Newly added water-free creatures can disable **Thirst > Needs Water**.

## Test a habitat

1. Place a trough inside a fenced enclosure, hire a keeper and add a staff gate.
   Watch a thirsty creature drink, then let the trough fall below its refill
   threshold and confirm the keeper brings water and records the supply expense.
2. Make a pond on clear ground. Verify land creatures stay on the bank, drink
   when they need water, and cannot drink through a fence separating them from it.
3. Paint an unsuitable ground type, inspect the creature's percentages, then
   restore its preferred mix and watch environment satisfaction recover.
4. Test an Oni with a cave, a Chupacabra with a tree and a Unicorn beside a tree.
   Let a tired Wolpertinger dig, wake and return to its existing mound.
5. Save with water, painted ground, partially used troughs and sleeping creatures.
   Load and check the pond surface, ground types, trough stock, thirst and valid
   shelter homes. Older saves start creatures hydrated and retain their existing
   terrain; new habitat layers can then be added normally.

The models remain simple placeholders. Water depth and shelter data are separate
from their appearance, so later art changes can keep these gameplay rules.

## Kappa and Nökk: water predators

Kappa is a Yokai species found by expeditions in **Japan**. Nökk is Nordic and
found in **Northern Europe**. Both start with zero specimens and use the existing
expedition reward and creature placement menu. Their stable internal IDs are
`Kappa` and `Nokk`; translated display names can use accented characters.

Both species swim, wade along shallow banks and walk on land. They prefer reachable ponds, use water for
sleep and fear retreats, and need enough swimming area for good environmental
satisfaction. The creature panel reports reachable water area, its target area,
suitability and minimum swimming depth. A trough can supply drinking water to
land animals but cannot replace a pond as an aquatic habitat or shelter.

| Starting setting | Kappa | Nökk |
| --- | --- | --- |
| Origin | Yokai | Nordic |
| Expedition rating | 2 | 3 |
| Total group | 2-3 | 1 |
| Minimum reachable space per animal | 120 m² | 220 m² |
| Preferred swimming area | 40 m² | 96 m² |
| Minimum swimming depth | 0.4 m | 0.65 m |

These predators prefer people when available and otherwise hunt matching meat
prey, including goats. They grab living prey, drag it along a valid route into
water, then subdue it before feeding. They do not gain a meal from attacking on
dry land. Fences still block ordinary swimming and bank crossings, so protect
water routes as carefully as land routes.

Other creatures retain their ordinary thirst behaviour: they approach a reachable
pond bank to drink, which can put them close to a waiting predator. Fear and
fences continue to affect that encounter. No special goat-delivery rule forces
prey toward these species.

Select `Resources/Creatures/Kappa.asset` or `Nokk.asset` to balance **Aquatic**
settings: swimming ability and speed, minimum depth, search radius, desired water
area, shallow-water wading, water preference, preferred prey tags, capture time, dragging speed and
subduing time. **Shelter** uses Water mode; group, space, hunger, damage, threat
and visitor appeal remain ordinary species settings. The Kappa turtle and Nökk
horse bodies are procedural placeholders and can later receive visual prefabs.

To test, make a sufficiently deep pond with a clear, reachable bank, place an
aquatic predator and a thirsty goat nearby, and watch drinking and hunting
interact. Try a fence across the bank, interrupt a capture with staff, then save
and load during both swimming and a capture. Keep public paths away from an
unprotected pond if visitors should remain safe.
