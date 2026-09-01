---
name: feature-steps
description: >
  Use whenever creating a new feature folder under `features/` and writing the plan/steps as
  Markdown, or when the user says "hacer plan por escrito", "crear carpeta en features",
  "guardar los steps como .md", or asks to transcribe a proposal/plan into the repo. Ensures the
  folder, numbering, and step-file naming match this repository's existing convention.
---

# Feature Folder & Steps Checklist

This project keeps every feature's plan as Markdown files inside `features/`. Follow this
workflow when asked to create or write a feature plan.

## 1. Inspect the existing convention first

Before creating anything, list `features/` and read one recent steps file (e.g.
`features/09-fix-weekly-monthly-quicklog/09-steps.md` and `features/10-user-timezone/10-steps.md`)
to mirror the established format exactly.

## 2. Pick the folder name and number

- Folders live at the repo root under `features/`.
- Folder pattern: `NN-<short-kebab-slug>` where `NN` is the next sequential number
  (increment the highest existing number — e.g. if `10-user-timezone` exists, use `11-...`).
- The slug should be short, kebab-case, and describe the feature.

## 3. Create the folder

New-Item -ItemType Directory -Force -Path "features/NN-<slug>"

## 4. Write the step file

Create `features/NN-<slug>/NN-steps.md`. Multiple plans per feature use the same `NN-` prefix:
`NN-draft.md`, `NN-plan-<name>.md`, `NN-missingsteps.md`, etc. The canonical deliverable is
`NN-steps.md`.

Structure `NN-steps.md` with this template:

```
# Implementation Steps — <Short Feature Title>

## Problem

(What is currently broken or missing. Reference file paths and line numbers.)

## Goal

(What the change should achieve, user-facing.)

## Approved decisions

(Bullet list of the explicit decisions already agreed with the user, so the plan
faithfully captures agreed scope.)

---

## 1. <Layer/Area>

File: `path/to/file.cs`

- [ ] Actionable task
- [ ] Actionable task

## 2. <Next Layer/Area>

File: `path/to/file2.cs`

- [ ] Actionable task

## N. Verification

- [ ] `dotnet build` (0 warnings / 0 errors)
- [ ] `dotnet test`
- [ ] Manual smoke checks
```

## Formatting rules

- Start every filename with the same `NN-` numeric prefix as the folder.
- Use `##` sections grouped by layer/area (Domain, Application, API, Frontend, Tests).
- Prefer actionable `- [ ]` checkboxes over prose paragraphs; include exact file paths.
- Keep tasks ordered so later steps depend on earlier ones.
- Write content in the language the user is using (this project often drafts in Spanish);
  keep code/snippets in C# regardless.
- Reference concrete files and line numbers from the current codebase when describing the
  problem or target changes.

## Verification

- Confirm the folder exists with `Test-Path` and the `.md` file is in place.
- Confirm no numbering collision: the chosen `NN` must be unique under `features/`.