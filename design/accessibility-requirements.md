# Accessibility Requirements

> **Status**: Committed
> **Author**: user + ux-designer
> **Last Updated**: 2026-10-26
> **Accessibility Tier Target**: Basic (+ 3 elevated items)
> **Platform(s)**: Android (touch, no gamepad)
> **Template**: Accessibility Requirements

## Accessibility Tier Definition

### Tier Definitions

| Tier | Core Commitment | Typical Effort |
|------|----------------|----------------|
| **Basic** | Critical player-facing text is readable at standard resolution. No feature requires color discrimination alone. Volume controls exist for music, SFX, and voice independently. The game is completable without photosensitivity risk. | Low — primarily design constraints |
| **Standard** | All of Basic, plus: full input remapping on all platforms, subtitle support with speaker identification, adjustable text size, at least one colorblind mode, and no timed input that cannot be extended or toggled. | Medium — requires dedicated implementation work |
| **Comprehensive** | All of Standard, plus: screen reader support for menus, mono audio option, difficulty assist modes, HUD element repositioning, reduced motion mode, and visual indicators for all gameplay-critical audio. | High — requires platform API integration and significant UI architecture |
| **Exemplary** | All of Comprehensive, plus: full subtitle customization (font, size, color, background, position), high contrast mode, cognitive load assist tools, tactile/haptic alternatives for all audio-only cues, and external third-party accessibility audit. | Very High — requires dedicated accessibility budget and specialist consultation |

### This Project's Commitment

**Target Tier**: Basic, with three elevated items

**Rationale**: All Mighty One is an Android-only collection of 12 touch mini-games
(runners, puzzle, endless dodge/survival) with a shared hub. Touch is the only
input, so Standard's input-remapping and gamepad criteria do not apply. Most
modules are reflex or timed play, so Standard's "no timed input that cannot be
extended" cannot be met as written without changing the games. Comprehensive's
screen-reader support needs TalkBack integration for runtime Unity UI, which is
outside current capacity. Basic is therefore committed in full, and the cheap,
high-value items that the shared feedback and audio systems make practical are
elevated.

**Features explicitly in scope (beyond tier baseline)**:
- Reduced-motion / screen-shake toggle — one setting that disables shake and
  flash-heavy effects from the shared feedback system (Comprehensive item).
- Haptics on/off toggle — lets players disable vibration.
- Non-color cues for gameplay-critical colors — shape, icon or label wherever
  color alone carries meaning, audited per game (Basic requires this; here it is
  checked module by module in the Per-Feature Matrix).

**Features explicitly out of scope**:
- Screen reader (TalkBack) support for menus and in-game UI — needs integration
  work beyond current capacity. Listed in Known Intentional Limitations.
- Input remapping and gamepad/keyboard navigation — touch-only target.
- Colorblind mode and adjustable text size for in-game HUDs — not elevated;
  non-color cues cover the color risk instead. Listed in Known Intentional Limitations.
- Extendable or toggleable timed input in the reflex modules — would change the
  core gameplay. Listed in Known Intentional Limitations.

---

## Visual Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Minimum text size — hub, menus, shop | Basic | All non-gameplay screens | Not Started | 32px minimum at the 1080×1920 reference canvas (≈14sp). Secondary labels 28px. UNVERIFIED px→sp conversion — confirm on a physical device. |
| Minimum text size — in-game HUD | Basic | All 12 modules | Not Started | 36px for critical readouts (score, level, lives, timer); 28px for non-critical. |
| Text contrast | Basic | All UI text | Not Started | 4.5:1 body text, 3:1 large text (WCAG AA), checked on final colors, including text over game scenes. |
| Color-as-only-indicator audit | Basic (elevated: audited per module) | All UI and gameplay | Not Started | See audit table. Each entry needs a shape/icon/label backup. |
| Screen flash / strobe | Basic | All VFX and shared feedback effects | Not Started | Audit flash-heavy effects against the no more than 3 flashes/second guideline (Harding FPA, per template). Reduced-motion mode also lowers flash amplitude. |
| Reduced-motion / screen-shake toggle | Comprehensive (elevated) | Shared feedback system, hub juice, camera shake in all modules | Not Started | Single setting, persisted in PlayerPrefs, read by the shared feedback layer so modules don't each implement it. Cannot remove gameplay movement. |
| Colorblind modes | Standard | — | Out of scope | Non-color cues are the mitigation. See Known Intentional Limitations. |
| UI scaling / adjustable text size | Standard | — | Out of scope | See Known Intentional Limitations. |
| High contrast mode | Comprehensive | — | Out of scope | See Known Intentional Limitations. |
| Brightness/gamma controls | Basic | — | N/A | Android system brightness is the control on this platform. |
| Subtitles | Basic | Voiced content | N/A unless voiced content is added | No voiced content identified. Revisit if any is added. |

