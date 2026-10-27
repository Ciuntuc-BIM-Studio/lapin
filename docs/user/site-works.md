---
title: Site works
order: 55
summary: Facade scaffolding and formwork for floors and beams, laid out from the model by their configurators.
---

The **Site works** tab models the temporary works of the site: facade scaffolding, and the formwork of floors and beams. Lapin lays them out from the model and counts what they need. When the model changes, they follow it.

![The Site works tab](shots/ribbon-site-works.webp)

Scaffolds and formwork are **temporary**: they are built and taken down in the **Execution** phase. The first one you place adds that phase if the project has none, and makes it the active phase. Views of earlier phases leave them out. See **Manage → Phases**.

![A scaffold round the house, the first floor's formwork on its props](shots/site-works-3d.webp)

## Scaffolding

- **Scaffold on walls** (<kbd>SW</kbd>): select the walls (Shift-click for more), then press it. The scaffold goes round their outer faces, at its type's distance from the wall, up to the walls' top. It closes its corners itself. Move a wall or make it taller, and the scaffold follows.
- **Scaffold** (<kbd>SC</kbd>): draw its line in plan along the facade, and press Enter to finish. The scaffold stands on the left of the line. Choose the other side in its properties.

Each straight run is split into the type's bay lengths, with as few bays as reach its end. The lifts climb until the top deck is within reach of the work top. The guardrail stands above the top deck. The feet stand on the terrain, on base jacks. If the ground falls more than the jacks take up, Lapin says so.

![A scaffold's properties](shots/scaffold-properties.webp)

In its properties you can set the **work top**: leave it empty for the walls' top. You can also choose whether the feet stand **on the terrain** or **on the storey**, or **detach** it from the walls.

Every scaffold and formwork has a **type**. You choose it at the top of the properties, or give one element's type to others with Match Type (MA). Under **Type parameters** are the values the configurator sets: the system, the bay lengths, the width, the lift, the plywood, the spacings, the article. You can change them there too. A change is the type's, so every element of that type follows it. Create Similar (CS) draws a new one of the same type. A type you create in the configurator is the one the next scaffold or formwork takes.

**Scaffold types…** opens the configurator. It sets the system (modular or frames), the bay lengths, the width and the lift, the distance from the wall, the decks, guardrails and toe boards, the anchors, the base jacks, the access bays and the protective net. The elevation shows a run of the length and height you choose, with what it counts.

![The scaffold configurator](shots/scaffold-configurator.webp)

## Formwork

- **Formwork** (<kbd>FW</kbd>): select floors and beams, then press it. Each one gets the formwork of all its formable faces.
- **Formwork on face** (<kbd>FF</kbd>): pick one face. In 3D, the face under the cursor lights up with its name: a floor's soffit, a beam's bottom or one of its sides. In plan, click a floor's edge or inside its outline. A face picked later goes on the same formwork.

A **floor's formwork** covers its soffit with plywood on secondary beams, on primary beams, on props. The beams under the floor are not formed again, and neither are the walls and columns it rests on or its openings. The spacings come from the type's table, by the floor's thickness. The props reach down to the floor below, the ground, or the storey under it. The free edges are formed too, as high as the floor plus an upstand. In the properties you choose which faces are formed, and the direction of the primary beams.

A **beam's formwork** is its bottom, on bearers and props, and its sides. Each side is as high as the beam below the floor cast with it. Columns and deeper beams interrupt it. You can add chamfer fillets along the bottom edges.

**Formwork types…** opens the configurator, for floors and for beams. For floors it sets the system (timber beams and props, or panels), the plywood, the beams and their catalogue lengths, the spacing table, the edges, the props and the reuses. For beams it sets the bearers, the props, the yokes by the beam's depth, the chamfer and the reuses. The section shows the layers and the prop the engine picks for the height you choose.

![The formwork configurator](shots/formwork-configurator.webp)

## Walls, columns, ring beams and footings

**Formwork** and **Formwork on face** also work on walls, columns and footings:

- **A wall** gets both faces and its free ends, less its doors and windows. A layered wall with a ring beam or a concrete band gets only those. In its properties you can choose both faces, the ring beams or the free ends.
- **A column** gets its sides, up to the floor or beam cast on it.
- **A footing** gets the sides of each step of a pad, or the sides of a strip's sole and stem.

Whatever stands against a face is left out: another wall, a column. A floor cast on top stops the face at its soffit. The type sets the panels (their widths and heights), the ties through walls, the column clamps and the push-pull props.

![The configurator for walls and columns](shots/wall-formwork-configurator.webp)

## Formwork plan

**Formwork plan** makes a plan of the active storey in the Execution phase, cut a metre above the floor, **seen from below**: a ceiling plan. The props are cut. The formwork above them is drawn as you would see it looking up: the primary and secondary beams, the plywood and the edges. It lies the way the plan lies, so the two match.

**Formwork plan from above** makes the same plan seen from above, with the formwork drawn dashed over the cut. Any plan can be switched between the two in its properties (**Seen from**) or in **View range**.

![The ground floor's formwork plan](shots/formwork-plan.webp)

## What they count

Each scaffold and formwork lists its quantities in its properties:

- the scaffold's area, bays, lifts, standards, decks and anchors;
- the formwork's contact area by face, props by size, and beams by catalogue length.

**Formwork schedule** and **Scaffold schedule** add their tables to the project browser, under Schedules, to put on a sheet or export.

The formwork schedule closes with a line per type:

- **the set**: the most formwork cast at once. That is per execution phase when the work is split into several, else per storey.
- **what to buy**: the set, or the total divided by the type's reuses, whichever is more.

Each type has its **article** for the bill of quantities: the code in the norms your estimate uses, and its text. You enter them in the configurator, and the schedule shows them on each line.

In IFC they are written as assemblies (`IfcElementAssembly` FORMWORK and SCAFFOLD) with the status TEMPORARY. The pieces are members, plates and accessories. Each comes with its construction product resource, and the formwork is connected to the element it forms.
