# Creature behaviour and feeding

## Wellbeing and urgent needs

Hunger, thirst, exhaustion, fear and unsuitable habitat can lower happiness
and raise agitation. Serious individual deficits cannot be averaged away by
other satisfied needs. An awake creature can seek urgent food while a failed
sleep search waits for its next retry; actual sleeping retains its recovery.
See [Creature wellbeing and urgent needs](Creature-Wellbeing-Guide.md) for
Inspector controls, defaults, diagnosis and a Play-mode check.

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

## Tatzelwurm and Allghoi Khorkhoi

Both species use the same editable carnivore, drinking, health, fear, happiness
and burrow rules as the existing roster. Tatzelwurm is available through Western
Europe expeditions; Allghoi Khorkhoi, the Mongolian deathworm, is found in East
Asia and retains Mongolia as its species origin.
Their stable specimen and save IDs are `Tatzelwurm` and `AllghoiKhorkhoi`.

| Species | Initial balance | Habitat and social preference |
| --- | --- | --- |
| Tatzelwurm | Rating 2; 120 health; 28 damage; 35 threat; 100 square metres | At least 35% dirt, 20% thicket and 15% stone, with stone limited to 40%; alone or a pair |
| Allghoi Khorkhoi | Rating 4; 250 health; 65 damage; 80 threat; 220 square metres | At least 60% sand and 10% dirt; little grass, thicket or stone; solitary |

Both hunt ordinary eligible prey, including goats, mythical herbivores, visitors
and staff. They need reachable drinking water. Allghoi Khorkhoi's lower thirst
rate and larger size are starting balance values; its attacks use normal damage.

Exhaustion makes either creature seek a reachable existing burrow or dig a new
one in clear natural ground. The visible dirt mound remains after waking and
can be reused or saved. Stronger visible threats can make either species retreat
underground until it is calm and safe to emerge. Stone, paving, water, buildings,
physical obstacles and steep ground cannot host a new burrow. Keep accessible
dirt or sand available even in Tatzelwurm's rocky habitat.

An escape intention permits a tunnel attempt under an ordinary fence when the
entry and exit are diggable. Buried foundations block this route. A blocked
attempt still consumes the intention and observes the existing retry cooldown;
the animals use normal agitation and fence damage rules.

Select either asset under `Assets/Mythologicum/Resources/Creatures` to tune its
needs, combat values, habitat fractions, group limits, **Shelter**, **Requires
Burrow For Sleep**, **Can Burrow Fences**, dig duration, depth and energy cost.
The placeholder body is a cat-headed serpent with front paws for Tatzelwurm,
and a red segmented worm with a circular mouth for Allghoi Khorkhoi. Set
**Visual Prefab** to replace either visual while keeping its simulation rules.

For a Play-mode check, obtain each species, give it appropriate mixed ground and
water, and wait for exhaustion. Verify that it disappears into a persistent mound,
emerges after resting and later reuses that home. Compare tunnel escape attempts
at an ordinary fence and a fence with buried protection. Save and load with both
species present and inspect their specimen counts, needs and shelter homes.

`BurrowingSpeciesNativeChecks.RunChecks()` checks actual imported profiles,
habitat scoring, completed research and procedural bodies from a prepared
Play-mode validation fixture.

### Deep fences

Research **Deep fences**, then select **Building > Fences > Deep fence**. Its
buried foundation blocks tunnelling, including beneath a staff gate or viewing
window fitted to that fence. Heavy fences keep their existing burrow protection.
Deep fences remain climbable, vulnerable to phasing and damageable; they are not
a replacement for every specialist fence.

Tune health and containment independently under **Resources/Fences/DefaultFences
> Rules > Deep**. **Resources/Fences/FenceVisuals > Deep Foundation** controls the
foundation width, buried depth, visible height and color. These visuals follow
the ground. Research cost and work are in **Resources/Progression/DefaultProgression**.
