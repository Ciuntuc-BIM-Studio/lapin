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
| `products/lapin.json` | the engine modules Lapin loads (building, drawing, ifc, cost) |
| `vendor/bubblebim-pro/` | the engine (submodule) |
| `docs/plan.md` | the plan: decisions, milestones M0–M7 |

## Run

Requires .NET 10, Node 22 + pnpm, Rust (for the desktop shell).

```sh
pnpm setup        # submodule, packages, engine build
pnpm dev          # engine on ws://127.0.0.1:47810/ws, UI on http://localhost:5180
pnpm dev:desktop  # the same in the Lapin window
```

Note: the engine's boolean library (manifold) is built for macOS arm64 only for now.
