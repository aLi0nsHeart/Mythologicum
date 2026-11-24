# Visitor types and balancing

New visitors belong to one of four types. Each visitor still needs food, rest,
safe paths and interesting exhibits; preferences change which exhibits they
choose and how rewarding those visits feel.

| Type | Starting behaviour |
| --- | --- |
| Thrill seekers | Prefer exciting creatures and rarity; fear builds more slowly, but escaped dangerous creatures still cause panic. |
| Wonderlust | Prefer magical, beautiful or mysterious creatures; watch a little longer. |
| Adventurers | Prefer rarity, new species and new origins; have more patience and wait longer before revisiting the same individual. |
| Children | Prefer approachable, playful creatures; become hungry and tired sooner, seek services earlier, and are more easily frightened. |

Children currently visit independently; family groups remain a future feature.
Researched attractions include petting-zoo visits for suitable creatures; see the
[attractions guide](Attractions-Guide.md) for placement and balancing.

## Adjust the visitor mix and behaviour

Outside Play mode, open **Mythologicum > Gameplay > Balancing Tools > Visitors**,
or select `Assets/Mythologicum/Resources/Visitors/DefaultVisitors.asset` in the
Project window. Expand **Visitor Types > Type Settings** in the Inspector.
Keep one entry for each of the four types.

**Arrival Weight** controls relative shares of new arrivals. The starting weights
are all 1, giving each type an equal chance. Weights 4, 2, 1, 1 give expected
shares of 50%, 25%, 12.5%, 12.5%; small groups can differ by chance. Set a type's
weight to 0 to stop new arrivals of that type. Existing visitors retain their
type. Keep at least one positive weight; an entirely disabled mix falls back to
General visitors.

The main visitor fields remain the shared base values. A type's **Needs and
Movement Multipliers** adjust those values: 1 keeps the base value, 1.2 means
20% more, and 0.8 means 20% less. For example, **Hunger Multiplier** changes hunger
gain and **Energy Multiplier** changes energy drain. A lower **Hunger Threshold
Multiplier** sends visitors for food earlier; a higher **Rest Threshold
Multiplier** sends them to rest earlier. **Fear Gain/Loss**, **Patience**,
**Watching Seconds**, and walking/running multipliers tune their other differences.
**Body Scale** controls the actor/collider dimensions; the Children profile starts
smaller than adult profiles.

Under **Exhibit Interests**, **Appeal Weight** weights that type's species appeal,
**Rarity Weight** adds the creature's existing rating, and **New Species/Origin
Bonus** rewards discoveries. The resulting interest is limited to 0-1.
**Interest Choice Weight** determines how strongly that interest competes with
travel distance when choosing an exhibit. **Minimum/Maximum Exhibit Reward** and
**Exhibit Reward Multiplier** scale satisfaction; **Service Reward Multiplier**
scales completed food/rest rewards. **Revisit Multiplier** scales the normal
cooldown for watching the same creature again.

**Display Name** and **Description** contain localization IDs. Edit their English
text in `Resources/Localization/localization.csv` while retaining the IDs.
Make balancing edits outside Play mode so they remain after stopping the game.

## Adjust a creature's audience appeal

Select a species asset under `Assets/Mythologicum/Resources/Creatures`. Expand
**Visitor Appeal** and adjust **Thrill**, **Wonder**, **Adventure**, and
**Children** from 0 (little appeal) to 1 (strong appeal). These are independent
design choices: changing audience appeal does not change Health, Damage, Threat,
escape behaviour or whether a creature is safe. A thrilling exhibit still needs
proper containment, and a creature popular with children can still be dangerous.

## Adult and child models

Open **Balancing Tools > People models**, or select
`Assets/Mythologicum/Resources/People/People.asset`. **Adult Visitors** supplies
the three adult types; **Child Visitors** supplies Children only. An empty pool
uses that age group's placeholder instead of borrowing a model from the other
pool. The existing **Civilians** pool remains for General visitors restored from
older parks. Staff model references remain separate.

## Smooth arrivals

In `DefaultVisitors.asset`, expand **Simulation Updates**. **Prewarm Visitor
Models** is enabled by default: the loading screen prepares each configured
visitor model and its animation resources before play. The temporary models do
not count as guests, charge admission or change the visitor mix.

**Arrival Path Cells Per Frame** defaults to 256. New guests share an entrance
path search prepared over several frames, rather than rebuilding it for every
arrival. Lower this budget if preparation causes a long frame; increase it if
guests wait noticeably at the entrance after large path changes. Results are used
only when complete, and the reachable paths and exhibit preferences stay the
same. Visitors still react immediately to danger and can leave normally.

**Reputation Refresh Seconds** still defaults to 5 simulation seconds. Arrivals
reuse that exhibition snapshot; construction changes refresh it immediately.
Admission prices continue to use the current entrance price. These settings are
shared by all four types, alongside their individual needs and interests.

## Inspect and test in a park

Click a visitor to see their type, interests and needs. While they watch a
creature, the panel also shows that exhibit's interest percentage. Adventurers
show completed species/origin discovery counts. **Park Management > Statistics**
lists current counts for all four types, plus General visitors when any are present.

For a manual check, connect several safely contained species to the entrance,
provide a kiosk and rest area, and let visitors arrive. Compare selected
visitors' choices, interest percentages and needs. Temporarily increase one
arrival weight to make that type easier to find. Save, load, and inspect the
visitors again.

Visitor type and completed species/origin discoveries persist through saves.
Only a completed watch of a still-visible, living creature records a discovery;
walking toward an exhibit or interrupting the watch does not consume its novelty.
Older saves restore existing visitors as neutral **General visitors**, preserving
their original behaviour; new arrivals use the four configured types. Discovery
history belongs to each visitor rather than the park's research or encyclopedia.

See `Tests/README.md` for automated model, native UI and save-validation helpers.
