---
title: The interface and shortcuts
order: 15
summary: Comfortable, Compact and Focus modes, folding the side panels, and the keyboard shortcuts you can change.
---

<!-- video interface -->

## Workspaces

A workspace shows only the ribbon tabs your work needs, in the order that work uses them. Choose it from the button beside **File**, which names the workspace you are in, or on the start screen, at the top of **View → Interface…**, or from **Commands…** (Workspace: …): **All** (every tab of your edition), **Architecture**, **Structure & rebar**, **MEP**, **Site & site works** or **Drafting 2D**. The project stays one: a hidden tab's tools are still in Commands… and the keyboard shortcuts, and a model made in one workspace opens whole in another.

![The workspaces in View → Interface…](shots/workspaces.webp)

When the ribbon's tools are wider than the window, the mouse wheel over them scrolls them sideways.

The tabs stay on one line. When the window is too narrow for all of them, the last ones go under **More** at the end of the row, and the tab you are on always stays in sight.

The tabs are grouped by the work they serve. **Interior** holds the finishes and the furniture. **Site** holds the terrain, the landscape and the site works (scaffolding and formwork). **Construction** holds the work schedule and the estimate (4D and 5D).

Show, hide or move a tab under **Tabs** in the same dialog and the choice becomes **My workspace**, kept on this computer for next time; the other workspaces are still there to switch to. **View** always stays, since Interface… is in it.

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

In every mode the top bar keeps **Undo** and **Redo** at its right end: with their names in Comfortable, as two arrows in Compact and Focus (<kbd>Ctrl</kbd> <kbd>Z</kbd> and <kbd>Ctrl</kbd> <kbd>Y</kbd>; their tooltip says what they undo).

Below the modes, each part can be set on its own: the ribbon, the quick tools (or none: the number keys still work), the command line, the HUD, the tool's prompt and fields, the key plan, the panels and the height of the property rows. Changing one part makes the choice **Custom**.

Below the parts, **Tabs**: untick the ribbon tabs you do without (an architect may hide Structure, Simple rebar and MEP, an engineer Architecture and Site). View always stays, since Interface… is there. The tools of a hidden tab are still in Commands… and in the keyboard shortcuts.

Lapin keeps your choice on this computer, for you. The desktop app and the browser keep theirs apart. In the browser, Lapin starts in Compact when the window is less than 800 pixels tall, until you choose.

## Quick tools

The quick tools are the numbered slots on the view's left edge, against the project browser, so they move with it; small and see-through until the pointer comes to them (View → Interface… can float them at the bottom of the view instead); the keys <kbd>1</kbd>–<kbd>9</kbd> start them. Each workspace has its own: they start as your edition gives them, and you choose them yourself:

- drag a tool from the ribbon onto a slot. A tool stands in one slot only, so it leaves the one it was in;
- drag one slot onto another to swap them;
- right-click a slot: **Choose a tool…**, **Empty this slot** or **Reset quick tools**;
- **View → Quick tools…** sets the nine slots from lists, and **Reset to the edition's** brings back your edition's own.
- a tool that places elements of a type (a wall, a door, a floor, a stair…) can hold one of the project's types: choose it in the slot's right-click menu, or beside the tool in **View → Quick tools…**. The slot then shows the type's name, and pressing it starts the tool with that type, so one slot can hold the brick walls and another the partitions. The type is kept by its name: the slot works in every project with a type of that name, and in one without it the tool takes its current type.

Floating at the bottom, each slot shows its tool's name, and an empty slot after the last waits for a tool. Lapin keeps your quick tools on this computer, for you.

The arrow at the bar's end hides the quick tools; a small **Quick tools** handle stays where they were and brings them back. They are hidden apart in the model's views (plans, 3D, sections) and in sheets and schedules, so you can keep them off the documents and on the drawings.

![The quick tools: a wall type in slot 3, the stair in slot 7, slot 2 empty](shots/quick-tools.webp)

![View → Quick tools…](shots/quick-tools-dialog.webp)

