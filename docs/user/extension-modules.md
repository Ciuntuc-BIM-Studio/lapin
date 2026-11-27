---
title: Extension modules
order: 126.5
summary: Install an extension module from Lapin Extensions in the desktop app: what it may do, its licence and licence agreement, the restart, turning it off, updating and removing it, and why Lapin may refuse to load one.
---

## What an extension module is

An **extension module** adds tools to Lapin itself, not only content to your libraries: tabs and buttons in the ribbon, views, dialogs and commands, with the code behind them. A module has an engine part (compiled .NET code), an interface part (a plug-in), or both. Content packs (furniture, types, hatches…) are only read by Lapin; a module **runs**.

A module can therefore do on your computer **everything you can**: read and change your files, use the network, start programs. Lapin says so wherever it shows one: **Runs code with full access**. That is why modules are handled differently from everything else in the catalogue:

- only authors the catalogue's maintainer lists may publish one, and its **full source** is read by a reviewer and approved before it is listed;
- the catalogue **rebuilds its package from that source** and lists it only if the result is the same, byte for byte, as the package submitted;
- the catalogue **signs** it, and Lapin checks the signature and **every file** of an installed module each time it starts.

Modules run in the **desktop app only**. Lapin in the browser shows them in the catalogue but offers no **Install**.

## Before you install

Open **File → Extensions…**, find the module in **Browse** and click it. Its details say, besides the author, the licence and the Lapin versions it works with:

- that *an extension module runs code with everything you can do on this computer*, that Lapin Extensions reviewed its source and signed it, and that it is desktop only;
- **It asks to**: what it does with that access, as its author declares it (change the open project, reach the internet…);
- what it holds, when its author lists it.

Its page on the store, [store.lapinbim.com](https://store.lapinbim.com), says the same in more words, the modules of Lapin it needs (an edition without them cannot load it), the source commit its package is built from, its licence text and its **licence agreement**. Downloading the file there asks you to accept that agreement first; you do not need the file, Lapin downloads it for you.

## Install

1. Click **Install**. Lapin downloads the package and checks its size, its SHA-256 and the catalogue's signature; a file changed on the way is refused and nothing is installed.
2. Lapin shows **Runs code with full access** with the module's licence: **OK** goes on, **Cancel** stops.
3. When the module has a **licence agreement**, Lapin shows it next, with its version: accept it to install. If you cancel either, the module is not installed (*Not installed: the terms were not accepted.*).
4. **Installed** now lists it with *Loads when Lapin restarts.*, and the top of the list says *Changes to extension modules take effect when Lapin restarts.* Click **Restart now**: Lapin saves your work (or asks), stops and starts again.

After the restart, the module's tools are where its author put them (its own group in a ribbon tab, a pane, a dialog). Lapin keeps the version of the agreement you accepted; an update whose agreement has a new version asks you again.

## Installed: on, off, update, uninstall

Each module in **Installed** shows **Runs code with full access**, an **On** switch and what this start made of it. Every change takes effect at the next start, and **Restart now** appears as soon as one waits:

- **On** switched off: *Turns off when Lapin restarts.*, then *Off.* The module stays installed and is not loaded. Switch it on again the same way.
- **Update**, when the catalogue has a newer version: the new version is downloaded and checked beside the one running (*The new version loads when Lapin restarts:* with its number); the old one is removed at the start after.
- **Uninstall**: *Removed when Lapin restarts.* Its files are deleted at the next start, before anything is loaded.

A project in which you used a module still opens without it, in an edition without it or after you uninstall it: Lapin keeps what the module made in the project as it is, and saves it back unchanged, so nothing is lost when the module is installed again.

## When Lapin does not load a module

Lapin checks each installed module at every start, and one that fails a check is not loaded: Lapin starts without it, and **Installed** says *Not loaded:* with the reason. The usual reasons:

| What it says | What to do |
|---|---|
| a file of it *was changed*, or *is missing* | install it again (uninstall, restart, install) |
| it *needs* a module *which this edition does not have* | use an edition of Lapin with that module (Planning and Cost, for a planning module) |
| it was made for a newer Lapin | update Lapin (Help → Check for updates) |
| *It could not be loaded* or *It could not start*, with an error | it was built against a version of Lapin's engine that has changed: wait for its update, or tell its author |

When its interface part cannot run (it needs a newer Lapin, or it fails while it loads), Lapin starts without that part, says *Extensions: their interfaces were not loaded:* with their names, and **Installed** shows *Its interface was left out:* with why.

A version that Lapin Extensions withdraws after publishing it is no longer offered in the catalogue.

If you open a project that holds the data of an extension you have not installed (or have turned off), Lapin says so once: *This project uses … (extension), which is not installed. Its data is kept and untouched.* Its data stays in the project and is saved back as it was; **Open Extensions** takes you to the catalogue (or to **Installed**, when you only turned it off) to install or turn it on. Close the notice with ✕.

See also [Extensions](extensions.md) for the catalogue, content packs, scripts and paid extensions.
