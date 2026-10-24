# Lapin and AI agents: the MCP bridge

Every Lapin tool is an engine command with JSON parameters. `scripts/lapin-mcp.mjs` lets an AI agent drive the Lapin that is open, through the Model Context Protocol, on stdio.

The agent's edits go through the same engine as the user's, so:
- they show at once in the window;
- each one is one undo step.

## Setting it up

```sh
claude mcp add lapin -- node /path/to/lapin-pro/scripts/lapin-mcp.mjs
```

Any MCP client that runs a stdio server works the same way. The bridge needs Node 22 and nothing else.

## Finding the engine

While it runs, the engine writes its port to `<user-dir>/host.json`. The user folder is:
- `~/Library/Application Support/ro.ciuntucbimstudio.lapin` for the desktop app on macOS;
- `~/.local/share/Lapin` under `pnpm dev`.

`LAPIN_PORT` overrides this (`pnpm dev` uses 47810).

The engine listens only on 127.0.0.1.

## Tools

| Tool | What it does |
|---|---|
| `lapin_info` | The product, its modules, every command, and the classes with their fields. Call it first. |
| `lapin_model` | The entities (id, class, name, storey, attributes), optionally only some classes. |
| `lapin_execute` | One command, for example `architecture.addWall {x1, y1, x2, y2}`. Lengths are in metres. |
| `lapin_query` | A question that changes nothing, for example `view.drawing`, `schedule.csv` or `civil.elevationAt`. |
| `lapin_quantities` | An element's quantity sets. |
| `lapin_undo`, `lapin_redo` | One step back or forward. |
| `lapin_save` | Save the project. |
