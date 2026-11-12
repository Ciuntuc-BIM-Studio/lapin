---
title: Extensions
order: 126
summary: Add furniture, project templates, profiles, hatch patterns, title blocks and price lists made by others from the Lapin Extensions catalogue, and remove them again.
---

## The catalogue

**Lapin Extensions** is a free catalogue of things made for Lapin by its users and by us: furniture packs, project templates, profile libraries, hatch patterns, title blocks and price lists. Each one goes into the library Lapin already has for it, so you use it as if you had made it yourself.

Open **File → Extensions…**. **Browse** lists the catalogue: search it by name, tag or what an extension holds, or pick a category.

![Extensions: Browse, a furniture pack's details](shots/extensions-browse.webp)

Click an extension to see its details before you install it:

- **Author** and **Licence**. Every extension has a licence, chosen by its author. A *permissive* one (MIT, CC-BY, CC0…) lets you use what it holds in any project; for another, *read its terms* first.
- **Works with**: the Lapin versions it was made for. One that does not fit your Lapin says so instead of offering **Install**.
- **Size**, the file, and its **SHA-256**: the fingerprint of the file the catalogue reviewed.
- **What it holds**: its items, when its author lists them.
- **Homepage** and **Forum topic**: where to ask its author.

## Install, update, uninstall

Click **Install**. Lapin downloads the file, checks that its size and SHA-256 are exactly those the catalogue lists (a file changed on the way is refused, and nothing is installed), then takes it into its library:

| Kind | Where it goes |
|---|---|
| Furniture | **Interior → My furniture…** |
| Project templates | **File → New from template…** |
| Profiles | the profile libraries on this computer |
| Hatch patterns | My patterns, offered by **Manage → Materials** |
| Title blocks | the title blocks on this computer |
| Price lists | **Construction → Price lists…**, marked indicative: the prices are the author's, check them against your own |

If your library already has something of the same name, Lapin asks before replacing it. Nothing an extension brings is ever run: extensions are content.

**Installed** lists what this computer has. Each row opens where it went (**My furniture…**, **Project templates…**), offers **Update** when the catalogue has a newer version, and **Uninstall**.

![Installed: the furniture pack, with the button that opens My furniture](shots/extensions-installed.webp)

![The pack's furniture types in My furniture](shots/extensions-my-furniture.webp)

**Uninstall** takes away what the extension added to your libraries, and only that. What you already put in your projects stays in them: a furniture type inserted in a project, a project started from a template.

## Scripts

The catalogue also lists **Python scripts**. On the desktop you can install one now; it runs once Lapin has its Python console, and only when you run it. Before you install it, its details say what it asks for (to read or change the open project, to reach files or the internet). A script can do on your computer what you can, so install scripts from authors you trust. The browser takes content packs only.

![A script's details: what it asks for, and that it is not runnable yet](shots/extensions-script.webp)

## Another catalogue

**Catalogue…** at the bottom of the dialog sets the catalogue's address: an https address, or the path of an `index.json` on this computer or a shared folder (a firm's own catalogue). Leave it empty for Lapin's own. The arrow beside it reads the catalogue again.

## Publishing your own

Anyone can publish an extension: export it from Lapin (My furniture → Export, Save as template…, a profile library, a title block), attach the file to a release of a GitHub repository of yours, and add its manifest to the catalogue by a pull request on [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Its README says how, and what the automatic checks look at.
