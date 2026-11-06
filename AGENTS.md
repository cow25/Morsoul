# Morsoul project rules

Read [README.md](README.md) for contributor setup, the system/folder map, runtime composition, asset destinations, verification commands and remaining integration work. This file owns agent constraints and ownership rules. Keep both documents aligned when changing public contracts or workflows.

## Ownership and merge boundaries

- The user performs all commits. Agents must not commit, push, change Git configuration, or migrate LFS history.
- Unity version: 6000.6.7f1. Keep Force Text serialization enabled and commit every asset together with its existing `.meta`. Never recreate GUIDs to fix a conflict.
- 2D/Core owner: `Assets/Scripts/{Core,Data,Board,Engine,Gameplay,Flow,Hub,Dialogue,Journal,Interrogation,Cutscene,Title,Resolution}`, their editor generators, UI assets and prefabs, `Title`, `Hub_LowerMarket`, `Hub_Wilderness`, and the demo stand-in scenes `Prologue_2D` and `Investigation_01_Placeholder` (retired once the 3D prologue/vault replace their routes).
- 3D owner: `Assets/Scenes/PrologueRuins.unity`, future investigation scenes, root-level `PlayerController`, `ScanController`, `HUDManager`, `DiaryManager`, and `Assets/Scripts/{Interaction,Player,Systems}`. Do not move or edit these without coordinating with that developer.
- Shared changes requiring coordination: Packages, ProjectSettings, input actions, render pipeline settings, tags/layers, build profiles, Core public contracts and data schemas. Choose one editor per shared asset at a time.
- Use independent prefabs or art-only additive scenes for parallel scene work. An additive art scene must contain no second player, camera, input listener or Core runtime. It requires an explicit load/unload owner; merely creating the file does not integrate it.
- Hub and content generators can overwrite designer changes. Do not regenerate a hand-authored scene or asset without reviewing the confirmation and backing up the work. Keep art outside generator-owned assets.
- Existing ScriptableObject definitions and GUIDs are integration contracts. Do not rename IDs or alter save semantics without coordinating both lanes.

## Git and Unity merging

`.gitattributes` selects the `unityyamlmerge` driver but does not install it. Each developer must configure it locally. Run the following yourself in Git Bash after verifying the Unity executable path:

```sh
git config --local merge.unityyamlmerge.name "Unity Smart Merge"
git config --local merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "%O" "%B" "%A" "%A"'
git config --local merge.unityyamlmerge.recursive binary
```

Run `git lfs install` on each checkout and `git lfs pull` before opening Unity. LFS patterns do not retroactively convert committed binaries; history migration requires separate explicit approval from the whole team. `.asset` files may be binary, so no blanket text conversion is applied. Smart Merge reduces conflicts; it does not make simultaneous edits safe. Inspect merged scenes in Unity and never resolve all conflicts by blindly taking one side.

Build Settings list only existing scenes, with Title first. Coordinate additions when investigation scenes arrive; do not add fake paths or guessed GUIDs. Active Build Profiles may override the shared scene list, so inspect the active profile during Play testing.

## 3D integration contract

Do not add a second service locator or local copy of Focus. Assembly-CSharp can reference the auto-referenced Morsoul assemblies. If the 3D lane later adds an asmdef, explicitly reference `Morsoul.Core`, `Morsoul.Data`, and `Morsoul.Flow` only when used. Core must never reference that asmdef.

- Bootstrap from any scene with `Morsoul.Core.CoreRuntime.Ensure()`; cache the result in the scene adapter. Do not create another persistent player or camera under Core.
- While actively scanning call `core.Focus.TickScan(Time.deltaTime)` once per frame; display `core.Focus.Percent` out of 100. Do not block scans at zero: strained scanning remains functional. Call `TickRest` only while resting. Remove the local 120-point counter only in the 3D owner's integration change.
- Author stable clue IDs matching the `ClueData` assets, for example `C1_01`. The existing 3D `ClueNode` has its own enums and does not currently register into Core. Resolve its ID with `core.ResolveClue(id)` and reject unresolved IDs visibly instead of marking collection complete.
- When a `GameFlow` is bound in the scene, use its `HandleClueRegistered(clue)` so interrogation evidence, the hub journal and persistent Session receive the same collection. For a scene without GameFlow, `core.Session.RegisterClue(clue)` persists it but does not refresh already-bound UI. Scene composition must choose one collection adapter, not call both independently.
- For a recovered latent clue, a bound `GameFlow.HandleClueRegistered(clue)` registers its per-case latent flag and derives the Runic Log slot from the current `L<n>_<case>` ID convention. Without GameFlow, call `Session.RegisterClue(clue)` and `Session.RegisterLatentClue(clue)` after tool/access checks. `MarkRunicLogById(log.id)` accepts 1-based log IDs; `MarkRunicLog(slot)` accepts 0-based slots. Plain `RegisterClue` does not mark logs. Use `IsLatentLogCollected(caseId)` for per-case checks, not the legacy session-wide latch. Refresh journal log views explicitly; persistent registration alone does not bind UI.
- Story flags use `core.Session.SetFlag(key)` and `HasFlag(key)`. Do not introduce another persistent diary or flags singleton.
- For a checkpoint without travel publish `new CheckpointRequestedEvent { Reason = "Investigation" }` on `core.Bus`. To travel publish `new SceneLoadRequestedEvent { SceneName = actualSceneName }`; GSM validates availability, checkpoints and loads. Do not also call SceneManager.LoadScene or publish a second travel checkpoint for the same request.
- `GameStateManager.Ensure().routes.Set(key, sceneName)` configures routing. Use `investigation.<caseId>` and `hub.<caseId>` keys; `caseloop` -> `CaseLoop` is seeded and shared (it resolves `Session.CurrentCase` via `CoreRuntime.ResolveCase` and `Assets/Resources/CaseDatabase.asset` — run **Morsoul > Setup > Rebuild Campaign Wiring** after adding case assets). Agree actual scene names before registration; a route is not proof a scene exists in the active build profile. Case 5 is board/cutscene-only and must not gain an investigation route.
- Use the existing 3D spawn owner. `Morsoul.Hub.SceneEntryPoint` is a 2D helper, not a mandatory 3D component; do not run it alongside the 3D PlayerSpawner.
- Keep current 3D root scripts and PrologueRuins GUIDs unchanged during this handoff. A folder/asmdef migration is a coordinated later change.

