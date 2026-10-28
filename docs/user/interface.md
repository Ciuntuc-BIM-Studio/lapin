---
title: The interface and shortcuts
order: 15
summary: Comfortable, Compact and Focus modes, folding the side panels, and the keyboard shortcuts you can change.
---

## Three modes

**View → Interface…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) chooses how much stands around the view. Pick one of the three modes at the top of the dialog; the change shows at once.

![The Interface dialog: the three modes, and each part on its own](shots/interface-dialog.webp)

- **Comfortable** is the window as the other pages show it: the ribbon with large tools and their names, a command line under each drawing, and the HUD, the hint, the length and angle fields, the key plan and the quick tools over the view.
- **Compact** gives the view most of the window:

![Compact: more room for the drawing](shots/interface-compact.webp)

1. **The ribbon** is one band of icons. Thin rules part the groups. Hover over a tool to see its name, its keys and its group.
2. **The project browser** folds to a rail. Its icons open the project, the levels or the IFC tree over the view for a moment.
3. **The quick tools** <kbd>1</kbd>–<kbd>9</kbd> stand on the view's left edge.
4. **The key plan** is a button. Click it to open the map, and its × to fold it again.
5. **One status line** holds the tool's name and what it asks for, what you type, the length and angle fields while you draw, SNAP, ORTHO and POLAR, the level and the view, then the project.

- **Focus** keeps only the tabs and a thin command line. Click a tab and its tools drop over the view; the band folds again once you pick a tool, or with <kbd>Esc</kbd> or a click elsewhere. The panels hide. When something is selected, a small card shows what it is and its first properties; **All properties…** opens the properties panel over the view. <kbd>Tab</kbd> brings both panels back, and hides them again.

![Focus: the selection in a card](shots/interface-focus.webp)

Below the modes, each part can be set on its own: the ribbon, the quick tools (or none: the number keys still work), the command line, the HUD, the tool's prompt and fields, the key plan, the panels and the height of the property rows. Changing one part makes the choice **Custom**.

Lapin keeps your choice on this computer, for you. The desktop app and the browser keep theirs apart. In the browser, Lapin starts in Compact when the window is less than 800 pixels tall, until you choose.

## Folding the side panels

In every mode, the project browser and the properties panel fold to a thin rail on their side:

- the button at the top right of the panel folds it, and the arrow on its rail unfolds it;
- double-click the edge between the panel and the view;
- <kbd>Ctrl</kbd> <kbd>B</kbd> folds or unfolds the browser, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> the properties, and <kbd>Tab</kbd> both.

Click an icon on a folded panel's rail to open the panel over the view without unfolding it. It goes away with <kbd>Esc</kbd> or a click elsewhere; its pin keeps it open. Drag the edge of a panel to make it wider; Lapin remembers the width.

![A folded browser opened over the view from its rail](shots/browser-overlay.webp)

## Keyboard shortcuts

**View → Keyboard shortcuts…** (<kbd>KS</kbd>) lists every command the keyboard can reach, by tab. Each row shows the command's shortcuts.

![The keyboard shortcuts, searched for "wall"](shots/shortcuts-dialog.webp)

- **Search** by a command's name or by a key.
- **+** adds a shortcut: press a combination (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), or type up to three letters and press <kbd>Enter</kbd>. <kbd>Esc</kbd> stops.
- If another command already has the shortcut, Lapin says which one. **Reassign** takes it from that command, **Keep both** leaves it to both (the first in the list wins), **Cancel** changes nothing.
- **×** removes a shortcut. The arrow at the end of a row puts that command back as the preset has it, and **Reset all** does it for every command.
- **Preset** chooses the set you start from: **Revit-style** (the default: Revit's usual letters, where Lapin has the command) or **Lapin classic** (Lapin's own letters from before).
- **Export…** writes your shortcuts to a file, and **Import…** reads one, for another computer or a colleague.

Tooltips, the menus and the command palette (<kbd>Ctrl</kbd> <kbd>K</kbd>) always show the shortcuts as they are now.

### Letters and the command line

Letters such as <kbd>WA</kbd> work when no field has the focus and the command line is empty: type them over the view. The command runs at once. When a command line command starts with the same letters (<kbd>DI</kbd> and <kbd>DIMADD</kbd>), Lapin waits a moment: keep typing to write the longer command, or press <kbd>Enter</kbd> or <kbd>Space</kbd> to run the shorter one now. While a command asks for a point or a value, letters and digits go to it. Command line commands such as <kbd>L</kbd>, <kbd>M</kbd> or <kbd>TRIM</kbd> followed by <kbd>Enter</kbd> work in every preset. <kbd>Esc</kbd> always cancels.

### The default shortcuts

These are the Revit-style shortcuts, as Lapin has them now:

<!-- shortcuts -->

The number keys <kbd>1</kbd>–<kbd>9</kbd> pick the quick tools.
