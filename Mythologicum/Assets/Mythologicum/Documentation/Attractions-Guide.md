# Attractions: building, testing and balancing

Attractions are optional, researched services with separate admission tickets.
Visitors choose them according to their type, interest, price, travel distance
and available capacity. Hunger, tiredness and real danger still take priority.

## Unlock and build

1. Build a Research Centre, connect its entrance to the park's paths and hire a
   researcher through its information panel.
2. Open technology research and complete **Visitor Attractions**.
3. Research each attraction you want to use. **Hot Air Balloon Ride** also
   requires **Viewing Tower**.
4. Open **Building > Buildings > Attractions**, choose an unlocked attraction
   and place it with its entrance connected to the park entrance by paths.
   **Viewing Window** is also available under **Building > Fences**.

Click a placed attraction to see its occupancy, capacity, duration and accounts,
or change its ticket price. A viewing window has its own price and accounts when
selected. Income appears under **Attraction tickets** in Park Management.
Upkeep continues while a venue is unused or disconnected.

| Attraction | Starting audience and experience |
| --- | --- |
| Petting Zoo | Strongest appeal to Children; a paid entrance into a secure enclosure containing suitable creatures. |
| Viewing Tower | Strong appeal to Adventurers and Wonderlust; nearby creatures improve its panoramic experience. |
| Viewing Window | Strong appeal to Thrill seekers; visitors enter a protected 2 x 1 glass viewing building extending into an enclosure. |
| Hot Air Balloon Ride | Premium attraction for Wonderlust and Adventurers, with a larger exhibit radius. |
| Predator Feeding Gallery | Strongest appeal to Thrill seekers; requires a nearby dangerous creature and gains a bonus while it is feeding. |
| Trickster Theatre | Appeals to Children and Thrill seekers; needs a nearby fear-feeding creature such as Hitotsume to perform. |
| Cabinet of Curiosities | Strongest appeal to Wonderlust; species research improves the contribution from nearby creatures. |
| Expedition Pavilion | Strongest appeal to Adventurers; nearby creatures from origins a visitor has not discovered give a novelty bonus. |
| Clockwork Carousel | Strongest appeal to Children; a simple ride that does not require an exhibit. |

Creature-linked attractions use real living, visible creatures within their
configured radius. A feeding gallery observes feeding; it does not dispense food.
The Cabinet and Expedition Pavilion can operate without nearby creatures, but
lose their corresponding exhibit bonuses.

## Place a petting entrance

The Petting Zoo is a **fence-mounted gateway**. Build a closed enclosure first,
choose the Petting Zoo and point at a fence segment, just as with a staff gate.
The entrance snaps onto the fence and automatically faces into the enclosure.
Its cyan access tile is immediately outside the fence and needs a connected path;
an existing path can stay in place. The gateway occupies no building tile.
Use the rotate control (**R** by default) to switch sides where both are suitable.

Keep the rear fence segment in place: the gateway displays a gate there while
the fence remains an animal barrier. A staff gate or viewing window cannot occupy
that same segment. Add a separate staff gate for ordinary keeper access.
Older saved petting entrances retain their original building placement and access
tiles. Newly placed entrances use the fence-mounted layout. The mounted arch
follows the ground height without flattening a building foundation.

The enclosure needs at least one suitable animal. **Goats, Wolpertinger and
Unicorns** start as pettable species. Every living, uncontrolled animal inside
must be pettable and within the gateway's **Maximum Creature Threat** setting.
An unsuitable animal or a breach makes the attraction unavailable. Paying
visitors enter through the gateway, approach an animal and leave through the
gateway. Authorized contact does not frighten suitable animals or make staff
treat them as escaped exhibits. Real predators remain dangerous.

## Place a viewing window

Choose **Viewing Window** and point at a straight stretch of two fence segments.
The building snaps to that fence, with **two tiles along the boundary and one tile
of depth inside the enclosure**. Keep both interior tiles clear. The cyan access
tile outside the fence needs a path connected to the park entrance.

Visitors enter through the outside door, watch creatures from the protected glass
bay, and return to the same path. The viewing building occupies its two interior
tiles and remains separate from ordinary animal movement. It provides an indoor
view rather than access to roam around the enclosure. Existing staff gates,
petting entrances and other viewing windows cannot share its attachment segments.
The building has a foundation like other occupied buildings.

Older saved windows installed directly on a single fence remain supported with
their original price, accounts and viewing behaviour. Place a new window to use
the building version.

## Visitors inside attractions

Visitors use the entrance and move to passenger or viewing positions inside each
attraction. Tower guests use the stairs to reach the observation platform, where
they remain visible during the visit, then descend to the public path. Galleries,
theatre audiences, the Cabinet of Curiosities and the Expedition Pavilion use
positions within their building footprints. Carousel passengers move with its
seats, and balloon passengers travel in the basket.

Balloon and carousel rides carry groups through three stages:

1. **Boarding:** the ride stays at its boarding position while passengers enter.
   A full group can depart once everyone has finished boarding. A smaller group
   departs after the boarding window, with at least one ready passenger.
2. **Riding:** the same group travels together. The balloon ascends, follows its
   route, then returns and lands. New arrivals wait outside and cannot join a
   flight or a moving carousel.
3. **Unloading:** passengers return to the public entrance. The ride takes its
   next group only after the last passenger has physically exited.

Hunger, tiredness or an interrupted attraction can end a visit early. Passengers
already on a moving ride return and land before unloading; they do not jump out
mid-flight. An unfinished visit does not charge a ticket. Demolition of these
passenger attractions is blocked while people are inside, so wait for them to
exit before removing the building.

