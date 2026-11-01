# Park management

The bottom **Park management** button is available before the park entrance is
placed. It opens Statistics, Finances, Prices & budget, and Star rating. Simulation
continues while the window is open. Close or Escape returns to the park; pressing
Escape again opens Pause.

## Money

Finances shows actual income, expenses and net cash flow during the last 60
simulation seconds. The first minute displays how much of that window has been
recorded; it does not extrapolate a single early sale into a large forecast.
Pausing stops both simulation and financial-window aging.

Operating profit subtracts building upkeep, salaries and feeder supplies. The
separate one-off investment figure includes construction, hiring, technology and
expeditions (including teams and permits). Total cash flow includes all expenses,
so a large construction purchase can make cash flow negative while the park's
ordinary operation remains profitable. Starting capital is not sales income.

Prices & budget lists the entrance, kiosks and rest areas. Edit a price and press
Apply price. New admissions and service orders use the new amount; customers
already eating or resting retain their quoted price. The current fair admission
price, acceptance and potential arrivals help judge pricing. Actual arrivals also
depend on capacity and a usable entrance. Infinite funds remains available for
sandbox play and still records transactions for statistics.

## Stars and customer reputation

Stars range from zero to five. Exhibition appeal comes from living creatures
visible from entrance-connected public paths. Higher existing creature ratings
contribute more, different mythical species give a variety bonus, and duplicate
specimens give diminishing returns. Domestic goats contribute only a small capped
amount. Neither enclosure membership nor fencing is required; fences can contain
creatures while leaving them visible. Terrain and buildings can obstruct views.

Satisfied departures improve visitor reputation; unsatisfied departures reduce
it. Visitor deaths immediately reduce reputation and add a safety penalty. Safe
operating time and later excellent visits gradually restore trust. Feedback is
recorded once per departure or death, and history is retained in saves.

Fair admission starts at 15 and increases by 5 per park star. Above that price,
fewer prospective visitors accept; at 1.8 times the fair price, admission is
refused. Reputation and stars also affect potential attendance. Food and rest
have their own star-dependent fair prices. Visitors reject excessively priced
services and prefer affordable reachable alternatives. Price complaints appear
in visitor details and the park statistics.

The window explains exhibition appeal, customer reputation, safety penalties,
review count and deaths separately. A collection of dangerous creatures can raise
appeal, while harm to visitors can still outweigh that benefit.

## Saves and testing

Recent finance history, reputation and price-feedback counters are optional
extensions to save format 3. Older saves load with an empty recent financial
window and neutral customer reputation; exhibition appeal is rebuilt from their
actual creatures and connected paths. Loading does not issue new sales or reviews.

Pure checks are in `Tests/Management`. Native interaction, rendering and save/load
checks use `Tests/Management/Unity/ParkManagementIntegrationValidation.cs` in an
isolated Unity project. The cost audit and its assumptions are documented in
`EconomyBalance.md`.
