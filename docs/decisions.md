# Decisions and open questions

Format: ID · date · question · default used · status.

## D-001 · 2026-11-02 · No Unity Editor in agent container
- Update 2026-11-03: the .NET 8 SDK is now installed from Ubuntu packages. `Hushlands.Core` compiles with warnings as errors and 169 engine-free EditMode tests pass (tools/dotnet). The first compile found one real error (a duplicate local `k` in ChunkGenerator); the first test run found three wrong test expectations, all fixed. Unity-bound code (Runtime, Editor, PlayMode) still needs Unity.
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

## D-043 · 2026-11-02 · Landslide tuned for tension, not punishment
- Default chase never catches a rider riding at cruise; it only closes on a rider stuck at minimum speed. Danger fires only when it is gaining or within 10 m (normal riding: 0% Danger time). Fits "calm"; harder modes can raise pace ratio later.
- Status: open (playtest).

## D-044 · 2026-11-02 · Collapse warning rule tightened
- At 18 m/s the old 22.5 m warning ended after the latest takeoff point (24.3 m). Rule `collapse.warning` now requires 1 s at max speed plus the takeoff zone; generator slack 1.25 → 1.4. Content still 200/200; golden values repinned.
- Status: decided.

## D-045 · 2026-11-02 · Squall is mild weather
- Bible lists a squall with the landslide but no rules. Default: short seeded gust windows with a small headwind, denser fog within the mist-fairness check, scarf gusts; no new crash cause.
- Status: open.

## D-046 · 2026-11-02 · No shop: vehicles unlock by lifetime coins
- Hard rule 4 forbids a shop. Default: `unlockLifetimeCoins` thresholds on coins earned; coins are never spent. Thresholds 150–1200 are first guesses.
- Status: open (economy review).

## D-047 · 2026-11-02 · Fairness for every vehicle
- The toboggan jumps 5% lower than the sled, so default-vehicle fairness could make stretches impossible on it. Content now uses the weakest jumpScale; the bot validates all six vehicles. Content 200/200 for each; goldens repinned.
- Status: decided.

## D-048 · 2026-11-02 · Save format
- Engine-free text save (no JsonUtility in Core) with CRC32, atomic swap and one backup. Version 1; future versions migrate forward, older builds never overwrite a newer save.
- Status: decided.

## D-049 · 2026-11-02 · Goals and homecoming
- Bible names goals and progression without content. Default: 10 calm goals that rotate three at a time, coin rewards feeding vehicle unlocks; waiting duckling groups carry across runs and come home with later rescues.
- Status: open (design review).

## D-050 · 2026-11-02 · Run end in the slice
- The slice stretch is finite: a run ends at its end (or on quit). The endless run that chains biomes is T14/T15.
- Status: decided.

## D-051 · 2026-11-02 · Fix: MonoBehaviours sharing files
- `MistLowPass`, `AudioMixBridge`, `ScarfLine` shared files with other classes, so scenes could not serialize them. Split into same-named files; validator now enforces the rule.
- Status: decided.

## D-052 · 2026-11-02 · Pause without timeScale
- Input is processed in FixedUpdate (T3), which timeScale 0 stops, so the pause could never be ended. Pause sets physics to script mode and holds rider and run ticks; FixedUpdate keeps running.
- Status: decided.

