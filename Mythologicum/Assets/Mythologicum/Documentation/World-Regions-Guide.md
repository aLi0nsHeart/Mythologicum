# Permanent expedition regions

Use these **23 regions** for Mythologicum's world map and all future creature
additions. This is the game's agreed geographic scheme, based on the designer's
four reference maps, with Oceania and Antarctica added explicitly. Country and
cultural origin labels on species remain independent of expedition regions.

| Map group | Permanent regions |
| --- | --- |
| Americas | North America; Central America; Caribbean; South America |
| Europe | British Isles; Northern Europe; Western Europe; Eastern Europe; Southern Europe |
| Africa | North Africa; West Africa; Central Africa; East Africa; Southern Africa |
| Asia | West Asia; Central Asia; South Asia; East Asia; Southeast Asia; North Asia |
| Oceania | Australia & New Zealand; Pacific Islands |
| Antarctica | Antarctica |

The supplied European map places Germany, Austria and Switzerland in Western
Europe, and Poland and the Baltic states in Eastern Europe. Northern Europe
includes Iceland, Denmark, Norway, Sweden and Finland. The British Isles are a
separate expedition region. The supplied Asian map places Mongolia, China and
Japan in East Asia. These conventions determine game regions even where other
geographic classifications differ.

Oceania's Pacific Islands group covers the island areas outside Australia and
New Zealand. Antarctica has its own map group. Regions without assigned creatures
remain visible for future content; their expedition permits cannot be purchased.

## Current creature distribution

| Region | Current species |
| --- | --- |
| North America | Chupacabra, Wendigo, Thunderbird, Kalupelik |
| Central America, Caribbean, South America | Chupacabra |
| Northern Europe | Nökk |
| Western Europe | Wolpertinger, Unicorn, Waldschrat, Tatzelwurm |
| West Africa | Ninki Nanka |
| Central Africa | Mokele-Mbembe, Abada |
| East Africa | Nandi Bear, Dingonek, Marozi |
| Southern Africa | Grootslang, Inkanyamba, Aigamuxa |
| West Asia | Manticore |
| East Asia | Hitotsume, Yurei, Oni, Kappa, Allghoi Khorkhoi |

All 24 mythological species remain available, plus the domestic goat outside
the expedition catalogue (25 species assets in total). The other eleven regions
are currently empty. See [North American creatures](North-American-Creatures-Guide.md)
for the new profiles and aviary/aquarium care. Bigfoot, Yeti and the Loch Ness
Monster remain reserved for DLC and are absent from the base expedition pools.

Mongolia, Persia, Central Europe and Yokai can still appear as species origins.
For example, Allghoi Khorkhoi retains **Mongolia** as its origin while being
found through **East Asia** expeditions. Adding a creature from a new country
means assigning it to one of the existing regions.

## Designer settings

Select **Assets > Mythologicum > Resources > Progression > DefaultProgression**.
Under **Rules > Regions**, each entry has its stable ID, localized name,
continent group, permit cost, map position and species IDs. Adjust prices,
positions and creature pools there. Keep the agreed region IDs and groups.
The large world overview shows all 23 numbered region markers together.
Hover a marker for its localized name; select it to show its permit, creatures
and mission controls in the compact right column. Teams and active expeditions
remain available along the bottom. There is no continent filter to hide regions.
The existing UI layout and typography presets control the panel and its text;
region **Map Position** still controls each marker's geographic anchor.

`Scripts/Progression/WorldRegionCatalog.cs` contains the authoritative region
list and fallback defaults. The serialized asset must match it geographically;
designer balancing can change costs and species pools. Names use
`expedition.region.*` and `expedition.continent.*` localization IDs. New strings
have English source text and use the existing English fallback in untranslated
languages.

## Existing saves

Old regional access and active expeditions migrate when loaded:

| Former region | Current region |
| --- | --- |
| Central Europe | Western Europe |
| Japan, China, Mongolia | East Asia |
| Middle East | West Asia |
| Combined West & Central Africa | West Africa and Central Africa |

Merged access is counted once. An old combined African permit retains access to
both new regions. Its active Mokele-Mbembe and Abada expeditions move to Central
Africa; other expeditions remain in West Africa. New West Africa permits only
unlock West Africa. Mission timing, assigned teams, outcomes, rewards and owned
specimens remain intact. New saves write only the permanent IDs and a region
schema marker so a later load does not repeat the old split.

## Quick Play-mode check

1. Open an expedition centre. Check that all 23 world-map markers are visible
   together and that hovering them reveals the correct region names.
2. Select East Asia and confirm both the Japanese creatures and Allghoi Khorkhoi
   are offered at the relevant mission difficulties.
3. Compare West Africa's Ninki Nanka with Central Africa's Mokele-Mbembe and
   Abada. Empty regions should explain that no species are assigned.
4. Load an older park with permits or active missions. Check migrated access,
   remaining mission time and specimen stock, then save and load again.
