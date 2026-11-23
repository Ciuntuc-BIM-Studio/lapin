---
title: Working on a CDE server
order: 126
summary: Sign in to your office's CDE server (Atrium Server, such as cde.lapinbim.com), clone a project, save your work to the server and get the others', keep the issues in step, and, as an administrator, manage the accounts and projects.
---

A CDE server keeps your office's projects in one place: every save with its author and note, the versions and issue sheets, the members of each project and their roles, and the issues (BCF topics) everyone answers. Lapin works on it from the desktop app and from the browser. The company's server is at **cde.lapinbim.com**; your office can run its own, Atrium Server, which is free.

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

A project's page shows the history of its main line (who saved, when, with what note, and the versions and issue sheets made from each save), the branches in progress, the **Activity**, the **Members** and the **Rules**.

On the desktop, File → Collaborate… shows the Activity and the Rules of the project the file is connected to.

**Activity** lists, newest first, the saves to the server, the versions and issue sheets, and every change to an issue: who created it, commented, changed its status, assigned it. **More** reads older entries.

**Members**: owners add someone who has an account on the server (by e-mail address), change a role, or remove a member. A project always keeps at least one owner.

**Rules** (owners change them; the others read them):

- who saves to main: editors and owners, or owners only;
- who makes issue sheets;
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

## Administration

A server administrator sees **Administration** (on the start screen in the browser; **File → Collaborate… → Administration…** on the desktop):

- **Accounts**: every account with its projects. **Create account** makes one from an e-mail address and a name; the server makes its first password and shows it **once**: copy it and give it to the person. **Disable** ends someone's sign-ins and refuses new ones (their projects, saves and issues stay); **Enable** lets them back. The key button sets a new password, shown once. **Administrator** gives or takes the administration (you cannot take your own, nor disable yourself).
- **New project**: a project made on the server, with you as its owner. Open its page to add the members.

The browser keeps nothing of the passwords: they are shown once and gone.
