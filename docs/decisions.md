# Decisions and open questions

Format: ID · date · question · default used · status.

## D-001 · 2026-11-02 · No Unity Editor in agent container
- Context: cloud container is Linux, no Unity install, no license, no GPU. Installing external SDKs blocked by policy.
- Default: author project as text (manifest, asmdefs, C#, editor bootstrap). Unity generates `ProjectSettings/*` and `.meta` on first open. Verification via `tools/validate_project.py` locally and GameCI in `.github/workflows/unity-ci.yml`.
- Needs from owner: repo secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; or run the playbook commands on a Windows machine.
- Status: open.

## D-002 · 2026-11-02 · Exact Unity 6.3 LTS patch and package versions
- Default: `6000.3.0f1`; HDRP 17.3.0, Cinemachine 3.1.4, Input System 1.14.2, Splines 2.8.1, Test Framework 1.6.0. Editor resolves/upgrades on first open; commit the resolved `packages-lock.json`.
- Status: open (confirm on first open).

## D-003 · 2026-11-02 · Core interfaces engine-free
- Default: `Hushlands.Core` uses `noEngineReferences`; 2.5D plane is `SlopePoint(distance, height)`. Engine adapters live in `Hushlands.Runtime`. Enables fast pure-C# tests and content-tool reuse.
- Status: decided.

## D-004 · 2026-11-02 · Graphics API and backend
- Default: D3D12 only, IL2CPP. Needed for DLSS and HDRP volumetrics at 4K.
- Status: decided.

## D-005 · 2026-11-02 · Frame budget percentile
- Bible says "budget 16.6 ms" without percentile. Default: p95 ≤ 16.6 ms in config (`budgetPercentile`).
- Status: open.

## D-006 · 2026-11-02 · Sub-agents
- Default: lead runs T0 inline; lanes spawn from T2 on, when parallel text-only work exists. One Editor driver rule still applies.
- Status: decided.

## D-007 · 2026-11-02 · Config pattern (T1)
- Default: engine-free `[Serializable]` settings classes in `Hushlands.Core` hold every number and self-validate; one `ScriptableObject` wrapper per block (`SettingsAsset<T>`), all linked from `Resources/Hushlands/HushConfigSet`. Lets pure-C# logic and tests use the same defaults as assets.
- Status: decided.

## D-008 · 2026-11-02 · Duckling restore on retry
- Bible: "checkpoint groups restored". Unclear whether groups rescued after the last checkpoint are kept.
- Default (kind): rescued groups are always kept; unrescued groups after the checkpoint go back to Ahead; groups passed before the checkpoint stay Waiting.
- Status: open.

## D-009 · 2026-11-02 · Placeholder tuning values
- Non-canon numbers (drag, jump velocity, flip rate, camera profiles, vehicle scales, costs, pickup durations) are first guesses. T3, T4, T8, T11 tune them by measurement.
- Status: open.

## D-010 · 2026-11-02 · Camera arbitration
- Default: highest `priority` request per step wins (FailureHold > Danger > Boost > Air > Vista = Shelter > Cruise).
- Status: open until T4.

## D-011 · 2026-11-02 · Python reference model for content
- No Unity here, so `tools/qa/content_sim.py` mirrors generator, checker and bot, reading all numbers from the C# settings. `ContentTests` pins golden values (RNG outputs, seed-1 stretch) so any drift fails in Unity.
- Status: decided.

## D-012 · 2026-11-02 · Fairness speed range
- Rules are checked at all speeds 6–18 m/s (sampled 7 times), not just the speed the bot reaches (~6–12 m/s in 12-chunk stretches). Conservative; may limit hazard size later. The bot proves rideability, the checker proves fairness at any speed.
- Status: open (revisit with T3 real physics).

## D-013 · 2026-11-02 · Camera framing contract for T4
- Default: camera must show ≥ 40 m of slope ahead (`visibleAheadMeters`), enough for the 1.5 s reveal at 18 m/s (27 m).
- Status: open until T4.

## D-014 · 2026-11-02 · Collapse zones are required jumps
- Bible gives a 1 s collapse warning but not the mechanic. Default: a collapse zone is a gap that opens after its warning; warning starts ≥ 1 s × 18 m/s × 1.25 before it.
- Status: open.

## D-015 · 2026-11-02 · Hazards and landings stay inside one chunk
- Keeps slope constant through each jump, so physics are exact. Chunks with no room are rest chunks (still have checkpoints, pickups, nests).
- Status: decided.

## D-016 · 2026-11-02 · Tap fires on release
- Tap and hold share one button, so ground jump fires on release (< 0.18 s), not press; holding becomes tuck. Adds up to 0.18 s latency; the fairness takeoff window (0.15–0.35 s) and bot timing absorb it. Coyote and jump buffer soften it.
- Status: open (playtest; alternative is jump-on-press with tuck on a second gesture).

## D-017 · 2026-11-02 · Physics plane
- Default: PhysX runs in flat world XY (x = distance, y = height). The visible world is mapped onto a curving spline in presentation (T7). Keeps physics exact and identical to the content model.
- Status: decided.

## D-018 · 2026-11-02 · Speed means horizontal speed
- Canon 6–18 m/s applied to horizontal plane speed (along-slope is at most 3% higher at 14°). Matches the content jump model exactly.
- Status: decided.

## D-019 · 2026-11-02 · Rider footprint in fairness
- Real collider has length; hazards are widened by `riderFootprintMeters` (0.5 m, ≥ half collider length, cross-checked in `HushConfigSet`). Shrinks generated obstacles; still 200/200.
- Status: decided.

## D-020 · 2026-11-02 · Collapse zones built open in T3
- Track builder builds collapse zones as open pits. Timed collapse with its 1 s warning is T10.
- Status: decided.

## D-021 · 2026-11-02 · Camera profiles retuned for the reveal contract
- T1 profiles framed ~12 m ahead; the 1.5 s reveal at 18 m/s needs 27 m and T2 fixed 40 m. Side-on camera now sits 38–60 m off the plane (Cruise 44 m, 40° FOV). Rider reads smaller on screen; hero framing for T7 must work within this.
- Status: open (T7 visual review).

## D-022 · 2026-11-02 · Reduced motion = framing locked to Cruise
- Locking only FOV broke the contract for wide states. Default: reduced motion uses Cruise framing for all states (except FailureHold) and 1.5× slower vertical follow; states still drive audio.
- Status: open.

## D-023 · 2026-11-02 · Only FailureHold is exempt from the contract
- Shelter and Vista can contain jumps, so they must still show 40 m ahead.
- Status: decided.

## D-024 · 2026-11-02 · One procedural CinemachineCamera
- Default: core computes the pose; a single CinemachineCamera applies it (Brain renders, noise/impulse/cutscenes available later). Seven priority-blended vcams would make blends and the contract unverifiable outside Unity.
- Status: decided.

## D-025 · 2026-11-02 · Pitch ignored in framing math
- Pitch is limited to ≤ 10° (profiles use 3–6°); framing math treats the view as side-on.
- Status: decided.

## D-026 · 2026-11-02 · Assist "extra reveal" via speed, not camera
- Extra look-ahead would push the rider outside the safe frame (T4 contract). Default: assist speedScale 0.85 gives 40 m / 15.3 m/s = 2.6 s reveal (≥ 1.5 + 0.5 s), checked in `HushConfigSet`.
- Status: decided.

## D-027 · 2026-11-02 · Assists auto-accept
- "Free assists after 3 failures": default turns them on automatically with a gentle line; `autoAcceptAssists` can switch to a prompt (UI in T13). Never gated by progression.
- Status: open.

## D-028 · 2026-11-02 · Retry timing
- Hold 0.9 s (skippable by tap after 0.3 s) + 0.6 s mist wipe; rider moved at the wipe midpoint so no teleport is visible. Retry speed = minimum speed; run time keeps counting, so the ramp resumes.
- Status: open (playtest).

## D-029 · 2026-11-02 · Scarf method pending measurement
- T6 says "choose by measured cost". No Unity here, so `ClothSettings.method` stays Verlet (the tie-break default) until `ScarfBenchmarkTests` runs and "Hushlands/Tech/Apply Scarf Benchmark Result" writes the measured choice. Editor timings are relative; T17 re-measures in a player build on the GPU matrix.
- Status: open (needs a Unity run).

## D-030 · 2026-11-02 · Scarf anchor carry
- Plain verlet stretched 180% at 18 m/s and during a 100 ms hitch (Python model). Default: carry 85% of anchor motion per substep, drag on true world velocity, translate the rope through unsimulated hitch time, 6 iterations. Worst stretch 4.6% (limit 6%).
- Status: decided.

## D-031 · 2026-11-02 · Benchmark harness constants
- Anchor test motion (arc amplitudes, spacing, body offset) in `ScarfBenchmark` are QA-harness constants, not gameplay tuning; kept in code.
- Status: decided.

## D-032 · 2026-11-02 · T7 "final quality" not achievable in this environment
- No Unity, GPU or authored art here. Delivered: the full slice pipeline with procedural placeholder geometry in the canon palette, golden-hour lighting, mist, camera, audio hooks and UI overlay. Final quality needs authored original hero assets (terrain textures, grass, sled, rider, ducklings, nest) and an Editor look pass. The gate cannot be approved on placeholder art.
- Status: open (owner).

## D-033 · 2026-11-02 · Slice is the first build scene
- The player and `-hushCapture -hushAutopilot` open the slice directly, so the 4K capture records real gameplay.
- Status: decided.

## D-034 · 2026-11-02 · No audio clips yet
- Original audio is required and none exists. T7 ships the mist low-pass and snapshot bridge; the AudioMixer asset and clips are authored later (T16).
- Status: open.

## D-035 · 2026-11-02 · Overlay layout numbers
- Font sizes and label boxes in `RetryOverlay` are placeholder layout (legacy runtime font). T13 replaces the overlay with the motion-graphics UI and moves layout to config.
- Status: decided.

## D-036 · 2026-11-02 · Mist visual mapping
- Local mist mean free path = 45 m / density, so a bank at the fairness threshold (0.35 x 0.85) stays see-through; checked by `MistVisibility` in config validation.
- Status: decided.

## D-037 · 2026-11-02 · T7 gate: provisional approval
- Owner said "continue to next task" after the T7 gate report. Treated as provisional approval of the procedural placeholder slice. The 4K capture, Unity run and visual sign-off remain owed and are required before T14.
- Status: open.

## D-038 · 2026-11-02 · Pickup effects (bible lists names only)
- Defaults: lantern thins mist, feather boosts cruise speed (capped at 18 m/s), tea doubles score, magnet pulls coins, golden mistbell is a big coin bonus. All values in `PickupSettings`.
- Status: open (design review).

## D-039 · 2026-11-02 · No pickup respawn on retry
- Collected pickups stay collected and distance scores only new ground, so crashing cannot farm score. Timed effects end on a crash.
- Status: decided.

## D-040 · 2026-11-02 · Rescue mechanic
- Bible says "rescuing ducklings" without a mechanic. Default: pass a nest grounded (no button), ducklings ride in a covered basket; rescues count at once and are never undone (kind; matches D-008).
- Status: open (design review).

## D-041 · 2026-11-02 · Fix: retry restore boundary
- T1 code kept a nest Waiting when it sat just after the retry checkpoint, though the rider restarts before it. Fixed to "at or after the checkpoint"; two tests that encoded the bug were corrected.
- Status: decided.

## D-042 · 2026-11-02 · Covers reopen only when riding resumes
- Restore happens mid-wipe; opening covers there would show ducklings while the run is still failing. Covers now reopen in RunFlow as the phase returns to Riding.
- Status: decided.
