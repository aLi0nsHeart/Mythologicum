# Creature and environment design

Confirmed design:

- Enclosures are spatial descriptions of fenced areas, not containers that own creatures.
- Creatures may be placed anywhere, including outside enclosures.
- Creature needs depend on the environment around the creature's actual position.
- Fences and gates form barriers. Movement accounts for climbing, hungry Yurei phasing, and physical attacks on destructible fences.
- Reachable visitors and feeding animals share perception, food-target, and reachability logic. An enclosure designation must not make a visitor safe or food unreachable.
- A path connection through a staff gate represents authorized staff access, not unrestricted creature passage.

Current prototype:

- Enclosure detection and inspection report area, forest coverage and staff access.
- Closed gates count as perimeter barriers. Gate animation and staff-agent passage are not implemented yet.
- `EnvironmentGrid` stores map-wide environment data and supports both area sampling and local radius sampling. It has no enclosure or creature ownership dependency.
- **Enclosures** in the bottom toolbar contains tree and gravestone placers and the goat dispenser. Dispensers work without paths and release goats through their marked release point.
- Trees are tracked independently from path/building occupancy and remain walkable creature enrichment. One constructed tree per tile adds 50 percentage points of forest coverage to that tile, capped at full coverage. Demolition removes that contribution while preserving any authored underlying forest coverage. Chupacabras climb trees to sleep; physical trunk avoidance is not implemented.
- Gravestones form a separate walkable enrichment layer and provide Yurei sleeping sites. They do not reserve grid cells, count as trees or alter forest coverage. Trees and gravestones cannot share a tile; paths and buildings cannot be placed over either enrichment type. Demolition frees the enrichment location, and decorative ground cover returns. Gravestone visuals are procedural placeholders.
- Fences and gates have solid barrier colliders in addition to selection triggers.
- Creatures alternate calm roaming with rest, recognize prey by profile tags, fear visible creatures with higher threat when safety barriers do not separate them, and seek reachable food when hungry. Hunters retain a target's last seen location briefly instead of switching targets constantly. Hitotsume instead feeds on successfully frightening visitors without dealing damage.
- Closed staff gates block ordinary creature movement even when the staff path network treats them as authorized access points. They retain their fence kind's damage and phasing protections.
- Health, hunger, fear, energy and gradually changing happiness are per-agent state. Maximum health, damage, threat, origin, diets, targets, timing, movement speeds, enrichment requirements, space and preferred companion counts are shared species data.
- Environment satisfaction measures nearby reachable trees, or gravestones for species with a graveyard requirement. Space measures the total ordinary walkable area reachable without crossing barriers; special escape abilities do not make an undersized habitat satisfactory. Herd counts other living creatures using the same species profile, within herd range and in the same ordinary reachable region. These needs work outside fenced enclosures too.
- Current defaults: Chupacabras want 2 trees within 12 metres, 100 square metres and 1-2 other Chupacabras (2-3 total animals). Hitotsume prefers solitude. Yurei wants 2 gravestones and tolerates 0-19 companions (up to 20 total); Oni wants 500 square metres and tolerates 0-2 companions (up to 3 total). Goats want 48 square metres and 2-8 other goats within 12 metres; trees are optional for them.
- Happiness combines hunger satisfaction (30%), freedom from fear (10%), environment (25%), space (20%) and herd (15%). It changes gradually rather than jumping when the surroundings change. Below the profile's happiness threshold, agitation shortens rest periods, speeds up ordinary walking and adds a reddish visual tint.
- Outside construction mode, clicking a creature shows its detailed needs panel. Clicking empty ground selects the enclosure there, if any.
- Hunting transitions through timed strikes and feeding states. A strike subtracts damage from health; surviving targets are released and can flee, while zero health causes death and a brief corpse. Chupacabra's 40 damage kills a healthy 40-health goat in one strike. Hunger relief arrives only after feeding completes. A new barrier invalidating an attack cancels it safely.
- Herbivores graze regenerating grass on natural, unpaved ground. This is separate from forest coverage.
- Visitors use the same shared prey/threat rules, normally follow connected paths, watch creatures, buy food at kiosks and recover energy at rest areas. Escaped threats can frighten them off paths or attack them.
