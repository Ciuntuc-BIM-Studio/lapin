# Panels on other screens, and palettes users build (plan)

Forum topic 257, posts #13–17 (the tester Rychlas, Ionut's answer at #17), 25.11.2026. Decision 858.

Two wishes:

- **A.** Any panel, not only a view, can leave the main window for a second screen, as AutoCAD's palettes do.
- **B.** Users build their own palettes (tools, types, symbols, scripts) and float them, in the window or on another screen.

## 1. The user's answers (25.11.2026)

| Question | Answer |
|---|---|
| Which panels leave the window | **Any panel**: a general mechanism, a "Pop out" button in every panel's header |
| How a panel's window behaves | **As AutoCAD's**: above Lapin while Lapin is active, behind other programs when another is |
| What a palette holds | **Ribbon tools, types with their settings, symbols and blocks, scripts and graphs** |
| Where palettes are kept | **On the user's computer**, with export and import as a file for colleagues (later: an extension in the store) |
| My tools and palettes | **One model**: a palette is a named list of buttons, and My tools is the first palette, shown as a ribbon tab |
| After a restart | **The windows come back as they were** (same screen, same size). A window whose screen is gone comes back into the main window. Layouts can be saved by name ("Two screens", "Laptop") |
| In the browser | **Floating inside the page**, as today; no Pop out |

## 2. What exists

- **Views in windows of their own** (decision 445, `ui/src/app/windows.ts`).
  - The same interface starts with `?pane=…` and connects to the same engine, so the model is one.
  - `syncWindows` passes the shared state between windows as Tauri events: the storey, the selection, the tool, the theme, the types picked.
  - A tab dragged out of the window opens where it is dropped.
- **Side panels** (`ui/src/app/panels.ts`, `ui/src/shell/PanelDock.tsx`, decisions 427 and 757).
  - The browser and the properties can dock left or right, stack, or float over the view (`browserFloat`, `propsFloat`).
  - Only these two panels are known, by the hard-coded `PanelId = 'browser' | 'props'`.
- **My tools** (decision 479, `ui/src/app/myTools.ts`).
  - Its data is a list of tabs (`MyTab` → `MyGroup` → `MyItem {tool, large}`), with one tab shipped now.
  - It is kept in the browser's storage, and can be exported and imported.
- **The quick tools** (decision 259, `QuickSlot` in `ui/src/platform/modules.ts`).
  - A slot already holds a tool with a type (`typeId`, `typeName`): the precedent for "types with their settings".
- **The script and graph palettes** (decisions 528 and 536: `modules/scripting/PaletteWindow.tsx`, `modules/graphs/PaletteWindow.tsx`).
  - These are floating panels drawn from what a script or a graph declares, and are opened from My tools.
- **The window-state plugin** (`tauri-plugin-window-state`) is on `feat/desktop-window`, which is not merged yet; it is waiting for a check on Linux.

## 3. A — any panel in a window of its own

### 3.1 One registry of panels

`PanelId` becomes an open registry.

- A panel is `{ id, title, icon, render(), module? }`, registered by the shell (the browser, the properties, the tool properties, the key plan, the catalogue, layers, Visibility/Graphics) and by modules (Script, Planning, cost, graphs, a palette of B).
- Each panel's place is one of:
  - `docked(side, order)`
  - `floating(rect)`: inside the window, as today
  - `window(label, screen, rect)`: desktop only
  - `closed`
- The two panels known today keep their places through an `upgrade` of the stored settings: nothing a user has set moves.

### 3.2 The panel window

- It works like the view windows:
  - The same app starts with `?panel=<id>`: the panel alone, no ribbon, no views.
  - It opens on the same engine, and the shared state arrives by `syncWindows`.
- `SHARED` grows with what panels show:
  - the active view;
  - the project browser's open nodes;
  - the tab picked in the catalogue.
  - Each panel's own state (scroll, filters) stays in its window.
- **Commands from a panel window** go to the engine as from the main window. Each is one undo step, and every window sees the diff.
- **A tool picked in a panel window**:
  - It is set in the shared state (`tool` is already in `SHARED`).
  - The main window takes the focus, so the next click lands in the view.
- **Keys pressed in a panel window** follow the same shortcut registry:
  - Undo, redo, Escape and the tools' keys act on the model and the main window's views, not on the panel window.
  - Typing in a field stays in the field.
- **Closing**:
  - Closing a panel window docks the panel back in the main window.
  - Closing the main window closes every panel window: the app ends.

### 3.3 "As AutoCAD's": above Lapin, not above other programs

