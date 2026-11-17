# Staff queries and unused status formatting — 16 November 2026

Unity 6000.0.74f1, disposable native Editor, seed 2471, 10 warm-up updates and
60 measured updates at a 0.1-second simulation step. The existing park fixture
uses flat ground and placeholder actors; rendering, UI, imported models, weapon
effects and mass construction invalidation are excluded. Native allocation
counters are unsupported in these runs, so their zero totals are not evidence
of allocation-free simulation.

`Park-Before.json` is the current runtime after visitor-arrival optimization.
`Park-After.json` adds reusable staff BFS buffers, approach-score pruning before
sight checks, and cached strict-ground danger membership. `Park-Final.json` also
removes unused per-frame visitor/staff status formatting left over from floating
labels. `UI-Before.json` records UI maintenance separately; it was not changed.

| Median CPU time, milliseconds | Before | Staff query changes | With unused status formatting removed |
| --- | ---: | ---: | ---: |
| Small park, 51 actors | 3.182 | 1.957 | 2.167 |
| Medium park, 142 actors | 4.301 | 3.861 | 3.504 |
| Large park, 274 actors | 11.640 | 11.234 | 9.697 |
| Large park, staff only | 7.477 | 6.734 | 6.323 |
| Large park, visitors only | 2.060 | see JSON | 1.443 |

These are individual observed runs, not a statistical FPS claim. The final large
park has lower median CPU time, but its maximum update is 101.577 ms versus
77.101 ms before. Occasional long simulation updates remain and need separate
profiling; this pass does not claim to remove worst-case search spikes.

`StaffQueryChecks.txt` contains 686 native reference-equivalence and containment
checks. Informational totals over the fixture's ranged queries are 1132.088 ms
for the original implementation and 433.794 ms optimized. Repeated contained-
danger queries total 65.850 ms versus 0.620 ms with a warmed component cache.
This small fixture is not a whole-park speed multiplier; reference runs precede
optimized runs, and timings have no pass/fail thresholds.

The model suite separately checks exact BFS order, public list ownership and
zero allocations for warmed reusable buffers on the supported Mono runner.
Actual staff job integration covers hiring, research, care, carrying and security.
See `Tests/README.md` for the standalone native helpers and disposable-project setup.
