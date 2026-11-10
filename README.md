# Morsoul: World's Case

Unity investigation game combining 2D hubs, 3D investigations, dialogue, deduction, interrogation and case resolution.

**Contributors: start here. Agents: read [AGENTS.md](AGENTS.md) before changing anything.** That file owns repository rules, lane boundaries and merge instructions. This README is the system map and onboarding guide, not a declaration that the full campaign is finished.

## Quick start

1. Install **Unity 6000.6.7f1**, matching [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Do not upgrade the editor or packages as part of unrelated work.
2. Clone your assigned branch. In the checkout, run `git lfs install` and `git lfs pull` before importing assets.
3. Add this repository folder in Unity Hub and let Unity restore packages and import assets. Do not commit `Library`, `Temp`, `Logs` or generated IDE files.
4. Read the ownership rules below. The 2D/Core work is developed on branch `2D`; coordinate integration with the 3D owner rather than assuming both branches have identical scenes.
5. Open `Assets/Scenes/Title.unity` **by itself** and enter Play. Do not leave the market scene loaded alongside Title: both scenes' players and cameras would run.
6. For development with unintegrated investigations, enable **F1 > Session > Investigation development fallback** before New Game. The runtime catalog gates the opening and case entrances; disabled/unavailable paths report status instead of loading stand-ins. Continue requires a readable checkpoint.
7. Check **File > Build Profiles** if travel fails. An active profile can override the shared scene list.

Do not regenerate scenes or content merely to open the project. Generators can overwrite authored work.

## Where to work

All script folders in this table are under `Assets/Scripts/`.

| System / folder | Responsibility | Main entry points |
| --- | --- | --- |
| `Core` | Persistent services, event bus, Focus, Session, checkpoints and scene routing | `CoreRuntime`, `SessionState`, `GameStateManager`, `CoreEvents` |
| `Data` | ScriptableObject schemas, case content contracts and clue lookup | `CaseData`, `ClueData`, `EvidenceGraphData`, `ClueRegistry` |
| `Gameplay` | Case grading and ending-selection rules | `CaseSession`, `EndingSelector` |
| `Flow` | Binds board, interrogation, resolution and ending playback | `GameFlow`; `Editor/FlowSceneFactory` |
| `Hub` | 2D movement, targeting, NPCs, inspection, travel, scene stores and visual profiles | `HubContext`, `HubPlayerController`, `HubJournal`, `HubFlags`, `EntityVisual` |
| `Engine` | Pure weighted-board rules: directed-link evidence DAG, solver, progression, endings, canonical hash/seal — no Unity deps | `EvidenceGraph`, `DeductionEngine`, `ProgressionRules`, `EndingResolver`, `CanonicalState` |
| `Board` | Weighted board (`WeightedBoardSession` + `WeightedBoardController`/`WeightedBoard.uxml`) for cases with `evidenceGraph`; legacy pairing/premise model (`DeductionBoard`/`BoardController`) remains for cases without one | `WeightedBoardSession` (rules), `WeightedBoardController` (UI) |
| `Interrogation` | Evidence moves, phases, composure, strikes and mistrial UI | `InterrogationEngine`, `InterrogationController` |
| `Dialogue` | Dialogue data, branching, clue/flag effects and presentation | `DialogueData`, `DialogueRunner`, `DialogueController` |
| `Journal` | Physical/testimony cards, logs, dossier and dissonance display | `JournalController`, `JournalFilter` |
| `Cutscene` | Ordered still/video shots, captions, advance and skip | `CutsceneData`, `CutsceneShotData`, `CutscenePlayerController` |
| `Resolution` | Grade and unease presentation, dismissal | `ResolutionController` |
| `Title` | Current uGUI title menu and scene generator | `TitleMenuController`, `Editor/TitleMenuSceneFactory` |
| Root scripts, `Interaction`, `Player`, `Systems` | Existing 3D player, inspection, scan, HUD and spawning lane | `PlayerController`, `ScanController`, `DiaryManager`, `InteractionProbe`, `PlayerSpawner` |

Runtime modules use `Morsoul.*` assembly definitions. Editor generators live in `Editor` folders/assemblies. Keep dependencies inward: scene adapters can reference Core and Data; Core must not reference scene-specific or 3D assemblies.

### Other important locations

| Path | Contents |
| --- | --- |
| `Assets/Data/Cases/Case00` through `Case05` | Authored/generated case assets, including clues, premises and interrogation data |
| `Assets/Resources/ClueRegistry.asset` | Build-accessible clue ID index used to restore clue assets on Continue |
| `Assets/Scenes` | Title, hubs, art overlays, PrologueRuins and CaseLoop |
| `Assets/Prefabs/UI` | Reusable panel prefabs |
| `Assets/*.uxml`, `Assets/*.uss` | UI Toolkit layouts/styles for gameplay panels |
| `Assets/UI Toolkit/MainPanelSettings.asset` | Shared UI Toolkit panel settings |
| `Assets/Art` | Sprite, portrait, cutscene, environment and UI art categories plus visual profiles |
| `Assets/Audio` | Music, SFX and VO categories |
| `Assets/Tests/EditMode` | Rule, data, controller and persistence tests |
| `Packages`, `ProjectSettings` | Shared package/editor/build/render settings; coordinate changes |

The current Title uses **uGUI**, not the removed UI Toolkit TitlePanel pipeline. Gameplay panels use the existing `PanelRenderer` pattern.

## Ownership and merge safety

- **2D/Core owner:** the modular systems above, UI, dialogue, case flow, content generators, Title and both hubs.
- **3D owner:** `PrologueRuins.unity`, investigation scenes, root-level 3D scripts, and `Interaction`, `Player`, `Systems`. Existing prototype systems are not safe to delete just because Core provides replacements.
- **Coordinate first:** Core public APIs, data schemas, input actions, tags/layers, packages, render settings, build settings/profiles and shared scenes.
- **Launcher backend is separate.** In-game state and persistence belong to Core, not to a second launcher-owned game backend.
- Commit assets with their `.meta` files. Preserve GUIDs and stable content IDs. Move assets through Unity where possible.
- Use separate prefabs or additive art scenes for parallel work. Do not put another player, camera, input owner or Core runtime into an art overlay.
- `.gitattributes` provides LFS patterns and Unity merge attributes. Each developer still needs the local Smart Merge setup in [AGENTS.md](AGENTS.md#git-and-unity-merging). Do not migrate LFS history or blindly resolve YAML conflicts by taking one side.
- Agents must not commit, push or change Git configuration without explicit permission. Prior one-time commit permission does not carry forward.

## Runtime model: reuse these systems

```text
Title / scene adapters / panels
              |
              v
CoreRuntime (persistent services)
  SessionState  <->  SaveState / SaveService
  FocusModel         EventBus -> GameStateManager -> SceneRouteTable
              ^
              |
GameFlow -> Board -> Interrogation -> Resolution
              -> ending cutscene -> case completion -> route
```

- `CoreRuntime.Ensure()` boots the persistent service stack, including the scene driver. Do not create another service locator, Focus model, persistent diary or save manager.
- `SessionState` is live cross-scene truth: clue IDs/assets, flags, validated board node IDs, per-case weighted-board histories, grade inputs, scene/player context, Prologue/cutscene flags, log slots, grades and unlocked tools.
- `SaveState` is the serialized checkpoint format. Scene systems should update Session and request a checkpoint rather than editing save JSON directly.
- `HubJournal` and `HubFlags` are scene-scoped stores, not competing persistent owners. Runtime composition must bind their views/listeners.
- `CaseSession` tracks grading within the active case. `GameFlow` coordinates panel events and writes results back into Session.
- Checkpoints live at `Application.persistentDataPath/morsoul_checkpoint.json`. HMAC signatures detect edits; a key embedded in a game client is not tamper-proof security. The primary save replacement path uses `File.Replace`; unsupported-filesystem fallback/recovery still needs platform validation.
- Save files are sealed `saveVersion` 4 records. Optional player/interrogation/return contexts carry explicit presence flags because Unity JSON expands null inline objects into zero-valued objects. Every persisted field is represented in `SaveCanonicalMap`; version-2 and version-3 sealed files verify against their original maps before migration, and version-1 signature files migrate only from explicit completed-case flags.
- Regular `WriteCheckpoint` calls advance the monotonic `lastTrustedUtc` watermark but do not claim a graceful close. `OnApplicationQuit` calls `WriteCloseCheckpoint`, which writes `closedAtUtc`; Continue applies trusted offline recovery, clears the close marker and immediately persists the consumed interval so it cannot be credited twice.
- The 2D hub path saves and restores player position/facing. If a checkpoint scene is missing, Continue falls back to the case hub and clears the old pose rather than applying it to the wrong scene.
- An interrupted `CS00_ELIZA_INTRO` save is rewound on Continue to the `case00_eliza_precontact` state: the player resumes at `(6, 0)` in `Prologue_Wilderness`, `activeCutsceneId` clears, and the trigger can fire once instead of skipping or resuming halfway through.

### Scene routing

Routes are configured through `GameStateManager.Ensure().routes.Set(key, actualSceneName)`.

| Route key | Current default |
| --- | --- |
| `title` | `Title` |
| `caseloop` | `CaseLoop` |
| `hub.0`, `hub.1` | `Hub_LowerMarket` |
| `hub.2` | `Hub_CathedralNave` |
| `hub.3` | `Hub_Wilderness` |
| `hub.4` | `Hub_IronHollow` |
| `hub.5` | Not seeded; Case 5 is board/cutscene-only |
| `opening` | `PrologueRuins`, guarded by the explicit integration catalog; development simulation goes directly to `Prologue_Wilderness` |
| `prologue.wilderness` | `Prologue_Wilderness` (post-gate 2D approach and Eliza first contact) |
| `investigation.1` | Not seeded; normal entrance dispatches through the catalog, with opt-in development simulation until the real vault is integrated |
| `investigation.2`-`investigation.4` | Not seeded; their hub gates stay route-keyed but safely refuse until real scenes are registered |

A route entry does not add a scene to a Build Profile. Register real scenes and add them to the active build list together. `GateInteractable.routeKey` and `CarrierInteractable.routeKey` resolve through this table before their serialized `targetScene` fallback; do not add nonexistent scene paths to suppress warnings. Continue resolves the checkpoint scene first, then the saved case's hub, then Title; it clears the saved pose whenever it cannot resume the checkpointed scene.

## 3D integration checklist

Read [the full contract](AGENTS.md#3d-integration-contract) before adapting the prototype scripts.

1. Cache `CoreRuntime.Ensure()` in the scene adapter. Keep the existing 3D player/spawn owner; do not add the 2D `SceneEntryPoint` beside `PlayerSpawner`.
2. While actively scanning, call `core.Focus.TickScan(Time.deltaTime)` **once per frame**. Display raw `Percent` energy / `Maximum`, using `Normalized` for bar fill. Zero Focus must not block scanning: strained scans remain functional. Rest regeneration uses `TickRest` only during rest.
3. Resolve a stable authored clue ID using `core.ResolveClue(id)`. Reject an unresolved ID visibly; do not mark collection successful without its asset.
4. If the scene has a bound `GameFlow`, call `flow.HandleClueRegistered(clue)`. It forwards to the journal/Session and handles latent clues. Do not independently fire several collection pipelines.
5. Without GameFlow, `core.Session.RegisterClue(clue)` persists the clue. For an actual latent clue, also call `RegisterLatentClue(clue)` after tool/access checks. The current convention maps `L1_01` through `L4_04` to log slots 0-3 and per-case latent flags. Explicit `MarkRunicLogById(log.id)` uses **1-based** log IDs; `MarkRunicLog(slot)` uses **0-based** slots. Do not infer collection just from entering a zone.
6. Use `Session.SetFlag` / `HasFlag` for persistent story state. Already-bound scene flag/journal views may need refreshing; a Session write alone is not a UI binding.
7. For a checkpoint without travel, publish `CheckpointRequestedEvent { Reason = "Investigation" }` on `core.Bus`. For travel, publish `SceneLoadRequestedEvent { SceneName = actualSceneName }`. Do not also call `SceneManager.LoadScene` for the same request.
8. If adding a 3D asmdef later, reference only the Morsoul assemblies it uses. Coordinate the migration and preserve script GUIDs.
9. The 3D Prologue gate should attach `Morsoul.Hub.PrologueGateSeam` to its owned trigger/interaction. It requires the Case 0 board solved plus `PrologueGateUnlocked`, stamps the robe flag during the exit interaction, validates `Prologue_Wilderness`, then uses the GSM checkpoint/load path. The separate Eliza trigger owns completion and Case 1 advancement.

The legacy `ScanController` still has its own 120-point focus and different zero-focus behavior. `DiaryManager` stores legacy `ClueNode` objects. Their conversion is a 3D-owner integration task, not evidence that the shared contracts are already in use.

## Running and composing a case

### Existing scenes

- **Title:** normal New Game / Continue entry point.
- **Hub_LowerMarket / Hub_CathedralNave / Hub_Wilderness / Hub_IronHollow:** 2D hubs with movement, dialogue, gated investigation entrances and carrier travel. Hub boards are denied opening/mutation. Ignatius, Accord, Vane and Harlan explicitly enter their accepted current-case interrogation; legacy CaseDoors are inactive in the first three hubs. Iron Hollow retains the Case 5 entry, guarded to the correct hub and campaign state. Investigation gates use the integration catalog, not fake scene targets.
- **Prologue_Wilderness:** dedicated 2D approach loaded by the Prologue gate. It restores the player at `(-8, 0)`, owns the separate full-lane first-contact trigger at `x = 8`, uses `BoxCollider2D` tree/blocker bounds and a `BoxCollider2D` player body, and completes the Prologue only after the in-world Eliza sequence.
- **PrologueRuins:** 3D-owned scene. Do not regenerate or restructure it from the 2D lane.
- **CaseLoop:** dedicated 2D encounter host for accepted Cases 1–4, and the board/cinematic exception for Case 5. With no `activeCase`, GameFlow resolves `Session.CurrentCase` through CaseDatabase. Cases 1–4 with Core never open a board here and require an accepted, unresolved current-case investigation. Set `GameFlow.investigationScene = true` only in real investigation scene composition; that mode uses `Session.InvestigationCaseId`, persists board state and returns to 2D rather than starting interrogation in 3D.

### Campaign progression

- Accepted Cases 1–4 unlock the matching 2D suspect interaction, which enters CaseLoop for `Session.CurrentCase`. Confession/resolution still owns case completion. Completing a case advances `CurrentCase`, unlocks the next case's authored tool, and routes `hub.<id+1>` -> `hub.<id>` -> `title`. An ending routes to Title after its cutscene.
- `Case_05` (board-only, authored `preloadedClues`) can be played end-to-end today. For Cases 2-4, **F1 > Scenarios > C<n>: board ready** seeds their authored non-latent clues, unlock flag and current-case hub; the unavailable investigation gates remain locked instead of using throwaway stand-ins.

### CaseLoop setup

1. Open `Assets/Scenes/CaseLoop.unity`. To pin a case for standalone testing assign `GameFlow.activeCase` (e.g. `Assets/Data/Cases/Case05/Case_05.asset`); leave it unassigned for session-driven play.
2. Check the board, interrogation, resolution, cutscene, dialogue, journal and flag-store references. The factory creates the panel wiring, but it does not author all content.
3. For production/session-backed testing, enter through Title or the hub CaseDoor so the save/session lifecycle is real. Opening CaseLoop alone now lazily creates the default Core/Session, which is useful for inspecting the current case but is not a New Game or Continue test.
4. Investigation cases use Session-collected clues at case initialization when Core is live. Board-only authored `preloadedClues` take precedence. Late collection needs an explicit rack refresh through `BoardController.SetClues`; do not assume every Session change rebuilds every view.
5. Bind journal/dialogue flags at runtime with `SetFlags(hubFlags.Set)`. The flag set is not serialized; calling SetFlags only inside an editor generator does not persist that binding. Populate journal `allLogs` and synchronize its displayed collected logs with Session as part of scene composition.
6. Assign `GameFlow.endingCutscenes[1]`, `[2]`, `[3]` (index 0 unused). For GameFlow-owned endings, leave each `CutsceneData.nextSceneName` empty so `CutsceneEnded` can drive case completion. A cutscene with its own destination owns travel instead.
7. Missing ending media currently completes the ending immediately. With a live Core, case completion routes to Title; without Core, no GSM travel occurs. Do not mistake either fallback for verified cinematic playback.

Controls include **E** for interaction/dialogue/cutscene advance, **G** for journal, and holding **Esc** to skip a cinematic. **H is investigation/finale-only**, not a hub shortcut. WeightedBoardController guards opening and gameplay mutations through its scene-owned permission callback; the retained hub BoardHotkey rejects Mode2D. The dedicated Case 5 host explicitly authorizes its board. Cases with evidenceGraph cannot fall back to legacy slot gameplay on a missing/invalid weighted panel. Modal coordination runs through `HubContext.UiBlocked`/the scene composer's `CanOpen`: journal, board, interrogation, resolution, dialogue, cutscene, travel and rest all gate each other, and the journal registers itself as a blocker while open. Play-mode verification of cursor/pause behavior across delivered scenes is still outstanding.

## Content and asset authoring

### Case data

- Cases 0-5 have data assets and wired `EvidenceGraphData`. Cases 0-1 use approved datasets; Cases 2-5 are provisionally approved pending tuning. Data existence is not integration readiness. Rebuild Campaign Wiring no longer builds stand-ins or installs hub boards. Legacy stand-in assets/menus remain isolated compatibility fixtures; normal investigation entry uses the explicit catalog and opt-in simulation, with no automatic missing-scene success.
- Follow the approved GDD/case documents for content decisions. The team's authoritative Obsidian documents are maintained outside this checkout; obtain them from the project owner rather than inventing story facts or assuming another contributor has the same local vault path.
- Keep clue, premise and inquiry-node IDs stable. Save restoration relies on them.
- Run **Morsoul > Data > Validate All Cases** after content edits, **Rebuild Clue Registry** after adding/removing clue assets, and **Rebuild Case Database** after adding/removing case assets. **Morsoul > Setup > Rebuild Campaign Wiring** runs the case-database rebuild plus the hub CaseDoor installer in one step. Review the generated asset diff and its `.meta` files.
- Run **Morsoul > Data > Audit Evidence Graphs** after editing an `EvidenceGraphData` asset: it validates the C1-C7 constraints and enumerates every authored-link subset through the solver, reporting unlock counts per conclusion and flagging any board that unlocks the complete conclusion without the full solution set.
- Generator menus exist for Cases 0+1, 2, 3, 4+5, Case 1 hub dialogue, opening and Case 0 outro. They are authoring tools, not routine startup steps. Regeneration may replace manual content.

### Art and audio destinations

| Asset | Destination / assignment |
| --- | --- |
| Character sprites | `Assets/Art/Characters`; bind through `Assets/Art/Profiles` |
| Backgrounds and set dressing | `Assets/Art/Environments`; place in the matching hub art overlay |
| Dialogue portraits | `Assets/Art/Portraits`; assign dialogue data references |
| Cutscene stills/video | `Assets/Art/Cutscenes`; assign cutscene shot data |
| UI textures | `Assets/Art/UI`; assign the relevant UI layout/component |
| Music, effects, voice | `Assets/Audio/Music`, `SFX`, `VO`; play through `core.Audio` (volumes in PlayerPrefs, music/ambience crossfades, cinematic ducking) |

`VisualProfile` holds sprite, tint/material, offset, scale, sorting and optional animator controller. `EntityVisual` applies it and keeps the blockout quad when no sprite exists. Optional animator parameters are `Moving` (Bool), `Speed`, `MoveX`, `MoveY` (Float).

Use **Morsoul > Hub > Install Art Pipeline Into Hub Scenes** to upgrade saved hubs without regenerating layouts. Save dirty scenes first. Keep art in `Hub_LowerMarket_Art` / `Hub_Wilderness_Art`; `HubArtSceneLoader` owns additive loading/unloading. Existing custom profiles and art overlays should be preserved.

`Assets/Art/ArtImportSettings.asset` defines category-specific first-import defaults. Later artist import overrides are preserved. Folders and import presets do not provide finished sprites, animation, audio or VO. Final media must still be supplied, assigned and tested.

## Verification

### Editor

- **Window > General > Test Runner > EditMode > Run All** executes tests.
- **Morsoul > Data > Validate All Cases**, **Validate Dialogue Assets**, **Validate Cutscene Assets** validate authored content.
- `bash build.sh` from the repository root is a **compile check**, not a Unity player build, test run or scene validation. It needs an imported checkout's `Library/ScriptAssemblies`. On another Windows installation, set `UNITY_DATA` to that installation's `Editor/Data` directory.

### Batch EditMode tests (PowerShell example)

Close Unity for this project first. Set the two paths for your machine, then run:

```powershell
$unity = "C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe"
$project = (Get-Location).Path
& $unity -batchmode -projectPath $project `
  -executeMethod Morsoul.Flow.Editor.BatchTestRunner.RunEditMode `
  -testResults "$project/Library/EditModeResults.xml"
```

**Do not add `-quit` to this asynchronous runner.** Its completion callback writes XML and exits. The command exits 0 on no reported failures, 1 on failed tests, 2 on results-writing failure. Inspect the XML rather than treating editor startup or exit alone as proof of a run. The runner source's example currently still includes `-quit`; use the command above.

Never run a second Unity process against an open project. Wait for Unity batch shutdown before running `build.sh`, because shutdown can clear `Temp/build`.

### Manual acceptance checks

- Title alone > New Game > hub, with no duplicate player/camera.
- Collect clues/flags > checkpoint > stop Play > Continue > restored journal and state.
- Missing scene request warns without stranding Continue.
- Board validation > checkpoint > reload > premises and weighted-board histories restored. Test actual Unity lifecycle order, not only handler calls.
- Interrogation mistakes, board errors, wrong conclusions and hint tiers affect grade; mistrial waits for Retry confirmation and retains its penalty after reload.
- Move in a hub > checkpoint > quit > Continue > position/facing restore; missing checkpoint scenes fall back to the case hub, then Title, without applying the old pose.
- Prologue acceptance: solve Case 0 > unlock/cross the gate > restore at `(-8, 0)` > walk into the Eliza trigger at `(8, 0)` > interrupt and Continue to verify the pre-contact resume > finish and verify `ElizaIntroSeen`, `EasternRouteUnlocked`, `PrologueCompleted`, Case 1 and the `Hub_LowerMarket` route.
- Case 1 development acceptance: enable the explicit fallback, talk to Ovan/Ignatius for testimony/warrant > interact with VaultGate > verify marked simulated eligible clues and accepted deduction without a stand-in load > talk to Ignatius again > complete INT_01 > verify grade/Unease, next hub, checkpoint and Continue. H and the retired CaseDoor must not open a board in the hub. Test actual board UI in an investigation host or the dedicated finale, not by restoring hub shortcuts.
- Campaign spine acceptance: progress through the four hubs with development fallback enabled, satisfy each normal access flag, enter its catalog-backed gate, then interrogate Accord/Vane/Harlan through their NPC interactions. Confirm tool unlocks before Cases 3/4, no hub boards, no replayed rewards, preserved locked logs and Brokk's post-confession terminal gate. At campaign Case 5 revisit earlier entrances for newly eligible logs without changing CurrentCase; only Iron Hollow's finale entry may enter Case 5. Disable fallback to verify unintegrated refusal. Continue must restore real return context or clear stale poses on a missing-scene fallback.
- Graceful quit > reopen > Continue > offline Focus recovery applies once; a backwards clock grants zero without negative time.
- Collect logs across scenes > verify Session count and journal display > test each ending with real playback and return to Title.
- Reopen hubs with profiles/art overlays; rerun installer on a test copy and confirm custom assignments survive.
- Inspect scenes after a real 2D/3D branch merge and verify the active Build Profile.

The restructuring checkpoint executed **348 EditMode tests: 348 passed, 0 failed**, and `bash build.sh` reported **0 errors, 0 warnings**. These checks cover the tested contracts and compile surface, not full Play-mode campaign, media or serialized-scene acceptance.

## Remaining 2D-scope work

### Investigation restructuring (in progress)

`Assets/Resources/InvestigationIntegrationCatalog.asset` defines explicit per-area readiness and per-area fallback opt-ins. All Case 0–4 investigations are currently NotIntegrated; Case 5 is NotApplicable. The global default is off. In Editor/Development builds, enable **F1 > Session > Investigation development fallback** before New Game or entering a case gate. `InvestigationEntryDriver` installs the entry adapter at runtime: New Game dispatches the opening through it, and route-keyed investigation gates dispatch after normal access checks. Integrated areas with broken routes fail rather than simulate. Cases 1–4 simulate in the current hub; Case 0 proceeds to the real `Prologue_Wilderness` approach without marking Eliza's scene complete. No stand-in scene is loaded by this path.

`ClueAcquisition.CanAcquire` checks tool, combined acquisition, story-flag, log availability and post-Case-4 revisit restrictions. `ClueData.requiredFlag` is additive authored metadata; L4 requires `Case4_BrokkFreed`, set by the Case 4 confession outcome and preserved by its content generator.

`InvestigationCompletion.TrySimulate` is the headless development-only completion operation for the future entry adapter. It stages existing board history, adds only eligible clues and missing solution links, submits through `WeightedBoardSession`, then commits the normal once-only reward without advancing the campaign or opening interrogation. Locked/invalid/unsolvable states fail without partial mutation. Already-solved boards are preserved; lawful revisits can collect newly eligible logs. The entry driver enforces catalog/access gates, checkpoints changed state once and notifies bound hub clue/flag views. F1 displays status and simulation history; newly eligible clues can be collected on a later valid visit.

Provenance uses the existing sealed Session flags: `Investigation.Source.Real.<case>`, `Investigation.Source.Simulated.<case>`, `Investigation.SimulationUsed.<case>`, and `Investigation.SimulatedClue.<case>.<clueId>`. Use Session query methods rather than writing these flags directly. Old saves have Unknown source; revisits must not relabel accepted deductions. Real investigation entry captures the origin 2D scene/pose/facing and `InvestigationCaseId` in the sealed save, separately from campaign `CurrentCase`. Accepted boards return through GSM to that pose (or the case hub fallback) without advancing campaign case. The four delivered suspects are wired into 2D encounter entry; old non-finale CaseDoors are inactive. Physical legacy board objects/assembly dependencies remain for compatibility, but hub interaction/mutation is denied. Journal log-view hydration, full modal coordination, interrogation snapshots/abort restoration, finale confirmation/content and final campaign assets still require completion.

Before calling the current 2D vertical slice complete, verify and finish:

- Ending `CutsceneData` assignments per case on `GameFlow.endingCutscenes`, plus the Case 5 `entryCutscene` ("Echoes of the Past"); without them, endings complete immediately and the finale board opens directly, with no cinematic.
- Per-case grading and checkpoint semantics across all delivered scene transitions, including strained-scan state boundaries and mistrial replay.
- Save replacement/recovery behavior on target filesystems; the unsupported-Replace fallback is not yet proven.
- Assigned ending/opening media, portraits, animation, final UI art and audio clips for `core.Audio` playback (music/ambience/SFX sources exist; authored clips do not).
- End-to-end Play testing and merged-scene inspection.

Out of scope for the current 2D pass: replacement Zone B scenes and other non-2D integration. Do not create throwaway 2D investigation scenes; `investigation.2`-`investigation.4` stay unresolved and their gates warn/refuse cleanly.

When a contract, route, scene or asset workflow changes, update this README and the relevant rules in AGENTS.md in the same change. Do not replace verified limitations with completion claims just because an engine compiles.
