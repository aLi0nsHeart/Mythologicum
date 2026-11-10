# Park operations guide

These tools help you run, inspect and balance a park. They use the existing
simulation and saved park data. No scene editing is required.

## Letters and safety alerts

Open **Mail** using the letter icon in the bottom toolbar. Its alert artwork stays
visible while unread letters remain. Opening the inbox leaves letters unread;
click an individual letter to mark it read, or choose **Mark all read**. The
**All letters / Unread only** button changes the list filter. Letters with a
location move the camera there when clicked.

Safety letters show **WARNING** or **CRITICAL** before their title. Alongside the
existing fence, expedition and research notices, the park now monitors:

- A creature leaving an area that previously contained it.
- A dangerous creature with an ordinary ground route to an entrance-connected
  visitor path.
- Injured visitors and visitor deaths.
- Goat feeders that stay empty.
- Staff members who stay blocked while trying to work.

Placing a creature outside a fence does not itself count as an escape. New
placements and transport receive a short grace period before public-path checks.
An unresolved problem sends one letter; recovery and a later recurrence can send
another after the cooldown. Loading a park establishes a fresh monitoring baseline
so existing conditions do not immediately flood the inbox.

Edit these thresholds, delays and individual alert switches in **Mythologicum >
Gameplay > Balancing Tools > Alerts**. The asset is
`Resources/Management/DefaultAlerts.asset`.

## Map overlays

Open **Park Management > Map overlays**, then choose a view. Park Management closes
so you can inspect the map. The temporary legend includes a **Hide** button.

| View | Colours and meaning |
| --- | --- |
| Path connections | Teal paths connect to the main entrance; red paths do not. |
| Enclosure space | Blue cells belong to a secured area; red cells belong to a breached area. |
| Staff access | Teal ground is reachable from open workplaces with hired staff; red ground is unreachable. |

The views follow actual path connections, enclosure detection and staff navigation,
including gates and terrain. An enclosure overlay describes the physical space; it
does not assign a creature to an enclosure.

Use **Balancing Tools > Overlays & history** to edit overlay colours, height above
ground, refresh interval and construction-history capacity. These settings live in
`Resources/Management/DefaultOperations.asset`.

## Named saves and autosaves

**Save game** in Pause opens the save browser. Enter a park name and choose
**Create new save** to add a manual slot. Choose **Overwrite** beside an existing
manual save to replace it after confirmation. Each replacement keeps the previous
version as that slot's backup.

**Load game** in Pause and **Load Game** in the main menu open the same browser.
Each row shows the name, save time, map dimensions and funds. Loading from inside a
park asks for confirmation because it replaces the current running park. When a
readable backup exists, **Load backup** loads that previous version. A damaged slot
does not hide the other saved parks. The earlier single `park-save.json` remains
available as a legacy save.

Autosaving is enabled by default. It rotates through **three separate autosave
slots every five minutes of active real play time**. Pausing and paused menus do
not advance the timer; choosing 2x or 3x simulation speed does not shorten it.
Autosaving briefly displays its result in the HUD. Manual saves keep their own
slots.

Adjust **Balancing Tools > Autosaves**, or select
`Resources/Saving/DefaultSaveSettings.asset`. Controls include the enable switch,
interval, number of rotating slots, failure retry delay and HUD message duration.
Saves use Unity's persistent data directory, separate from project assets.

## Play speed, shortcuts and interface scale

The small controls near the bottom centre pause or resume the simulation and
select its three speeds. The standard bindings are:

| Action | Default key |
| --- | --- |
| Pause or resume simulation | Space |
| Normal / double / triple speed | 1 / 2 / 3 |
| Pause menu | Escape |
| Camera movement | W, A, S, D; arrow keys also work |
| Camera rotation | Q / E |
| Buildings / paths / fences / creatures | B / P / F / C |
| Enclosures / terrain / demolish | H / T / X |
| Staff gate / rotate building | G / R |
| Undo construction | Ctrl+Z |

Camera movement and interface interaction remain responsive when the simulation
is paused. Escape first closes an open menu or cancels the active tool.

Open **Settings > Keyboard shortcuts** to change an action's primary key. Click
its binding, press the new key, and read the result in the capture window. Duplicate
bindings are refused. Escape cancels capture and remains available for closing
menus; arrow keys remain alternate camera controls. Camera zoom uses the mouse
wheel. **Reset defaults** restores the standard settings and shortcuts.