## D-053 · 2026-11-02 · Reloads hidden by mist
- "No loading cuts": Ride again, back to title and vehicle changes reload the scene only after a full mist wipe, and the new scene starts covered and clears. The cut exists but is never seen.
- Status: open (a streamed in-place rebuild could replace it with T14's endless run).

## D-054 · 2026-11-02 · Placeholder typography and panel contrast
- Legacy runtime font, procedural layout. Contrast is validated against the opaque panel colour; the panel is semi-transparent over the world (0.55; 0.85 in high contrast), so T17 must check real composited frames.
- Status: open.

## D-055 · 2026-11-02 · Gentle timing applies at the next ride
- The tap window in the input sampler updates at once; the rider's own rules take it at scene start (next ride).
- Status: decided.

## D-056 · 2026-11-02 · Biome content profiles leave approved biomes untouched
- Per-biome knobs (slopes, hazard mix, mist chance, second surfaces) apply only to biomes with a profile. Meadow Sea keeps the global values, so its T7 content and golden values are unchanged.
- Status: decided.

## D-057 · 2026-11-02 · Windmouth Ridge art direction (default)
- No reference given. Default: calm dawn on a frosted ridge, cooler than Meadow's golden hour, still the canon palette for mist (mist blue) and accents (gold); frost-pale ground and grey-brown rock as neutrals. Procedural placeholder props (boulders, cairns) with shape constants in code until authored art.
- Status: open (approval gate).

## D-058 · 2026-11-02 · Windmouth gate: provisional approval
- Owner said "continue" at the Windmouth gate; treated as provisional approval on placeholder art. Render, capture and sign-off remain owed.
- Status: open.

## D-059 · 2026-11-02 · Pine Hollow art direction and shelters (default)
- Default: cool late-afternoon pine valley in the canon palette (emerald ground, mist blue fog, gold accents). New content option: canopy Shelter zones that drive the camera's Shelter state. Shelters only draw random numbers in biomes that use them, so earlier biomes' content is unchanged; shelters clamp to the room in each chunk.
- Status: open (approval gate).

## D-060 · 2026-11-02 · Fix: mirror parser read nested braces
- The Python mirror stopped a profile at the first "}," inside an array initializer and missed Pine Hollow's shelter settings, so its runs did not match the C#. Parser now closes on the profile's own brace. Found because the shelter count was 0.
- Status: decided.

## D-061 · 2026-11-02 · Pine Hollow gate: provisional approval
- Owner said "continue" at the Pine Hollow gate; treated as provisional approval on placeholder art. Render, capture and sign-off remain owed.
- Status: open.

## D-062 · 2026-11-02 · Terrace Country art direction, vistas and terraces (default)
- Default: soft morning over tea terraces, canon terracotta earth and mist blue. Vista zones use the same zero-draw rule as shelters; terraces quantise only the backdrop hills (never the track corridor) and only for biomes with a terrace step, so other biomes' terrain is unchanged. Tea and terraces are generic agricultural forms, not any specific place or game.
- Status: open (approval gate).

## D-063 · 2026-11-02 · Terrace Country gate: provisional approval
- Owner said "continue" at the Terrace Country gate; treated as provisional approval on placeholder art. Render, capture and sign-off remain owed.
- Status: open.

## D-064 · 2026-11-02 · Mirror Lake water and duckling safety (default)
- Water is backdrop only (stepped still pools per chunk, below the track), so the rider never meets water and ducklings are never near it: nests ≥ 10 m from the lake band by config check. A real reflective water shader (planar/SSR) is an authored-art item; the placeholder is a smooth mist-blue HDRP Lit plane.
- Status: open (approval gate).

## D-065 · 2026-11-03 · Compile-checking Unity-bound code without Unity
- Runtime, Editor, PlayMode and engine-bound EditMode code now compile outside Unity against Unity 6000.0 engine reference assemblies (NuGet Digitalroot.References.Unity 6000.0.75, "publicized", so internal-API misuse would not be caught) plus tools/dotnet/Stubs: HDRP / SRP Core (Graphics @ 6000.0/staging), Cinemachine 3.1.4 and Test Framework 1.4.6 (sha1-checked registry tarballs), and UnityEditor (UnityCsReference @ 6000.0). Every stub member was matched to a declaration in that source, except EditorSceneManager.SaveScene / NewSceneSetup and HDMaterial.ValidateMaterial (documented API; wrapper file not located). Stubs catch missing or misspelled APIs and type errors; they cannot catch runtime behaviour.
- Found and fixed: `Hushlands.Core.MenuItem` collided with `UnityEditor.MenuItem` in every Editor menu file (Unity would have rejected the Editor assembly) → renamed UiMenuItem / UiMenu; VerletScarf serialized field warning (CS0649) → explicit default.
- Status: decided. Real Unity compile + test run still owed (project version 6000.3 vs refs 6000.0).

## D-066 · 2026-11-04 · Runtime review fixes (beyond what a compile can see)
- Retry teleport left the scarf and sled flex in their pre-crash pose (scarf whipped across the gap) → reset on Flow.Retried.
- UiInput sampled WasPressedThisFrame in FixedUpdate while only the player input set fixed-update processing; autopilot runs read menu input unreliably → UiInput sets the mode too.
- After the run ended the rider slid off the run-out and fell forever under the summary → world pauses on RunEnded.
- Active Input Handling was never set, so the Input System would receive no events (and warn) → ProjectBootstrap sets PlayerSettings.activeInputHandler = 1 (same serialized setting the Input System package edits).
- IL2CPP engine stripping could remove MeshCollider (only reached via CreatePrimitive(Quad)) → lake uses a plain mesh; link.xml preserves runtime-only collider/cloth types and the game assemblies.
- CI relied on -executeMethod running before the test runner → EditorAutoBootstrap runs the idempotent bootstrap whenever the config set is missing.
- Status: decided.

## D-067 · 2026-11-11 · The journey: one continuous run over all five biomes (default)
- Canon order and lengths come from BiomeSettings (Windmouth 900 m → Meadow 1200 → Pine 1100 → Terrace 1200 → Mirror Lake 1000; about 5.2 km, 59 chunks). Each biome draws from the same random stream as its standalone slice, so journey content is the content already validated per biome; nest groups are numbered across the whole run and mist is clamped across borders.
- Borders blend over BiomeSettings.transitionMeters (80 m, smoothstep): backdrop terrain, sun, fog, exposure, scarf wind and audio mist base. Corridor and front-of-track rules hold for any blend. The lake bed fades in with the blend; water planes exist only on lake chunks. Fog heights follow the track as it descends (a single slice keeps its fixed heights).
- The camera profile switches at the border midpoint (dominant biome). Slice_Journey is now the first build scene (what the player opens); single-biome slices stay for tuning, tests and captures.
- Verified: 11 engine-free tests, Python mirror 40/40 journeys on all 6 vehicles (docs/logs/journey_content.txt), all assemblies compile. Not verified here: Unity run, frame time of the 5 km scene (terrain is built in one go; streaming is a T17 item if the 16.6 ms budget is missed).
- Status: open (needs Unity run and capture).

## D-068 · 2026-11-11 · T15 directors are presentation only (default)
- T15 names five things and no numbers. Defaults: nothing here may change fairness or content. Weather and the day swing touch only sun, exposure and fog (fog at most 5% thicker; first guess of 8% failed the mist check in Pine Hollow and was thinned). Rain or snow particles are deferred: no authored particle material exists and a default one would render wrong in HDRP.
- Pacing does not edit the generated stretch or the squall timetable (both validated); it only gates calm-only presentation (weather changes, humor beats) and is the hook for T16's mix.
- Humor beats are props, not duckling gags, so duckling safety code is untouched. Lines pass the calm-words check.
- Failure choreography acts on the placeholder body only; a mist puff covers gap and landslide spills.
- AtmospherePresenter now runs in every slice (replaces the journey-only blend), so slices and the journey share one path.
- Status: open (needs Unity run and capture).

## D-069 · 2026-11-11 · T16 audio is synthesized placeholder (default)
- Original audio is required and none is authored, and an AudioMixer asset cannot be made without the Editor. Default: generate every clip in code at start-up (filtered noise beds, a sine pad, short tones) and mix with per-source gains that follow the camera state, so the game has sound and the mix rules are testable now. Authored clips and a mixer can replace the clips later without changing SoundMix; AudioMixBridge stays as the optional mixer path.
- No music beyond the pad: composing is an authored-art item. No per-biome ambience yet (birds, water): same reason.
- Camera pass found no defect: biome switches already blend in the rig; a journey-long continuity test now guards it.
- Status: open (needs a listen in Unity; levels are first guesses).

## D-070 · 2026-11-12 · T17 QA findings and fixes
- Soak (whole run session over full journeys, duckling audit every step) found a real bug: the landslide was paced on the cruise target, which ramps to the 18 m/s cap after ~270 s, while terraces and boardwalk hold the rider near 13.7 m/s. On a 5 km journey the slide caught a perfect rider twice (seed 2). Fix: pace on the slower of target and real speed. Mirror updated; 200/200 stretches and 20/20 journeys never caught (closest 22.4 m, was 10.5 m); a stalled rider is still caught (107 s, was 87 s), so the chase stays real.
- Second bug: BotPilot never rewound after a retry teleport, so the autopilot (captures, soak runs, play mode tests) skipped every jump between the checkpoint and the spill and spilled again. Fix: rewind its hazard cursor when the rider is moved back.
- Performance default: the journey's backdrop (terrain tiles, prop buckets, water, mist volumes) is switched on only within 700 m ahead / 350 m behind the rider (30 m hysteresis). The edge is under 10% transmittance in the thickest fog, so nothing visibly pops in; at most a third of the journey is on at once. Gameplay objects are never culled.
- Not possible here (owed on the Windows test GPU): GPU matrix, 4K frame-time captures, real input devices, capture review of fairness and duckling safety in rendered frames.
- Status: decided (fixes); open (hardware QA).

## D-071 · 2026-11-12 · T18 polish and final build: what is done here and what is owed
- Done: review pass over T14–T17 code; the autopilot now only rewinds on a backward move (a retry), so contact jitter at a landing cannot trigger an extra hop. CI also runs the journey mirror. Playbook lists every check.
- The final Windows x64 player build is `BuildScript.BuildWindows` (CI job build-windows, GameCI, fails on any build warning). It needs a Unity licence and a Windows runner; neither exists in this environment, so the build, the 4K captures and the visual approvals (T7, T13, T14 per biome, journey) remain owed to the owner.
- Status: open until a Unity build, PlayMode run and captures are attached.

## D-072 · 2026-11-13 · Local Unity runner fixes (no Unity in this session)
- `run_local.ps1` fixes found by parsing and dry-running it under PowerShell 7 with stub Unity and player executables:
  - `Step a b -eq 0` passed `-eq 0` to Step as extra arguments, so bootstrap and build always counted as FAIL. The calls are now parenthesised.
  - The player writes `Captures\` beside its `_Data` folder (`Builds\Windows\Captures`). The script was looking in the repo root, so it never found the capture. It now reads that folder, copies the new frametime txt and png into `QA/local`, and writes the budget lines to the summary.
  - Capture exit code (0 within budget, 2 over) now counts toward RESULT, and so do a missing capture, a missing test XML or any failed test. The capture has a timeout (`-CaptureTimeoutSeconds`, default 900).
- Default used: the 900 s timeout is a script argument, not game config, because it bounds the QA tool, not the game.
- Status: open until the script runs on Windows with Unity 6000.3.0f1.
