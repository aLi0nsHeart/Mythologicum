# Buildings, personnel and creature care

Restart Play mode after the scripts import. Placed buildings, personnel, prices and research belong to the current session; this prototype does not yet save a park between sessions.

## Inspect buildings and change prices

1. Place the park entrance and connect its entrance tile to paths, then add visitor facilities as before.
2. Finish construction with **Escape**, then click a building. Its information panel shows connection state, upkeep, salaries, income, expenses and **operating profit** for that particular building.
3. At the park entrance, kiosk or rest area, enter a price and click **Apply price**. Other buildings of the same type keep their own prices.
4. Entry starts at **15**, meals start at **8**, and rest starts free. The actual price displayed on each placed building is authoritative. Prices affect new customers; a visitor already eating or resting finishes at the price quoted when that service began.

Income is recorded at admission or when a meal/rest session completes. Interrupted services do not make a sale. Operating profit subtracts recorded upkeep, personnel hiring and supply deliveries from this building's income. Construction purchases are paid from park funds separately. Upkeep continues for disconnected buildings and includes salaries until personnel are dismissed or lost.

The **Infinite funds** toggle remains available in the park overview. It preserves the park balance while income and expense totals continue to record activity.

## Hire personnel

Three additional choices are available under **Buildings**: **Research centre**, **Mercenary headquarters**, and **Zookeeper station**. They use the imported building models and need a path connection to the park entrance.

1. Build and connect one of these workplaces.
2. Leave construction mode and click it.
3. Click **Hire**. Each workplace holds at most **5 employees**. A hire costs **50** by default; each employee adds **0.5 per second**, or **30 per minute**, to upkeep.
4. Watch the employee's activity in the building panel. Click **Dismiss one** to remove one employee and stop that salary.

Employees appear near their workplace and travel through the park. A disconnected workplace cannot start normal work. Demolishing it removes its personnel. A task cannot cross a solid fence without an accessible staff gate or an existing gap.

The current models use a doctor for researchers, police for mercenaries and the construction worker for zookeepers. They are role placeholders; staff behaviour comes from the game simulation.

## Research creature care

1. Place a living creature and build a connected research centre.
2. Hire a researcher, then choose a creature species in the building panel's study list.
3. Researchers travel to a reachable viewing position and observe a living specimen. Research requires a visible animal; placing a building and hiring staff alone is insufficient.
4. Watch the study percentage rise and read the unlocked notes in the building panel or the selected creature's panel. Scroll the care notes when they extend below the visible area.

Each active researcher contributes **1 percentage point per second** of observation by default. Park-wide knowledge for a species unlocks four stages:

| Progress | Information revealed |
| --- | --- |
| 25% | Food and feeding behaviour |
| 50% | Habitat enrichment and sleeping site |
| 75% | Space and group needs |
| 100% | Containment abilities and escape pressure |

Several researchers can study the same species. Knowledge remains available if a particular employee or research centre is removed. Choose another species after a study completes. Creature research is separate from **technology research**, which remains a later feature.

## Mercenaries and retirement

Mercenaries patrol visitor paths and respond to creatures that can reach the public path network without crossing a containing barrier. A fenced exhibit remains protected; a missing segment can expose its creatures to the public area.

A mercenary contributes **35 group strength**. A target's threat determines the needed group size, so a threat-100 Oni requires **3 mercenaries** at the current defaults. They find one another and regroup before approaching a threat they cannot handle alone. Too few available mercenaries means the response waits for help. Shooting range starts at **8 metres**, with **35 damage** per shot and a **1-second** firing interval.

To request retirement, finish construction, click a creature and press **Retire**. Mercenary shots cannot cross fences or gates. Provide a staff gate or gap so mercenaries can enter an enclosure and find a clear firing position. Clicking the button does not instantly kill the creature.

## Zookeeper care, fences and feeder deliveries

Zookeepers automatically look for injured creatures, damaged standing fences and low goat-feeder supplies. Care requires a route to the patient, fence or feeder. Build a staff gate into an enclosure when keepers need to enter it.

- **Healing:** keepers restore **20 health per second** while treating a patient. They sedate dangerous patients before approaching for treatment.
- **Fence repair:** a keeper restores **30 fence health per second**. The fence must still exist; a completely destroyed segment must be rebuilt by the player.
- **Goat feeders:** each dispenser starts with **6 releases** in stock. Stock is consumed only when a goat is successfully released. Keepers deliver supplies when stock is low; a delivery takes **3 seconds** at the feeder and costs **12** by default. An empty feeder waits for supplies. The existing live-goat limit and release interval still apply.

Personnel can be frightened or attacked. A path connection to a workplace does not guarantee that every enclosure or job is reachable or safe.

## Sedate and physically move a creature

1. Click a living creature and press **Sedate**. A zookeeper travels close enough to fire a sedative dart.
2. Wait for its information panel to show that it is sedated. Sedation lasts **90 seconds** by default; ordinary sleeping does not enable transport.
3. Press **Move**, then click clear ground at the desired destination. **Escape** cancels choosing a destination.
4. A zookeeper walks to the creature, picks it up, carries it along a valid route, and places it at the selected location. The animal remains visible during transport.

Moving is a staff job, not a teleport. Both the pickup point and destination need staff access. A sealed enclosure needs an appropriate gate or gap before a keeper can carry an animal into it. Occupied or blocked destinations are rejected. Sedation does not expire while the creature is being carried, but it can wear off while a request is still waiting for a keeper. If no keeper is available, hire one at a connected zookeeper station.

An animal sleeping up a tree, hidden inside a gravestone, or currently crossing a fence must return to accessible ground before it can be tranquilized and collected. The keeper waits for a safe opportunity.

## Adjusting prototype values

Stop Play mode before making lasting Inspector changes.

- **Main Camera > Build Mode Controller > Building Catalog** holds construction costs, base upkeep, hiring cost, salaries, feeder capacity and delivery costs.
- **Assets > Mythologicum > Resources > Staff > DefaultStaff** holds movement, mercenary group strength, weapon range/damage, healing, repair, sedation and research rates.
- Creature profiles retain each species' health, threat, habitat and containment needs.

Imported character clips do not include every job animation. Shooting effects, care work and carrying use simple prototype presentation where necessary.