| OS | How |
|---|---|
| Windows | An owned window (Tauri `parent` = the main window): it stays above its owner, hides with it when minimised, and can sit on another screen |
| macOS | `parent` makes a child window that moves with the main one, which defeats a second screen. Instead, a normal window with `alwaysOnTop` switched on while one of Lapin's windows has the focus and off when none has (focus and blur events, debounced so a click from one Lapin window to another does not flicker) |
| Linux | As macOS, `alwaysOnTop` by focus; check on X11 and on Wayland (GNOME and KDE): some Wayland compositors ignore always-on-top, and the window then stays a normal window |

**To check first, as a spike:** do the three OSes behave as above? This should be measured on a real Windows and Linux machine before the rest is built.

### 3.4 Layouts that come back

- **What is saved:** the places of the panels, each window's screen and rectangle, and the main window's state.
- **Restoring:**
  - The windows reopen at start.
  - A screen that is no longer connected sends its panels back into the main window, without losing their place for when the screen returns.
- **Named layouts:** View → Panels → Layouts, with Save layout… and a list to switch between them ("Two screens", "Laptop").
- **Storage:** kept on the computer, like the interface settings, and exportable with them.
- **Depends on** `feat/desktop-window` (the plugin and the main window's state), merged first.

### 3.5 The browser

- No Pop out button.
- A panel set as `window` on the desktop shows as `floating` in the browser.
- The same settings file serves both.

## 4. B — palettes built by the user

### 4.1 One model with My tools

- **The data:** `MyTab` becomes `Palette { id, name, groups: [{ id, label, items: PaletteItem[] }] }`.
- **The default palette:** My tools is the first palette, `MY_TAB`, read from today's storage by an upgrade, without changing anything the user sees.
- **Where a palette is shown:** each palette shows as one of:
  - a **ribbon tab** (as My tools today);
  - a **panel**, docked or floating, through the registry of §3.1;
  - a **window** on another screen, through §3.2.
- **Editing:** one editor, today's Customize My tools… grown into Customize palettes…, makes, renames, orders, imports and exports palettes.

### 4.2 What a button is (`PaletteItem`)

| Kind | Holds | Pressed |
|---|---|---|
| `tool` | a tool's id (today's `MyItem`) | starts the tool |
| `typed` | a tool, a type (id and name, as `QuickSlot`) and optionally its settings (layer, height, offset…) | starts the tool with that type and those settings. A type dragged from the catalogue onto a palette makes one |
| `symbol` | a saved symbol, component, group or block from the library | places it, as Place does |
| `script` | a project script or a script tool (decision 489) | runs it (one undo step, named after the button); its palette opens if it has one |
| `graph` | a visual graph | opens its palette (decision 536), or bakes it straight away if marked so |

**What every button shares:**

- Each button has a label, an icon (taken from what it holds, or one picked), and large or small.
- **A missing target:** a button whose target is missing (a type not in this project, a module this edition lacks) stays in the palette, greyed out, with a tooltip saying what is missing. It comes back when the target does, as My tools does today.

### 4.3 Kept and shared

- **On the computer:** palettes are kept with the interface settings (per user).
- **Export and import:**
  - Export writes `<name>.lapin-palette.json`.
  - Import adds the palette.
  - A type or symbol in it is named by its GlobalId and its name; on import it is found in the open project by id, then by name, or the button stays greyed out.
- **Later:** a palette published as an extension in the store (`docs/plan-extensions.md`), with its symbols and scripts inside.

## 5. Stages

| Stage | What | Size |
|---|---|---|
| P0 | Spike: an empty panel window on Windows, macOS and Linux (X11, Wayland), "above Lapin only" as in §3.3; merge `feat/desktop-window` after its Linux check | small |
| P1 | The panel registry (§3.1), today's two panels moved onto it with their settings upgraded; the other panels (catalogue, layers, key plan, tool properties, Script, Planning…) made into registered panels that can dock or float | medium |
| P2 | Pop out: panel windows (§3.2), the shared state widened, tools and keys sent to the main window | medium |
| P3 | Layouts that come back, and named layouts (§3.4) | small |
| P4 | Palettes: the data model and My tools upgraded, a palette shown as a tab, a panel or a window; Customize palettes… | medium |
| P5 | The kinds of button: `typed` (drag from the catalogue), `symbol`, `script`, `graph`; export and import | medium |
| P6 | The guide (interface.md, a new palettes.md, RO), its scenes, and a reply on the forum | small |

**Order of the stages:**

- P0 first, because what A can promise on each OS depends on it.
- P4 can start after P1, beside P2.

## 6. Open questions for later

- A palette as a ribbon tab: should any palette be able to be a tab, or only My tools? The answers point to "any"; to confirm when P4 is designed.
- Should the panel windows of a shared (multiplayer) session be allowed for a guest? Probably yes: they are only views of the same session.