### Known readiness gaps

Core and panel engines are not equivalent to a complete playable campaign. The Algorithm.md weighted board (Morsoul.Engine + WeightedBoardController) is wired for cases with `CaseData.evidenceGraph`; legacy `DeductionBoard` remains for cases without one, and no case dataset is authored yet — do not invent graph numbers (spec-blocked). Wired since the audit: board racks use collected-session evidence (preloadedClues still win for board-only cases), validated board nodes restore on Continue, interrogation strikes and strained-scan state feed grading, the mistrial penalty survives the checkpoint reload and the reload waits for the player's Retry confirm, latent `L<n>_<case>` clues mark their runic-log slot + per-case unease flag, ending selection reads `SessionState.RunicLogCount`, and requested endings play `GameFlow.endingCutscenes` before `CaseCompletedEvent` routes home. Gate/carrier travel goes through the GSM's guarded load when Core is live (direct `SceneManager` only without it). Campaign progression is wired: hub CaseDoor -> CaseLoop (session case via CaseDatabase) -> completion advances `CurrentCase` and routes next hub -> current hub -> title. Still open before shipping: author and register every delivered scene route (investigation scenes and remaining hub routes must not be faked), assign ending cutscene data per case, and Play-test the full loop. Validate these with the 3D lane before claiming integration complete.

## Art integration workflow

- `Morsoul > Hub > Install Art Pipeline Into Hub Scenes` upgrades saved hubs without regenerating their layouts. Save any dirty hub scene first; use a saved scene rather than an untitled editor scene. The installer preserves existing art overlays and appends only existing overlays to the shared build list.
- Assign sprites, tint, material, visual offset/scale and optional animator controller in `Assets/Art/Profiles/<EntityName>.asset`. Player's profile is shared across both hubs. A missing sprite keeps the blockout quad. `EntityVisual` reapplies its serialized profile on scene load, so assigning art does not require hub regeneration. For immediate editor refresh after changing a profile asset, reapply/reselect its EntityVisual component or reopen the scene.
- Optional animation parameters: `Moving` (Bool), `Speed`, `MoveX`, `MoveY` (Float). Names are configurable on HubCharacterAnimator. Only existing correctly typed parameters are driven.
- Put background/set dressing in `Hub_LowerMarket_Art` or `Hub_Wilderness_Art`; keep gameplay, cameras and players in the hub scene. HubArtSceneLoader loads the art overlay additively and owns its cleanup. Factories do not overwrite existing overlay scenes.
- `Assets/Art/ArtImportSettings.asset` configures category-specific first-import defaults for new Art/Audio folders. Reimports preserve artist overrides. `.keep` files retain empty category folders in Git. Sprites still need actual art; the pipeline does not generate finished assets.
- Cutscene stills/video and dialogue portraits use their existing data references. Assign final assets there instead of modifying the generator's hardcoded placeholder assignments and regenerating authored content. UI textures belong under Art/UI, but this pass does not redesign or replace existing UI visuals.

## Verification

- `bash build.sh` from the repository root compiles Morsoul runtime, editor and EditMode test assemblies using the installed Unity compiler. It does not execute tests or validate serialized scenes.
- Unity: Window > General > Test Runner > EditMode > Run All. Do not claim tests pass from a compile check alone.
- Run Morsoul > Data > Rebuild Clue Registry after adding clue assets, then inspect the generated diff.
- Run Morsoul > Data > Audit Evidence Graphs after editing EvidenceGraphData: it validates C1-C7 and enumerates every authored-link subset through the solver. The intended result is exactly one board unlocking the complete conclusion and no unlock path missing part of S*. EVG_00/EVG_01 are provisional (regenerate via Morsoul > Data > Draft Provisional Evidence Graphs); replacing provisional numbers is a design sign-off, not a code change.
- Smoke-test Title > New Game > Hub; checkpoint > stop Play > Continue; journal restoration; missing-scene fallback. Inspect the active Build Profile if a scene refuses to load.
- Do not run a second Unity process against an already-open project to execute tests. Run `build.sh` after Unity batch processes exit, not concurrently: Unity clears Temp on shutdown and can remove the compiler's Temp/build output mid-run.