### Color-as-Only-Indicator Audit

| Location | Color Signal | What It Communicates | Non-Color Backup | Status |
|----------|-------------|---------------------|-----------------|--------|
| Splat — board tiles | Painted vs unpainted color | Progress / which tiles remain | Not yet checked | Not Audited |
| All other modules and the hub | Not yet inspected | — | — | Not Audited |

---

## Motor Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Minimum touch target size | Basic | All buttons and tappable UI (hub, menus, shop, in-game buttons) | Not Started | 48dp minimum (Android Material guideline; UNVERIFIED against current Google docs) ≈ 115px at the 1080×1920 reference canvas (px conversion UNVERIFIED — confirm on device). Spacing between adjacent targets ≥ 8dp. |
| Single-finger gestures only | Basic | All 12 modules | Not Audited | No required multi-finger gesture, pinch, or simultaneous multi-touch. Audit each module; list exceptions in Known Intentional Limitations. |
| One-hand play (portrait) | Basic | All 12 modules + hub | Not Audited | Audit: can every module be played one-handed in portrait, with the primary controls reachable by one thumb? Record per module in the matrix. |
| Hold-to-press alternatives | Standard | Hold inputs | Not Audited | Hold inputs not yet listed. List them during the per-module audit; toggles are not committed (Standard item). |
| Input timing adjustments | Standard | Timed / reflex play | Out of scope | Would change core gameplay in the reflex modules. See Known Intentional Limitations. |
| Haptics on/off toggle | Basic (elevated) | All vibration feedback, including MA_HapticFeedback actions from the kit | Implemented in hub; module coverage Not Audited | Hub Settings popup has a Vibration switch (`MOST_HapticFeedback.HapticsEnabled`); `CollectionFeedback` honors it. Audit that kit `MA_HapticFeedback` actions inside modules also go through it. |
| Input remapping, input method switching, aim assist, HUD repositioning | Standard / Comprehensive | — | N/A or out of scope | Touch-only, no gamepad or keyboard. Aim assist and HUD repositioning not committed. See Known Intentional Limitations. |

---

## Cognitive Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Pause anywhere | Basic | All 12 modules | Not Audited | Audit each module for a reachable pause/exit during play. List any state where pausing is prevented, with the reason. |
| Reading time for UI | Standard | Auto-dismissing toasts and popups (daily reward, mission claim, unlock notices) | Not committed | No auto-dismiss audit committed. Revisit if any popup carries information the player must act on. |
| Tutorial persistence | Standard | First-play hints | Not committed | Open question: can a dismissed first-play hint be retrieved later? |
| Difficulty options | Standard | All modules | Out of scope | Fixed per-module difficulty. See Known Intentional Limitations. |
| Cognitive load documentation | Comprehensive | Per system | Out of scope | No systems-index exists at the minimal tier. |
| Quest/objective clarity, navigation assists | Standard | — | N/A | No quest or world-navigation systems in a mini-game collection. |
| Visual indicators for audio-only information | Standard | Gameplay SFX | See Auditory Accessibility | Handled in that section. |

---

## Auditory Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Independent music / SFX control | Basic | Global audio | Implemented (switches) | Hub Settings popup: Music switch and Sound switch via `MOST_AudioManager` (`Music_IsOn`, `SFX_IsOn`). On/off only, no sliders. Not exposed inside modules, only in the hub. |
| Voice volume | Basic | Voiced content | N/A | No voiced content identified. Revisit if any is added. |
| Gameplay information carried by audio only | Basic (principle) | All 12 modules | Not Audited | Every sound that changes what the player should do next needs a visual equivalent. SFX audit below is unfilled. |
| Subtitles / closed captions for SFX | Basic / Comprehensive | — | N/A / Out of scope | No dialogue identified; SFX captions not committed. |
| Mono audio, directional audio indicators, hearing-aid mode | Standard / Comprehensive | — | Out of scope | See Known Intentional Limitations. |

