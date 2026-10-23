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
- `vendor/bubblebim-pro`: a submodule with the user's private C# engine.
  - Lapin uses it only as a geometry library: Platform.Geometry (B-rep) now; Platform.Booleans (manifold), Step.cs/StepFile.cs and IfcModel.cs later.
  - Lapin does not use its graph model. Its platform patterns (module folders, session, undo, protocol, product packaging) were copied and adapted; code from BubbleBIM Pro and Atrium (`~/Documents/GitHub/Atrium`, MIT-0) may be reused.
- `docs/decisions.md`: the register of decisions taken, with their sources.
- `docs/plan.md`: decisions, the architecture and milestones M0–M9.
- `docs/format-lapin.md`: the `.lapin` package, format 3: the analysis, and what is done.
- `docs/transfer.md`: what moves from the old Lapin, and how.
- `docs/mock/`: screenshots of the UI mock.

The old Lapin (ifc-lite fork, MPL-2.0) lives in `~/Documents/GitHub/Lapin/ifc-lite`, on branch `lapin`. A full backup is in `~/Documents/GitHub/_backups/lapin-2026-10-23/`.

## Run

```sh
pnpm setup                     # submodule, packages, engine build
pnpm dev                       # engine ws://127.0.0.1:47810/ws + UI http://localhost:5180 (LAPIN_PRODUCT=<id>, default lapin)
pnpm dev:desktop               # the same in the Tauri window
pnpm typecheck && pnpm test    # UI (vitest) and engine (xUnit); pnpm test:engine for the engine alone
pnpm products                  # what every product resolves to
pnpm build:product <id>        # one edition as a desktop app → build/<id>/bundle (check:licenses first)
```

If pnpm complains about @tauri-apps when installing, use `CI=true pnpm install`. `.npmrc` hoists `@tauri-apps/*` for the vendor platform code. `pnpm check:licenses` checks the UI's npm packages and the engine's NuGet packages.

## Rules

- **MIT stays clean.**
  - No ifc-lite code or dependency, and no file from the fork that was modified upstream code (those stay MPL).
  - Only files created in the fork may be ported (list: `~/Documents/GitHub/Lapin/docs/lapin-own-files.txt`). Rewrite them rather than copy them.
  - The ifc-lite parts, wall joins and the drawing-2d generator, are reimplemented from the behaviour specified in `docs/transfer.md`.
  - Production dependencies must be permissive; `pnpm check:licenses` enforces it.
- **Lapin is IFC-native.** Its model is IfcWall, types, IfcMaterialLayerSetUsage and rels, held by a Lapin C# engine.
  - Interop with BubbleBIM goes through IFC carrying design intent: Axis reps, layer sets, `Pset_BubbleBIM_Intent`, and stable GlobalIds.
- **Port tests before code.** The old repo's tests listed in `docs/transfer.md` are the specification, ported with the same cases and numbers.
- **Commits** never carry a Claude co-author or a "Generated with Claude Code" line. Add files by explicit path.
- **Privacy.** The Bubbles plugins never go public. Never ask the user to paste passwords or tokens in chat.

## Next step

M1 is done (2026-10-24): the engine, the layered walls, `.lapin` format 3, products, binary meshes, the recovery journal, and the UI wired to the engine (plan view, 3D, selection, inspector, wall types, File menu, recovery).

The user approved M1–M7 plus roofs with walls extended under the roof, to reach a PoC at the old Lapin's level. Decisions taken along the way go into `docs/decisions-pending.md` for review at the end.

Next comes M2, the walls: joins, mitre, zones, finish layers, lintels, cut priorities, foundations. The milestones are in `docs/plan.md`, and the transfer order is in `docs/transfer.md`.
