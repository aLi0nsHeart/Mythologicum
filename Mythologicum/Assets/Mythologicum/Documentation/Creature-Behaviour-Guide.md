# Creature behaviour and feeding

## Escape attempts

A successful escape-desire roll grants one attempt. The creature approaches a
barrier and tries its usual method first: climbing or phasing. Spiked fences
reject climbing and sanctified fences reject phasing, with a visible failed
attempt. If the creature is still agitated afterward, it may deliver one fence
strike. A creature whose usual method is a physical strike can only start it
while agitated.

One strike applies the creature's Damage once, then the attempt ends. Reinforced
fences resist physical damage. A sufficiently damaged fence can still break from
that single strike. Hunger, fleeing or an obstructed prey target alone do not
grant repeated attacks. Successful crossings also consume the desire immediately.
The creature waits for Escape Retry Seconds and a fresh desire before trying
again. Needs-driven blocked attempts use the same cooldown to avoid rapid retries.

Species assets expose escape chances, retry time, maximum approach duration,
climbing/phasing timing, damage and strike energy cost. The old multi-strike field
is retained for serialized compatibility and hidden; every attempt now has one
strike regardless of that legacy value.

## Yurei

`Resources/Creatures/Yurei.asset` has **Needs Food** disabled. Food-free creatures
stay at Hunger 0 (full), including during sedation, capture and transport. Loading
an older hungry Yurei normalizes this stat while retaining its other needs.
The information panel explains that food is not required.

Yurei's phasing no longer depends on hunger. Sanctified fences still block it.
Other food-requiring phasing species retain their configured hunger threshold.
Graveyard environment, space, herd and gravestone sleeping requirements remain.

## Hitotsume and scarecrows

Open **Building > Enclosures > Place scarecrows**, then click clear ground.
Scarecrows are walkable enrichment: creatures can reach them without opening a
fence. They support shared demolition, terrain movement, construction Undo and
save/load. The purchase cost is charged once; practising does not consume stock.

A hungry fear-feeder prefers reachable people sharing its accessible area,
including lower-threat civilian staff. It can practise on a reachable scarecrow
before considering people beyond barriers. Stronger threats still frighten the
creature according to normal threat rules. Successful scares relieve hunger
without damage or capture; people remain free to flee. Destroying a practice
target before the scare finishes prevents the meal.

The scare pose has anticipation, a large hop, squash/stretch and landing, for
both placeholder bodies and imported models. Only the visible body jumps; its
logical position and collider remain on the ground, so the pose cannot cross a
fence. The information panel identifies scarecrow practice.

Select the Hitotsume species asset to adjust **Scare Approach Distance**, **Scare
Range**, **Scare Seconds**, **Scare Cooldown Seconds**, **Scare Jump Height**,
**Scare Jump Forward**, **Scare Jump Squash**, **Food Relief**, and **Can Use
Scarecrows**. Select **Mythologicum > Gameplay > Balancing Tools > Scarecrows**
for the purchase cost, height and placeholder colors.

## Saving and testing

Saves retain scarecrow positions and remaining escape, fence-retry and scare
cooldowns. An in-progress barrier/scare action is cancelled on restoration with
its retry cooldown retained, avoiding an immediate extra strike or duplicate
meal after loading. Older saves without these optional fields remain readable.

For a quick Play-mode check:

1. Place a park entrance, fence a small test area, place a Hitotsume and a
   scarecrow. Observe Hunger decrease after the completed practice jump.
2. Put a wooden or spiked fence between a Chupacabra and open ground. A desire
   should grant one climb attempt; a blocked, agitated creature can follow with
   one hit and then pause.
3. Put a Yurei behind a sanctified fence. Its phase attempt should recoil; its
   hunger stays full while other needs can still affect happiness.
4. Save during an attempt, load, and verify that the next hit is not immediate
   and scarecrows remain placed.

Native test setup and entry points are listed in `Tests/README.md`.