The models are still procedural placeholders. Passenger movement and ride phases
use simulation time, so pausing also pauses boarding, flights and unloading.

## Trickster performances

Put a Hitotsume or another fear-feeding creature within the theatre's exhibit
radius and line of sight. Keep public paths safely separated from its enclosure.
A free, awake performer can make a controlled scare animation, relieving its
hunger without applying a panic impulse to the audience. Busy performers are
retried; a visit with no performance is cancelled without a ticket charge.
Normal dangerous encounters still trigger ordinary fear and escape behaviour.

## Change the balance in Unity

Stop Play mode before editing so changes persist. Select these assets in the
Project window, then expand their fields in the Inspector:

| Asset | What to change |
| --- | --- |
| `Assets/Mythologicum/Resources/Buildings/DefaultBuildings.asset` | Expand an attraction's entry: footprint, construction cost, upkeep, default **Attraction Price**, **Service Capacity**, **Service Seconds**, and its nested **Attraction** settings. |
| `Assets/Mythologicum/Resources/Attractions/ViewingWindow.asset` | New viewing-building height, construction cost, ticket price, capacity, duration, upkeep, frame/glass colours and attraction settings. The same prices and attraction settings also tune legacy fence windows. Also available through **Mythologicum > Gameplay > Balancing Tools > Viewing windows**. |
| `Assets/Mythologicum/Resources/Visitors/DefaultVisitors.asset` | Attraction decision interval, gap between visits, travel and price penalties, plus existing visitor needs and type settings. |
| `Assets/Mythologicum/Resources/Progression/DefaultProgression.asset` | Research prices, work durations, prerequisites and building unlocks. |
| Species assets in `Assets/Mythologicum/Resources/Creatures` | **Pettable**, **Threat**, **Rating**, origin and fear-feeding behaviour. |

Under **Attraction**, **Appeal** contains independent 0-1 preferences for each
visitor type. **Minimum Interest**, **Decision Weight** and **Maximum Price**
affect selection. The affordable price limit is scaled by the visitor's interest,
so a ticket can be too expensive for one type but suitable for another.
**Satisfaction Reward**, **Exhibit Radius/Bonus**, **Novelty Bonus**,
**Feeding Bonus** and **Revisit Seconds** tune the result and repeat visits.
Theatre hunger relief/cooldown and balloon height/radius are adjustable here too.

Passenger and group settings are also under each building's nested **Attraction**
entry in `DefaultBuildings.asset`:

| Setting | Effect |
| --- | --- |
| **Entry Seconds** (`entrySeconds`) | Time to move from the public entrance to a passenger position. |
| **Exit Seconds** (`exitSeconds`) | Time to return from that position to the public entrance. |
| **Boarding Window Seconds** (`boardingWindowSeconds`) | Group loading window after the first person starts boarding. A full, ready group may leave sooner; zero removes the extra wait. |
| **Passenger Spacing** (`passengerSpacing`) | Spacing between passenger positions, fitted to the attraction's available area. |
| **Balloon Ascent Fraction** (`balloonAscentFraction`) | Fraction of the trip used for ascent, and again for descent; the remainder is the flight between them. |
| **Carousel Turns** (`carouselTurns`) | Whole revolutions per group ride, returning the seats to their boarding positions. |

**Service Capacity** sets the number of passengers. **Service Seconds** sets the
viewing or ride duration after entry; boarding and exit movement have their own
timings above.

Upkeep fields are **per simulation second**; multiply by 60 for the displayed
per-minute cost. In-game ticket edits belong to the individual placed attraction.
Asset default prices are used for newly placed instances.

## Manual test checklist

1. In a park with a connected entrance and researchers, verify an attraction is
   unavailable before its research completes, then unlock and place it.
2. Start with an affordable Carousel. Watch visitors enter, ride on the moving
   seats and leave before the next group boards. Check that completed ticket
   income and upkeep reach its accounts.
3. Add a fenced enclosure with goats and a correctly oriented Petting Zoo.
   Watch a visitor enter, meet an animal, pay on completion and return outside.
4. Add a Viewing Window to two straight fence segments, with two clear tiles
   inside and a connected visitor path outside. Watch visitors enter the protected
   bay, finish viewing and return outside. Confirm animals stay out of the bay
   and the building earns its own ticket revenue.
5. Try a gallery near a dangerous feeding exhibit and a theatre near Hitotsume.
   Check that visitors enter their viewing positions, their preferences differ,
   and controlled performances relieve the performer's hunger.
6. Add a Viewing Tower and watch visitors use its platform. Add a balloon ride
   and watch one complete group board, fly, land and unload while later arrivals
   wait outside. Pause during flight to check that both basket and passengers stop.
7. Save with people on the tower or in an airborne balloon, load it and let their
   visits finish. Check the passenger positions, shared ride phase, original
   ticket quotes and payment once on completion. Window accounts and revisit
   history also remain saved.

Ride saves retain the group's phase, elapsed route time, seats and quoted prices.
They also snapshot ride timing and capacity. If these balance settings change,
the loaded group finishes with its original configuration; the next group uses
the new defaults after everyone has left.

Visitors reserve capacity while approaching, and pay once when a visit completes.
Changing a price during an active visit retains that visit's quote. Cancelled,
disconnected or demolished attractions do not charge an unfinished ticket.
Visitors already inside a demolished petting gateway retain an exit route.
