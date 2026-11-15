# Central European creatures

Wolpertinger, Unicorn and Waldschrat are available through **Central Europe**
expeditions. Their specimen stock starts at zero; successful missions add them
to the normal creature placement menu. Research, inspection, health, needs,
economy/reputation and saving use the same systems as the existing roster.

## Starting balance

| Species | Food and habitat | Social preference | Special behaviour |
| --- | --- | --- | --- |
| Wolpertinger, rating 1 | Grazes; 32 m² of accessible ground | 2–8 total | Digs a sleeping burrow and can tunnel under fences during an escape attempt |
| Unicorn, rating 2 | Grazes; 160 m²; at least 70% nearby open meadow and 1 tree | 2–5 total | Shy around stronger threats; prefers reachable tree shade for ground rest |
| Waldschrat, rating 3 | Grazes; 500 m²; 12 nearby trees within 18 m | 1–3 total | Large, hungry forest herbivore; can deliver one agitated fence strike |

These are editable starting values. Creature space, food and habitat are measured
at its actual position, including outside any enclosure. Meadow counts reachable,
unpaved nonforest ground. Extra meadow does not replace the Unicorn's tree need.
Shade is a resting preference; an exhausted Unicorn can still sleep on bare ground.

## Burrowing

Wolpertinger digs into clear natural ground when exhausted, sleeps out of sight,
and visibly emerges after recovering energy. Paving, buildings, physical obstacles
and unwalkable slopes prevent a new burrow. Paving an occupied sleeping site
wakes the animal safely. Burrows are temporary creature actions rather than
permanent construction objects.

An escape desire allows one tunnel attempt. Wooden, spiked and sanctified fences
allow digging underneath; reinforced fences have buried foundations that reject
it. Both entry and exit must be clear natural ground. The logical cell changes
only when the crossing finishes. A blocked attempt animates, ends the desire and
retains its retry cooldown. Existing one-strike agitation rules remain in force.

Underground creatures cannot be watched, detected as prey, shot or sedated from
the surface. Staff must wait for them to emerge. On loading, underground sleepers
and interrupted tunnels restore on the surface at their saved logical cell;
energy is retained and escape cooldowns prevent a free replacement attempt.

## Editing the creatures

Select the assets in `Assets/Mythologicum/Resources/Creatures`:

- **Wolpertinger:** Requires Burrow For Sleep, Can Burrow Fences, Burrow Seconds,
  Burrow Depth and Burrow Energy Cost.
- **Unicorn:** Minimum Open Ground Fraction, Required Trees and Prefers Tree Shade
  For Sleep, plus normal fear and herd settings.
- **Waldschrat:** Required Trees, Environment Radius, Minimum Space, Hunger Per
  Second, Damage and normal escape settings.

Each profile includes health, damage, threat, diet, origin, herd and happiness
tuning. The new Herbivore tag lets existing carnivores recognize these animals as
potential prey without giving them the goat's domestic/infinite-placement status.

In **Fences/DefaultFences**, each fence type's **Blocks Burrowing** setting controls
buried protection. The reinforced-fence research description explains its default.

The three profiles use distinct procedural placeholder bodies: an antlered rabbit,
a horned horse, and a woody forest giant. Placeholder Kind, Body Scale and the
coat/accent/detail colors are editable. Assigning a Visual Prefab takes precedence,
so an artist can replace the placeholder later without changing its behaviour.

## Play-mode check

1. Buy Central Europe expedition access and a team at a connected expedition centre.
   Survey/capture missions can find the first two creatures; dangerous missions
   can find Waldschrat.
2. Give a Wolpertinger natural ground and observe burrow sleep when it tires. Compare
   an escape attempt against a wooden fence and a reinforced fence.
3. Compare a Unicorn's environment score in an open meadow and a densely wooded
   area, then observe ground rest near a reachable tree.
4. Give Waldschrat a large forested area and compare its needs with a small, bare
   enclosure. Its food is ground vegetation rather than goats or visitors.
5. Save and load with all three present; check specimen counts, needs and positions.

Petting-zoo visits are intentionally reserved for a separate feature, as agreed.
Native and model test entry points are described in `Tests/README.md`.
