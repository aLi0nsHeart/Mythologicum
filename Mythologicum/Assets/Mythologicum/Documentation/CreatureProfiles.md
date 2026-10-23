# Editing creature species

Creature behaviour is driven by editable Unity assets. Each living animal has its own hunger, fear and energy; changing a species asset changes the shared rules, not all animals' current needs.

1. In Unity's Project window, open **Assets > Mythologicum > Resources > Creatures**.
2. Click **Chupacabra** or **Goat**.
3. Change the values in the Inspector. Make lasting edits while Play mode is stopped.

The starting values are intentionally fast for testing. Hunger runs from **0 (full)** to **100 (starving)**. At **Hunt Threshold**, a carnivore looks for food and a herbivore looks for plants. **Hunger Per Second** controls how quickly hunger returns. **Food Relief** controls how much a meal reduces hunger. **Graze Seconds** controls the time a plant meal takes.

Fear runs from **0 (calm)** to **100 (terrified)**. Seeing an agent matching **Feared Tags** makes the animal flee immediately and raises fear. It seeks a nearby safe destination, then settles while fear recovers. **Flee Threshold** controls continued flight after losing sight of a threat while still within **Flee Safe Distance**. **Walk Speed**, **Run Speed** and **Vision Range** are measured in metres and seconds.

**Fear Range** separately limits how close a threat must be before it frightens the animal, within its normal vision range. Goats use **3 metres** by default; their general vision remains 14 metres. Fear already accumulated takes time to recover after the threat moves away.

**Tags** describe the animal to others; **Prey Tags** describe what it hunts; **Feared Tags** describe what frightens it. The Chupacabra has the Predator tag and hunts Goat or Visitor. The goat has the Goat tag and fears Predator. The Visitor tag prepares the same interaction for future visitors; visitors are not implemented yet. **Diet** controls whether food means prey or plants.

Profiles deliberately contain no enclosure assignment. An enclosure describes the surrounding land; it does not own the animal. Fences and gates determine which routes the creature can physically use. A species with **Can Climb Wooden Fences** enabled can cross ordinary wooden fences with a climbing animation. Spiked fences and closed staff gates still block it; a Chupacabra can try a spiked fence, fail, and wait **Climb Retry Seconds** before another attempt.

**Energy** runs from **100 (rested)** to **0 (exhausted)**. The separate exhaustion value is its inverse, so 20 energy means 80 exhaustion. **Initial Energy** is applied when a creature spawns. **Energy Drain Per Second** consumes energy during awake activity, including idle pauses. Hunting, fleeing and attacking use **Strenuous Energy Multiplier**; socializing uses **Socializing Energy Multiplier**. **Climb Energy Cost** charges additional effort for a climb attempt.

At **Sleep Threshold** the animal seeks sleep and resumes ordinary activity at **Wake Threshold**. Actual sleeping restores **Sleep Recovery Per Second** instead of draining energy. The defaults are 100 initial energy, 0.35 drain/second, 3x strenuous drain, 20 sleep threshold, 85 wake threshold and 8 recovery/second. An idle animal therefore needs roughly four minutes to get tired; lower **Initial Energy** to test sleep sooner. Ordinary **Resting** is an awake pause and does not restore energy.

Goats can sleep on the ground. Chupacabras have **Requires Tree For Sleep** enabled: they must reach a tree, climb it, sleep above ground, then descend. Without a reachable tree they cannot recover energy by sleeping. **Tree Climb Seconds**, **Fence Climb Seconds**, and **Failed Climb Seconds** tune the placeholder animation timing. Social animals can spend **Socializing Seconds** interacting with a nearby same-species companion; solitary Chupacabras do not seek these interactions.

**Happiness** is the main satisfaction score (0–100, higher is better). It combines fullness (30%), calmness (10%), environment (25%), available space (20%), and herd satisfaction (15%). Happiness changes gradually; below **Happiness Threshold**, agitation increases and affects behaviour. **Minimum Space** is measured in square metres. **Required Trees** and **Environment Radius** describe nearby environmental needs: Chupacabras initially need two trees and 100 square metres. Trees satisfy this preference and provide sleep sites; energy is tracked separately from the current happiness formula.

**Minimum Companions** and **Maximum Companions** count other animals of the same species within **Herd Range**, excluding the animal itself. The Chupacabra is solitary (0–0); goats prefer 2–8 companions. A maximum of -1 removes the upper limit. **Pause Min/Max Seconds**, **Prey Memory Seconds**, **Attack Seconds**, **Feeding Seconds**, and **Flee Safe Distance** tune the shared behaviour without changing code.

To add a species profile later, right-click in this Creatures folder and select **Create > Mythologicum > Creature species**. Give it a display name, diet, tags and appearance, then keep **Manually Placeable** enabled to include it in the creature catalogue. New tags or new types of behaviour need code support; changing the existing rules and numbers does not.

These assets are the species catalogue loaded at startup. Changing their names or balancing values does not require editing a scene. Saving placed animals and their current needs between game sessions is a separate future feature.