The **UI scale** slider in Settings changes the whole interface. Font, colour,
artwork and authored layout controls remain in the [UI designer guide](UI-Designer-Guide.md).
Settings persist on this computer through Unity's PlayerPrefs.

Design defaults for the three speed values, player UI-scale range and speed-dock
geometry are in **Balancing Tools > Play controls**, using
`Resources/Interface/DefaultPlayControls.asset`.

## Construction Undo

Click **Undo construction** or press **Ctrl+Z** to undo the most recent recorded
construction action. A path or fence route counts as one action; a continuous
terrain-brush stroke also counts as one. Building placement, gates and habitat
objects participate in the same history. Paid construction returns its original
purchase expense when successfully undone.

Undo checks the current park first. It refuses an action if a creature or person
is using the affected ground, a fence changed or took damage, a building gained
personnel, customers, animals or research, or restoring terrain would disturb a
foundation or make an existing path too steep. The button's hover text explains
the current reason. Clear the problem and try again when appropriate.

Undo changes construction only. Creature activity, staff jobs and ongoing park
expenses continue. **Demolition clears construction history**, and loading a save
starts a fresh history. History is limited to 20 actions by default and is not
stored in save files. There is no Redo control.

## Validate balancing data

Open **Mythologicum > Gameplay > Validate gameplay data**, or use the validation
button in Balancing Tools. It checks the current assets without changing them.
Click a result to select its source asset in the Project window.

Checks include missing references, invalid numeric values, species and progression
identities, building footprints and entrances, incompatible sleep requirements,
missing research prerequisites and prerequisite cycles. An unused future region
without species produces a warning so you can distinguish unfinished content
from data that prevents the current game from working.

Run validation after adding content or changing several related settings. Then
test the behaviour in Play mode: a valid configuration can still need balancing.
The [gameplay balancing guide](Gameplay-Balancing-Guide.md) explains the individual
settings categories.

## Repeatable performance measurements

Stop Play mode, then open **Mythologicum > Gameplay > Run large park benchmark**.
Save any untitled scenes first; the benchmark leaves your open scenes unchanged.
Choose a seed, map size, warm-up ticks, measured ticks and three population sizes.
Click **Run benchmark**. **Cancel benchmark** stops it and removes its temporary
objects. The benchmark creates a temporary additive scene and cleans it up when
finished; it does not replace the authored park scenes or their assets.

The defaults use 51, 142 and 274 total actors across creatures, visitors and staff.
The report records each subsystem's mean, median, 95th percentile and maximum CPU
tick time, together with the hardware, Unity version and actual populations.
Results are JSON and CSV files in `LocalCache/Performance` inside the project;
`LatestBenchmark.txt` points to the newest report. These generated files are local
working data.

The fixture uses connected paths, flat ground and placeholder actors. It exercises
movement, perception, navigation and staff work with stable populations. Rendering,
the interface, imported artwork, combat effects and large construction changes
are outside this measurement. Use it to compare simulation changes under the same
conditions; inspect a representative playable park separately for frame rate and
visual performance.

## A short Unity test session

1. Start Play mode, create a park and place the entrance plus a short path. Open
   each map overlay, inspect its legend, then hide it.
2. Add another short path route and press Ctrl+Z. Confirm that the route disappears
   while the earlier entrance and paths remain.
3. Select 2x speed, pause with Space, resume, then open and close Pause. The selected
   running speed should return. Change UI scale and one shortcut in Settings,
   test the new key, then restore your preferred settings.
4. Save to a new named slot, make a visible construction change, and load that
   slot after confirming. Check that the saved construction returns. Overwrite the
   slot once to make its previous version available through **Load backup**.
5. For a quick autosave test, stop Play mode, temporarily reduce the autosave
   interval in Balancing Tools, save the asset, and start a new session. Leave the
   park running until its HUD message appears, then inspect Load game for the
   autosave. Restore the desired interval afterwards.
6. When a safety or operations letter arrives during normal play, open Mail and
   confirm it remains unread until clicked or marked read. Click a located letter
   to inspect the affected area.

Stop Play mode before running the data validator or benchmark for your next
balancing comparison.