### Gameplay-Critical SFX Audit

| Sound Effect | What It Communicates | Visual Backup | Caption Required | Status |
|-------------|---------------------|--------------|-----------------|--------|
| Shared cues (click, win, lose, unlock, gems) via `CollectionFeedback` | Result / reward confirmation | Not yet checked per module | No, if a visual result state exists | Not Audited |
| All module-specific SFX | Not yet inspected | — | — | Not Audited |

---

## Platform Accessibility API Integration

| Platform | API / Standard | Features Planned | Status | Notes |
|----------|---------------|-----------------|--------|-------|
| Android | AccessibilityService / TalkBack | None (screen reader out of scope) | Out of scope | See Known Intentional Limitations. NOT SOURCEABLE — whether Unity 6000.0 provides a runtime accessibility API for this is not covered by docs/engine-reference/unity/. Verify against Unity's 6000.0 docs before reopening this. |
| Android | System settings (font scale, display size, "remove animations") | None committed | Not Started | Open question: should the reduced-motion toggle default from the system animation setting? NOT SOURCEABLE — how Unity exposes that setting is not covered by the repo reference. |
| Google Play | Store accessibility requirements | None identified | Not Assessed | No Play Store accessibility requirement was checked this session. Check before submission. |
| Xbox, PlayStation, Steam/PC, iOS, PC screen readers | — | — | N/A | Android is the only target platform. Add rows if a port is started. |

---

## Per-Feature Accessibility Matrix

| System | Visual Concerns | Motor Concerns | Cognitive Concerns | Auditory Concerns | Addressed | Notes |
|--------|----------------|---------------|-------------------|------------------|-----------|-------|
| Hub (game cards, shop, daily reward, missions, settings) | Text size and contrast on popups; locked/unlocked card state may rely on color | Touch target size of cards and popup buttons | Auto-dismissing popups and toasts (not audited) | Reward cues | Partial | Settings popup has Music, Sound and Vibration switches. Everything else Not Audited. |
| Shared feedback layer (`CollectionFeedback`) | Flash/shake effects: none found in `_Collection` scripts; kit modules not checked | Haptics | None | Click/win/lose/unlock/gems cues through the audio manager's SFX/UI groups | Partial | Honors the Vibration switch and Sound switch. Natural single hook for the reduced-motion toggle. |
| The 12 game modules (Cubey, Block Puzzle, Pistol, Squad Runner, Crowd Count, BigBrain, Bull Dodge, ThreeRoads, Splat, Block Adventure, Ninja, Bomb Survival) | Not Audited | Not Audited | Not Audited | Not Audited | Not Started | Replace with one row per module as each is audited. Splat tile coloring is a known color-signal candidate. |

---

## Accessibility Test Plan

Evidence (screenshots, short recordings) is kept in `production/qa/evidence/accessibility/`.

| Feature | Test Method | Test Cases | Pass Criteria | Responsible | Status |
|---------|------------|------------|--------------|-------------|--------|
| Text contrast | Automated contrast analyzer on screenshots | Hub, every popup, each module's HUD and menus, text over game scenes | Body text ≥ 4.5:1; large text ≥ 3:1 | ux-designer | Not Started |
| Text size and touch targets | Manual measurement at the 1080×1920 reference canvas, then on a physical Android device | All non-gameplay screens and each module HUD | Text ≥ 32px (menus) / 36px (critical HUD); targets ≥ 48dp. Confirms the UNVERIFIED px/dp conversions. | qa-tester | Not Started |
| Color-as-only-indicator | Manual — view each module's screenshots in grayscale | Every state where color distinguishes meaning (e.g. Splat painted tiles) | No essential information is lost in grayscale; backup cue present per audit table | ux-designer | Not Started |
| Flash / strobe | Manual review of recorded play per module | All effects from shared feedback and kit VFX | No more than 3 flashes/second | ux-designer | Not Started |
| Reduced-motion toggle | Manual — enable once implemented, play each module and the hub | All shake, flash and juice effects | No screen shake; flash amplitude reduced; gameplay unaffected | qa-tester | Not Started |
| Haptics toggle | Manual on a physical device — Vibration off, play each module | Shared cues and kit `MA_HapticFeedback` actions | No vibration anywhere | qa-tester | Not Started |
| Audio-independence | Manual — Music and Sound off (and device muted), play each module | Every module and hub flow | Every module completable; no audio-only information | qa-tester | Not Started |
| Pause and one-hand play | Manual — one thumb, portrait, each module | Start, play, pause/exit | Reachable pause or exit; primary controls reachable one-handed; exceptions logged | qa-tester | Not Started |

