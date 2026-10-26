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
pnpm dev                       # engine ws://127.0.0.1:47810/ws + UI http://localhost:5180 (LAPIN_PRODUCT=<id>, default lapin)
pnpm dev:desktop               # the same in the Tauri window
pnpm typecheck && pnpm test    # UI (vitest) and engine (xUnit); pnpm test:engine for the engine alone
pnpm products                  # what every product resolves to
pnpm build:product <id>        # one edition as a desktop app → build/<id>/bundle (check:licenses first)
pnpm build:web <id>            # the same edition for the browser (engine in a Web Worker) → build/<id>/web; needs the wasm-tools workload (engine/src/Host/Lapin.Browser/README.md)
```

Linux (.deb, .AppImage) builds run in GitHub Actions: Actions → Build Linux → Run workflow, or a tag `v*`; the packages are the run's artifacts. Windows builds are made by hand on a Windows machine (`pnpm build:product lapin` from a Visual Studio x64 developer prompt; manifold built with MSVC by `node scripts/build-manifold.mjs`), tested there, and uploaded to the site with `ciuntucbimstudio-site/deploy/upload-downloads.sh` (the installer and a portable zip); the Windows workflow was removed on 26.10.2026. macOS is signed and notarized locally.

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

## Next step

M1–M7 and the roofs are done as a PoC (2026-10-24): layered walls with joins, zones and lintels; floors; structure; roofs with walls under them; doors and windows with their configurator; materials and drafting standards; the 2D drawings (plans, sections, elevations, annotations); sheets with PDF and DXF; joinery and lintel schedules; phases; old projects (format 2) open; the macOS build. A second round added CAD drafting (command line, snaps, ortho, tracking, layers), the `studio` module (solids from sketches, live booleans), the `civil` module (terrain from survey points and LandXML, platforms), steel profiles, slab and roof openings, VV/graphics sets, a Romanian UI and an MCP bridge (`docs/mcp.md`). The old Lapin's issue list is checked in `docs/issues-old-lapin.md`. A third round (25.10.2026) added the IFC export, and the UX items #115–118: 2D zoom commands, panes (split) and detached view windows, Lapin's own tool icons (`ui/src/icons`), the key plan over the views. Then `docs/plan-direct-editing.md` (#119): grips on axes, outlines and sketches, live length and angle input, the spreadsheet-style properties panel with user parameters, CAD names and roles, closed/morphing/two-rail sweeps and lofts, seven new dimension kinds, the cut order in Manage, Studio classes and types, walls attached to roofs. Then (#121): curved walls and beams (an arc axis, joined by its tangent), grips in 3D, the cut order by material, type parameters inherited by elements, curtain walls with their configurator, flip arrows in plan, move to storey. Then (25.10.2026) the visual seams: where one material goes on in the same plane from one element into another (a facade over the storey above), no line is drawn between them, in 3D, elevations and sections (`Seams`, `SeamTests`); the elements stay apart. Then `docs/plan-views-and-joinery.md` (#125–127): the browser's sets and duplicating views, sheets, levels and 3D views; the joinery configurator's drawings from the symbol engine, used by plans and elevations; reveals, sills, surrounds and thresholds as parts of doors and windows.

Then the engine in the browser (#124, `docs/plan-browser-engine.md`, stages 1–4 done on 25.10.2026): the same engine AOT-compiled in a Web Worker, 6.8 MB, 2.4× native; the desktop unchanged. Live at https://lapin.ciuntucbimstudio.ro/web/ (`deploy/web/deploy.sh`).

Then (#129): foreign IFC recognised into native elements on import (`docs/spec-ifc.md`, Recunoașterea): `IIfcRecognizer` in the platform, bodies read as prisms, walls with axes, layers, openings, doors and windows, floors, columns, beams, pad footings; the rest as references drawn with real faces.

Next: the user reviews `docs/decisions-pending.md` (#1–111). Open items needing the user: signing and notarization, Windows (manifold), and what the review changes. Milestones are in `docs/plan.md`.
