---
title: Archicad
order: 128
summary: Take the model of a running Archicad into Lapin through the free Tapir add-on, as native elements where Lapin rebuilds Archicad's own bodies and as references for the rest, and send Archicad's IFC to your project's Atrium server.
---

**File → Collaborate → Archicad: Receive…** brings the elements of an Archicad that is running on the same computer into the project, live, with no IFC file in between. **Get latest from Archicad** then brings only what changed in Archicad since, and the status bar says when it changed. **Archicad: Send to Atrium…** has Archicad save an IFC and puts it on the project's server as a reference model. Both are in the desktop app, not in Lapin for the browser, and both need Archicad to be running.

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
- **Georeference the project from Archicad's location** (on by default): see [Georeferencing](#georeferencing).
- **Receive as a copy**, shown only when the Archicad model was itself sent from this project: the elements come in with ids of their own, beside the elements already here, which are never touched.

**Receive** runs as one undo step, with its progress and a Cancel button. The report says how many elements came in native and how many as references, what was added, updated, unchanged and removed, which storeys were made, and why elements stayed references (for example, a wall Lapin could not rebuild within the tolerance).

To keep the received model on the server: receive it, then **File → Collaborate → Save to server**.

### The project information

A receive also reads Archicad's **Project Info** (File → Info → Project Info…) and puts it into Lapin's project information (**Manage → Project information…**), in the same undo step:

- An **empty** field of the project is filled: the project's name from *Project Name*, its number from *Project Number* (else *Project Code*, else *Project ID*), the phase from *Project Status* when it is one of Lapin's phases (DTAC, PT…; otherwise it is the phase name), the client and their address, the site's address (the full address, else its lines, town, county, postcode and country joined), the designer (Archicad's *Company*) with their address, registration number, phone and e-mail, the project manager (the company's contact) and who drew it (*CAD Technician*).
- A field the project **already has** with another value is **never overwritten**. The report lists it with both values (In Lapin, In Archicad); tick the ones to take and click **Take the ticked values** (one more undo step).
- What Archicad says that no field takes (the project's description, keywords, the site's name, custom autotexts…) is kept apart, read-only: Project information shows it under **From Archicad**. It is replaced at each receive.

Before you receive, the dialog says how many fields Archicad would fill and how many differ. If Archicad has a dialog open (for example the Project Info dialog), Archicad answers nothing until it is closed: Lapin says that Archicad is busy, and which dialog is open; close it and try again.

Lapin and Atrium Server map Archicad's fields with the same table, so a value lands under the same field on both sides.

### Georeferencing

When the project has no georeference yet (**Manage → Location**), a receive takes Archicad's location (Options → Project Preferences → Location Settings in Archicad):

- Archicad's **survey point**, when it is set (map coordinates, or a coordinate system named), ties the project to the map: its eastings, northings and elevation, the coordinate system when Lapin knows it (Stereo 70, UTM, Lambert-93…; another one keeps the coordinates with no system, and the report says so), and the angle of the project's X axis to the map as the angle to true north. A survey point turned against the project origin is taken with its turn.
- Otherwise, Archicad's **latitude and longitude** place the project's base point.
- Archicad's **untouched default** is never taken: a new Archicad project stands in Budapest (longitude 19.05477, latitude 47.5597) with its survey point at the origin and no coordinate system, which says nothing of your model.

A project georeferenced already keeps its own. The dialog says before the receive what it would do with Archicad's location; untick **Georeference the project from Archicad's location** to leave the project's location alone. **Origin: Archicad's survey point** moves the model to the survey point only when it is not turned; with a turned one, receive from the project origin (the project's location then carries the turn).

### What becomes native

- **Walls**: straight walls, basic and composite. A composite wall becomes a wall type with its skins. The type keeps the composite's Archicad GUID: a later receive finds it by it, and when the composite changed in Archicad (its skins, their materials, its name), the type is updated in place and its walls are rebuilt on it and measured again, rather than a second type being made. A building material renamed in Archicad renames its Lapin material the same way.
- **Slabs**: with their holes; a composite slab becomes a slab type.
- **Columns and beams** with rectangular or round sections (a beam runs through the columns as Archicad's does). One of a profile or of several segments is read from its body, and is native when Lapin has its section.
- **Doors and windows**: their openings become native, in their host walls. The door or window itself stays a reference, unless Lapin's configurator reproduces its body.
- **Zones** become rooms.
- Everything else comes in as references, with Archicad's surfaces as materials.

The elements keep **Archicad's own IFC ids** as their GlobalIds, so they are the same ids as in Archicad's IFC export. Receiving again leaves what did not change as it is, updates in place what changed (it keeps its GlobalId, and is measured again), and removes what is gone from Archicad. Each element keeps its Archicad guid, layer and ID in the **Archicad** property set, Archicad's custom properties in sets named as their groups, the IFC property sets Archicad maps to it under their IFC names, and its classification.

## Get latest from Archicad

After a receive, Lapin keeps a mark of Archicad's state with the project (in its history, never in the model or a commit). **File → Collaborate → Get latest from Archicad** asks Archicad what changed since that mark, and reads only the elements created or modified since (with a wall's doors and windows, and a door's or window's wall), removes the ones deleted in Archicad, and updates the rest in place, keeping their GlobalIds, measured by the same fidelity gate as a receive. It is one undo step. Elements that did not change are not touched. Moving a wall in Archicad usually changes the walls joined to it and the windows in it too; Lapin reads those again and leaves them as they are when nothing in them changed.

Get latest reads the whole model and compares each element instead when it cannot ask what changed: Archicad was restarted or the project closed and opened again (Archicad forgets its marks then), the model was received before this version, or the project changed here since (an undo of a receive or of an update). If the Archicad project open is not the one received (none of its elements are), it refuses and says so: open that project in Archicad, or receive this one as a copy.

While the project is linked to an Archicad model and the window is visible, the status bar asks Archicad every 10 seconds whether the model changed, without reading any element, and shows **Archicad model changed (N)** when it did (N elements created, modified or deleted). A click on it runs Get latest from Archicad. Nothing shows while Archicad is closed or busy with a dialog, nor while a receive or an update runs.

## Send to Atrium

**Archicad: Send to Atrium…** needs the project to be connected to a server, and you to be signed in to it (**File → Collaborate…**). Choose the Archicad and:

- the **IFC translator**, one of Archicad's own (the one made for previews is offered first);
- the **title** of the model (the Archicad project's name by default);
- the **folder** on the server, by default the reference models' folder;
- the **discipline**, if you want one;
- the **file name**.

Lapin has Archicad save the IFC with the translator, puts the file in the project's store on the server, makes its reference document there (a model, received) and updates the project's register of reference documents. With **Also propose the project information** (on by default), Archicad's Project Info then goes to the server as a **proposal** for the project's information: an owner applies it, in part or whole, in **Publishing → Project information** ([CDE server](cde-server.md#project-information)); nothing on the server changes until then. When Archicad says nothing the server lacks, nothing is proposed. The dialog tells each step and, at the end, where the model is. A copy of Archicad that cannot save (a demo or education copy may refuse; Archicad 29's demo did save IFC when this was tested) gives Archicad's own message, and the dialog offers the other route: receive the model, then **File → Collaborate → Save to server**.

## Limits

- Roofs, stairs, railings, curtain walls, morphs and objects stay references in this version, and so do curved, slanted and trapezoid walls.
- 2D elements (dimensions, texts, fills) are not received.
- Hotlinked modules are flattened: their elements come in as the project's own.
- Archicad must be running, on the same computer, with Tapir installed.
- It is in the desktop app only.
