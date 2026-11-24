---
title: Working on a CDE server
order: 126
summary: Sign in to your office's CDE server (Atrium Server, such as cde.lapinbim.com), clone a project, save your work to the server and get the others', keep the issues in step, and, as an administrator, manage the accounts and projects.
---

A CDE server keeps your office's projects in one place: every save with its author and note, the versions and issue tags, the members of each project and their roles, and the issues (BCF topics) everyone answers. Lapin works on it from the desktop app and from the browser. The company's server is at **cde.lapinbim.com**; your office can run its own, Atrium Server, which is free.

There is no sign-up: the server's administrator makes your account and gives you a first password, which you change after you sign in.

## Signing in

**In the browser**, open the server's address. The start screen asks for your e-mail address and password. Closing the tab signs you out; a reload keeps you signed in.

**On the desktop**, open **File → Collaborate…**, choose **Server**, type the server's address and sign in. The app keeps the sign-in for next time on this computer, never in the project.

Signed in, **Change password…** sets your own password; your other sign-ins end.

## Projects and their pages

In the browser, the start screen lists the projects you are a member of, with your role in each:

- a **Viewer** looks, keeps copies, and comments on issues;
- an **Editor** also saves to the server, and makes and changes issues;
- an **Owner** does everything, and manages the project's members and rules.

A project's page shows the history of its main line (who saved, when, with what note, and the versions and issue tags made from each save), the branches in progress, the **Activity**, the **Members** and the **Rules**.

On the desktop, File → Collaborate… shows the Publishing, the Activity and the Rules of the project the file is connected to.

An **issue tag** is an issue made with **Manage → Issue…** (its revision, status, container and files) once it is on the server; it never changes.

**Activity** lists, newest first, the saves to the server, the versions and issue tags, the submissions and their decisions, and every change to an issue: who created it, commented, changed its status, assigned it. **More** reads older entries.

**Members**: owners add someone who has an account on the server (by e-mail address), change a role, or remove a member. A project always keeps at least one owner.

**Rules** (owners change them; the others read them):

- who saves to main: editors and owners, or owners only;
- who makes issue tags directly: owners only, or editors and owners;
- who approves submissions: owners only, or editors and owners;
- who comments on issues: everyone, viewers too, or editors and owners;
- who closes an issue: its author and owners, or anyone who may change it;
- whether **Resolved** needs a comment saying how it was resolved.

## Clone, save to server, get latest

**Clone to work on** brings the project with its history. In the browser the copy lives in the browser until you save to the server; on the desktop it goes into a new file.

**File → Save to server…** asks for a short note and sends your work. The status bar says **Not saved to the server** while you have work the server has not got; a click opens Save to server.

**File → Get latest from server** brings the others' saves into your project, in one undo step.

**Open latest, read-only** (in the browser) opens the latest save to look at: nothing goes back to the server. Each save can also be downloaded as a `.lapin` file or as its IFC copy.

## When two people saved

If someone saved to the server after you last got the latest, your save is refused and **File → Collaborate…** opens on the choices:

- **Keep my work as a branch** sends your work to the server as a branch in progress (named after you and the date), then brings your project to the server's latest;
- **Discard my commits** brings your project to the server's latest; your commits stay in the file as a local branch.

## Issues

On a project whose remote is a CDE server, **Manage → Model remarks…** shows the server's issues. The remarks in the project and the issues on the server are the same topics, kept in step by their GUID; the server is the reference, and the project keeps a copy for working offline.

**Sync issues** (in the remarks dialog, and in File → Collaborate…) brings what changed on the server (in one undo step) and sends what you changed. Save to server and Get latest sync the issues too.

- Two people who change **different fields** of one issue (one its priority, the other its description) both keep their change.
- When two people change **the same field**, the last one to sync wins. The Activity shows who changed what.
- A new comment goes to the server before a status change, so **Resolved** finds the comment that explains it.
- An issue you delete is deleted on the server when your role allows it (owners); otherwise it comes back at the next sync.

What your role does not allow is greyed out:

- viewers do not make issues; a viewer assigned to an issue changes only its status;
- editors change the issues they made or are assigned to;
- only the issue's author or an owner closes it (unless the project's rules let anyone who may change it close it);
- only owners delete issues.

The types, statuses, priorities and labels offered are the project's own on the server, and **Assigned to** lists the project's members. When the server refuses a change it says why (for example *Say how it was resolved: add a comment, then set Resolved.*): a value your role may not set comes back as the server has it; a status waiting for its comment stays in your project until you add the comment and sync again.

## Publishing

