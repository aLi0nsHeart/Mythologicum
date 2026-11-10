# Economy balance audit

Costs are expressed per simulation minute. The actual GameScene catalogue and
`BuildingDefinition.Defaults()` use the same new rates; building geometry, model
assignments, construction prices, staff hiring fees and service prices are retained.

| Building | Construction | Base upkeep/min | Salary per employee/min |
| --- | ---: | ---: | ---: |
| Park entrance | 0 | 3.0 | - |
| Expedition centre | 500 | 5.4 | - |
| Kiosk | 250 | 3.0 | - |
| Goat dispenser | 150 | 2.4 | - |
| Rest area | 120 | 1.8 | - |
| Research centre | 600 | 6.0 | 9.0 |
| Mercenary headquarters | 450 | 4.8 | 12.0 |
| Zookeeper station | 350 | 3.6 | 6.0 |

All eight buildings total 30/min base upkeep. One researcher, one mercenary and
one keeper add 27/min. A 12-unit supply delivery refills a six-goat dispenser when
stock reaches two. At one goat per 20 seconds, uninterrupted consumption therefore
costs approximately 9/min, rather than 6/min: four consumed goats trigger a top-up.
Existing live-goat limits and pauses can reduce actual supply spending.

The resulting small-park overhead is about **66/min**. The old uniform upkeep and
salaries produced **147/min** under the same assumptions (48 base + 90 wages + 9
supplies). Fully staffing all three workplaces with five employees each now costs
about 174/min including base upkeep and continuous supplies, versus 507/min before.

## Revenue assumptions

The actual visitor profile offers a potential arrival every eight seconds:
7.5 arrivals/min at neutral starting reputation. An accepted 15-unit ticket gives
112.5/min before population or entrance constraints. Even assuming every visitor
stays the maximum 300 seconds, the 30-visitor capacity permits a long-run turnover
of 6/min, or 90/min in admission revenue. This leaves about 24/min over the modest
66/min overhead before food sales.

Very poor satisfaction without deaths reduces the potential demand multiplier to
0.7, giving 78.75/min admission revenue at accepted starter prices. Death penalties,
excessive prices, disconnected paths and blocked entrances can reduce revenue
further: profitable operation still requires a functioning park.

At five stars, fair admission is 40. Even the conservative six visitors/min can
produce 240/min, enough to cover the 174/min fully staffed scenario. This is a
planning estimate, not a guarantee of attendance.

A kiosk starts at 8 per meal and 3/min upkeep; 0.375 meals/min covers its base
upkeep. Its two service slots and three-second service time can handle baseline
arrivals. A free rest area costs 1.8/min and supports satisfaction and longer stays;
an optional accepted price of 3 covers its base upkeep at 0.6 completed rests/min.
Food/rest income is charged on completed service, so interrupted visits do not
create sales. Feeder supplies are an expense, not a visitor income source.

## Starting capital and progression

Starting capital remains 5,000. Buying all eight buildings costs 2,420; one hire in
each workplace adds 150. A team, North America permit and survey cost 720. The
food and rest technologies add 350. Together this is 3,640, leaving **1,360** before
optional advanced fences and subsequent expedition attempts. At 66/min, this is
over 20 minutes of overhead even with no admissions. The initial park needs fewer
buildings because kiosk and rest construction follow their technology unlocks.

`BalanceChecks` reads the actual visitor asset, checks startup and capacity
assumptions, and runs the real finance/building accounting for ten simulated
minutes with ticket sales and physical feeder-stock top-ups. It also checks that
maximum staffing requires a developed park, rather than making every employee
slot immediately affordable. Further playtesting should tune these values as
visitor populations, additional services and species are expanded.
