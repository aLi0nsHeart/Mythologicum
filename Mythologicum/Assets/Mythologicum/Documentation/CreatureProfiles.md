# Editing creature species

The creature catalogue lives in **Assets > Mythologicum > Resources > Creatures**. Select a species asset in the Project window and edit its Inspector while Play mode is stopped. Each animal keeps its own current health, hunger, fear and energy; the asset defines shared species rules.

## Current species

Group sizes below include the selected animal. The Inspector's **Minimum/Maximum Companions** fields exclude it: 1-2 companions means a total group of 2-3.

| Species | Origin | Health / damage / threat | Food and special behaviour | Preferred group |
| --- | --- | --- | --- | --- |
| Goat | Domestic | 40 / 5 / 5 | Grazes on natural ground; sleeps on ground | 3-9 |
| Chupacabra | Southern New World | 120 / 40 / 40 | Hunts goats and visitors; climbs wood; sleeps in trees | 2-3 |
| Hitotsume | Yokai | 80 / 0 / 20 | Feeds by frightening visitors; eager to escape; climbs wood | 1 |
| Yurei | Yokai | 120 / 35 / 60 | Hunts goats and visitors; very slow hunger; graveyard habitat and gravestone sleep | 1-20 |
| Oni | Yokai | 400 / 90 / 100 | Hunts goats and visitors; rapid hunger; seeks to break vulnerable fences | 1-3 |

These are prototype balancing values. Chupacabra damage equals goat health, so one successful strike kills a full-health goat. A non-lethal strike reduces health rather than automatically killing its target. **Attack Seconds** controls strike timing. There is no automatic resurrection. **Origin** identifies the species' setting; it does not assign an enclosure or change combat by itself.

## Hunger, fear and health

Hunger runs from **0 (full)** to **100 (starving)**. **Initial Hunger**, **Hunger Per Second**, **Hunt Threshold** and **Food Relief** tune the feeding loop. Goat grazing uses **Graze Seconds**; carnivores pursue matching **Prey Tags**. The Chupacabra and goat retain their existing food rules. Yurei starts at 20 hunger and gains 0.15 per second; Oni gains 4 per second.

Hitotsume has **Fear** diet and **Feeds On Fear** enabled. It approaches a visitor, performs a timed scare, and gains hunger relief from a successful fright without dealing damage. **Scare Range** is 6 metres, **Scare Seconds** is 1.5 and **Scare Cooldown Seconds** is 8. A scare is distinct from an attack.

Fear runs from **0 (calm)** to **100 (terrified)**. A visible creature with a higher **Threat** can frighten a lower-threat creature within its perception/fear range. Equal threat does not automatically frighten peers. Safe barriers and perception still matter; threat is not a map-wide aura. Fear accumulated during danger takes time to recover. Goats retain their close 3-metre fear range. Visitors have 60 health, zero damage and threat 10, so even harmless Hitotsume can scare them.

**Maximum Health** and **Damage** are separate from hunger relief. Damage of zero cannot kill an animal or break a fence. Hunger reaching 100 does not yet cause starvation death. Saving individual needs between sessions and a full medical system are separate features.

## Environment, sleep and social needs

Chupacabras want **2 trees within 12 metres**, **100 square metres** of reachable space and a group of 2-3. They must reach and climb a tree before sleeping restores energy.

Yurei wants **2 gravestones within 12 metres** and **60 square metres** of reachable space. Plant gravestones from **Enclosures > Place gravestones**. It sleeps in a reachable gravestone instead of a tree. Removing the occupied gravestone stops that recovery; a missing sleep site never substitutes ground sleep for this species.

Oni wants **500 square metres** of reachable space per animal and tolerates groups up to 3. Hitotsume prefers solitude and 80 square metres. Goats need 48 square metres and prefer 2-8 other goats. Goat, Hitotsume and Oni sleep on the ground.

Energy runs from **100 (rested)** to **0 (exhausted)**; exhaustion is its inverse. Awake activity drains energy. Hunting, fleeing, attacking and other strenuous actions consume it faster; socializing has a separate multiplier. Actual sleeping restores energy until **Wake Threshold**. Ordinary **Resting** is an awake pause. Defaults are 100 initial energy, 0.35 baseline drain/second, sleep threshold 20, wake threshold 85 and recovery 8/second. To test sleep quickly, temporarily set **Initial Energy** to 15, then restore it to 100 afterwards.

**Happiness** combines fullness, calmness, environment, available space and herd satisfaction. Below **Happiness Threshold**, agitation increases. Gravestones fulfil Yurei's environment preference; trees fulfil the Chupacabra's. The information panel displays origin, health, damage, threat, needs, total group size, crowding and escape urge. Companion counts use reachable ground and **Herd Range**, never enclosure ownership. Yurei and Oni use a large herd range so their group limits apply across the accessible area.

## Fences and movement

Casual exploration uses walking routes. Local food and sleep sites are preferred. **Settling Seconds** delays non-urgent climbing for newly placed animals (45 seconds by default); severe hunger, exhaustion, fleeing and a deliberate escape intention can override that caution. Gaps remain ordinary ground.

| Fence | Health | Wooden climbing | Yurei phasing when very hungry | Creature strikes |
| --- | --- | --- | --- | --- |
| Wooden | 120 | Allowed for capable species | Allowed | Can destroy |
| Spiked | 220 | Blocked | Allowed | Can destroy |
| Heavy / reinforced | 800 | Blocked | Allowed | Immune |
| Sanctified | 300 | Blocked | Blocked | Can destroy |

Yurei's **Can Phase Fences** activates at **Phase Hunger Threshold**, initially **80 hunger**. This means very hungry, not well-fed. Phasing bypasses an eligible barrier without destroying it. Heavy fences stop creature strikes, including Oni, but do not stop Yurei phasing. Sanctified fences stop phasing, but physical strikes can still destroy them. Ordinary damaging creatures can attack vulnerable fences; Oni's **Can Break Fences** makes that an active means of reaching its goals. A fence at zero health disappears and its edge becomes traversable.

Gate barriers inherit their fence type's protection. Ordinary animal/panic movement treats closed gates as barriers; authorized path travel retains its existing gate permission.

## Escape intentions

Hunger above 75, agitation and space pressure add to a rare base escape chance. The ordinary baseline is **0.05% per minute**; Hitotsume uses **12% per minute** because escaping to frighten people is a strong motivation. Successful purposeful pursuits can also involve barrier abilities independently of a random escape roll.

The information panel shows current chance, contributing reasons, space pressure and attempt status. This is the chance of forming an intention, not of defeating a fence. Sleeping, urgent actions and cooldowns pause rolls. Probability accounts for elapsed time rather than frame count. Space pressure compares shared ordinary ground area with the combined minimum space needs of animals there, excluding visitors. Species do not acquire new abilities just because they want to escape.

To add another profile, right-click the Creatures folder and choose **Create > Mythologicum > Creature species**. Existing rules and values are editable data; genuinely new abilities require code support. Leave **Manually Placeable** enabled to include a species in the creature menu.
