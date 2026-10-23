# Visitor and economy prototype

Restart Play mode to use the updated prototype. Visitors arrive automatically; they are not placed from the creature menu.

## A first visit

1. Place the park entrance, then build paths from its cyan entrance tile. Visitors enter through that path tile and travel along the connected path network. Without an accessible entrance path, arrivals wait.
2. Build an enclosure beside the visitor path, add creatures and give them suitable space, trees and food. Visitors look for visible animals from the paths and pause to watch them.
3. Add **Buildings > Kiosk** and **Buildings > Rest area**. Rotate them with **R** if necessary, and connect each cyan entrance tile to the park's path network. A red closed building cannot serve visitors.
4. Wait for hunger and tiredness to develop. Hungry visitors seek a connected kiosk, spend time eating, and reduce their hunger. Tired visitors seek a connected rest area, pause there and regain energy. These facilities have limited service capacity, so one facility cannot serve an unlimited crowd simultaneously.
5. Outside construction mode, click a visitor to inspect hunger, fear, energy, satisfaction, visit duration and willingness to stay. The explanation below the numbers describes the visitor's current activity. Click elsewhere to deselect.
6. A pleasant visit increases satisfaction and preserves patience, so visitors stay longer. Hungry, exhausted or frightened visitors become dissatisfied and leave sooner. Every visit still has a maximum duration. Visitors leave through the park entrance when a route is available.

The top-left overview shows current visitors, average satisfaction, departed and lost visitors, and stranded visitors. Removing a connecting path can strand a visitor; restoring access allows routes to work again. Lost visitors are deaths, rather than ordinary departures.

## Safety

Visitors fear a nearby, visible predator when no ordinary ground barrier separates them. A Chupacabra behind a complete fence can be watched safely; an escaped Chupacabra can frighten and hunt visitors using the same prey rules as goats. Fleeing visitors may leave the path to escape danger. Fence and gate placement therefore matters even when a visitor path is close to an enclosure.

To test this, watch guests beside a safely fenced Chupacabra, then open a gap or let it climb a wooden fence. Compare their reactions once the predator is on accessible nearby ground. Spiked fences prevent Chupacabra climbing; closed staff gates also block animals. There is no staff rescue or medical system yet.

## Money

- New sessions start with prototype funds. Ticket sales on arrival and kiosk meals add income.
- Building construction spends money. Buildings also have running costs; these continue over time and can put the park into debt. A purchase cannot exceed available funds.
- Rest areas restore visitor energy for free. Each service still has a capacity limit.
- The **Infinite funds: on/off** button in the overview enables sandbox construction. While enabled, transactions leave the balance unchanged; income and expense totals continue to show activity. Turning it off restores the balance requirement for purchases.
- Construction prices, meal prices, service duration/capacity and building running costs are early balancing values. This prototype does not include a complete finance simulation, refunds, salaries or supply deliveries.

## Adjusting visitors

In the Project window, open **Assets > Mythologicum > Resources > Visitors > DefaultVisitors**. Stop Play mode before making lasting changes.

- **Arrivals** controls spawn interval, population limit and ticket price.
- **Needs** controls starting hunger/energy, their rates, and when visitors seek food or rest.
- **Movement and perception** controls walking/running, view range, nearby fear detection and decision timing.
- **Visit** controls watching time and minimum/maximum visit duration.

For a quicker food/rest test, temporarily raise **Initial Hunger** or lower **Initial Energy**, restart Play mode and watch new arrivals. Restore the values afterwards. Building-specific prices and capacity are in the existing **Main Camera > Build Mode Controller > Building Catalog** settings.

Capsules, watching pauses and service motions are placeholders. Visitors currently use the same flat grid world as creatures. Save/load, crowd simulation, queue art, benches with seated character animations and final visitor models come later.
