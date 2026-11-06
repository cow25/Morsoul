# Morsoul: World's Case

Unity investigation game combining 2D hubs, 3D investigations, dialogue, deduction, interrogation and case resolution.

**Contributors: start here. Agents: read [AGENTS.md](AGENTS.md) before changing anything.** That file owns repository rules, lane boundaries and merge instructions. This README is the system map and onboarding guide, not a declaration that the full campaign is finished.

## Quick start

1. Install **Unity 6000.6.7f1**, matching [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Do not upgrade the editor or packages as part of unrelated work.
2. Clone your assigned branch. In the checkout, run `git lfs install` and `git lfs pull` before importing assets.
3. Add this repository folder in Unity Hub and let Unity restore packages and import assets. Do not commit `Library`, `Temp`, `Logs` or generated IDE files.
4. Read the ownership rules below. The 2D/Core work is developed on branch `2D`; coordinate integration with the 3D owner rather than assuming both branches have identical scenes.
5. Open `Assets/Scenes/Title.unity` **by itself** and enter Play. Do not leave the market scene loaded alongside Title: both scenes' players and cameras would run.
6. New Game currently falls back to `Hub_LowerMarket` when no opening route is configured. Continue requires a readable checkpoint.
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
| `Engine` | Pure weighted-board solver: directed-link evidence DAG, no Unity deps | `EvidenceGraph`, `DeductionEngine`, `DeductionCertificate` |
| `Board` | Clue pairing, causal prompts, premises and master hypothesis (superseded model, pending migration to `Engine`) | `DeductionBoard` (rules), `BoardController` (UI) |
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
- `SessionState` is live cross-scene truth: clue IDs/assets, flags, validated board node IDs, log slots, grades and unlocked tools.
- `SaveState` is the serialized checkpoint format. Scene systems should update Session and request a checkpoint rather than editing save JSON directly.
- `HubJournal` and `HubFlags` are scene-scoped stores, not competing persistent owners. Runtime composition must bind their views/listeners.
- `CaseSession` tracks grading within the active case. `GameFlow` coordinates panel events and writes results back into Session.
- Checkpoints live at `Application.persistentDataPath/morsoul_checkpoint.json`. HMAC signatures detect edits; a key embedded in a game client is not tamper-proof security. The primary save replacement path uses `File.Replace`; unsupported-filesystem fallback/recovery still needs platform validation.

### Scene routing

Routes are configured through `GameStateManager.Ensure().routes.Set(key, actualSceneName)`.

| Route key | Current default |
| --- | --- |
| `title` | `Title` |
| `caseloop` | `CaseLoop` |
| `hub.0`, `hub.1` | `Hub_LowerMarket` |
| `hub.3` | `Hub_Wilderness` |
| `opening` | Not seeded |
| `investigation.<caseId>` | Not seeded; register only delivered scenes |

A route entry does not add a scene to a Build Profile. Register real scenes and add them to the active build list together. Do not add nonexistent `C1_Vault` paths to suppress warnings. Continue attempts to fall back to the saved case's hub when its checkpoint scene is unavailable. Not every case currently has a hub route.

## 3D integration checklist

Read [the full contract](AGENTS.md#3d-integration-contract) before adapting the prototype scripts.

1. Cache `CoreRuntime.Ensure()` in the scene adapter. Keep the existing 3D player/spawn owner; do not add the 2D `SceneEntryPoint` beside `PlayerSpawner`.
2. While actively scanning, call `core.Focus.TickScan(Time.deltaTime)` **once per frame**. Display `Percent` on a 0-100 scale. Zero Focus must not block scanning: strained scans remain functional. Rest regeneration uses `TickRest` only during rest.
3. Resolve a stable authored clue ID using `core.ResolveClue(id)`. Reject an unresolved ID visibly; do not mark collection successful without its asset.
4. If the scene has a bound `GameFlow`, call `flow.HandleClueRegistered(clue)`. It forwards to the journal/Session and handles latent clues. Do not independently fire several collection pipelines.
5. Without GameFlow, `core.Session.RegisterClue(clue)` persists the clue. For an actual latent clue, also call `RegisterLatentClue(clue)` after tool/access checks. The current convention maps `L1_01` through `L4_04` to log slots 0-3 and per-case latent flags. Explicit `MarkRunicLogById(log.id)` uses **1-based** log IDs; `MarkRunicLog(slot)` uses **0-based** slots. Do not infer collection just from entering a zone.
6. Use `Session.SetFlag` / `HasFlag` for persistent story state. Already-bound scene flag/journal views may need refreshing; a Session write alone is not a UI binding.
7. For a checkpoint without travel, publish `CheckpointRequestedEvent { Reason = "Investigation" }` on `core.Bus`. For travel, publish `SceneLoadRequestedEvent { SceneName = actualSceneName }`. Do not also call `SceneManager.LoadScene` for the same request.
8. If adding a 3D asmdef later, reference only the Morsoul assemblies it uses. Coordinate the migration and preserve script GUIDs.

The legacy `ScanController` still has its own 120-point focus and different zero-focus behavior. `DiaryManager` stores legacy `ClueNode` objects. Their conversion is a 3D-owner integration task, not evidence that the shared contracts are already in use.

## Running and composing a case

### Existing scenes

- **Title:** normal New Game / Continue entry point.
- **Hub_LowerMarket / Hub_Wilderness:** generated 2D gameplay scenes with optional additive art overlays.
- **PrologueRuins:** 3D-owned scene. Do not regenerate or restructure it from the 2D lane.
- **CaseLoop:** shared case-loop scene. With no `activeCase` assigned, its `GameFlow` resolves `Session.CurrentCase` through `Assets/Resources/CaseDatabase.asset` at Play time; assign `activeCase` only to pin one case for standalone testing. Hub scenes have a "CaseDoor" interactable (`[E] Begin Case`) that travels here through the guarded GSM path.

### Campaign progression

- Entering the case loop begins `Session.CurrentCase`. Completing a case without an ending advances `CurrentCase` and routes `hub.<id+1>` -> `hub.<id>` -> `title` in order until one loads. An ending (`Ending != 0`) routes to Title after its cutscene.
- `Case_05` (board-only, authored `preloadedClues`) can be played end-to-end today; investigation cases still need their evidence collected into Session first (or a 3D scene delivering it).

### CaseLoop setup

1. Open `Assets/Scenes/CaseLoop.unity`. To pin a case for standalone testing assign `GameFlow.activeCase` (e.g. `Assets/Data/Cases/Case05/Case_05.asset`); leave it unassigned for session-driven play.
2. Check the board, interrogation, resolution, cutscene, dialogue, journal and flag-store references. The factory creates the panel wiring, but it does not author all content.
3. For production/session-backed testing, ensure Core exists **before** `BeginCase` — entering via the hub CaseDoor or Title guarantees this. Starting CaseLoop alone does not automatically create Core. Without Core, GameFlow retains standalone evidence/log-count fallbacks; that mode is not a persistence test.
4. Investigation cases use Session-collected clues at case initialization when Core is live. Board-only authored `preloadedClues` take precedence. Late collection needs an explicit rack refresh through `BoardController.SetClues`; do not assume every Session change rebuilds every view.
5. Bind journal/dialogue flags at runtime with `SetFlags(hubFlags.Set)`. The flag set is not serialized; calling SetFlags only inside an editor generator does not persist that binding. Populate journal `allLogs` and synchronize its displayed collected logs with Session as part of scene composition.
6. Assign `GameFlow.endingCutscenes[1]`, `[2]`, `[3]` (index 0 unused). For GameFlow-owned endings, leave each `CutsceneData.nextSceneName` empty so `CutsceneEnded` can drive case completion. A cutscene with its own destination owns travel instead.
7. Missing ending media currently completes the ending immediately. With a live Core, case completion routes to Title; without Core, no GSM travel occurs. Do not mistake either fallback for verified cinematic playback.

Controls exposed by the controllers include **E** for hub interaction/dialogue or cutscene advance, **G** for journal, **H** for board, and holding **Esc** to skip a cutscene. Scene composition must prevent overlapping modal input. The journal pauses simulation while open.

## Content and asset authoring

### Case data

- Cases 0-5 have data assets. Data existence does not imply a corresponding playable investigation scene or registered campaign route.
- Follow the approved GDD/case documents for content decisions. The team's authoritative Obsidian documents are maintained outside this checkout; obtain them from the project owner rather than inventing story facts or assuming another contributor has the same local vault path.
- Keep clue, premise and inquiry-node IDs stable. Save restoration relies on them.
- Run **Morsoul > Data > Validate All Cases** after content edits, **Rebuild Clue Registry** after adding/removing clue assets, and **Rebuild Case Database** after adding/removing case assets. **Morsoul > Setup > Rebuild Campaign Wiring** runs the case-database rebuild plus the hub CaseDoor installer in one step. Review the generated asset diff and its `.meta` files.
- Generator menus exist for Cases 0+1, 2, 3, 4+5, Case 1 hub dialogue, opening and Case 0 outro. They are authoring tools, not routine startup steps. Regeneration may replace manual content.

### Art and audio destinations

| Asset | Destination / assignment |
| --- | --- |
| Character sprites | `Assets/Art/Characters`; bind through `Assets/Art/Profiles` |
| Backgrounds and set dressing | `Assets/Art/Environments`; place in the matching hub art overlay |
| Dialogue portraits | `Assets/Art/Portraits`; assign dialogue data references |
| Cutscene stills/video | `Assets/Art/Cutscenes`; assign cutscene shot data |
| UI textures | `Assets/Art/UI`; assign the relevant UI layout/component |
| Music, effects, voice | `Assets/Audio/Music`, `SFX`, `VO`; playback wiring remains a separate integration task |

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
- Board validation > checkpoint > reload > premises restored. Test actual Unity lifecycle order, not only handler calls.
- Interrogation mistakes affect grade; mistrial waits for Retry confirmation and retains its penalty after reload.
- Collect logs across scenes > verify Session count and journal display > test each ending with real playback and return to Title.
- Reopen hubs with profiles/art overlays; rerun installer on a test copy and confirm custom assignments survive.
- Inspect scenes after a real 2D/3D branch merge and verify the active Build Profile.

The last recorded automated run during the integration work was **189 passed, 0 failed**. That is historical evidence, not a guarantee for later edits and not a full Play-test result.

## Remaining integration work

Before calling the campaign complete, verify and finish:

- Delivered 3D investigation scenes, their `investigation.<caseId>` routes and build-profile entries, plus hub routes for cases 2, 4 and 5.
- Ending `CutsceneData` assignments per case on `GameFlow.endingCutscenes`; without them, endings complete immediately with no cinematic.
- 3D prototype Focus/clue migration through the shared APIs.
- Board lifecycle/late-evidence refresh, journal flag/log refresh and the journal's `MarkedForBoard` action consumer.
- Per-case grading and checkpoint semantics across all real scene transitions, including strained-scan state boundaries and mistrial replay.
- Save replacement/recovery behavior on target filesystems; the unsupported-Replace fallback is not yet proven.
- Assigned ending/opening media, portraits, animation, final UI art and audio playback.
- End-to-end Play testing, modal input coordination and merged-scene inspection.

When a contract, route, scene or asset workflow changes, update this README and the relevant rules in AGENTS.md in the same change. Do not replace verified limitations with completion claims just because an engine compiles.
