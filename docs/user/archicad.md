---
title: Archicad
order: 128
summary: Take the model of a running Archicad into Lapin through the free Tapir add-on, as native elements where Lapin rebuilds Archicad's own bodies and as references for the rest, and send Archicad's IFC to your project's Atrium server.
---

**File → Collaborate → Archicad: Receive…** brings the elements of an Archicad that is running on the same computer into the project, live, with no IFC file in between. **Archicad: Send to Atrium…** has Archicad save an IFC and puts it on the project's server as a reference model. Both are in the desktop app, not in Lapin for the browser, and both need Archicad to be running.

## Setting up

Lapin talks to Archicad through **Tapir**, a free add-on (MIT licence) that adds commands to Archicad's own JSON API.

1. Download Tapir from its [releases page](https://github.com/ENZYME-APD/tapir-archicad-automation/releases): version 2.0 or newer, for your Archicad (25 to 30), on Mac or Windows. Install it as its page says.
2. Open Archicad with the project you want to take.
3. In Lapin, choose **File → Collaborate → Archicad: Receive…**. Lapin looks for Archicads on ports 19723 to 19744 (a second Archicad takes the next port) and lists each one with its project, its version and its port.

An Archicad without Tapir, or with a version too old, is listed with what is wrong, and cannot be received from until it is fixed. If none is found, the dialog says so and links this page.

## Receive

Pick the Archicad, and the dialog reads its model and says what a receive would do: the elements by type, the storeys, how many would be added, updated or removed, and what is left out. Then choose:

- **Storeys**: **Make the missing storeys** (the default) makes a Lapin storey for each Archicad story the project lacks and puts each element on its own; **Only the project's storeys** puts everything on the storeys the project already has.
- **Origin**: **Archicad's project origin** (the default) or **Archicad's survey point**, where the coordinates start.
- **Recognise elements** (on by default): walls, slabs, columns, beams, openings and zones become native Lapin elements where they match Archicad's own 3D body within 1 mm or 0.5 %; the rest come in as references. Turn it off to receive everything as references.
- **Receive as a copy**, shown only when the Archicad model was itself sent from this project: the elements come in with ids of their own, beside the elements already here, which are never touched.

**Receive** runs as one undo step, with its progress and a Cancel button. The report says how many elements came in native and how many as references, what was added, updated, unchanged and removed, which storeys were made, and why elements stayed references (for example, a wall Lapin could not rebuild within the tolerance).

To keep the received model on the server: receive it, then **File → Collaborate → Save to server**.

### What becomes native

- **Walls**: straight walls, basic and composite. A composite wall becomes a wall type with its skins.
- **Slabs**: with their holes; a composite slab becomes a slab type.
- **Columns and beams** with rectangular or round sections (a beam runs through the columns as Archicad's does). One of a profile or of several segments is read from its body, and is native when Lapin has its section.
- **Doors and windows**: their openings become native, in their host walls. The door or window itself stays a reference, unless Lapin's configurator reproduces its body.
- **Zones** become rooms.
- Everything else comes in as references, with Archicad's surfaces as materials.

The elements keep **Archicad's own IFC ids** as their GlobalIds, so they are the same ids as in Archicad's IFC export. Receiving again leaves what did not change as it is, updates in place what changed (it keeps its GlobalId, and is measured again), and removes what is gone from Archicad. Each element keeps its Archicad guid, layer and ID in the **Archicad** property set, Archicad's custom properties in sets named as their groups, the IFC property sets Archicad maps to it under their IFC names, and its classification.

## Send to Atrium

**Archicad: Send to Atrium…** needs the project to be connected to a server, and you to be signed in to it (**File → Collaborate…**). Choose the Archicad and:

- the **IFC translator**, one of Archicad's own (the one made for previews is offered first);
- the **title** of the model (the Archicad project's name by default);
- the **folder** on the server, by default the reference models' folder;
- the **discipline**, if you want one;
- the **file name**.

Lapin has Archicad save the IFC with the translator, puts the file in the project's store on the server, makes its reference document there (a model, received) and updates the project's register of reference documents. The dialog tells each step and, at the end, where the model is. A copy of Archicad that cannot save (a demo or education copy may refuse; Archicad 29's demo did save IFC when this was tested) gives Archicad's own message, and the dialog offers the other route: receive the model, then **File → Collaborate → Save to server**.

## Limits

- Roofs, stairs, railings, curtain walls, morphs and objects stay references in this version, and so do curved, slanted and trapezoid walls.
- 2D elements (dimensions, texts, fills) are not received.
- Hotlinked modules are flattened: their elements come in as the project's own.
- Archicad must be running, on the same computer, with Tapir installed.
- It is in the desktop app only.
