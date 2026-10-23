# Chupacabra prototype

Restart Play mode after importing the new scripts. Placeholder capsules stand in for creature art.

## Quick test

1. Place the park entrance as before.
2. Open **Creatures (C)** at the bottom. Select **Goat**, then click clear ground.
3. Leave it alone: hunger rises, the goat grazes on natural ground, and hunger falls after its meal. A green patch marks grazing.
4. Place a **Chupacabra** nearby. It detects prey within 14 metres; the goat starts fleeing when the predator closes to 3 metres. The goat seeks safety about 6 metres from the last perceived danger, then settles as fear fades.
5. Watch a successful hunt: the Chupacabra briefly lunges, the captured goat shakes, then tips onto its side. The Chupacabra spends 4 seconds feeding before hunger drops. The brief corpse then disappears. These are procedural placeholder movements, not final character animations.
6. Repeat with a complete wooden fence between them. They can see through it; the goat remains blocked, but the Chupacabra can approach and climb over to pursue prey. Attacks cannot pass through a fence. Closed staff gates block animals.
7. Remove a fence segment to create a reachable route. The Chupacabra can now pursue its prey through the gap. When prey disappears from view, the hunter follows its last seen location for up to 5 seconds before giving up or choosing another visible target.

Creatures can be placed inside or outside enclosures. They move according to barriers and food/threat perception, never an assigned enclosure ID. Small cardinal turns are expected from the current grid movement.

## Trees and creature needs

1. Open **Enclosures (H)** at the bottom and choose **Place trees**. Click empty ground to plant several trees; **Escape** finishes planting. Use **Demolish (X)** to remove them.
2. Finish construction, then click a creature. The needs panel shows happiness, hunger, fear, energy, exhaustion, sleep requirements, environment, accessible space and herd satisfaction. Click elsewhere to deselect.
3. A Chupacabra wants **2 reachable trees within 12 metres**, at least **100 square metres** of reachable space, and **no other Chupacabras within its 12-metre herd range**.
4. Goats need at least **48 square metres** of reachable space and prefer **2-8 other goats within 12 metres**. The selected animal is not included in its companion count. Goats have no tree requirement.
5. Build a small fenced area and compare space satisfaction with a larger one. Open a gap and observe that accessible area grows; creatures use reachable ground rather than an assigned enclosure.
6. Plant or remove nearby trees and watch the Chupacabra's environment score change. Trees also change the forest coverage shown when inspecting an enclosure.

Happiness moves gradually toward a score combining hunger, fear, environment, space and herd satisfaction. Below **50 happiness**, agitation increases: the placeholder becomes redder, walks faster and takes shorter idle pauses. Roaming alternates between travel and rest rather than constantly choosing new destinations.

Trees are walkable enrichment placeholders that now provide Chupacabra sleeping perches. Physical trunk avoidance and final species animations come later.

## Energy, sleep and climbing

1. Select a creature and watch **Energy** fall while awake. **100 is rested, 0 is exhausted**; **Exhaustion** shows the inverse. Hunting and fleeing drain energy three times faster than normal, and socializing drains it twice as fast. An awake **Resting** pause does not restore energy.
2. For a quicker test, stop Play mode and temporarily set a species asset's **Initial Energy** to **15**, below its default **Sleep Threshold** of 20. Restart Play mode and place that species. Restore **Initial Energy** to **100** when finished testing.
3. A tired goat lies down and sleeps on the ground, recovering energy until the default **Wake Threshold** of **85**. A nearby threat can interrupt its rest.
4. Give a tired Chupacabra a reachable tree: it approaches, visibly climbs up, sleeps above ground, then climbs down before resuming ground movement. Remove reachable trees and confirm it looks for a sleep site instead of recovering on the ground.
5. With a hungry Chupacabra on one side of a wooden fence and a goat on the other, watch the Chupacabra climb up and over. Goats cannot climb fences.
6. Choose **Fences > Spiked fence** to build the same barrier with spikes. The Chupacabra approaches, makes a short failed climb attempt, and remains on its original side. Attempts have a retry delay instead of playing every frame. Closed staff gates also prevent climbing.
7. Place goats together, away from predators: occasional **Socializing** pauses should consume energy faster than idle roaming. Chupacabras are solitary and do not seek socializing.

Climbing, lying down, breathing during sleep and failed climbing use procedural placeholder motions. Fence climbing uses the same physical barrier rules as pathfinding; it is a species ability rather than an enclosure escape switch. Spiked fences can still be bypassed through an open gap or a different reachable route.

## Goat dispenser

- Choose **Enclosures > Goat dispenser**. It uses a rotatable 2 x 2 footprint and a dedicated entrance.
- No path connection is required. The cyan tile marks the animal release point; position it inside the enclosure.
- It tries to release a goat every **20 seconds** by default, through clear reachable ground near its release point.
- Dispensers operate independently of the park's path network. Other buildings still need their path connections.
- Each dispenser keeps at most **12 living released animals** by default. The prototype also caps the whole population at **100** to keep testing manageable.
- Demolishing a dispenser stops production; its existing goats remain independent animals.

Dispenser timing, live limit and optional species override are in **Main Camera > Build Mode Controller > Building Catalog > Goat dispenser**. Stop Play mode before making lasting edits. Creature species assets are explained in [CreatureProfiles.md](CreatureProfiles.md).

## Prototype scope

- Overhead labels display species, current activity, happiness, hunger, fear and energy, plus agitation when applicable. Click a creature outside construction mode for the detailed needs panel.
- Hunger and fear run from 0 to 100; reaching 100 hunger does not yet cause starvation death.
- Natural, unpaved ground provides grass. A grazed tile recovers after 20 seconds; roads and buildings are not edible plants. Tree planting affects enrichment and forest coverage separately from grazing.
- Attacking and feeding have short timed states with procedural placeholder animation. Feeding relief is delayed until the feeding state completes. There is no separate health pool, damage model or combat system yet.
- Save/load of creatures and visitors comes later.
