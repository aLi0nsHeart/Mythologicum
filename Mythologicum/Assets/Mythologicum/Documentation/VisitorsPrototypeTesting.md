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

Visitors fear a nearby, visible creature with higher threat when no ordinary ground barrier separates them. A Chupacabra behind a complete fence can be watched safely; an escaped Chupacabra can frighten and hunt visitors using the same prey rules as goats. Harmless Hitotsume can also frighten people without dealing damage. Friendly park staff do not trigger visitor fear. Fleeing visitors may leave the path to escape danger. Fence and gate placement therefore matters even when a visitor path is close to an enclosure.

To test this, watch guests beside a safely fenced Chupacabra, then open a gap or let it climb a wooden fence. Compare their reactions once the predator is on accessible nearby ground. Spiked fences prevent climbing but can take physical damage; reinforced fences resist creature attacks and sanctified fences block phasing. Closed staff gates retain their barrier's protections.

Mercenaries can respond to escaped creatures, while zookeepers heal injured animals, repair standing fences, deliver feeder supplies, and handle sedation and transport. Staff need reachable routes and suitable gates. See [BuildingManagement.md](BuildingManagement.md) for the new building panels, hiring and care workflow.

## Money

- New sessions start with prototype funds. Ticket sales on arrival and completed visitor services add income to their individual buildings.
- Outside construction mode, click a placed entrance, kiosk or rest area to edit its current price. Prices belong to that particular building. New sessions start with entry tickets at 15, kiosk meals at 8 and rest free; each rest area can also charge an editable price. Visitors already eating or resting keep the price quoted when they began that service.
- Building construction spends money. Buildings also have running costs and employee salaries; these continue over time and can put the park into debt. A purchase cannot exceed available funds.
- Each building panel shows its upkeep, revenue, expenses and operating profit. Operating profit excludes the construction purchase and includes recorded hiring and supply-delivery costs. Services retain their capacity limits.
- The **Infinite funds: on/off** button in the overview enables sandbox construction. While enabled, transactions leave the balance unchanged; income and expense totals continue to show activity. Turning it off restores the balance requirement for purchases.
- Construction prices, service duration/capacity, salaries, supply-delivery costs and building running costs are early balancing values. Personnel and finite goat-feeder supplies are described in [BuildingManagement.md](BuildingManagement.md).

## Adjusting visitors

In the Project window, open **Assets > Mythologicum > Resources > Visitors > DefaultVisitors**. Stop Play mode before making lasting changes.

- **Arrivals** controls spawn interval and population limit. Current ticket prices are set on the placed entrance's information panel.
- **Needs** controls starting hunger/energy, their rates, and when visitors seek food or rest.
- **Movement and perception** controls walking/running, view range, nearby fear detection and decision timing.
- **Visit** controls watching time and minimum/maximum visit duration.

For a quicker food/rest test, temporarily raise **Initial Hunger** or lower **Initial Energy**, restart Play mode and watch new arrivals. Restore the values afterwards. Starting service values, capacity, upkeep and staff costs are in **Main Camera > Build Mode Controller > Building Catalog**; edit current per-building prices through its information panel during play.

Visitors use the imported civilian models, while watching pauses and service motions remain placeholders. Visitors currently use the same flat grid world as creatures. Save/load, crowd simulation, queue art and benches with seated character animations remain later work.
