# All Mighty One — Remaining Work

Last updated: 2026-10-30 (after the quick-fix pass)

Everything still open, most important first. Items fixed today are listed at the end. None of today's
changes are committed yet.

---

## 1. Release blockers (must be done before Google Play)

| # | Item | Where | Who | Notes |
|---|------|-------|-----|-------|
| 1.1 | Unity Ads **Android Game ID** is empty | `Assets/_Collection/Scripts/Monetization/UnityAdsService.cs:14` | You (ID from the Unity Ads dashboard), then Claude | While it is empty, ads run in placeholder mode and earn nothing. The iOS ID on line 15 is only needed if the game ships on iOS. |
| 1.2 | **GitHub Pages is not switched on**: the privacy URL gives a 404 | GitHub repo → Settings → Pages | You | The game now links to `https://brianromanss.github.io/All-Mighty-One/`. The repo is public and `docs/index.html` and `docs/terms.html` are pushed, but Pages is off. Switch on "Deploy from a branch: main, /docs". **Note:** `docs/` also holds internal studio files (`CLAUDE.md`, the architecture and adoption docs, `engine-reference/`); Pages would publish those too. Moving the two policy pages to their own folder (e.g. `site/`) avoids that. Ask Claude if you want it. |
| 1.3 | No **release keystore** | Player Settings → Publishing Settings | You | Create it and keep a backup: Play needs the same key for every update. |
| 1.4 | No **consent prompt** for analytics and ads, and consent is on by default | `CollectionAnalytics` (pref `AMO_AnalyticsConsent`), `AdsBridge` / `UnityAdsService` | You decide, Claude builds | Not a quick fix: it needs a decision first. Option A is Google's UMP consent SDK (the standard for EU/UK and what Play reviewers expect). Option B is our own first-launch popup: it sets the analytics pref and Unity Ads' consent metadata, and asks before UGS or ads start. |
| 1.5 | No **Android device build** has been made yet | — | You + Claude | Needs a device and the keystore. |

## 2. Google Play store setup

