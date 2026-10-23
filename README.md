# Lapin

BIM authoring for architects and engineers, on the **BubbleBIM Pro** engine.

Lapin is a desktop application. Its interface (React, TypeScript, three.js) runs in a
Tauri window and talks over a local WebSocket to the BubbleBIM Pro engine (C#, .NET 10),
which holds the model as a graph of building elements and derives their geometry.

> Private repository. Licensed under the MIT licence (see `LICENSE`).
> The BubbleBIM Pro engine (`vendor/bubblebim-pro`, a git submodule) is a separate
> project with its own terms.

## Layout

| Folder | What |
|---|---|
| `ui/` | Lapin's interface: ribbon, 3D view, panels (React + three.js) |
| `desktop/` | the Tauri shell: the window, native dialogs, starts the engine |
| `products/*.json` | editions: the UI modules a build bundles and the engine modules it loads |
| `ui/src/modules/` | the UI modules: core, architecture, structure, drafting, phases, cost |
| `vendor/bubblebim-pro/` | the engine (submodule) |
| `docs/plan.md` | the plan: decisions, milestones M0–M7 |

## Modules and editions

A module brings its ribbon tabs, its quick tools (the 1–9 bar over the view)
and its property sections (`ui/src/platform/module.ts`). An edition is a list
of modules (`products/<id>.json`): a build bundles only those, so a module an
edition does not have is not in its code at all.

```sh
LAPIN_PRODUCT=lapin-architecture pnpm dev    # Core + Architecture + Drafting only
LAPIN_PRODUCT=lapin-architecture pnpm build
```

## Licences

Lapin is MIT. `pnpm check:licenses` (run by `pnpm build`) fails when a
production dependency is not under a permissive licence (MIT, ISC, BSD,
Apache-2.0…), so nothing copyleft gets in by accident.

## Run

Requires .NET 10, Node 22 + pnpm, Rust (for the desktop shell).

```sh
pnpm setup        # submodule, packages, engine build
pnpm dev          # engine on ws://127.0.0.1:47810/ws, UI on http://localhost:5180
pnpm dev:desktop  # the same in the Lapin window
```

Note: the engine's boolean library (manifold) is built for macOS arm64 only for now.