On a CDE server an issue goes from Lapin to the server in two ways, both from the end of **Manage → Issue…**, under **On the CDE server**. Either way the project is saved to the server first, so the issue goes with a save the server holds; the files the project keeps (the PDFs, the transmittal, the IFC when kept) go with it.

- **Submit for approval** asks an approver to publish the issue. It waits in the project's **Submissions** until an approver approves it (the server then makes its issue tag) or rejects it. Editors and owners submit.
- **Make the issue tag now** puts the issue tag on the server at once, without approval, where the project's rules let you (owners by default). On a folder remote this is the only way.

A WIP issue stays in the project: issue to Shared or Published to put it on the server.

When the project has IDS files, they are checked before **Submit for approval**, and a failing specification stops the submission unless you say why: see [IDS checks](ids.md).

Issuing the same sheets again makes a **new revision of the same issue** (Issue… proposes it: **Issue as** → *New revision of issue #n*, the revision after its last): the submission and the issue tag carry that issue's number and identity, `#1 P01`, then `#1 P02`. Once the later revision is published, the earlier Published one moves to **Archived**. **New issue** gives the documents a new number instead.

The project's page (in the browser) and **File → Collaborate… → Publishing** (on the desktop) have these tabs:

- **Containers**: the ISO 19650 containers. **WIP** lists the branches in progress; **Shared** and **Published** list the issue tags with their revision, status, date and who issued them (**approved** when it came through the queue); a Published revision goes to **Archived** when the same issue is published again at a later revision. **Open** opens a revision read-only; **.lapin** and **IFC** download it; **files** lists the issued files, each to download.
- **Submissions**: the queue, waiting ones first (**Show** picks the others: approved, rejected, withdrawn, all). Click one for its commit, documents, files and **gate**: the open issues that stop it. An issue is tied to a document by a reference link `lapin-document:<document name>`, which Lapin sets itself: a remark made with a sheet in front is tied to that sheet's document, and the remarks dialog shows, adds and removes a remark's documents (see [Remarks](cde.md)); Sync issues sends the ties to the server. With no documents, every open issue of priority Critical stops it. Click an issue of the gate to open it in Model remarks (in the project open here, after Sync issues).
  - **Approve** (with an optional note) makes the issue tag; the resolved issues tied to the submission are closed. While the gate fails, only an owner approves, by ticking **Approve despite the open issues** and writing a note: the override and its note are kept.
  - **Reject** needs the reason.
  - **Withdraw** is the author's.
  - What your role and the project's rules do not allow is greyed out; the author does not approve their own submission (unless they are the project's only owner).
- **Handover diff**: pick two issued revisions, **Compare**: the documents added, removed, changed (another file) and unchanged between them, and how many model parts changed. **Export to XLSX** writes the handover report with this diff.
- **Register** (when the server checks documents): the project's document register. Owners **Import a register (CSV or XLSX)…**: its first row names the columns, the code (required: `code`, `document`, `cod`…), the title, the expected revision, and any value the title block must show (`client`, `beneficiar`…). The tab shows the documents, which columns were read, and the **import warnings** (a row without a code, a code met twice). Importing again replaces it; **Delete** removes it. Members read it.

**Handover report (XLSX)** writes an Excel workbook of the project open here: a **Report** sheet (project, server, when, by whom, the counts), **Issues** (number, title, type, status, priority, assigned to, due date, labels, dates and authors, comments), **Comments**, the **Journal** (every change to the issues on the server: who, when, the old and the new value) and, from the Handover diff, the **Handover diff** sheet. Lapin writes it itself; it opens in Excel, LibreOffice and Numbers.

## Checks on the documents

A CDE server such as cde.lapinbim.com checks every PDF and DXF sheet a submission issues, which often come from other tools, as soon as it is submitted. Only **errors** stop the approval; warnings and notes are shown.

- **V1**, names and revisions: the file's code matches the project's naming rule and is in the register; its revision is the submission's and the register's, and does not go back; the title block's drawing number and revision are the file name's.
- **V2**, the title block: found, its required fields there, the revision, scale and date written as such, the date not in the future, the values the register or the settings expect. A page without text (a scan) cannot be read.
- **V3**, an empty sheet, or the drawing shifted off the page when it was printed.
- **V4**, a placeholder: a sheet holding little more than a cross.
- **V5**, another project's sheet: its title block names another project, project code or number, or a keyword of another project is on the page.