- [ ] Fill in the **Data safety** form. It has to match `docs/index.html`: analytics, ads, no purchases.
- [ ] Define every analytics event in the Unity dashboard **Event Manager**: `game_start`, `level_start`, `level_complete`, `level_fail`, `run_end`, `game_unlock`, `ad_interstitial`, `ad_rewarded`, `daily_reward`, `mission_claim`.
- [ ] Check Google Play's **accessibility requirements** for submission.
- [ ] Upload the store listing: the 8 captioned screenshots are in `ArtPrompts/kept/Store screenshots`, and the icons are in `Art/Store/`.
- [ ] Final **app name** (Block Puzzle's in-game logo name is also still your call).
- [ ] The hub's Settings popup grew a row (Reduce Motion), so retake the store screenshot if one of them shows Settings.

## 3. Testing and performance on a real device

- [ ] Measure **FPS / battery** on a real Android phone. The 60 fps target and mobile quality settings have only been checked in the editor.
- [ ] Check **build size and load times** (never measured).
- [ ] Feel-test the **juice pass** on a phone: button squash, end-screen pops, confetti, lose flash, gem count-up, and the Reduce Motion switch.
- [ ] Check the px/dp conversions on a phone (32px ≈ 14sp, 48dp ≈ 115px).
- [ ] Known flaky tests to watch:
  - `BlockAdventureTests.GreedyPlayerReachesTheTarget(24)` (random trays).
  - `ThreeRoadsTests`: one session died mid-test on 2026-10-30 and passed when rerun alone.
  - `BreakTests.Nav_HoppingThroughEveryGameLeavesNoDuplicates`: times out on a cold first scene load after a re-import (8 s limit). Raise the limit if it keeps happening.

## 4. Decisions waiting on you

- [ ] **Double confetti**: Cubey, Splat and the runner finish lines already have the kit's 3D confetti, so wins in those games show both kinds. Keep both, or skip the new confetti in those games?
- [ ] **Commit** today's work (juice pass + quick fixes). The file list is in section 8.

## 5. UI / visual

- [ ] **Win/lose screens consistency** check across all 12 games (left open since polish Phase A).

## 6. Accessibility (from `design/accessibility-requirements.md`)

Nothing has been audited yet. Open questions:

- [ ] Show a one-time **photosensitivity notice** at first launch? (your call, before store release)
- [ ] Put Music / Sound / Vibration switches in each game's **pause menu**, or keep them in the hub only?
- [ ] Can a dismissed **first-play hint** be shown again later?
- [ ] Should Reduce Motion default from Android's **"remove animations"** setting?
- [ ] Does Unity 6000.0 have a runtime accessibility API that works with **TalkBack**?
- [ ] **Per-game audits**: color-only signals (Splat tiles are a known candidate), gameplay-critical sounds, hold inputs, multi-finger gestures, games without a pause.
- [ ] **Text contrast** check (WCAG AA) and **color audit** of the 12 games and the hub.
- [ ] `/ux-review` was requested earlier and has not been run.

## 7. Housekeeping

- [ ] `production/session-state/active.md` should be re-created from `.claude/docs/templates/session-state.md` (startup hook warning).
- [ ] No game brief, GDDs or sprint plan exist (startup hook warning). These are optional for a finished collection, but `/project-stage-detect` or `/sprint-plan` can create them.

---

## 8. Done on 2026-10-30 (uncommitted)

**Juice pass**
- New `CollectionJuice`: confetti on win screens, red edge flash on a lost run, and end-screen panels that pop in.
- Every in-game button squashes when pressed.
- The gem counter counts up and bumps.
- New `CollectionFeedback.ReducedMotion` setting.
- Evidence is in `production/qa/evidence/2026-10-30-juice/`.

**Quick fixes**
- **Privacy URL**: the hub's Privacy button now opens `https://brianromanss.github.io/All-Mighty-One/` instead of `example.com`. It changed in `HubController.cs` and `Hub.unity`. It works once Pages is on (see 1.2).
- **SampleScene** is removed from the build list (`ProjectSettings/EditorBuildSettings.asset`). No scene loads by build index, so nothing else shifts.
- **Bull Dodge lose screen**: the kit's extra "Tap To Continue" label is hidden. "TAP TO RETRY" remains, and the full-screen tap area still works. The change is in `Dodge.unity`, and `DodgeSetup` does the same if the scene is rebuilt.
- **Reduce Motion switch** added to hub Settings, under Vibration. The Settings popup is taller, the hub scene was rebuilt with `HubSetup.Run`, and a new test covers it (`PolishTests.ReduceMotionSwitchInSettingsSavesTheSetting`).
- `bash.exe.stackdump` is deleted, and `*.stackdump` is added to `.gitignore`.
- Removed from this list: the "insufficient coins vs starter pack" item belonged to a different project.

**Files changed**
- `.gitignore`
- `Assets/Most In One/Common/MOST/Common MOST/UniversalGameManager.cs`
- `Assets/Most In One/Dodge/Scenes/Dodge.unity`
- `Assets/_Collection/Editor/Setup/DodgeSetup.cs`
- `Assets/_Collection/Editor/Setup/HubSetup.Scene.cs`
- `Assets/_Collection/Scenes/Hub.unity`
- `Assets/_Collection/Scripts/Common/CollectionFeedback.cs`
- `Assets/_Collection/Scripts/Common/CollectionJuice.cs` (new)
- `Assets/_Collection/Scripts/Hub/GemCounter.cs`
- `Assets/_Collection/Scripts/Hub/HubController.cs`
- `Assets/_Collection/Tests/PlayMode/HubSnapshots.cs`
- `Assets/_Collection/Tests/PlayMode/PolishTests.cs`
- `ProjectSettings/EditorBuildSettings.asset`
- `design/accessibility-requirements.md`
- `production/qa/evidence/2026-10-30-juice/`
- `production/remaining-work.md`

**Tests**
- After the juice pass: 142/142 across all 16 classes.
- After the quick fixes: HubTests, PolishTests, BullDodgeTests and DailyTests all pass. BreakTests passes except `Nav_HoppingThroughEveryGameLeavesNoDuplicates`, which timed out twice right after the scene re-import and then passed on a third run. The cause is a cold first load (EpicRunner took 7.4 s to deserialize against the test's 8 s limit; a warm load takes about 1 s). The other 11 test classes were not rerun after the quick fixes; none of them touch the hub, the build list or Bull Dodge.
