# Lapin (lapin-pro)

Lapin rewritten without ifc-lite: a classic BIM modeller on the IFC schema, desktop first, MIT.
Private repo `Ciuntuc-BIM-Studio/lapin-pro`. The user writes in Romanian; the UI, code, comments and commit messages are in English.

## Where things are

- `engine/`: Lapin's C# engine (.NET 10), `engine/Lapin.slnx`.
  - `src/Model/Lapin.Model`: IFC entities and objectified relationships, transactions (one undo step each), GlobalIds, the layer rules (axis on the core).
  - `src/Platform/Lapin.Platform`: the module contract and loader, the built-in `core` module (spatial structure, materials, rename/delete), the session and its diffs, geometry.
  - `src/Platform/Lapin.Package`: the `.lapin` package, format 3 (zip, SHA-256 parts, Arrow tables per module × class × storey).
  - `src/Modules/<Group>/Lapin.<X>`: an engine module with its `module.json` (`requires` lists the modules it needs). Built into `engine/artifacts/modules/<id>/`.
  - `src/Host/Lapin.Host`: the WebSocket JSON-RPC host (BubbleBIM Pro's protocol); prints `LAPIN_HOST_READY {"port":…}`.
  - `tests/Lapin.Engine.Tests`: xUnit, including the tests ported from the fork.
- `ui/`: React 18 + three.js + zustand + lucide, run with Vite on port 5180.
  - `ui/src/shell/`: the ribbon, project browser, properties panel and start screen.
  - `ui/src/modules/<id>/`: the UI modules (core, architecture, structure, drafting, phases, cost). The contract is in `ui/src/platform/module.ts`.
  - `ui/src/engine/session.ts`: the WebSocket JSON-RPC client.
- `products/<id>.json`: an edition, a list of modules. `scripts/product.mjs` completes it with what the modules require; dev, the UI build and `build:product` all use it, so a package holds only the product's modules (engine and UI).
- `desktop/src-tauri`: the Tauri shell.
- `engine/src/Geometry`: Lapin.Geometry (the B-rep kernel) and Lapin.Booleans (manifold's C API).
  - Taken from the user's private BubbleBIM Pro engine on 25.10.2026 (it was the submodule `vendor/bubblebim-pro`); Lapin's own MIT code since, so the repo needs no other repository to build.
  - `engine/native/<rid>/`: manifold's native library. macOS's is committed; `node scripts/build-manifold.mjs` builds it (CI builds Linux's).
  - Lapin does not use BubbleBIM Pro's graph model. Its platform patterns (module folders, session, undo, protocol, product packaging) were copied and adapted; code from BubbleBIM Pro and Atrium (`~/Documents/GitHub/Atrium`, MIT-0) may be reused.
- `docs/decisions.md`: the register of decisions taken, with their sources.
- `docs/plan.md`: decisions, the architecture and milestones M0–M9.
- `docs/format-lapin.md`: the `.lapin` package, format 3: the analysis, and what is done.
- `docs/transfer.md`: what moves from the old Lapin, and how.
- `docs/mock/`: screenshots of the UI mock.

The old Lapin (ifc-lite fork, MPL-2.0) lives in `~/Documents/GitHub/Lapin/ifc-lite`, on branch `lapin`. A full backup is in `~/Documents/GitHub/_backups/lapin-2026-10-23/`.

## Run

```sh
pnpm setup                     # packages, engine build
pnpm dev                       # engine ws://127.0.0.1:47810/ws + UI http://localhost:5180 (LAPIN_PRODUCT=<id>, default lapin; LAPIN_ENGINE_CONFIG=Release for timing a big model as a build would)
pnpm dev:desktop               # the same in the Tauri window
pnpm typecheck && pnpm test    # UI (vitest) and engine (xUnit); pnpm test:engine for the engine alone
pnpm products                  # what every product resolves to
pnpm build:product <id>        # one edition as a desktop app → build/<id>/bundle (check:licenses first)
pnpm build:web <id>            # the same edition for the browser (engine in a Web Worker) → build/<id>/web; needs the wasm-tools workload (engine/src/Host/Lapin.Browser/README.md)
pnpm docs:user                 # the user guide (docs/user/*.md, Romanian in docs/user/ro) with its pictures retaken in both languages by scripts/user-docs/scenes.mjs → build/user-docs
pnpm docs:user --full --jobs 4 # every scene again (default: only the scenes whose inputs changed; a full run by itself with no state or after 7 days), N at once
```

The user guide (https://lapin.ciuntucbimstudio.ro/docs/, Romanian at /docs/ro/; ? → User guide opens the interface's language; linked from ciuntucbimstudio.ro) is published by `deploy/web/deploy.sh` after each web build: the app goes live first, then the guide (`--app-only`, `--docs-only`, `--full`, `--jobs N`, `--dry-run`; a failing guide leaves the app published). The guide's runner (`scripts/user-docs/run.mjs`) takes scenes in parallel workers (each its own engine, Vite and Chrome; by default min(4, cores/2.5)) and only those whose inputs changed (`incremental.mjs`: the scene's code and helpers, the pages showing its pictures, the modules it drives or names in `touches`, and shared code — ui/ and engine/ outside the modules — which takes every scene again), state in `build/user-docs-cache/<product>.json`; it prints what it takes and why. A change users see (a tool, a dialog, a menu) updates its page in `docs/user` and `docs/user/ro`, and its scene in `scripts/user-docs/scenes.mjs`; a scene that no longer finds what it drives fails the run, so the guide never shows an interface that is gone. The video tutorials are on YouTube and listed in `docs/user/videos/videos.json` (id, YouTube id, length, titles in both languages) with their posters beside it; a page shows one with `<!-- video <id> -->`, which plays from youtube-nocookie.com only when clicked, and the Videos page shows them all (`<!-- videos -->`).

Linux (.deb, .AppImage) builds run in GitHub Actions: Actions → Build Linux → Run workflow, or a tag `v*`; the packages are the run's artifacts. Without Actions, `scripts/build-linux-docker.sh [product] [ref]` runs the same steps in an Ubuntu 22.04 x64 container (Docker: OrbStack on the Mac) → `build/<product>-linux/`. Windows builds are made on the Mac with `pnpm build:product lapin --target windows` (cross-compiled: cargo-xwin, NSIS, the committed `engine/native/win-x64/manifoldc.dll`; docs/release-windows.md), or by hand on a Windows machine (`pnpm build:product lapin` from a Visual Studio x64 developer prompt; manifold built with MSVC by `node scripts/build-manifold.mjs`); either way tested on Windows and uploaded to the site with `ciuntucbimstudio-site/deploy/upload-downloads.sh` (the installer and a portable zip); the Windows workflow was removed on 26.10.2026. macOS is signed and notarized locally.

Updates: the desktop app looks for a newer build of its edition at start and from Help → Check for updates (Tauri's updater; `ui/src/app/updates.ts`). A build's version is `0.1.<commit count>`. `node scripts/publish-update.mjs <product> <folders…>` signs the update packages with the update key (`~/.tauri/lapin-updater.key`, its password in the keychain item `lapin-updater`; never in the repo) and uploads the downloads and the per-platform manifests (`ciuntucbimstudio.ro/downloads/<product>-update/<target>-<arch>-<kind>.json`). The DMG, the installer and the AppImage update themselves; the .deb asks for the password (pkexec); the portable zip (its exe unmarked by a bundler, so it asks for `windows-x86_64-unknown.json`, the installer's manifest published under that name too) installs the installer's copy. Every published build is also archived, never served: the site's `deploy/archive-builds.sh` (called by `publish-update.mjs`; `LAPIN_NO_ARCHIVE=1` skips it) keeps each version's packages and update signatures in the server's `archive/<product>/<version>/` with `SHA256SUMS.txt` and `BUILD.txt` (version, commit); 0.1.227–0.1.467 hold their update packages only.

If pnpm complains about @tauri-apps when installing, use `CI=true pnpm install`. `.npmrc` hoists `@tauri-apps/*`. `pnpm check:licenses` checks the UI's npm packages and the engine's NuGet packages.

## Rules

- **MIT stays clean.**
  - No ifc-lite code or dependency, and no file from the fork that was modified upstream code (those stay MPL).
  - Only files created in the fork may be ported (list: `~/Documents/GitHub/Lapin/docs/lapin-own-files.txt`). Rewrite them rather than copy them.
  - The ifc-lite parts, wall joins and the drawing-2d generator, are reimplemented from the behaviour specified in `docs/transfer.md`.
  - Production dependencies must be permissive; `pnpm check:licenses` enforces it.
- **Lapin is IFC-native.** Its model is IfcWall, types, IfcMaterialLayerSetUsage and rels, held by a Lapin C# engine.
  - Interop with BubbleBIM goes through IFC carrying design intent: Axis reps, layer sets, the `Lapin_Intent` property set, and stable GlobalIds. Lapin's own property sets never start with `Pset_` (IFC keeps that prefix for its standard sets).
- **Port tests before code.** The old repo's tests listed in `docs/transfer.md` are the specification, ported with the same cases and numbers.
- **Commits** never carry a Claude co-author or a "Generated with Claude Code" line. Add files by explicit path.
- **Privacy.** The Bubbles plugins never go public. Never ask the user to paste passwords or tokens in chat.

## Models for subagents

Subagents default to Sonnet. Every code change in `engine/`, geometry, licence and architecture decisions: `model: "opus"`. Interface code, docs, translations, guide scenes, running tests: Sonnet. Searches, lists, renames: `model: "haiku"`, never for translations. When unsure whether a task touches `engine/`, use Opus. A few tool calls are done inline, not delegated.

## Standing principles

- An IFC import reproduces the file: nothing of Lapin's own is added to recognised elements (lintels, reveals, automatic joins), and what differs from the file by more than 1 mm or 0.5 % stays a reference (`RecognitionFidelity`, enforced). An existing IFC file is never written to.
- One rule for every curve: `Lapin.Geometry/Chords`. Coarser copies are for the screen only; the model, drawings, exports, quantities and picking use the parts themselves.
- Drawings are made by the engine, so sheets and the PDF print what the view shows. One resolver decides what a view shows (`VisibilityGraphics`) for drawings, sheets, 3D and mesh exports.
- A configurator's preview is the engine's, built by the same code as the element.
- GlobalIds stay stable: what is rebuilt or regenerated keeps its ids.
- What can be worked out is derived on every ask, not stored (the critical path, a task's elements, the estimate).
- A project saved by an older build is brought up to date by an `IProjectUpgrade` when it opens.
- Each command is one undo step. A command a guest of a shared session may run is listed in `EngineDispatcher.AllowedRemote`.
- Exchange formats are written after their public specifications, with no dependency.
- Norms and prices are the user's own; anything Lapin ships is marked indicative.
- The open repo holds hooks only; premium code stays in its private repo and open modules never depend on it.
- New interface strings go in all the languages of `ui/src/i18n`.

## What exists, and where to read about it

The history of what was built, decision by decision, is in `docs/history.md`; the register is `docs/decisions-pending.md` (both are long: search them, never read them whole). Add a finished piece of work as a paragraph at the end of `docs/history.md` and a row in `docs/decisions-pending.md`, not here.

- Walls, joins, layers, seams, view styles: `docs/view-styles.md`, `docs/plan-direct-editing.md`
- Doors, windows, views, sheets: `docs/plan-views-and-joinery.md`, `docs/plan-title-blocks.md`
- Sections, site plans, rooms: `docs/sections.md`, `docs/site-plans.md`, `docs/rooms.md`, `docs/spec-site.md`
- Visibility / Graphics, point clouds: `docs/plan-visibility-graphics.md`
- Stairs, terrain, CAD drafting: `docs/spec-stairs.md`, `docs/spec-terrain.md`, `docs/spec-drafting-cad.md`
- Studio (solids, sketches, thick solids): `docs/spec-studio.md`, `docs/plan-studio-editor.md`, `docs/plan-studio-sketch.md`, `docs/plan-studio-thick.md`, `docs/plan-3d-modelling.md`
- Groups, components, links, versions: `docs/groups.md`, `docs/components.md`, `docs/links.md`, `docs/versions.md`
- IFC (import, recognition, the fidelity gate, export): `docs/spec-ifc.md`, `docs/ifc-optimize.md`
- Other formats: `docs/dxf-import.md`, `docs/fragments.md`, `docs/bos.md`, `docs/sdnf.md`, `docs/plan-mesh-export.md`, `docs/html-export.md`
- Large models, display: `docs/plan-instancing.md`, `docs/plan-display-precision.md`
- Browser engine, multiplayer, assistant, MCP: `docs/plan-browser-engine.md`, `docs/plan-multiplayer.md`, `docs/plan-assistant.md`, `docs/mcp.md`
- CDE, documents, tags: `docs/plan-cde.md`, `docs/plan-documents.md`, `docs/plan-buildup-tags.md`
- 4D and 5D (`planning`, `cost`): `docs/plan-4d5d.md`
- Roofs, timber, steel, rebar, site works: `docs/plan-roof-framing.md`, `docs/plan-wood.md`, `docs/plan-steel.md`, `docs/plan-rebar.md`, `docs/plan-site-works.md`
- MEP, furniture, elevators: `docs/plan-mep.md`, `docs/plan-furniture.md`, `docs/plan-elevators.md`
- Urban: `docs/plan-urban.md`
- Apartment types: `docs/plan-units.md`
- Editions' override points: `docs/overrides.md`

Premium modules live in private sibling repos, each with its `products/<id>.json` and `"external"`: `../lapin-urban` (`urban`), `../lapin-mep` (`mep-pro`), `../lapin-rail` (`rail`), `../lapin-planning` (`planning-pro`). The BubbleBIM editions stay in private repos' `products/`, never here.

## Next step

The user reviews `docs/decisions-pending.md` (#1–111). Open items needing the user: signing and notarization, Windows (manifold), and what the review changes. Milestones are in `docs/plan.md`.
