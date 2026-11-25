# Game-Like UX with Mandatory CAD/BIM Precision: Plan

Proposed on 25.11.2026. This plan starts from what `ui/src/` does today and defines a staged upgrade toward effortless, game-like fluidity while preserving (and strengthening) Lapin's mandatory CAD/BIM precision.

## Guiding Principle: Precision-First Game Feel

Modern building games (*Cities: Skylines*, *The Sims*, *Satisfactory*, *Tiny Glade*) and spatial tools (*Blender*, *Figma*) feel effortless because **hands and eyes stay on the model**, visual feedback is immediate and volumetric, and UI appears only when relevant. In Lapin, every game-like affordance obeys one non-negotiable CAD/BIM rule:

> **Any freehand or direct-manipulation gesture can be overridden instantaneously by exact keyboard input (`3200`, `Tab`, `45`, `Enter`), geometric snaps, and explicit constraints — producing one clean IFC command and one undo step.**

## Alignment with `main` (24–25.11.2026 UI Changes & Standing Constraints)

All feature branches for this plan MUST branch from `main` (not `feat/terrain-82-3d-subregions`) and comply with the UI work merged on `main` on 24–25.11.2026:

1. **Ribbon & Key Tips (`0ecd9304c`, `dfc674e59`, `6f96b40b6`):** `InterfaceParts` on `main` includes `keyTips: 'alt' | 'always'`; `Ribbon.tsx` has `<QuickAccess />`, submenu File menu (`MenuSub.tsx`), and auto-collapsing small tools. The new `Flow` preset must include `keyTips`, and the Smart Eyedropper shortcut (`I` / `Alt+Click` on canvas) must not interfere with holding `Alt` alone for key tips.
2. **Updated `StartScreen.tsx` & `WorkspacePicker.tsx` (`dfc674e59`):** `main` already puts *Back to project* first (and on `Esc`), languages in one `<select>`, templates before unsaved work, and 3D thumbnails for recent projects (`recentThumbs.ts`). Role Selection must build on top of this layout without regressing any of those fixes.
3. **Panels & User Palettes Plan (`docs/plan-panels-and-palettes.md`, decision 858):** `main` plans an open `PanelId` registry (`docked / floating / window / closed`) and user palettes unified with `My tools`. The non-modal Build Drawer (`PlaceCatalog`) must plug into that same panel registry (so it can dock, float, or pop out on desktop) instead of inventing a parallel floating-panel mechanism.
4. **Wall Temporary Dimensions (`f5e2066f6`, #240):** Already implemented on `main` (wall length along faces and corner offsets). P8 therefore focuses only on remaining elements (columns, furniture clearances, 3D elevation grips).
5. **Error Boundaries (`a138d5b32`, `PartBoundary.tsx`):** Every new overlay, floating toolbar, pie menu, onboarding card, or drawer must be wrapped in `<PartBoundary>`.
6. **24 MB Web Build Budget (`a47c2bbf9`, currently ~23.03 MB):**
   - Onboarding sandbox mini-projects MUST be generated procedurally via engine commands (~2–5 KB TS), never bundled binary `.lapin` assets.
   - Rich tooltips MUST use lightweight inline SVG/CSS (no GIF/MP4).
   - Optional tactile audio MUST use synthetic Web Audio API (zero audio files).
7. **13 Languages (`ui/src/i18n/`, including `el.ts`, `sv.ts`, `th.ts`):** Every new user-facing string must be added to all 13 locale files, and user docs (`docs/user/` + `docs/user/ro/`) + scenes updated per `CLAUDE.md`.

## What Exists Today (Baseline Audit)

- **Aesthetic & Themes:** Clean "architect's sheet" look (`paper` / `graphite`) in `ui/src/theme.css`. Almost no CSS micro-transitions (only 4 across 2,800 lines).
- **Hotbar 1–9:** Already implemented per workspace with drag-and-drop and `Tool | Type` binding (`ui/src/shell/ViewportHud.tsx`, `ui/src/app/hotbar.ts`), but defaults to a tiny `28×28px` docked icon rail (`quickTools: 'docked'`).
- **Walk & Fly Physics:** Full FPS movement with gravity, jumping, stairs, door pass-through and `F` to fly (`ui/src/viewport/walk.ts`, `walkPhysics.ts`), but buried behind a plan right-click on a room — no 1-click button in `QuickViews` (`ui/src/App.tsx`).
- **Workspaces & Presets:** Discipline workspaces (`architecture`, `structure`, `mep`, `site`, `drafting`, `all`, `mine`) and density presets (`comfortable`, `compact`, `focus`) in `ui/src/app/interface.ts`, chosen via a compact radio list (`ui/src/shell/WorkspacePicker.tsx`) that currently changes only ribbon tabs.
- **Navigation Styles:** Multi-app mouse mappings (Lapin, Revit, AutoCAD, ArchiCAD, SketchUp, Blender, Rhino) in `ui/src/app/navStyle.ts`.
- **Precision Input (Fragmented 2D vs. 3D):** 2D uses `LiveBox` + `LiveBar` + `CommandLine` (`ui/src/viewport/LiveBox.tsx`, `cad/CommandLine.tsx`); 3D uses separate paths (`valueOwner.ts`, `parseVcb` in `Place3D.tsx`, a separate `<input>` in `AxisDrag3D.tsx`, `liveBegin('grip3d')` in `Grips3D.tsx`).
- **3D Placement Preview:** `Place3D.tsx` renders a 2D SVG polyline on the floor level rather than a volumetric 3D ghost mesh.
- **Catalog & Selection Context:** `PlaceCatalog.tsx` is a blocking modal dialog; `SelectionCard.tsx` only renders when `panels === 'hidden'` (Focus mode); `toolHint()` renders long prose sentences instead of scannable keycaps.
- **Properties & Cells:** `LengthField` / `NumberField` (`ui/src/shell/cells.tsx`) already support mixed units and arithmetic sums, with spreadsheet keyboard navigation, but have no drag-to-scrub.

---

## Architecture of the Improvements (10 Pillars)

### P1. Role Selection at Start ("Choose Your Class") & Optimal Initial Setup
Evolve `StartScreen.tsx` + `WorkspacePicker.tsx` into a visual role selector on first launch (always switchable later from the top bar):
- **Roles:** Architect · Structural Engineer · Roads & Site (Civil) · MEP / Installations · Drafting 2D / All.
- **One-step familiarity:** Optional *"Coming from…"* picker applying the matching `navStyle` (Revit, AutoCAD, ArchiCAD, SketchUp, Blender, Rhino, Lapin) and visual preset (`Flow` vs. `Comfortable`).
- **Full role configuration (not just ribbon tabs):**
  - Ribbon tabs & order (`WORKSPACE_TABS`).
  - Curated default **Hotbar 1–9** per role (`hotbar.ts`).
  - Prioritized starter templates on `StartScreen`.
  - Sensible discipline defaults (initial view, underlay/snap presets).

### P2. Interactive Role-Differentiated Onboarding ("Sandbox Mini-Projects with Missions")
Replace passive onboarding text with an optional, replayable **assisted mini-project** (a model pre-built to ~70% so the user completes something that already looks great in 3D, never starting from an intimidating blank grid):
- **Two short tracks (mini Skill Tree, dismissible & reopenable from Help):**
  1. **Universal Basics (~2 min):** Customize your workspace (`Flow` / `Comfortable`, drag a tool onto Hotbar slot `1`, pick mouse style), keyboard speed (`Ctrl+K`, 2-letter shortcuts like `WA`/`ZE`, `Ctrl+Z`), and CAD precision (`SNAP`/`ORTHO`, typing exact lengths, editing a temporary dimension).
  2. **Role Mission (~3–4 min, engine-verified steps):**
     - *Architect:* Close the 4th wall at exact `6000 mm`, place an entrance door & window, adjust a temporary dimension, switch to 3D and Walk inside (`WASD`).
     - *Structural:* Complete a missing bay — place grid axes, snap columns to intersections, add beams & slab, inspect with Section Box / rebar.
     - *Roads & Site:* Complete a road alignment curve on existing terrain, adjust elevation/profile in 3D, check survey coordinates / DXF export.
     - *MEP:* Enable architectural underlay, route a pipe/duct at exact `Z` elevation, connect equipment, isolate (`HI`) and inspect clashes in 3D.
     - *Drafting 2D:* Trace an exact contour with `SNAP` + `ORTHO`, apply `Offset` & `Trim`, add associative dimensions, place on a `1:50` Sheet.
- **Ghost Target in Viewport:** Translucent dashed target geometry showing where the mission expects the element, automatically checked off when the engine executes the real command with valid precision.
- **Stress-free Sandbox mode:** Does not clutter recent projects, includes a 1-click *Reset mission* button, and lets the user save as a real project or start clean on completion.

### P3. Unified Cursor Precision Pill (2D + 3D)
Unify `LiveBox.tsx` across `PlanView`, `Place3D`, `Move3D`, `AxisDrag3D`, and `Grips3D`:
- One consistent floating pill near the cursor showing live dimensions (`L`, `∠`, `W × H`, `ΔZ`), active snap badge (`Endpoint`, `Midpoint`, `Perp`, `Parallel`, `Axis Lock`) with semantic color coding, and live preview of math expressions (`6000/3 → 2000 mm`).
- Identical direct typing everywhere (`2500`, `Tab`, `45`, `Enter`) without manual input focusing.

### P4. 3D Volumetric Ghost Previews & Smart Magnets
- **Live 3D Ghost Meshes** in `Place3D.tsx`: render translucent 3D geometry (true wall thickness/height, door/window frame, column, furniture) instead of a flat 2D floor line.
- **Semantic Valid / Invalid Tinting:** Reuse `usePlacement` verdicts — green/accent tint when valid, red tint + concise reason badge at cursor when invalid or clashing.
- **Smart Magnets:** Automatic equal-spacing indicators (`= | =`) when centering openings or spacing columns, plus subtle magnetic alignment guides with 6–8 px hysteresis.

### P5. Pie Menu (Radial) + Contextual Mini-Toolbar + Smart Eyedropper
- **Radial Pie Menu (`Q` or Right-Click Hold 180ms):** 6–8 ergonomic sectors adapted to selection vs. empty space, supporting quick flick gestures (<200ms).
- **Contextual Floating Mini-Toolbar (all presets, not just Focus):** Extend `SelectionCard.tsx` into a compact 1-row action bar near selection (Type switcher, Flip, Duplicate, Isolate, Edit Sketch/Profile, Delete).
- **Smart Eyedropper / "Pick Block" (`Alt + Click` or `I`):** Clicking any existing element in 2D or 3D activates its authoring tool pre-configured with that exact type, material, and parameters.

### P6. Preset `Flow`, Upgraded Hotbar 1–9 & Non-Modal Build Drawer
- **New `Flow` preset** in `interface.ts`: 90%+ viewport area, folded panel rails, compact/auto-hiding ribbon, floating bottom **Game Dock Hotbar 1–9** (`52×52px` slots, clear keycap numbers, type badges, `Alt+1..9` or wheel to switch hotbar pages).
- **Non-modal Build Drawer (`PlaceCatalog.tsx`):** Convert the blocking modal into a dockable bottom/side drawer with direct drag-and-drop onto 2D/3D views, staying open for rapid multi-item placement.

### P7. Game-Engine 3D Navigation, 1-Click Walk & Split-Screen Sync
- **1-Click `Walk` + "Drop to Walk"** in `QuickViews` (`App.tsx`), plus `Right-Click + WASD` fly-orbit directly in 3D.
- **1-Click `Plan + 3D Live` Split-Screen** button in `QuickViews` (wrapping existing `splitPane('row')`) so users draw precisely in 2D while watching 3D update live.
- **3D ViewCube / Axis Gizmo** (integrated with `KeyPlan.tsx`) with smooth interruptible camera transitions (`200–250ms`) and animated 2D ↔ 3D tilt transition.
- **Camera Bookmarks:** `Ctrl+Shift+1..4` to save, `Shift+1..4` to fly smoothly between saved angles.
- **Hold-to-Peek X-Ray (`X` held):** Temporarily switches to X-Ray while held to inspect rebar, structure, or MEP behind finishes; releases back to previous visual style immediately.

### P8. Extended Direct Editing (WYSIWYG) & Drag-to-Scrub
- **Expand `TempDimMarks.tsx`** beyond openings, axes, and walls (walls already done in `f5e2066f6`) to columns, furniture (wall clearances), and 3D level/elevation grips.
- **Drag-to-Scrub** on `LengthField` / `NumberField` (`cells.tsx`): drag horizontally on labels for live smooth adjustments (`Shift` = 1 mm fine step, `Ctrl` = 100 mm coarse step; single undo step per drag; click remains exact keyboard input).
- **Hold-to-Drag Progress Ring** in `Move3D.tsx` for the `350ms` hold gesture (`HOLD_MS`).

### P9. Keycap Hint Bar, Just-in-Time Micro-Tips, Rich Tooltips & Tactile Feedback
- **Dynamic Keycap Hint Bar** replacing prose in `ViewportHud.tsx` (`[LMB] End · [0-9] Length · [Space] Flip · [Shift] Ortho · [Esc] Done`), lighting up live as modifier keys are pressed.
- **Just-in-Time Micro-Tips:** Non-blocking, one-time contextual tips when repetitive manual patterns are detected (e.g., clicking Wall in Ribbon 3× → *"Tip: press `WA` or `1`"*).
- **Rich Animated Tooltips:** 600ms hover on ribbon tools reveals a 3-second looping SVG gesture diagram + shortcut + *"Try it"* link.
- **Micro-Transitions & Optional Tactile Audio:** Fast (`80–150ms`, `prefers-reduced-motion`-compliant) CSS transitions, snap-lock pulse ring, and opt-in synthetic Web Audio clicks (snap, place, undo).

### P10. Omni-Search (`Ctrl+K`), Model Health Radar & Build Replay
- **Upgraded `CommandPalette.tsx`:** Recent/frequent commands at top, fuzzy search, icons, and instant jump to Storeys, Views, Sections, and Types.
- **Model Health Radar:** Subtle HUD badge (*"0 issues"* / *"2 unjoined walls · 1 MEP clash"*) with 1-click animated camera teleport and quick-fix action.
- **Time-Lapse / Build Replay:** Lightweight timeline slider scrubbing through command history or 4D phases for review and client presentations.

---

## Precision Guardrails Matrix

| Game-Like Feature | CAD/BIM Risk | Mandatory Precision Safeguard |
|---|---|---|
| Volumetric 3D Ghost & Free Placement | Off-by-1mm eyeballing | Geometric snaps always win; bare numeric typing locks exact length/angle; explicit visual badge for *Snapped* vs. *Free*. |
| Drag-to-Scrub Numeric Fields | Messy decimal drift | Clean adaptive increments (`10mm`/`50mm`, `Shift` = `1mm`, `Ctrl` = `100mm`); click keeps exact formula input; 1 drag = 1 undo step. |
| Minimal `Flow` Preset & Pie Menu | Hiding critical context | Active **Level + Elevation**, **Units**, and **SNAP/ORTHO/POLAR** stay permanently visible in a compact micro-HUD. |
| Camera & UI Animations | Slowing power users | Short durations (`100–250ms`), **immediately interruptible** by any pointer/key input, honoring `prefers-reduced-motion`. |
| Non-Modal Drag & Drop Catalog | Wrong storey or rotation | Automatic target storey detection (`storeyAt`), host-face normal alignment, `Space` for 90°/45° rotation. |

---

## Implementation Roadmap

### Phase 1 — Quick Wins & Role Setup (1–2 weeks)
1. **Role Selection ("Choose Your Role")** on `StartScreen.tsx` + `WorkspacePicker.tsx` with per-role default Hotbar 1–9 (`hotbar.ts`), starter templates, and optional `navStyle` picker.
2. **Dynamic Keycap Hint Bar** in `ViewportHud.tsx` (reactive to `Shift`/`Ctrl`/`Space`).
3. **1-Click `Walk` + `Plan + 3D Live` Split-Screen** in `QuickViews` (`App.tsx`) + *Drop to Walk* in 3D.
4. **Preset `Flow` + upgraded floating Hotbar 1–9** (`interface.ts`, `ViewportHud.tsx`).
5. **Smart Eyedropper (`Alt + Click` / `I`)** and **Hold-to-Peek X-Ray (`X`)**.
6. **CSS Micro-Transitions, Snap Pulse & Hold-to-Drag Ring** (`theme.css`, `Move3D.tsx`).
7. **Upgraded `CommandPalette.tsx`** (recent, fuzzy match, icons, view/storey jump).

### Phase 2 — Core Game-Like Precision UX & Interactive Onboarding (3–5 weeks)
1. **Interactive Onboarding Sandbox Mini-Projects** (Universal Basics + Role-Differentiated Missions with Ghost Targets and engine-verified auto-completion).
2. **Unified Cursor Precision Pill (2D + 3D)** across `PlanView`, `Place3D`, `Move3D`, `AxisDrag3D`, `Grips3D`.
3. **Contextual Mini-Toolbar on Selection** (all presets) + **Radial Pie Menu (`Q` / Right-Click Hold)**.
4. **Non-Modal Build Drawer (`PlaceCatalog.tsx`)** with 2D/3D drag-and-drop.
5. **Drag-to-Scrub** on `LengthField` / `NumberField` (`cells.tsx`) + **Extended `TempDimMarks`** (walls, columns, furniture).
6. **Just-in-Time Micro-Tips & Camera Bookmarks (`Ctrl+Shift+1..4` / `Shift+1..4`)**.

### Phase 3 — Next-Gen Spatial Experience
1. **3D Volumetric Ghost Previews** in `Place3D.tsx` with valid/invalid tinting and **Smart Magnets (`= | =`)**.
2. **3D ViewCube + Animated 2D ↔ 3D Transition** + `Right-Click + WASD` direct fly-orbit.
3. **Rich Animated Tooltips** (looping SVG gesture cards) + **Model Health Radar**.
4. **Time-Lapse / Build Replay** slider + **Optional Tactile Web Audio Pack**.
