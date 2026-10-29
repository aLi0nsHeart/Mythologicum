# Expeditions, research and letters

New parks begin with an entrance, expedition centre, research centre, mercenary headquarters, zookeeper station and goat dispenser available. Basic paths, wooden fences, staff gates, trees and gravestones are available. Kiosks, rest areas and advanced fences need technology research. All manually placed creature stock starts at zero, including after enabling Infinite funds. Goats are supplied by the dispenser.

## Expeditions

1. Place the park entrance and connect paths to an expedition centre.
2. Click the expedition centre to open its world map. Closing the map returns to the building's normal financial information.
3. Buy a team (first team 250; each additional team costs 100 more). Teams are shared between expedition centres, with a maximum of 12.
4. Select North America, South America, or Japan and buy its 350 permit. Permits are permanent and shared by the park.
5. Choose a mission and assign available teams. The screen shows its full price, duration, success chance and possible creatures before dispatch.

| Mission | Base success | Duration | Cost per team | Creature ratings |
| --- | --- | --- | --- | --- |
| Survey | 80% | 60 seconds | 120 | 1-2 |
| Capture | 60% | 120 seconds | 200 | 2-3 |
| Dangerous | 35% | 180 seconds | 350 | 3-5 |

Each additional team adds 12 percentage points to success, capped at 95%. Each successful mission returns two creatures of one eligible species. Teams return on failure too. Multiple missions can run at once, but a team can only join one active mission. Journeys continue if their expedition centre closes or is demolished.

Chupacabra (rating 2) currently appears in the Americas. Japan offers Hitotsume (1), Yurei (3) and Oni (5), depending on mission difficulty. Other map regions are visible but cannot be purchased until they have a creature roster. Rating is separate from combat threat.

A successful return adds stock to the bottom **Creatures** menu. For example, `Chupacabra x2` allows two placements. Invalid placement does not consume stock. Acquiring a creature does not spawn it automatically. Normal goat dispensers remain independent of this inventory.

## Technology research

Click a research centre, hire researchers and press **Technology research** near the top of its information panel. The rest of the building information is scrollable.

Each centre can run one technology project, and the same technology cannot be researched twice. Projects cost money up front. Each hired researcher travels to that centre's entrance and supplies one researcher-second of work per second. Several researchers speed up the project. Disconnecting the centre pauses the work; reconnecting resumes it. Dismissing all researchers pauses it too. Demolishing the centre cancels its project without refund.

- **Food services**: unlocks kiosks (200, 30 researcher-seconds).
- **Visitor comfort**: unlocks rest areas (150, 25).
- **Spiked fences**: unlocks spikes (250, 45).
- **Reinforced fences**: requires spikes, unlocks heavy barriers (600, 90).
- **Sanctified fences**: requires spikes, unlocks spirit-resistant barriers (700, 100).
- **Staff training**: makes creature research, technology work, healing and fence repair 25% faster (450, 60).
- **Expedition equipment**: adds 10 percentage points to future mission success (350, 45).
- **Expedition logistics**: requires equipment; future journeys take 25% less time (500, 60).

Creature observation and discovered care descriptions remain separate. A centre's researchers resume their chosen creature study after its technology project finishes. Other centres can keep studying creatures meanwhile. Expedition purchases and project costs appear in the relevant building's operating expenses.

## Letters

The bottom-right envelope button shows a red **!** when unread letters arrive. Open it to read the inbox; existing letters are marked read. New letters arriving while it is open remain unread until selected or marked read. Letters include destroyed or demolished fence gaps, expedition successes/failures, completed technology and creature research, and projects interrupted by demolition. Similar fence events within five seconds are grouped. The latest 100 letters are retained. Click a fence/research location letter to focus the camera there.

The map and research windows keep simulation time running. Escape closes the management window first; Escape again opens Pause. Pause freezes missions and research along with the rest of the park.

## Tuning and saves

Select `Assets/Mythologicum/Resources/Progression/DefaultProgression.asset` in Unity's Project window. Expand **Rules** to edit prices, durations, teams, regions, mission tiers, reward quantities and technology definitions. Region species entries use creature asset names, such as `Chupacabra`. **Map Position** uses normalized coordinates: (0,0) bottom left, (1,1) top right. Keep region, difficulty and technology IDs stable once saves exist. Individual creature **Rating** is editable in its species asset.

Save format 2 includes team ownership, permits, active mission reservations and results, unplaced creature stock, completed technologies, partial research and inbox read state. Dispatched missions retain their original duration and chance; loading does not reroll them or issue completed rewards twice. Time does not advance while the game is closed. Version 1 saves remain loadable: placed creatures/buildings are preserved, and technologies needed for their existing buildings/fences are adopted. Other progress begins at zero.

Automated checks: `Tests/Progression/ProgressionChecks.cs` exercises the rules without Play mode. `Tests/Progression/Unity/ProgressionIntegrationValidation.cs` tests actual UI purchases, employee work, event notifications and saving/loading in an isolated project.
