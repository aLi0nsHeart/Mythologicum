# Creature wellbeing and urgent needs

Select a species in **Assets > Mythologicum > Resources > Creatures**, then
find the **Happiness** heading and expand **Behaviour** in the Inspector. These settings apply to all
individuals of that species. Stop Play mode before making permanent changes.

## What the scores mean

Hunger, thirst, fear and exhaustion run from **0 (satisfied)** to **100
(severely unmet)**. Energy runs the other way: **100 (rested)** to **0
(exhausted)**. Environment, ground, shelter, space and herd scores also run
from **100 (suitable)** to **0 (unsuitable)**.

**Target happiness** describes the current conditions. **Happiness** moves
toward that target over time. **Agitation** is a separate 0-100% response to
poor happiness and serious unmet needs. A creature can therefore become
agitated before its previously high happiness has finished falling.

The information panel shows the target, agitation and unmet needs. Ground and
shelter are checked individually: enough trees must not hide unsuitable ground,
and a pleasant biome must not make up for a missing required sleeping site.
The combined Environment score remains a useful summary.

## Ordinary satisfaction and serious deficits

Ordinary happiness uses a weighted average of fullness, hydration, rest,
calmness, environment, space and herd satisfaction. Weights are relative and
do not need to add up to one. The default **Energy Weight** is **0.2**;
exhaustion now contributes to happiness alongside food and water.

A second rule checks each active need separately. Below **Critical Need
Threshold**, a need can limit target happiness and directly raise agitation.
Good space, grouping or scenery cannot cancel starvation or complete
exhaustion. Several serious deficits combine into stronger distress.

| Behaviour setting | Default | Effect |
| --- | --- | --- |
| Critical Need Threshold | 40 | Satisfaction below this score is serious. For hunger/thirst/fear this means the corresponding unmet stat is above 60. |
| Critical Happiness Cap | 20 | Maximum target happiness when a need is completely unmet. The cap eases toward 100 as that need approaches the critical threshold. |
| Critical Agitation | 0.85 | Agitation target contributed by one completely unmet need. Multiple severe needs combine toward 1. |
| Agitation Build Per Second | 0.2 | Maximum agitation increase per simulation second: 20 percentage points. |
| Agitation Recovery Per Second | 0.08 | Maximum decrease after conditions improve: 8 percentage points per second. |
| Unmet Need Threshold | 80 | Satisfaction below this value is listed in the information panel, including moderate problems. |

The existing **Happiness Change Per Second** defaults to **4**. **Happiness
Threshold** defaults to **50** and still raises agitation when actual happiness
falls below it. The stronger of happiness-based agitation and current need
distress supplies the agitation target. Recovery is gradual after feeding,
drinking, sleeping or improving the habitat.

For example, a starving creature at zero energy has two completely unmet
needs. With the defaults, its target happiness is at most **20**, and its
agitation target is about **98%**. Agitation rises toward that level over about
five simulation seconds. A thirst value of 54 and ground suitability of 50
are additional moderate problems; they remain visible even though they are
above the critical satisfaction threshold.

Only applicable needs contribute. Food-free creatures such as Yurei are not
penalized for hunger; species without a water requirement ignore thirst.
All intrinsic creatures include rest satisfaction; set **Energy Weight** to
zero to disable its happiness and distress contribution. A zero weight disables
that need's contribution and diagnosis. Set **Critical Need Threshold** to zero
to disable the direct serious-deficit policy while keeping the ordinary
weighted average. Unconfigured habitat or social requirements do not produce
artificial distress. Solitary species still dislike extra companions according
to their configured herd limits.

## Acting on needs

**Urgent Hunger** can give food priority while an awake creature searches for
sleep. A missing or unreachable sleeping site is retried after **Sleep Search
Retry Seconds** (default **5 simulation seconds**), rather than occupying every
decision. That waiting period leaves other needs and escape decisions available. A creature already
sleeping continues its recovery; ordinary **Resting** remains an awake pause.
Food, water, shelter and escape decisions still respect physical routes and
the existing single-attempt fence rules.

Special sleepers keep their requirements. A Chupacabra still needs a reachable
tree, a Yurei a gravestone and a water sleeper suitable water. A failed search
does not create free sleep or restore energy. Needs and wellbeing continue to
update during sedation, capture and keeper transport, while those states
retain control of movement and actions.

## Existing parks and a quick check

Existing saves retain individual hunger, thirst, energy, happiness and
agitation. Current needs are evaluated with the new species settings after
loading; no new park-save format is required. Existing authored species
values are preserved. Newly added behaviour fields use their defaults until
you adjust them in the Inspector.

1. Temporarily set a test species to high initial hunger and low initial
   energy, then place it in Play mode. Its panel should report the unmet needs,
   a low happiness target and rising agitation.
2. Provide reachable food, drinking water and the correct sleeping site.
   Watch the needs recover, target happiness improve, and agitation ease down.
3. Repeat with a fed, hydrated and rested creature in a suitable habitat; it
   should stay calm. Check a food-free creature separately to confirm hunger
   is absent from its problems.
4. Save while needs are poor, reload, and resume simulation. The needs should
   still influence wellbeing. Restore the test species' original initial
   values after testing.

## Validation, 2026-11-24

The complete model runner passed 68 suites. The disposable Unity run
`CreatureNeedsIntegrationValidation.Start` passed 48 checks, including the
screenshot's hunger/exhaustion/thirst/ground combination, urgent feeding,
missing-shelter retries, recovery, sedation, food-free species, localized
inspection, immediate paused placement and an actual disk-save/scene-load round trip. Imported defaults
were checked for species with both explicit and implicit Behaviour data.
See `Tests/Creatures/README.md` for commands and result files.

The existing `SteampunkUIIntegrationValidation.Start` run also passed 5,179
checks with 63 GPU captures. The creature inspector capture was reviewed to
confirm that the new target, agitation and diagnosis display correctly.
