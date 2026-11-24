---
title: IDS checks
order: 127
summary: Check the model against an IDS file (buildingSMART IDS 1.0), the information requirements a client or a BIM manager hands over; see what fails and where, turn the failures into remarks, export a report, and have the checks run before an issue goes to the CDE server.
---

An **IDS** (Information Delivery Specification) is a small file, `.ids`, that says what a model must hold: which classes and types, which attributes, property sets and values, classifications, materials, and what is part of what. Clients and BIM managers write them with any IDS editor; Lapin reads IDS 1.0, the buildingSMART standard, and checks the model against it.

Lapin checks the model **as its IFC export writes it**: what another tool would find in the IFC you deliver. Lapin's own property sets (`Lapin_Intent`, `Lapin_Parameters`, your own sets), the types, the layer sets and their materials, the classifications you assigned and the storeys can all be asked for.

## Adding an IDS file

Open **Manage → IDS…** and click **Add IDS file…**. The file is kept in the project, so it travels with the `.lapin` file and with the saves to a CDE server. Adding it again (a newer version of the same file name) replaces it; the trash button removes it. Each step can be undone.

A file that is not IDS 1.0 is refused, with every problem and its line: an unknown IFC version, a property with a value but no data type, an optional requirement with nothing to check, a value that is not a number where a number is asked, a pattern that does not read.

## Running the check

Click **Run the check**. Each specification of each file shows:

- **Pass**, **Fail** or **Skipped** (written for another IFC version than the model's);
- how many elements it applies to and how many pass. A *required* specification fails when nothing in the model is what it applies to; a *prohibited* one fails when anything is.

![IDS: the specifications with their result, a failing one opened](shots/ids-results.webp)

Click a specification to open it: its description, its instructions, its requirements, and each failure, with the element, what it has and what is needed (for example *no Office_Data.Code — Office_Data.Code*). Click a failure to select the element and see it in 3D; **Select the failing elements** selects them all. A large model lists the first failures; **More** brings the rest.

The check reads values as IDS says: texts exactly, numbers in metres, square metres and cubic metres whatever units the model uses, within a millionth; **true** and **false** for yes and no. It changes nothing in the model.

## Remarks and the report

**Make remarks** makes one remark (a BCF issue) for each failing specification, with its failing elements selected and the failures in its description, in one undo step. Running the check again and making the remarks again brings the same remarks up to date instead of adding new ones. On a project kept on a CDE server the remarks go to the server with **Sync issues**, at once when you are signed in.

**Report (XLSX)** writes a workbook: a summary, every specification, every failure with its GlobalId.

## Before an issue goes to the CDE server

When the project has IDS files, **Manage → Issue… → On the CDE server** runs them before **Submit for approval** and lists what fails. With the project's setting on (**A failing specification stops the submission**, in Manage → IDS…), the submission waits until the failures are fixed, or until you tick **Submit despite the IDS failures** and write why: the note goes with the submission, so the approver reads it. The same applies to **Make the issue tag now**. **Run before Submit for approval** turns the check off for the project.

In **File → Collaborate… → Publishing**, the **IDS** tab is the same panel.

## What IDS 1.0 does not check

IDS 1.0 reads no geometry, no clashes and no calculations: for those, use [Clash detection](clash.md) and the [checks before an issue](cde.md). IFC2X3's mapping of occurrence classes to types is not applied (Lapin writes IFC4).