## My tools

**My tools** is a ribbon tab of your own, the first in the tab row: you put in it only the tools you use, taken from any tab, in groups you name.

- **Put tools in.** Right-click any tool in the ribbon → **Add to My tools**, **Add to My tools in…** a group, or **Add to a new group in My tools**. Or drag a tool from another tab onto the **My tools** tab: held over it for a moment, the tab opens, and you drop the tool where a line shows it will land.
- **Order them in the ribbon itself.** Drag a tool before or after another, in its group or into another group; drag a group by its name to move it among the groups. A line shows where it will land, the others slide aside, and dropping anywhere else changes nothing. Double-click a group's name to rename it. Right-click a tool for **Make small** or **Make large**, **Move earlier**, **Move later** and **Remove from My tools**; right-click a group's name for **Rename group**, **Delete group**, **New group** and **Undo the last change**. By keys, <kbd>Alt</kbd> <kbd>←</kbd> or <kbd>→</kbd> moves the tool focused and <kbd>Delete</kbd> removes it.
- **Customize My tools…** (the gear at the end of the tab, View → Interface, or Commands…) shows every tool of the application on the left, searched and grouped by tab, and your tab on the right. Drag tools across, or press **+** (or <kbd>Enter</kbd>) to add one; drag within the right side to reorder; drag a tool back to the left to take it out. Each row also has buttons to move it up or down, to show it as a large or small tool, a list to move it to another group, and **×** to remove it. By keys, <kbd>Alt</kbd> <kbd>↑</kbd> <kbd>↓</kbd> move the tool focused (across the ends of its group too), <kbd>Alt</kbd> <kbd>←</kbd> <kbd>→</kbd> move it to the group before or after, <kbd>Delete</kbd> removes it and <kbd>Ctrl</kbd> <kbd>Z</kbd> undoes the last change. **Undo** and **Reset** are at the bottom, so a wrong drop loses nothing.
- **Export…** and **Import…** save the tab as a small file you can take to another computer or give to a colleague.

Lapin keeps My tools on this computer, for you (the desktop app and the browser apart). A tool your edition lacks is hidden, not lost: it comes back with its module.

![My tools](shots/my-tools-empty.webp)

![My tools](shots/my-tools-menu.webp)

![My tools: two groups, Walls and Openings](shots/my-tools-tab.webp)

![View → Interface → Customize My tools…](shots/my-tools-dialog.webp)

## The catalog