After **Submit for approval**, Manage → Issue… waits for the checks and shows what they found. In **Submissions**, a submission shows **Checks:** and the counts in the queue and, opened, the checks beside the gate's issues and the report: each file, what is wrong with the whole file, then its errors, warnings and notes, each with its page. **page n** shows the page with every finding drawn over it as a box tagged with its code; point at a box for what it says; **−** and **+** zoom. A DXF is shown as its page with the title block the server read, not its drawing.

When the checks find errors, fix the file and submit again; or, when the register or the check settings were wrong, correct them and click **Run checks again** (the author or an approver, while the submission waits): the submission is checked again with them. An owner can still approve with **Approve despite the errors** and a note, as for open issues.

**Rules → Checks on documents** (owners change them; the others read them):

- **Naming rule**: a regular expression the whole code must match, such as `(?<project>EL24)-[A-Z]{3}-L2-[A-Z0-9]{2}-[A-Z]{2}-[A-Z]-\d{4}` (the group `project` is the code's project field). **Try a code** says at once whether a code matches.
- **Project code**, **Project number**, **Project name**: what V5 compares with; **Another project's keywords**, one per line (an earlier project's code or place).
- **Expected values**: `field = value` lines every sheet's title block must show (the register's columns win over them).
- **A date in the future after** so many days; **Checks run**: V1 to V5, each on or off.
- **Title block**: *Found by its labels* (the default: DRAWING NO, TITLE, REVISION, DATE, SCALE, PROJECT and their Romanian forms), or *Described here*: a fixed region in a corner of the page (its size and margins in mm) and the fields, each with its kind, the labels it is printed after, whether it is required, its zone in the region, and the DXF attributes that give it.

**Save the check settings** sends them; a value the server does not accept is said next to its field. Activity shows each run of the checks with what it found.

## IDS on the server

A server that offers it (Atrium Server from W6b, such as cde.lapinbim.com) also checks every submission's model against the project's **IDS files** (buildingSMART IDS 1.0, see [IDS checks](ids.md)): the IFC copy Lapin saves with each commit, and each `.ifc` or `.ifczip` file the submission issues.

**Publishing → IDS on the server** lists the project's IDS files with their title, version, IFC versions, how many specifications they hold, who uploaded them and when. Owners click **Add an IDS file…** (a file of the same name replaces the one there) and the trash button to delete one; the others read them. The download button keeps a copy; the list button opens the file's specifications: what each applies to and what it requires. A file that is not IDS 1.0 is refused, with every problem the server found and its line.

With a project open (File → Collaborate…), owners also have **Put this project's IDS files on the server**: it lists the IDS files kept in the project (Manage → IDS…), each **New** on the server, **The same** as one there, or **Changed** (a file of that name on the server with other content). Tick the ones to send and click **Copy the ticked files**; replacing a changed one is confirmed first. Each file goes on its own: one the server refuses shows its problems beside it, and the others are still sent. The reverse, **Bring server IDS files into this project**, is in Manage → IDS… (editors and owners).

**Rules** has two switches (owners change them): **IDS files run on every submission**, and **A failing IDS specification stops the approval** (off, a failing specification is a warning, not an error).

A submission's checks show an **IDS** group after the documents: how many specifications pass and fail, then each IDS file over each model, specification by specification, **Pass**, **Fail** or **Skipped** (written for another IFC version). A failing one shows what it requires, the failing elements (GlobalId, class and name, the requirement, what the element has and what is expected; the first 50, and how many more), and **Failing GlobalIds**, with a button that copies them. Notes and warnings say when the commit has no IFC copy on the server, or a model could not be read, was too large, or took too long.

Before submitting, **Manage → Issue… → On the CDE server → Check on the server** (editors and owners) saves the project to the server and runs its IDS files there on that commit, showing the same report; nothing is submitted. It is the server's own check, the one the approval counts; the project's own IDS files (Manage → IDS…) still run here before Submit for approval as they did.

## Administration

A server administrator sees **Administration** (on the start screen in the browser; **File → Collaborate… → Administration…** on the desktop):

- **Accounts**: every account with its projects. **Create account** makes one from an e-mail address and a name; the server makes its first password and shows it **once**: copy it and give it to the person. **Disable** ends someone's sign-ins and refuses new ones (their projects, saves and issues stay); **Enable** lets them back. The key button sets a new password, shown once. **Administrator** gives or takes the administration (you cannot take your own, nor disable yourself).
- **New project**: a project made on the server, with you as its owner. Open its page to add the members.
- **E-mail outbox**: the mails the server writes (a submission to its approvers, its approval or rejection to its author, an issue to its new assignee), with their status. Sending is off until the server's administrator sets its SMTP settings; until then the mails wait in the outbox, and nothing is lost.

The browser keeps nothing of the passwords: they are shown once and gone.
