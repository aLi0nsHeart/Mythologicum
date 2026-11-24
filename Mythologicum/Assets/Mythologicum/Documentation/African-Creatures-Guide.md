# African creatures and the Manticore

The permanent map has five African regions: North, West, Central, East and
Southern Africa. The nine existing African species populate four of them;
North Africa is reserved for future creatures. The Manticore is available
separately through West Asia. See [World Regions](World-Regions-Guide.md) for
the fixed global list and migration of older permits and expeditions.

| Expedition region | Species asset | Rating | Starting husbandry design |
| --- | --- | --- | --- |
| Central Africa | MokeleMbembe | 4 | Large swimming herbivore; water shelter and a dry grassy/thicket bank for grazing |
| West Africa | NinkiNanka | 3 | Wetland predator; water shelter, vegetation and hunting space |
| Central Africa | Abada | 1 | Small social forest herbivore; tree shade, grazing and a total group of 2–6 |
| East Africa | NandiBear | 3 | Solitary forest predator; cave shelter |
| East Africa | Dingonek | 4 | Large aquatic predator; water shelter and reachable shores |
| East Africa | Marozi | 2 | Social spotted feline predator; grass/thicket, tree shade and a total group of 2–5 |
| Southern Africa | Grootslang | 5 | Large cave-dwelling predator; substantial space and reinforced containment |
| Southern Africa | Inkanyamba | 3 | Water serpent; water shelter and a mixed shoreline habitat |
| Southern Africa | Aigamuxa | 2 | Solitary desert predator; sandy open ground and ground sleep |
| West Asia | Manticore | 4 | Carnivore comfortable alone or paired; cave shelter, climbing and open habitat |

These ratings, diets, group sizes, habitat requirements and abilities are
editable starting designs. They do not claim to reproduce one definitive
folklore account. All ten have procedural placeholder bodies; each asset's
**Visual Prefab** can replace its appearance independently of its simulation.

## Designer controls

Select a species under **Assets > Mythologicum > Resources > Creatures** while
Play mode is stopped. Health, damage, threat, visitor appeal, hunger, thirst,
movement speeds, biome fractions, minimum space, herd limits and shelter needs
are all species data. Group sizes above include the animal itself; the
**Minimum/Maximum Companions** fields exclude it.

The four swimmers use **Aquatic** settings for depth, swimming speed, preferred
water area and visible body offsets. Mokele-Mbembe has **Drag Prey To Water**
disabled and uses ordinary herbivore grazing. Keep dry **Grass** or **Thicket**
reachable from its pond: submerged ground cannot supply grazing food. Its
**Behaviour > Grazing Search Steps** is increased so it can find a bank from
within a larger pool. An entirely water-filled exhibit will not feed it.

Ninki Nanka, Dingonek and Inkanyamba use the existing aquatic capture-and-drag
system. Their water and land routes still respect fences. Grootslang uses the
existing agitation-based, one-strike fence attack rules; reinforced fences
resist it. Manticore uses ordinary climbing, which spiked fences block.
Existing shelter, drinking, research and containment systems also apply to the
new species. Their size is visual and statistical: navigation still uses one
grid cell, including for large animals and cave entrances.

Edit **Resources > Progression > DefaultProgression** to change regional pools,
access costs or mission difficulty ranges. Species ratings control eligibility:
the default Survey finds ratings 1–2, Capture 2–3, and Dangerous 3–5. New species
start with zero inventory; successful expeditions grant placeable specimens.
The construction palette and encyclopedia discover the imported species
automatically. Names and origin labels use IDs in `Resources/Localization/localization.csv`;
only their English source cells are filled in.

## Play-mode check

1. Open a park with an expedition centre. Buy regional access and dispatch an
   appropriate mission; verify that the returned specimen appears in Creatures.
2. Give Mokele-Mbembe a sufficiently deep pond, an accessible bank and dry
   grass/thicket. Watch it leave the water to graze and return to water to sleep.
3. Provide caves for Nandi Bear, Grootslang and Manticore, and trees for Abada
   and Marozi. Check shelter and habitat information in the creature panel.
4. Compare Manticore climbing at wooden and spiked fences, and an agitated
   Grootslang's one-strike attacks at ordinary and reinforced fences.
5. Save and load with new species, specimen stock and research progress. Their
   identities, needs, water sleep and shelter records should persist.

Native integration test setup is listed in `Tests/README.md`.

## Regional and visual references

These are fictional game animals inspired by several kinds of material:
folklore, historical travel accounts, cryptozoology and later popular depictions.
Combat values, diets, exhibit needs and all husbandry mechanics above are game
design choices. Flight, venom, weather control and special foot-eye perception
are not implemented by these profiles.

- **Mokele-Mbembe:** the [Oxford academic chapter on Congo cryptozoology](https://academic.oup.com/columbia-scholarship-online/book/23261/chapter-abstract/184193871?login=false)
  discusses Congo Basin accounts and the modern long-necked dinosaur image.
- **Ninki Nanka:** [The Gambia's official country guide](https://visitthegambia.com/wp-content/uploads/2017/08/Official-country-guide.pdf),
  page 61, describes the legendary dragon associated with River Gambia creeks.
- **Abada:** [Merolla's historical Congo travel account](https://upload.wikimedia.org/wikipedia/commons/a/ad/Breve%2C_e_succinta_relazione_del_viaggio_nel_regno_di_Congo_nell%27Africa_meridionale%2C_fatto_dal_P._Girolamo_Merolla_da_Sorrento_%28IA_gri_33125011120454%29.pdf),
  illustrated page 60, supplies a horned-animal reference. Historical usage and
  horn counts vary; the game's small two-horned herbivore is an adaptation.
- **Nandi Bear and Dingonek:** [Hobley's 1913 account, “On some unidentified beasts”](https://journals.co.za/doi/10.10520/AJA19376812_297)
  records East African accounts. These colonial descriptions are variable and
  should not be treated as a complete representation of local traditions.
- **Marozi:** the [SMBE 2025 abstract book](https://www.ezices.cn/smbe2025/files/SMBE_2025_Abstract_Book.pdf),
  page 232, reports preliminary work on a spotted Kenyan lion specimen. Our
  entry is cryptid-inspired fiction, not a claim of a confirmed separate species.
- **Grootslang:** [Cornell's 1920 *The Glamour of Prospecting*](https://archive.org/details/glamourprospect00corngoog),
  page 142, recounts a giant serpent in southern Africa. The game's elephant-like
  head and tusks adapt the familiar modern hybrid depiction.
- **Inkanyamba:** the [Water Research Commission's *Water Wheel*](https://www.wrc.org.za/wp-content/uploads/mdocs/WaterWheel_2011_01_Jan-Feb.pdf),
  page 11, associates the serpent with Howick Falls;
  [Howick Tourism](https://www.howicktourism.co.za/about.html) describes the horse-headed image.
- **Aigamuxa:** the [publisher's encyclopedia preview](https://api.pageplace.de/preview/DT0400.9781317459385_A24376194/preview-9781317459385_A24376194.pdf),
  page 14, describes southern African Khoikhoi/Khoekhoe man-eaters with eyes in
  their feet. This motivates the placeholder's appearance; ordinary perception
  rules still control it.
- **Manticore:** [The Metropolitan Museum of Art](https://www.metmuseum.org/ko/essays/animals-in-medieval-art)
  identifies the Persian legend and its human face, lion body and scorpion tail.
  Its in-game origin remains **Persia**.