The project browser's **Catalog** tab shows the project's types for each tool that places typed elements: walls, doors, windows, floors, curtain walls, stairs, ramps, ceilings and skirtings. Each type is drawn small (a wall's layers as its plan cut, a floor's as its section, a door's or window's elevation, a skirting's profile), with what it is in a few words.

- click a card to place elements of that type: the tool starts with it;
- drag a card onto a quick slot to keep the tool with that type there;
- **Edit types…** opens the dialog where those types are made and changed.

![The catalog: the wall types](shots/catalog.webp)

![The window types, drawn by their elevation](shots/catalog-windows.webp)

**From the libraries** at the top of the tab lists what can be added to the project from every source: Lapin's own, **This computer**, the **firm folders** and the installed **extensions**. There are entries for types, symbols, line types, drafting standards, rule packs, bar shapes, shared parameters, hatch patterns and families, with a search, a kind and a source filter, the symbols drawn small, and each entry's source and licence. **Add to project** brings it in through the library's own import, in one step to undo; a type brings what it is made of (materials, profiles).

![The Catalog's libraries: the firm folder's line types and symbol](shots/catalog-libraries.webp)

### Firm folders in the library dialogs

Each library's dialog (**Symbols…**, **Type packs…**, **Line types** in Drafting standards, **Drafting standards**, **Rules…**, **Bar shapes…**, **Shared parameters**, **Materials** for `.pat` files, **Price lists…**, **Studio → Families…**, **Manage → Title blocks…** and **Interior → My furniture…**) has a **Firm folders** block that lists the files of its kind in your firm folders. A click reads the file through the dialog's own import, with the same choices as **Import…**. **Save to firm folder…** writes the library's own export into a writable folder, replacing a file of the same name. The small disk button keeps a copy of a file on this computer.

![Symbols: the firm folder's file beside This project and This computer](shots/symbols-firm-folder.webp)

### Save to library at a type configurator

The dialogs that make types (wall, floor, door and window, stair, curtain wall, column and beam, steel and furniture types) have **Save to library**: the chosen type is written as a `.lapintypes` with its layers, materials, profiles and parameters, to **This computer** or a writable firm folder. It then appears in the Catalog, ready to add to another project in one click.

![Save to library in the wall types](shots/save-type-library.webp)

## Folding the side panels

In every mode, the project browser and the properties panel fold to a thin rail on their side:

- the button at the top right of the panel folds it, and the arrow on its rail unfolds it;
- double-click the edge between the panel and the view;
- <kbd>Ctrl</kbd> <kbd>B</kbd> folds or unfolds the browser, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> the properties, and <kbd>Tab</kbd> both.

Click an icon on a folded panel's rail to open the panel over the view without unfolding it. It goes away with <kbd>Esc</kbd> or a click elsewhere; its pin keeps it open. Drag the edge of a panel to make it wider; Lapin remembers the width.

![A folded browser opened over the view from its rail](shots/browser-overlay.webp)

## Panels on either side

**View → Panels** puts the project browser and the properties panel where you want them, as in Revit: **Browser left, Properties right** (the default), **Browser right, Properties left**, **Both panels on the left** or **Both panels on the right**. With both on one side they stand one under the other; drag the divider between them to share the height (double-click it to halve), and drag the side's edge to make it wider. Each panel still folds, on its own: a folded one is a thin bar in the stack, or a rail when both are folded. The assistant tab (desktop) stays with the properties. Lapin remembers the arrangement on this computer.

![Browser and properties stacked on the left](shots/panel-layouts.webp)

## Light and dark

The moon at the right end of the ribbon turns the interface dark (graphite); the sun turns it light again (paper). In the dark theme plans, sections and elevations lie on the dark background, their black pens in light ink; sheets stay paper. The configurators' small drawings (a door's or window's plan, section and elevation, a roof's, a balcony's, an elevator's, a hall's, a stair's…) and the previews of tags, title blocks and materials are drawn on a light paper card in both themes, so their lines, texts and dimensions read as on a printed drawing.

The theme is kept: Lapin starts again in the one you left it in. **File → Preferences…** also offers **As the system**, which follows the computer's own light or dark setting.

## Preferences

**File → Preferences…** (<kbd>Ctrl</kbd>+<kbd>,</kbd>, also in **View → Interface**) holds your own settings, kept on this computer for every project and applied at once:

- **General**: the theme (light, dark, or as the system), the language, and links to **Interface…** and **Keyboard shortcuts…**.
- **Grid and snaps**: the plan's grid, adaptive or fixed, snapping to it, which object snaps the cursor takes, and the polar tracking angle (see [Snaps and helpers](drafting.md#snaps-and-helpers)).
- **Coordinates**: what the status line reads at the cursor, whether the project's points are shown, and the UCS (see [Real coordinates](site.md#real-coordinates)).
- **Display**: the switches of View → Graphics (grid, thin lines, drafts and DXF in 3D) and how the 3D view is navigated, in one place.
- **Profiles**: the profile standards a new project offers and those the pickers show (see [Profile libraries](profiles.md#standards)).
- **Libraries** (desktop app): the **firm folders**, any number of local, network or synced folders that hold library files (symbols, types, line types, standards, rule packs, bar shapes, parameters, hatches, price lists, profiles, families). Tick **writable** to let Lapin save into a folder. The library dialogs and the Catalog read them (below).

![File → Preferences → Libraries: a firm folder](shots/preferences-libraries.webp)

![File → Preferences: grid and snaps](shots/preferences.webp)

## View tabs and windows

Three quick buttons stand before the tabs: **3D** opens the 3D view, **Section** (<kbd>SE</kbd>) starts the section tool and **Measure** (<kbd>ME</kbd>) the measure tool, so the three most used views and tools are one click away from any view.

Each view opened has a tab above the view. Drag a tab to put the view somewhere else. Dragged out of the window, or onto its edge, it opens in a **window of its own** where you drop it, for a second screen (desktop app). Dropped on the right or bottom quarter of a view, it splits the view and opens beside it or under it. Dropped on the other pane, it opens there. While you drag, the place it would go is lit and named. A click on a tab still just opens it.

A right click on a tab offers the same: **Open in a new window**, **Split: open beside**, **Split: open below** (or **Open in the other pane** when the view is split), **Close** and **Close the others**. The window button at the right of the tabs, and **View → New window**, open the view in front in a window of its own. The windows share the model, the selection, the storey and the tool; each keeps its own zoom.

## What a click selects

A click, a selection box or a lasso takes only what the view shows, in plans, sections, elevations, sheets' views and 3D. An element the view's Visibility / Graphics hides (its category, a subcategory, a filter), one put away with Hide / Isolate or by the view's phase, one outside the view range or crop, on a level, link or group not shown, or cut away by a section box, is passed over: the click takes what is drawn under it, and the element under the cursor that lights up is one you can see. With **Drill into assemblies** on, a click inside a whole still steps into its parts. With the modelling lines shown instead of the drawing (the pen button at the top of the plan), everything on the screen can be picked. The project browser, a schedule, a link in the properties panel and the dependencies still select an element by its name, hidden or not; the properties panel then says *Not shown in this view*.

## Several elements at once

Select several elements (Shift-click, or drag a box) and the properties panel shows the last one picked, with **N selected** beside its class. A value they share is shown; one that differs reads *varies*. Change a field and every selected element that has it takes the new value, in one step that **Undo** takes back: the height and base offset of walls, the type of every selected wall, door or beam, the sill of windows, the phases of every selected element whatever its class, the layer and colour of drafted lines, the style of texts. What is each element's own stays its own: a room's number, a wall's length, a door's place along its wall.

## Match properties

**Modify → Match properties** (<kbd>MA</kbd>, or `MATCHPROP` on the command line as in AutoCAD; Revit calls it Match Type) gives other elements what one element has. Select the element to copy from, or start the command and click it first; then click each element that should take its properties. The element under the cursor lights up before you click, in plans, sections and 3D. Each click is one step that **Undo** takes back. **Apply to the selection** gives them to every other selected element at once. Press <kbd>Enter</kbd> or <kbd>Esc</kbd> when you are done.

The gear in the command line's bar lists what is carried, and Lapin remembers your choice: **Type** (to an element of the same class), **Dimensions and settings** (height, offsets, materials, cut priority, a text's style, a door's or window's frame position and sill; to another class only the fields it has too), **Parameters** (your own; a standard set's only to the same class), **Phases**, **Layer, colour and hatch** (drafted shapes) and **Name** (off at first). Where an element stands, its shape and its GlobalId are never carried. A pinned element still takes them: a pin keeps an element in place, not its settings.

![Match properties in plan: the wall copied from, the wall under the cursor lit, and what is carried](shots/match-properties.webp)

## Parameters worked out by formulas

A parameter of your own that is a number, a length, an area, an angle or a yes/no (not a text or a choice) can be worked out instead of typed. In **Properties → Parameters**, press the **Σ** button before its value: the value becomes a **formula** field. Type the formula and press <kbd>Enter</kbd> (or leave the field) to keep it. Under the field Lapin shows what the formula gives, *= 21*, or why it does not read; a formula that does not read is not kept. Press **Σ** again to go back to a value: the last result stays as the value.

![A Studs parameter worked out from the wall's length and a Spacing parameter](shots/param-formula.webp)

A formula is kept as written and worked out every time it is asked for, so it follows the element: change the wall's length or the *Spacing* and *Studs* changes with them. Tags, schedules and the IFC export read the result. What a formula may use:

- **Names**: the element's other parameters and its type's, and the element's own numbers (*Height*, *Width*). A name with spaces may be written without them: *Fire rating* or *FireRating*. A parameter may use another that is a formula.
- **Quantities**, when the element has them: *Length*, *Width*, *Height*, *GrossArea*, *NetArea*, *GrossVolume*, *NetVolume*.
- **Functions**: *min*, *max*, *round*, *floor*, *ceil*, *sqrt*, *sin*, *cos*, *tan*, *atan2*, *if* and *clamp*, with + − × ÷ and brackets. Decimals are written with a dot; arguments are separated by a comma or a semicolon.

**Names and functions** under the field lists the names this element offers. A yes/no formula is yes when its result is not zero (*if(Height > 3, 1, 0)*).

![A formula that does not read says why](shots/param-formula-error.webp)

The same language works in the live text of [symbols and tags](drafting.md), written `{= attr.w * 2 | 0.00}`.

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

There are two ways to type a command. **Letters** such as <kbd>WA</kbd> work when no field has the focus and the command line is empty: type them over the view. The command runs at once. When a command line command starts with the same letters (<kbd>DI</kbd> and <kbd>DIMADD</kbd>), Lapin waits a moment: keep typing to write the longer command, or press <kbd>Enter</kbd> or <kbd>Space</kbd> to run the shorter one now. While a command asks for a point or a value, letters and digits go to it. **Command line** commands such as <kbd>L</kbd>, <kbd>M</kbd> or <kbd>TRIM</kbd> followed by <kbd>Enter</kbd> work in every preset. <kbd>Esc</kbd> always cancels.

When the two collide, choose how they share the keyboard:

- **Letter shortcuts (Revit-style)**, in this dialog and in **Preferences → General → Keyboard**, is on by default. Turn it off to keep only the command line: every letter you type goes to it, and <kbd>Enter</kbd> or <kbd>Space</kbd> runs a command, an alias of yours, or, when the text is neither, the command whose letters it is (<kbd>WA</kbd> <kbd>Enter</kbd> still starts the wall). Chords (<kbd>Ctrl</kbd> + a key, the function keys, <kbd>Delete</kbd>) and the number keys of the quick tools keep working.
- The <kbd>`</kbd> key (the one left of <kbd>1</kbd>, found by its place on any layout, and a shortcut like the others: **Command line: take all typing**) gives the command line all the typing, with the letters on or off. The command line is outlined and says so; no letters start a tool, no number picks a quick tool. <kbd>Enter</kbd> runs what you typed and gives the typing back, unless a command then asks for a point or a value: the mode stays until that command ends. <kbd>Esc</kbd> or the same key leaves it.

### Command aliases

The **Command aliases** page of the dialog holds names of your own for commands. Type 1 to 12 letters or digits (the first a letter), find the command in the list (a command line command such as <kbd>LINE</kbd>, or any ribbon or app command), and press **Add alias**. Type the alias in the command line and press <kbd>Enter</kbd> or <kbd>Space</kbd>. Yours come before the built-in names. The page says what an alias replaces: a built-in command line name, a shortcut with the same letters, or a shortcut it makes wait (with the letters on, a shortcut that is an alias, or the start of one, waits a moment so you can type on and press <kbd>Enter</kbd>). Aliases are kept with your shortcuts, in **Export…** and **Import…** too.

![Command aliases, with what an alias shadows](shots/shortcuts-aliases.webp)

### The default shortcuts

These are the Revit-style shortcuts, as Lapin has them now:

<!-- shortcuts -->

The number keys <kbd>1</kbd>–<kbd>9</kbd> pick the quick tools.

## Moving a dialog

Every dialog moves by its title bar, as a window does: press on the title and drag, with the mouse or a finger. It stays inside the window, opens again where you left it for as long as the program is open, and a double click on the title puts it back in the middle. That way you can move a dialog off the part of the model you are working on, or look at the model behind it.
