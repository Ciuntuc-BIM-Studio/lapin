---
title: Project folder and issues
order: 125
summary: Issue sheets and the model into a project folder as ISO 19650 asks, named by the convention, with a transmittal, after the checks a drawing office does.
---

## The project folder

**Manage → Project folder** sets the folder the project is issued into: a folder on your computer, a network share, or a SharePoint or OneDrive folder synced to disk. Lapin asks for no account and talks to no server: it writes files, and whatever syncs the folder is your office's. The path is kept in the project, so it travels with the file; a folder that is not on this computer is a warning, not an error.

![Project folder: the folder, the containers, the naming](shots/project-folder.webp)

Inside the folder Lapin makes the four ISO 19650 containers on the first issue: **WIP**, **Shared**, **Published** and **Archived**. A status code suggests the container: S0 goes to WIP, S1–S7 to Shared, A, B and CR to Published.

## Naming

Files are named after ISO 19650-2: **Project-Originator-Volume-Level-Type-Role-Number**, then the status and the revision, for example `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. In the Project folder dialog you set the project code, the originator (your office), the volume, and the default level and role. The rest comes from the project:

- a sheet's **level** is the storey its plans draw (`00` at ±0.00, `01` above, `B1` below, `ZZ` when it draws several or none);
- its **role** is its discipline's (architecture A, structure S, MEP M, civil C);
- its **number** is the digits of its sheet number (`A-101` becomes `0101`);
- the model is `M3`, the transmittal `IE`.

## Checks

**Manage → Check** runs the rules a drawing office checks by eye before an issue, over the whole project. A **blocking** finding stops an issue; a **warning** can be overridden. Click a finding to go to the sheet, the view or the element.

![Check: the findings by rule](shots/cde-check.webp)

Blocking: a title block without a number, a title or who drew it; a view without a scale; a view outside the frame or over the title block; a sheet that changed since its last issue but is still at that revision; a sheet number that gives no file number. Warnings: who checked, the date and the phase left empty; a plan coloured by a room scheme, or with many hatches, without a legend; a plan, section or elevation on no sheet; a site plan without a north arrow; elements on no storey, without a type or a material; doors and windows without a tag; rooms without a number; the required IFC properties (IsExternal, LoadBearing, FireRating on doors) left empty.

## Issuing

**Manage → Issue…** writes an issue into a container (the desktop app only, since it writes files):

![Issue: the sheets, the revision, the status, the checks](shots/cde-issue.webp)

1. Give the issue a name, its **revision** (`P01`, `P02`… while preliminary, `C01`… once contractual; the next one is proposed from the last issue), its **status** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) and the **container**.
2. Tick the sheets that go, the model as IFC, each sheet as DXF too, or all the sheets in one PDF too. Each file's name is shown as it will be written.
3. The checks run as you choose. With a blocking finding the issue cannot go; with warnings alone tick **Issue anyway**.
4. **Issue** writes each sheet as a PDF, the IFC and the rest into the container, named by the convention, hashes every file (SHA-256), writes the **transmittal** (a PDF and a CSV: project, issue, date, revision, status, the files) and a **manifest** (JSON, with every hash) beside them, and records the issue in the project. Each printed sheet takes the issue's revision and status in its title block, and its revision table lists the issues it went out in.
5. The project is then saved as a **version** named after the issue (see [Files, versions and recovery](files.md)), so an issue can be reopened, restored or compared with the project as it is now.

The issues are listed at the bottom of the Project folder dialog, each with its files and **Open folder**.
