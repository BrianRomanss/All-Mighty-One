# Adoption Plan

> **Generated**: 2026-10-26
> **Project phase**: Production (inferred; no project.stage)
> **Engine**: Not configured (editor on disk: Unity 6000.0.66f2)
> **Template version**: v1.0+

Work through these steps in order. Check off each item as you complete it.
Re-run `/adopt` anytime to check remaining gaps.

---

## Step 1: Fix Blocking Gaps

None found.

---

## Step 2: Fix High-Priority Gaps

### 2a. Engine not configured
`project.yaml` has no `engine.*` block and `technical-preferences.md` is all
`[TO BE CONFIGURED]`. `CLAUDE.md` still imports the Godot VERSION.md and has
`[CHOOSE: …]` placeholders in its Technology Stack.
**Fix**: `/setup-engine unity`. It should also pin the version against the
installed editor (6000.0.66f2) and re-point the CLAUDE.md import to
`docs/engine-reference/unity/VERSION.md`.
**Time**: 30 min
- [ ] engine.name/version/language/rendering/physics set in project.yaml
- [ ] CLAUDE.md Technology Stack and engine import updated to Unity

### 2b. No game brief (NOT ASSESSED — file absent, format unchecked)
`design/game-brief.md` is the one design doc expected at `workflow: minimal`.
**Fix**: `/reverse-document` against the existing code to draft the brief
(the game is built; this is a brief of what exists), then re-run `/adopt`
to audit its six required fields.
**Time**: 1 session
- [ ] design/game-brief.md exists
- [ ] re-audit shows six required fields present, none placeholder

---

## Step 3: Bootstrap Infrastructure

Not on the minimal path — the brief's build order is the plan. Next:
`/create-stories` (or `/dev-story` once stories exist).

---

## Step 4: Medium-Priority Gaps

### 4a. naming.* unconfigured
**Fix**: set during `/setup-engine unity`, or `/settings naming.*=…`. **Time**: 5 min
- [ ] naming conventions set

### 4b. performance.* unconfigured
**Fix**: set target FPS / memory budgets for mobile. **Time**: 10 min
- [ ] performance budgets set

### 4c. Engine reference pin vs installed editor
VERSION.md pins Unity 6.3 LTS; installed is 6000.0.66f2, and "Installed at
pin time" is NOT DETERMINED. APIs cited from the reference may not exist locally.
**Fix**: let `/setup-engine` record the installed version, or upgrade the editor.
**Time**: 5 min
- [ ] installed version recorded in VERSION.md

---

## Step 5: Optional Improvements

### 5a. Forbidden Patterns / Allowed Libraries empty
Starts empty by design. Fill in as decisions are made (e.g. ads only via AdsBridge).
- [ ] optional

---

## Not Audited (so nobody reads this as a pass)
- GDDs, ADRs, stories: none exist (0 found).
- TR registry, control manifest, sprint-status, stage file, traceability
  matrix: out of scope at `workflow: minimal`.
- v1.0 migration check: not applicable, project.yaml exists.

---

## What to Expect from Existing Stories

Existing stories continue to work with all template skills. New format checks
(TR-ID validation, manifest version staleness) auto-pass when the fields are
absent — so nothing breaks. Do not regenerate stories that are in progress or done.

---

## Re-run

Run `/adopt` again after completing Step 2 to verify all high gaps are resolved.
The new run will reflect the current state of the project.
