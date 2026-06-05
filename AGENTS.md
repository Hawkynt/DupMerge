# Agent guide — DupMerge

Working agreement for **all** coding agents and human contributors working in
this repository. These rules are not optional. The full house spec lives in
the `Hawkynt/project-template` repo (`STANDARD.md`); this file is the
per-repo distillation.

## What this is

A C# CLI that reclaims disk space by detecting duplicate files and replacing
them with symbolic/hard links (configurable: size limits, read-only
handling, link kinds). Solution `DupMerge.sln`; tests in `DupMerge.Tests`
with category tiers (see `test-all-categories.bat` and
`coverage.runsettings`).

## Commits

- **Group changes semantically/logically** — one concern per commit.
- **Every subject line starts with a prefix**: `+` added · `-` removed ·
  `*` changed · `#` bug fixed · `!` critical todo.
- Never start a subject with "fix"/"bugfix"/"changed"/"modified".
- **No AI traces anywhere**: no `Co-Authored-By` AI lines, no "Generated
  with" footers, no agent mentions in messages, comments, or authorship.

## The loop (always, in this order)

1. **Before committing**: `dotnet build DupMerge.sln -c Release` and the
   required test tier until green (advisory categories may stay red
   locally). Filesystem-link semantics differ per OS — changes there get
   coverage on the ubuntu AND windows CI legs.
2. **Commit** (rules above) and **push**.
3. **Wait for CI**; on `main` a green CI triggers the nightly (prerelease +
   GFS prune, same-day replace). Fix and loop until everything is green.

Stable releases are **manual** (`gh workflow run release.yml`) — never cut
one unless explicitly asked.

## Code conventions

- Latest C# features; destructive operations (replacing files with links)
  must stay transactional — verify-then-swap, never delete first.
- Hash/compare logic changes get equivalence/boundary tests (empty file,
  same-size-different-content, hardlink-already, cross-volume).

## README & repo conventions

- Standard frame: title → badges → one-line `>` blockquote; fixed emoji
  mapping for the standard sections (`## ✨ Features`,
  `## 🛠️ Build from Source`, `## 🚀 Usage`, `## ❤️ Support`,
  `## 📜 License`).
- License is LGPL-3.0-or-later; the `## ❤️ Support` section and
  `.github/FUNDING.yml` stay intact.