---

## Known Intentional Limitations

| Feature | Tier Required | Why Not Included | Risk / Impact | Mitigation |
|---------|--------------|-----------------|--------------|------------|
| Screen reader (TalkBack) for menus and in-game UI | Comprehensive | Needs integration work beyond current capacity. NOT SOURCEABLE — Unity 6000.0 accessibility API support is not covered by docs/engine-reference/unity/. | Blind and low-vision players cannot use the app independently. | None committed. Reassess after reading Unity's 6000.0 docs; reopen if a supported path exists. |
| Colorblind modes | Standard | Not elevated; non-color cues are the chosen approach. | Colorblind players are affected wherever a module's non-color backup is missing. That is unknown until the per-module audit is done. | Color-as-only-indicator audit per module (committed). |
| Adjustable text size / UI scaling | Standard | Fixed minimum sizes are set instead (32px menus, 36px critical HUD). | Low-vision players who need larger text than the minimums. | None committed. Minimums are above the Basic baseline. |
| Extendable or toggleable timed input; difficulty options | Standard | Would change core gameplay in the reflex modules. | Players with motor or cognitive impairments may not succeed in reflex modules. | Players choose among 12 modules; per-module pace is not yet audited. |
| Input remapping, gamepad/keyboard navigation, aim assist, HUD repositioning | Standard / Comprehensive | Touch-only target. | None for the intended platform. | Reopen if a non-touch platform is added. |
| High contrast mode, mono audio, directional audio indicators, SFX captions, hearing-aid mode | Comprehensive / Standard | Not committed at Basic + 3 elevated items; no capacity for a full Comprehensive pass. | Low-vision and deaf/hard-of-hearing players get no dedicated mode. | Audio-independence test ensures no audio-only information; contrast minimums set. |

---

## Audit History

| Date | Auditor | Type | Scope | Findings Summary | Status |
|------|---------|------|-------|-----------------|--------|
| — | — | — | — | No audits performed yet. Spec authored 2026-10-26; all per-module items are Not Audited. | — |

---

## External Resources

| Resource | URL | Relevance |
|----------|-----|-----------|
| WCAG 2.1 | https://www.w3.org/TR/WCAG21/ | Contrast ratios and text-size guidance used above |
| Game Accessibility Guidelines | https://gameaccessibilityguidelines.com | Game-specific checklist by category and cost |
| AbleGamers Player Panel | https://ablegamers.org/player-panel/ | User testing with disabled players, if engaged later |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Show a one-time photosensitivity notice at first launch? | user | Before store release | Unresolved |
| Expose Music/Sound/Vibration switches inside module pause menus, or keep them hub-only? | ui-programmer | Before Production gate | Unresolved |
| Is a dismissed first-play hint retrievable later? | ui-programmer | Before Production gate | Unresolved |
| Should reduced-motion default from the Android system animation setting? NOT SOURCEABLE — Unity's exposure of it is not in docs/engine-reference/unity/. | unity-specialist | Before reduced-motion implementation | Unresolved |
| Does Unity 6000.0 provide a runtime accessibility API usable with TalkBack? NOT SOURCEABLE — not covered by the repo reference. | unity-specialist | Before reopening the screen-reader limitation | Unresolved |
| Google Play accessibility requirements for submission | release-manager | Before store submission | Unresolved |
| Per-module audits: color signals, gameplay-critical SFX, hold inputs, multi-finger gestures, modules without a pause path | qa-tester | Before Production gate | Unresolved |
| Verify px/dp conversions (32px ≈ 14sp, 48dp ≈ 115px) on a physical device | qa-tester | Before Production gate | Unresolved |
| No game brief or concept exists; confirm the tier rationale once `design/game-brief.md` is written | user | After game brief | Unresolved |
