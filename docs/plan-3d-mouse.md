# 3Dconnexion SpaceMouse support: plan

Asked on 25.11.2026 in forum topic 261 (Slawomir: has any work been done on integrating 3Dconnexion devices such as the SpaceMouse). Follows decision 219 (31.10.2026, a tester's report), which analysed the wheel emulation.

**Order: 6 of 6** (angle lock, structural usage, level datums, leaning walls, national packs, 3D mouse). Only S0 now; S1 waits for a confirmed tester.
**Status: S0 done (26.11.2026); S1 waiting for a tester with a device; S2 and S3 proposed.**

## What exists today

- 3DxWare only emulates the wheel on push/pull; `zoom.ts` `clampWheel` already tames that (decision 219).
- The camera can be driven from code: `CameraApi` in `ui/src/viewport/camera.ts` (`orbitTo`, `lookAt`, `moveTo`, `fit`). `Viewport3D.tsx` uses OrbitControls plus `navigate3d.ts`, the navigation styles in `app/navStyle.ts`, and walk mode in `walk.ts`. What is missing is the input.
- The desktop shell (`desktop/src-tauri`) has no HID dependency. Its webviews are WebKit on macOS and Linux (no WebHID) and WebView2 on Windows (WebHID exists, but its device chooser and permission flow are unverified: a risk to test). The browser build has WebHID only in Chrome and Edge.

## Options

- **A. WebHID in the UI.** Pure TypeScript, no dependency. Chrome and Edge, maybe WebView2.
- **B. The `hidapi` crate in Rust** (MIT; the C hidapi it wraps has a BSD-style option), events sent to the UI; every desktop OS. Costs: macOS may open the device exclusively and may need an Input Monitoring prompt; Linux needs a udev rule shipped in the .deb (an AppImage user installs it by hand).
- **B'. On Linux, the spacenavd socket** (the libspnav protocol, BSD). Clean.
- **C. The 3Dconnexion SDK** (navlib or 3DconnexionJS): proprietary, revocable, attribution and non-compete clauses; never in the MIT repo. At most a separate plugin; not planned.
- **D. Our own client for the driver's WAMP protocol:** undocumented and reverse-engineered, fragile; rejected.
- **E. The driver's own mapping:** users bind buttons to Lapin's shortcuts (Fit, the standard views). Free and works today, but no orbit.

## Licence-safe sources

Read the device through its own HID report descriptor, which follows the public USB HID spec (Generic Desktop page, the Multi-axis Controller usage, axes X/Y/Z/Rx/Ry/Rz, the Button page). Report IDs come from that descriptor. Vendor IDs (0x046d, 0x256f) are facts. Never copy GPL code (spacenavd, the Linux kernel driver). Community specs may be read as documentation; libspnav is BSD.

## Design (source-agnostic)

A device-agnostic `ui/src/viewport/spaceNav.ts`: six axes mapped to orbit about the pivot, pan and zoom (pan and zoom only in plan and 2D views); a dead zone, sensitivity, axis invert and swap, a dominant-axis mode; buttons mapped to Fit, Top, Front, Iso and Walk; while the device is active the emulated wheel is ignored, so zoom is not doubled. **Default pivot: the point under the cursor**, as the wheel already does. Settings under Settings → Navigation. The source is WebHID wherever `navigator.hid` exists; the Rust and spacenavd sources later feed the same mapping.

No engine, IFC, drawing or quantity changes; no `IProjectUpgrade`.

## Tests first

Pure unit tests on synthetic descriptors and reports (parsing, dead zone, pose deltas) and the wheel suppression. No hardware in CI.

## Stages

| Stage | Content | Effort |
|---|---|---|
| S0 | **Done 26.11.2026.** A guide note on mapping 3DxWare buttons to Lapin's shortcuts; the forum question below is drafted, not posted | S |
| S1 | WebHID source plus the mapping, settings, buttons. Web only, plus a WebView2 trial. Only once a tester with a device confirms Chrome or Edge on Windows (about 1–2 days, Sonnet). **Waiting for a tester.** | M |
| S2 | `hidapi` in Tauri for every desktop OS (macOS exclusive open, the udev rule in the .deb), spacenavd on Linux | M |
| S3 | Walk mode, the Studio editors, orbit about the selection | S |

Guide: a section in `docs/user/views.md` (EN and RO); the scene shows only the settings (no hardware); strings in all languages.

## What to ask the tester (any SpaceMouse: Compact, Wireless, Pro or Enterprise)

- Windows plus Chrome or Edge on the web build, once with 3DxWare running and once with it stopped.
- The Windows desktop app (WebView2).
- Orbit, pan, zoom, no doubled zoom, the buttons.
- Report the model, OS and driver version.

## Risks

A small audience and no device of our own; the WebView2 chooser and permission flow; macOS exclusive open; the udev rule for AppImage users.

## The user's answers (25.11.2026)

The user accepted every recommendation:

1. Do this at all: S0 now, S1 only once a tester is confirmed. **Yes.**
2. S1 on the web only, plus a WebView2 trial. **Yes.**
3. Accept the Linux udev rule in the .deb, in S2. **Yes.**
4. The default orbit pivot is the point under the cursor. **Yes.**
5. A 3Dconnexion-SDK plugin outside the repo: no, unless the HID route fails on macOS. **Yes.**

## S0, done (26.11.2026)

The guide has a section, "A 3D mouse (3Dconnexion SpaceMouse)", in `docs/user/views.md` and `docs/user/ro/views.md`, under 3D views. It says only what the repo shows: push and pull arrive as wheel turns (tamed by `clampWheel`); the keys Lapin has for the view are <kbd>+</kbd>, <kbd>-</kbd>, <kbd>Shift</kbd> <kbd>Z</kbd> and the typed letters ZE (Fit); Top, Front and the isometrics have no key yet (the PLAN button and the key plan's compass stops); Walk starts from a room's right-click menu; orbit, pan and the device's own settings are not supported. No scene needs changing (text only).

## The forum question (to post by the user; not posted)

> Thanks for asking, Slawomir. Lapin does not read 3Dconnexion devices yet: today the 3DxWare driver only sends the push/pull as mouse-wheel zoom, so there is no orbit or pan from the device. I added a short note to the user guide (3D views, "A 3D mouse") on what you can map in the driver meanwhile.
>
> I would like to add real support (orbit, pan, zoom and buttons, read through the device's standard HID reports, with no 3Dconnexion SDK), but I have no SpaceMouse to test with. Would you be willing to try a test build? If so, please tell me:
> 1. your operating system and version (Windows, macOS or Linux);
> 2. the model (Compact, Wireless, Pro, Enterprise...);
> 3. the 3DxWare driver version, or whether you run without the driver;
> 4. whether you use the desktop app or the web version, and which browser.
>
> The first test would be the web version in Chrome or Edge on Windows, then the desktop app.
