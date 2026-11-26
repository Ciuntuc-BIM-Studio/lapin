---
title: Reference documents
order: 127
summary: Keep the documents your project receives (the urbanism certificate, permits and agreements, land-book extracts, studies, a consultant's models) in one register with their folder, number, dates and validity, and the certificate's checklist of permits.
---

A project **receives** documents as well as issuing them: the urbanism certificate, the permits and agreements it asks for, the land-book extract, the topographic survey, the geotechnical study, a consultant's IFC. **Manage → Reference documents…** keeps them in a register inside the project, beside the issues. Nothing here is a container of the ISO 19650 flow: a received document does not move from WIP to Published. It has its own state.

The register works in the desktop app and in the browser, with a server or without one. The documents and their files are kept in the project file.

![The permits folder, with the permit's badge](shots/reference-documents.webp)

## The window

On the left, the **folder tree** with a count on each folder. The two folders at the bottom, *Issued written documents* and *Issued drawings*, are greyed: they show what the project issued, and nothing is dropped there. In the middle, the **list** (type, title, issuer, number, date, valid until, status), which you filter by text, type, phase, status, or *Expiring within N days*; click a column's head to sort. On the right, the selected document.

## Adding documents

**Drop files anywhere on the window**, or use **Add files…**. Each file becomes a document, in the folder you are looking at (or in the folder of its type). A file already held by another document is added anyway, and the window says so. A file above the size limit is not kept in the project file (only its fingerprint is): the window says which.

**New request** makes a document you have asked for and not received yet, with no file. When the file comes, attach it and the document becomes *Received*.

Drag a document (or several, with Ctrl or Cmd held) onto a folder to move it. When the project has a folder (Manage → Project folder), each document's file is also copied into `Reference/` there, in the same folders, and moves with the document.

## The document

Its form holds the title, type, issuer, number, date, validity, phases, role and folder, and the extra fields of its type. Its files can be previewed (a PDF page by page, an image inline), saved as a copy, taken off the document, or added to. An IFC (or an .ifczip) offers **Open as link**: it opens in the project as a read-only link, like File → Link IFC…. A file the project does not keep (one above the size limit, on a server) is fetched from the server first.

**Suggest** reads the file's text and proposes the number, the date, the issuer, the type and the validity, each with the words it was read from. You tick what is right and apply it; nothing is written without you. A scan has no text and gets no suggestion. A validity that comes only from the type's usual period is marked *indicative*: the dates written on the document are what count.

![Suggest, with the words each value was read from](shots/reference-suggest.webp)

Nothing is deleted outright. **Superseded by…** links a document to its newer one; **Withdraw…** asks why and keeps the record. **Delete…** is for a file added by mistake.

## The certificate's checklist

A document of the type *Urbanism certificate* has a **Checklist** tab: the permits and studies it asks for. Tick them from the list, or type your own. Link each item to the document that answers it, or press **Request** to make a requested document and link it. Each item is *missing*, *requested*, *received* or *expired*, and the summary says how many are still missing.

![The checklist: one permit received, one requested, one missing](shots/reference-checklist.webp)

The list shipped with Lapin is indicative. The norms and the validity periods are yours: Lapin never claims a legal period.

## Validity

A received document past its *valid until* date is **expired**; one that ends within 30 days is *expiring*. The status bar counts them; a click opens the window filtered by them. Nothing is shown when there are none.

![The status bar's counter](shots/reference-expiring.webp)

## Folders and types

**Folders and types** edits the project's own copy of the folder tree (rename, add, order, remove: a folder that holds documents stays until you move them), the language the folders are named in on disk, the list of document types with their folder, usual validity and extra fields, and the size above which a file is not kept in the project file. A project made in a Romanian interface starts with Romanian folder names.

## Scan folder

On the desktop, when the project has a folder, **Scan folder** lists the files in its `Reference/` that the register does not hold, with the folder and type their path suggests, for you to add. It also lists the register's copies that are missing there or lie in another folder. The register is the truth: a file gone from the disk is a warning, never a deletion.

## In written documents

The documents read the register (see [Documents](documents.md#reference-documents-in-a-document)):

- **Fields** `{{ref.<type>.number}}`, `.date`, `.issuer`, `.validUntil` and `.title` give the latest received document of a type that is still valid (by its own date): `{{ref.cu.number}}` is the urbanism certificate's number. Expired, superseded, withdrawn and requested documents are never read. `{{ref.<type>.count}}` says how many valid ones there are. Dates are written in the document's language.
- **Repeat over** *Reference documents* (`{{reference.title}}`, `.number`, `.issuer`, `.date`, `.status`, `.folderPath`, `.pages`, the type's extra fields…) and over the *Urbanism certificate's checklist* (`{{check.label}}`, `.stateText`, and the number, date and issuer of the document that answers it).
- **Tables**: *Permits and agreements* (the certificate's checklist with the documents answering it) and *Reference documents* (filtered by type, folder and status).

The certificate these read is the latest received certificate still valid.

![The list of permits and agreements, filled from the register](shots/documents-permits.webp)

## Sending them with an issue

**Manage → Issue…** has **Include reference documents**. Tick documents one by one, or a folder to tick the documents in it and in its subfolders (a requested document has no file and cannot go; a folder does not take superseded or withdrawn ones). Their files go with the issue as copies under their own names (a second file with the same name gets a number), listed in the transmittal (kind `REF`, with the document's number and title) and in the manifest with their SHA-256. On a CDE server they are files of the issue, not documents of the register.

![Issue: the certificate and the permits folder ticked](shots/issue-references.webp)

A file above the size limit is not in the project file. It is fetched from the project's server first. Without a server, the copy in the project folder or the file it was added from is used. When it is found nowhere, it is left out of the issue and Lapin says which.

## With a server

On a project kept on a CDE server, the documents are kept there too, and the **server is the truth**. **Sync issues and documents** (File → Collaborate → Collaborate…) brings the server's documents into the project and sends yours; **Save to server** and **Get latest** do the same along with the issues. Documents are matched by their id and merged field by field: when two people changed the same field, the last writer's value stays, and Lapin says how many edits met someone else's. What others changed is said by name.

- A file above the size limit stays on the server and not in your project file. It is fetched when you open it, preview it or save a copy.
- Deleting follows the roles: viewers read, editors add and change the documents they added, owners change any document, delete one and edit the folders and types. A change the server refuses (your role does not allow it) comes back as it was, and Lapin says why.
- The same register is on the project's page in the browser, in the **Documents** tab, without opening the project at all: see [CDE server](cde-server.md). There a consultant's IFC opens in the browser's 3D read-only, alone or linked into the project's latest commit, with the IDS report the server made of it.
