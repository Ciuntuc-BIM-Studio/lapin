---
title: Documents
order: 105
summary: The project's written parts — the technical report, the specification, the drawing list — made from templates, filled from the model, and exported to DOCX, ODT and PDF without any other program.
---

## What a document is

A project is more than its drawings: it has its written parts too, a technical report, a specification, a list of the drawings. In Lapin these are **documents**: each one starts from a template and is filled from the project — its information, its areas, its storeys and rooms, its wall types, its schedules and its sheets. What only you know (the structure, the fire safety, the site organisation) you write in the document's inputs. A document follows the model: change a wall type or add a room, and the document says so the next time you open it.

Lapin writes the files itself, in DOCX, ODT and PDF, with no Word or LibreOffice on your computer, in the desktop app and in the browser.

## Project information

**Manage → Project information…** holds what the documents ask of the project: its name, number and phase, the client and their address, the site's address, cadastral number and land book, the designer and their address, the chief architect, who drew it and who checked it. Write them once: every document reads them, and the sheets' title blocks take who drew and who checked from here when a sheet does not say. Each field is saved when you leave it.

![Project information: the project's number, phase, client, site and designer](shots/project-info.webp)

The same fields are in the Documents dialog's **Project information** tab.

## Making a document

**Manage → Documents…** (or the **+** beside **Documents** in the project browser) opens the project's documents. On the left are the documents already made and, under **New from template**, the templates; click one and the new document is made and filled at once.

![Documents: the project's documents and the templates, the document on its pages, its inputs and what is left to complete](shots/documents.webp)

In the middle is the document as it will print, page after page, with its table of contents, its tables and its numbers. A field the project does not have yet is marked where it stands. The project browser lists the documents under **Documents**; a click opens the dialog on one.

## Inputs

The **Inputs** tab has the texts only the designer can write, grouped as the document's chapters. In the Romanian templates each one starts with a hint marked **[De completat]** (to be completed) that says what goes there. Type over it: the preview follows a moment after you stop typing. **Reset to default** brings the template's text back.

![Inputs: the structure written over the template's hint](shots/documents-inputs.webp)

## To complete

**To complete** lists what the document still lacks: a project field left empty, a value the model does not have. Click one to see where it is in the document and to go to the field that fills it (a project field opens the **Project information** tab). The list empties as you fill it.

![To complete: the fields the document still lacks](shots/documents-missing.webp)

## Updating, renaming, copying

- **Update from template** takes the template's text and layout again, for a document made before the template changed. Your inputs are kept.
- **Rename**, **Duplicate** and **Delete** work on the document shown. A deletion is one undo step.

## Exporting

- **Export DOCX** and **Export ODT** write a file you can open and edit in Word or LibreOffice, with its headings as the program's own styles, its table of contents and page numbers as fields, and the project's pictures.
- **Export PDF** writes the document laid out by Lapin, ready to print or send.

A field still empty is left blank in the files, so check **To complete** before you export.

## Issuing documents

**Manage → Issue…** takes the documents with the sheets and the model (see [Project folder and issues](cde.md)). Under **Documents, each as a PDF:** every document is ticked at first; tick **Also as DOCX** for an editable copy too. Each file is named by the ISO 19650 convention (`RP` for a report, `SP` for a specification), hashed and listed in the transmittal, and the document takes the issue's revision and status.

![Issue: the documents with the sheets and the model](shots/issue-documents.webp)

## The templates

Lapin comes with three templates, in Romanian for now:

- **Memoriu tehnic de arhitectură** (the architectural technical report, after Law 50/1991): the general data from Project information; the height regime (for example `P+1E`) and the maximum height; the areas — built, gross, usable and basement — in total and per storey; the rooms of each storey with their areas; the walls by type, with their layers; the floors and roofs; the joinery schedule; the room finishes; inputs for the existing site, the use, the structure, the facades, the services, fire safety, accessibility, energy efficiency and the site organisation.
- **Caiet de sarcini: tâmplărie și finisaje** (the specification for joinery and finishes): the project's materials by category, the joinery schedule with its counts, the finishes room by room, and inputs for the general terms, the requirements, the execution and the acceptance, with default texts to adapt.
- **Borderou de piese scrise și desenate** (the list of written and drawn parts): the project's documents and its sheets, with their numbers, titles, scales and revisions.

A visual editor for your own templates comes later.
