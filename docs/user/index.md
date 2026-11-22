---
title: Lapin user guide
order: 0
summary: How to model, draw and publish a building with Lapin, step by step, with pictures taken from the app itself.
---

# Lapin user guide

Lapin is a BIM modeller for architects and engineers. You draw walls, floors, roofs, doors and windows in a plan. Lapin builds the 3D model from them, and draws the sections, elevations and sheets you print. Each element is a real IFC element, so the model goes to other BIM tools as it is.

Lapin runs in two places:

- **On your computer.** The desktop app for Windows, macOS and Linux is the fastest. It updates itself.
- **In the browser.** [lapinbim.com/app](https://lapinbim.com/app/) runs the same app without installing anything. It is a little slower.

The desktop app is downloaded from [lapinbim.com/download](https://lapinbim.com/download/); the same files are on [GitHub Releases](https://github.com/Ciuntuc-BIM-Studio/lapin/releases). On Linux, the `.deb` puts Lapin in the applications menu (under Graphics), with an icon your icon theme can replace, and opens `.lapin` projects with a double-click. The `.rpm` does the same on Fedora, RHEL and openSUSE. The AppImage carries the same menu entry for the tools that add AppImages to the menu (AppImageLauncher, Gear Lever).

Which Linux package to pick: Fedora, RHEL, openSUSE and their relatives, the `.rpm`; Ubuntu, Debian, Linux Mint and their relatives, the `.deb`; anything else, the AppImage. The `.deb` and the `.rpm` use the system's own WebKitGTK, which is newer and updated with the system; the AppImage carries an older WebKitGTK of its own, and the 3D view can be slower with it, notably on NVIDIA graphics under Wayland. The `.deb` and the `.rpm` ask for the administrator's password when Lapin updates itself; the AppImage updates itself without it.

![Lapin's workspace with a small house on the ground floor plan](shots/workspace.webp)

> The pictures in this guide are taken from Lapin itself each time a new version is published, so they show the app as it is today.

## Where to start

New to Lapin? Read [Getting started](getting-started.md), then [Walls](walls.md). The other pages can be read in any order. Prefer to watch? [Videos](videos.md) has a short tutorial for each part of Lapin.

<!-- pages -->

## Free, open source and helpware

Lapin is free and open source, under the MIT licence; its code is on [GitHub](https://github.com/Ciuntuc-BIM-Studio/lapin). It is also **helpware**: if Lapin helps you in any way, we encourage you, without obliging you, to help anyone who needs help, in any way you can: your time, your skills, a hand to someone nearby, a gift to people or a cause you trust. Lapin asks nothing for itself and takes no donations.

## When you are stuck

Click **Ask Me**, the rabbit at the top right of the app, or press <kbd>F1</kbd>, and type your question in your own words. If it has no answer, it sends your question to us. See [Help and feedback](help.md).
