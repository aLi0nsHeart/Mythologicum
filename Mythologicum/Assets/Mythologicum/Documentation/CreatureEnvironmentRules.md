# Creature and environment design

Confirmed design:

- Enclosures are spatial descriptions of fenced areas, not containers that own creatures.
- Creatures may be placed anywhere, including outside enclosures.
- Creature needs depend on the environment around the creature's actual position.
- Fences and gates form barriers. Later movement must account for each species' abilities.
- Reachable visitors and feeding animals belong to the same future perception, food-target, and reachability logic. An enclosure designation must not make a visitor safe or food unreachable.
- A path connection through a staff gate represents authorized staff access, not unrestricted creature passage.

Current prototype:

- Enclosure detection and inspection report area, forest coverage and staff access.
- Closed gates count as perimeter barriers. Gate animation and staff-agent passage are not implemented yet.
- `EnvironmentGrid` stores map-wide environment data and supports both area sampling and local radius sampling. It has no enclosure or creature ownership dependency.
- **Enclosures** in the bottom toolbar contains the tree placer and goat dispenser. Dispensers work without paths and release goats through their marked release point.
- Trees are tracked independently from path/building occupancy and remain walkable creature enrichment. One constructed tree per tile adds 50 percentage points of forest coverage to that tile, capped at full coverage. Demolition removes that contribution while preserving any authored underlying forest coverage. Actual climbing and trunk avoidance come later.
- Fences and gates have solid barrier colliders in addition to selection triggers.
- Creatures alternate calm roaming with rest, perceive other agents by profile tags, flee toward safety from the last detected threat, and seek reachable food when hungry. Hunters retain a target's last seen location briefly instead of switching targets constantly.
- Closed staff gates block creature movement even when the staff path network treats them as authorized access points.
- Hunger, fear and gradually changing happiness are per-agent state. Diets, targets, threats, timing, movement speeds, tree requirements, space and preferred companion counts are shared species data.
- Environment satisfaction currently measures reachable trees within the creature's environment radius. Space measures the total walkable area it can reach without crossing barriers. Herd counts other living creatures using the same species profile, within herd range and in the same reachable region. These needs work outside fenced enclosures too.
- Current defaults: Chupacabras want 2 trees within 12 metres, 100 square metres of reachable space and no nearby Chupacabra companions. Goats want 48 square metres and 2-8 other goats within 12 metres; trees are optional for them.
- Happiness combines hunger satisfaction (30%), freedom from fear (10%), environment (25%), space (20%) and herd (15%). It changes gradually rather than jumping when the surroundings change. Below the profile's happiness threshold, agitation shortens rest periods, speeds up ordinary walking and adds a reddish visual tint.
- Outside construction mode, clicking a creature shows its detailed needs panel. Clicking empty ground selects the enclosure there, if any.
- Hunting transitions through timed attack and feeding states. Captured prey freezes and shakes during the attack, then tips over as a brief corpse. Hunger relief only arrives after feeding completes. A new barrier invalidating an attack cancels it safely; there is no separate health or damage system yet.
- Herbivores graze regenerating grass on natural, unpaved ground. This is separate from forest coverage.
- Visitors are not implemented yet. The Visitor tag is already available for the shared prey/threat rules.
