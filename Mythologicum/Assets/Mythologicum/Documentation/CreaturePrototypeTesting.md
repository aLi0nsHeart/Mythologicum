# Creature prototype testing

Restart Play mode after scripts compile. The creature menu now contains **Goat**, **Chupacabra**, **Hitotsume**, **Yurei** and **Oni**. Chupacabra uses its capsule placeholder; the others use their available models. Unsupported character actions use animation fallbacks.

## Combat, fear and feeding

1. Place the park entrance, then select **Creatures (C)** and place a goat on clear ground. It grazes as hunger rises.
2. Place a hungry Chupacabra nearby. The goat fears it at close range and tries to flee. A successful 40-damage strike kills a full-health, 40-health goat; hunger relief follows feeding rather than target selection.
3. Click creatures outside construction mode to compare **Origin**, **Health**, **Damage** and **Threat**. Non-lethal strikes reduce health instead of automatically killing their target. Zero health means death, not an awake pause.
4. Compare species with different threat values. Visible higher-threat creatures can frighten lower-threat creatures within the relevant perception/fear range; equal-threat peers do not automatically frighten each other. Safely fenced exhibits should still be watchable.
5. Connect visitor paths beside a Hitotsume exhibit. Let it reach a visitor, through an opening or a climbable wooden section. Its timed scare should frighten the visitor and relieve its own hunger without reducing visitor health. Hitotsume has zero damage and cannot damage a fence by striking it.

## Species needs and groups

1. Give Chupacabras at least 100 square metres, two nearby trees and a group of 2-3. The information panel's group size includes the selected animal; the species Inspector's companion fields exclude it.
2. Hitotsume prefers to be alone. Oni tolerates groups up to 3 and needs 500 square metres per animal. Yurei tolerates groups up to 20. Their environmental and space needs still apply even when social preferences are satisfied.
3. Open **Enclosures (H)** and choose **Place gravestones**. Place at least two near a Yurei and watch its environment satisfaction respond. Gravestones can be removed using **Demolish (X)**.
4. Outside construction mode, select each species and inspect happiness, health, origin, hunger, fear, energy, environment, group size, space pressure and escape chance.

Animals remain free to use any physically available ground. Enclosures describe land and barriers; they do not own a creature. Decorations and keeper intentions do not impose an invisible movement boundary.

## Fast sleep test

1. Stop Play mode. Select a species under **Assets > Mythologicum > Resources > Creatures** and temporarily set **Initial Energy** to **15**.
2. Restart Play mode and place it. Goat, Hitotsume and Oni can sleep on ground. Chupacabra must reach and climb a tree. Yurei must reach a gravestone and sleep there.
3. Confirm actual sleeping restores energy toward the default wake threshold of 85. An awake **Resting** pause does not restore it.
4. Remove an occupied tree or gravestone. Recovery must stop; the creature must seek another suitable site rather than continuing to recover at a removed object.
5. Restore **Initial Energy** to **100** after testing.

## Fences, phasing and destruction

1. Build a complete wooden enclosure. A motivated Chupacabra or Hitotsume can climb it; casual wandering does not climb just because a route exists. Goats cannot climb.
2. Replace a segment with **Spiked** and compare climbing. Spikes block climbing but can receive physical damage.
3. Place an Oni with food outside a vulnerable barrier. It should approach, strike the fence over time, and cross only after the segment is destroyed. Fence health reaches zero before its visual and barrier disappear. Repeated attacks must not generate repeated demolition effects.
4. Repeat with **Heavy / reinforced** fence. It is immune to creature strikes, including Oni. It still has a health value, but creature attacks cannot breach it.
5. For a quick Yurei test, stop Play mode and temporarily set **Initial Hunger** to **85**. Restart and place it with prey beyond a barrier. Its default phasing threshold is **80**: only very hungry Yurei can phase. Its ordinary hunger rate is deliberately slow at 0.15 per second.
6. Compare wooden, spiked, heavy and sanctified barriers. A very hungry Yurei can phase through the first three, but **Sanctified** blocks phasing. Phasing leaves the barrier intact.
7. Sanctification does not make a fence physically indestructible: damaging creatures can still attack and destroy sanctified segments. Oni can break sanctified fences, while Yurei can phase through heavy ones. Use the appropriate protection for the animal.
8. Test matching gate types too; replacing a segment with a gate must preserve that fence type's supernatural or reinforced protection. Restore Yurei's **Initial Hunger** to **20** afterwards.

## Exploration and escape urge

New animals normally explore walkable ground before effortful non-urgent crossings. **Settling Seconds** defaults to 45. Urgent needs, fleeing and deliberate escape intentions can override initial caution.

The information panel shows **Escape urge** per minute, reasons, space pressure and the latest attempt status. Ordinary species have a 0.05% rare baseline per minute; Hitotsume uses 12%. Severe hunger, agitation and crowding add pressure. This is the chance of wanting to try, not of defeating a barrier. Sleeping, urgent actions and retry cooldowns pause attempts. Opening a gap provides an ordinary walking route regardless of enclosure identity.

## Goat dispenser and prototype scope

- **Enclosures > Goat dispenser** uses the imported one-cell hatch. Rotate it with **R**; its release point must face usable ground. It does not require a path.
- Defaults remain one release attempt every 20 seconds, up to 12 living goats from each dispenser. The shared simulation population cap is 100, including visitor actors.
- Demolishing the dispenser stops production while its existing goats remain independent.
- Hunger at 100 does not yet cause starvation death. A complete medical, breeding and save/load system is not included.
- Visitor food/rest service and economy tests are described in [VisitorsPrototypeTesting.md](VisitorsPrototypeTesting.md). Species tuning is described in [CreatureProfiles.md](CreatureProfiles.md).
