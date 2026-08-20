---
name: Rendering-Mermaid-Diagrams
description: Use when a Mermaid diagram in a Markdown file won't render (flashes and disappears in preview, shows a syntax error, or the target viewer doesn't support Mermaid), or when embedding a Mermaid flowchart as a static image with reliable colors/background instead of relying on live preview rendering
---

# Rendering Mermaid Diagrams

## Overview

Mermaid code blocks depend on the viewer supporting a Mermaid renderer. When that
renderer chokes (bad syntax) or is absent (plain Markdown viewer, PR diff, PDF
export), the diagram is invisible. Fix the syntax, then render a self-contained
SVG via the public `mermaid.ink` service and embed that image — no local
browser/puppeteer/mermaid-cli install required, works even on old Node.

## When to Use

- A `.mmd`/```mermaid``` block "flashes then disappears" in VS Code preview, or
  renders as a red error box.
- You need the diagram to show up in viewers that don't run Mermaid at all
  (GitHub file view without JS, plain text editors, PDF export).
- You want fixed, reliable node colors that don't depend on the viewer's theme.
- Local `@mermaid-js/mermaid-cli` isn't usable (needs modern Node + Puppeteer;
  many machines only have an old Node available).

Not needed if the target viewer reliably renders Mermaid live and a static
image isn't required — the code block alone is simpler to maintain.

## Common Mistakes (root causes worth checking first)

| Symptom | Cause | Fix |
|---|---|---|
| Diagram flashes once then vanishes in VS Code preview | Node label text contains raw `[...]` (e.g. `"[F0] some sheet"`), which collides with the `[...]`/`[(...)]`/`[/.../]` shape-delimiter brackets even inside quotes | Remove literal square brackets from label text; use `—` or `:` instead |
| `mermaid.ink/svg/<b64>` returns HTTP 404 body `Not Found` | Used **standard** base64 (`+`, `/`, `=` break the URL path) | URL-safe base64: replace `+`→`-`, `/`→`_`, strip trailing `=` |
| `mermaid.ink` returns HTTP 200 but the "diagram" is a tiny image with red text | Real Mermaid syntax error, not an encoding issue | Fix the diagram source; don't trust HTTP 200 alone — check size/content |
| Diagram looks fine in light-theme editor, unreadable/clashes in dark-theme viewer | The `/svg` endpoint ignores `?bgColor=` and returns a transparent background | Inject an explicit `<rect>` covering the full `viewBox` as the first drawn element (see script) |
| All boxes are the same color, hard to tell entity types apart | No `classDef`/`class` styling applied | Add `classDef` + `class` statements (see below) |

## Steps

1. Write/fix the Mermaid source (inline in the `.md` or a separate `.mmd`).
2. Add `classDef` + `class` lines to color-code node categories, e.g.:
   ```
   classDef db fill:#cfe3ff,stroke:#3a6ea5,color:#1a2e44;
   classDef sheet fill:#d6f0d6,stroke:#4a9c4a,color:#1a3d1a;
   class Sheet,Latest db;
   class F0,F01Data,TplKhcn sheet;
   ```
3. Render it: `node render-mermaid.js <input.md-or-.mmd> <output.svg>`
   (script in this skill folder — handles URL-safe encoding, error detection,
   and background injection; see script header for flags).
4. Embed the image in the doc: `![description](output.svg)`.
5. Keep the Mermaid source too — put it in a collapsed `<details>` block right
   after the image so it can be edited and re-rendered later:
   ```markdown
   ![diagram](diagram.svg)

   <details>
   <summary>Mermaid source (edit and re-render with render-mermaid.js)</summary>

   ```mermaid
   flowchart TD
       ...
   ```
   </details>
   ```

## Implementation

See [render-mermaid.js](render-mermaid.js) — extracts the ```mermaid``` block
from a `.md` (or reads a `.mmd` directly), URL-safe base64 encodes it, fetches
`https://mermaid.ink/svg/<encoded>` via `curl`, sanity-checks the response, and
injects a background rect before writing the final SVG.

Requires `curl` and any Node version (no Mermaid/Puppeteer dependency locally —
rendering happens on mermaid.ink). Sends only the diagram *text* to that public
service; don't use this path for diagrams containing secrets or sensitive data
— render locally instead in that case.
