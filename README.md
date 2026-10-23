# Lapin

BIM authoring for architects and engineers, on the **BubbleBIM Pro** geometry kernel.

Lapin is a desktop application and a classic modeller on the IFC schema. Its interface
(React, TypeScript, three.js) runs in a Tauri window and talks over a local WebSocket to
Lapin's engine (C#, .NET 10, `engine/`), which holds the IFC model and derives its geometry
with the BubbleBIM Pro geometry kernel.

> Private repository. Licensed under the MIT licence (see `LICENSE`).
> The BubbleBIM Pro engine (`vendor/bubblebim-pro`, a git submodule) is a separate
> project with its own terms.

## Layout

| Folder | What |
|---|---|
| `engine/` | Lapin's engine: the IFC model, the platform, the `.lapin` package, the modules, the host |
| `ui/` | Lapin's interface: ribbon, 3D view, panels (React + three.js) |
| `desktop/` | the Tauri shell: the window, native dialogs, starts the engine |
| `products/*.json` | editions: the modules a package holds |
| `ui/src/modules/` | the UI modules: core, architecture, structure, drafting, phases, cost |
| `vendor/bubblebim-pro/` | the BubbleBIM Pro engine (submodule), used for its geometry kernel |
| `docs/plan.md` | the plan: decisions, milestones M0–M9 |

## Modules and editions

A module has an engine half (`engine/src/Modules/…`: the IFC classes it authors,
its commands, their geometry) and a UI half (`ui/src/modules/<id>`: its ribbon
tabs, quick tools and property sections), or only one of them. A module's
`module.json` lists the modules it `requires`.

An edition is a list of modules (`products/<id>.json`), completed with what
they require (`scripts/product.mjs`). A package holds only those: a module an
edition does not have is not in its engine or in its interface. A project
opened in an edition without one of its modules keeps that module's elements,
read-only, and saves them back untouched.

```sh
pnpm products                                   # what every edition resolves to
LAPIN_PRODUCT=lapin-architecture pnpm dev       # Core + Architecture + Drafting only
pnpm build:product lapin-architecture           # → build/lapin-architecture/bundle
```

## Licences

Lapin is MIT. `pnpm check:licenses` (run by `pnpm build` and `pnpm build:product`)
fails when a production dependency, npm or NuGet, is not under a permissive
licence (MIT, ISC, BSD, Apache-2.0…), so nothing copyleft gets in by accident.

## Run

Requires .NET 10, Node 22 + pnpm, Rust (for the desktop shell).

```sh
pnpm setup        # submodule, packages, engine build
pnpm dev          # engine on ws://127.0.0.1:47810/ws, UI on http://localhost:5180
pnpm dev:desktop  # the same in the Lapin window
pnpm test         # UI (vitest) and engine (xUnit)
```

Note: the engine's boolean library (manifold) is built for macOS arm64 only for now.
