# Lapin (lapin-pro)

Lapin rewritten without ifc-lite: a classic BIM modeller on the IFC schema, desktop first, MIT.
Private repo `Ciuntuc-BIM-Studio/lapin-pro`. The user writes in Romanian; the UI, code, comments and commit messages are in English.

## Where things are

- `ui/`: React 18 + three.js + zustand + lucide, run with Vite on port 5180.
  - `ui/src/shell/`: the ribbon, project browser, properties panel and start screen.
  - `ui/src/modules/<id>/`: the UI modules (core, architecture, structure, drafting, phases, cost). The contract is in `ui/src/platform/module.ts`.
  - `ui/src/engine/session.ts`: the WebSocket JSON-RPC client.
- `products/<id>.json`: an edition. `modules` lists the UI modules and `engine` the engine modules; a build bundles only those (`LAPIN_PRODUCT=<id>`).
- `desktop/src-tauri`: the Tauri shell.
- `vendor/bubblebim-pro`: a submodule with the user's private C# engine.
  - Lapin uses it only as a geometry library: Platform.Geometry (B-rep), Platform.Booleans (manifold), Step.cs/StepFile.cs and IfcModel.cs.
  - Lapin does not use its graph model.
- `docs/plan.md`: decisions, the architecture and milestones M0–M7.
- `docs/transfer.md`: what moves from the old Lapin, and how.
- `docs/mock/`: screenshots of the UI mock.

The old Lapin (ifc-lite fork, MPL-2.0) lives in `~/Documents/GitHub/Lapin/ifc-lite`, on branch `lapin`. A full backup is in `~/Documents/GitHub/_backups/lapin-2026-10-23/`.

## Run

```sh
pnpm setup        # submodule, packages, engine build
pnpm dev          # engine ws://127.0.0.1:47810/ws + UI http://localhost:5180
pnpm dev:desktop  # the same in the Tauri window
pnpm typecheck && pnpm test
pnpm build        # runs check:licenses first
```

If pnpm complains about @tauri-apps when installing, use `CI=true pnpm install`. `.npmrc` hoists `@tauri-apps/*` for the vendor platform code.

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

Milestone M0b: the Lapin engine on IFC (`Lapin.Ifc.Model`, `Lapin.Authoring`) with the layered wall type: layer set, usage, axis on the core and flip. After that comes the M2 walls work (joins, mitre, zones, lintels, cut priorities). The order is in `docs/transfer.md`.
