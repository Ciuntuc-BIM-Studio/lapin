---
title: Connect an AI agent
order: 129
summary: Let an AI agent (Claude Code, Claude Desktop, Gemini CLI or any MCP client) work in the Lapin you have open: ? → Connect an AI agent gives the commands to copy; its edits show at once and are one undo step each.
---

An AI agent can drive the Lapin you have open, through the same commands as the interface. It reads the model, adds and changes elements, asks for quantities and drawings, writes documents and saves. What it does shows in the window at once, and **each of its edits is one undo step**, so Ctrl+Z takes it back like anything you did yourself.

The connection uses the Model Context Protocol (MCP), the standard way AI clients talk to programs, through a small script that comes with Lapin. It works with the **desktop app only**: the browser version of Lapin has no engine on your computer for an agent to reach.

## What you need

- The Lapin desktop app, **open** while the agent works. With several Lapins open, the agent says which are open and you name one (see below).
- **Node.js 22 or newer** on the computer (nodejs.org). Lapin does not install it. In a terminal, `node --version` shows what you have.
- An AI client that runs MCP servers on your computer: Claude Code, Claude Desktop, Gemini CLI, or another.

## Connect from the installed app

1. Open **? → Connect an AI agent…**.
2. The window shows where the script is in your installed Lapin, and a command for each kind of client, with a **Copy** button.
3. **Claude Code**: copy the command and run it in a terminal. It looks like

   ```sh
   claude mcp add lapin -- node "/Applications/Lapin.app/Contents/Resources/resources/lapin-mcp.mjs"
   ```

   with your own path (it has quotes because a path can contain spaces).
4. **Claude Desktop, Gemini CLI and other clients with an `mcpServers` setting**: copy the JSON into the client's settings file, then restart the client.

   ```json
   {
     "mcpServers": {
       "lapin": { "command": "node", "args": ["<the path shown in the window>"] }
     }
   }
   ```

   Claude Desktop reads `claude_desktop_config.json` (its Settings → Developer → Edit Config opens it); Gemini CLI reads `settings.json` in its `.gemini` folder. If the file already has `mcpServers`, add `lapin` beside the others.
5. Any other client that starts a stdio MCP server takes the same two parts: the command `node` and the script's path as its argument.

Then ask the agent about your model, for example "list the walls of the ground floor". The first thing it does is call `lapin_info`, which tells it the product, its modules and every command with its arguments.

## Connect from the source folder

If you run Lapin from its source (`pnpm dev`), use the script in the repository instead, the same one the app carries:

```sh
claude mcp add lapin -- node /path/to/lapin-pro/scripts/lapin-mcp.mjs
```

The app's script is a copy of this one, made when the app is built, so the two always behave the same.

## What the agent gets

| Tool | What it does |
|---|---|
| `lapin_info` | The product, its modules, every command with the arguments it reads, the queries, and the classes with their fields. The agent calls it first. |
| `lapin_model` | The elements (id, class, name, storey, attributes), optionally only some classes. |
| `lapin_execute` | One command, for example `architecture.addWall`. Lengths are in metres. |
| `lapin_query` | A question that changes nothing: a drawing, a schedule, the elevation of the site at a point, what an element is built from. |
| `lapin_quantities` | An element's quantities. |
| `lapin_documents`, `lapin_document_export` | The project's written documents, and one written as DOCX, ODT or PDF to a path on your computer. |
| `lapin_export_jg` | The analytical model as JG Structures' exchange JSON, with the report of its checks. |
| `lapin_undo`, `lapin_redo` | One step back or forward. |
| `lapin_save` | Save the project. |

The full list, with the details of each, is in `docs/mcp.md` of the source.

## Several Lapins open

With one Lapin open the script finds it by itself. With several, the agent's reply lists them; to pick one, set an environment variable for the script: `LAPIN_PROJECT` (a part of the project's name or path) or `LAPIN_PORT`. In Claude Code:

```sh
claude mcp add lapin -e LAPIN_PROJECT=House -- node "<path>"
```

In an `mcpServers` setting, add `"env": { "LAPIN_PROJECT": "House" }` beside `command` and `args`.

## Good to know

- The agent only reaches Lapin on **your own computer**: the engine listens on 127.0.0.1 and nothing is sent anywhere by the script. What the AI client itself sends to its provider is that client's matter, so read its terms before you let it see a project that is confidential.
- An agent can make mistakes. Look at what it did, and undo any step you do not want.
- This is separate from the **CDE server's MCP address**, which lets an AI client reach a project's documents and issues on Atrium Server, with no Lapin open. That one is set up in [Working on a CDE server](cde-server.md).

From a Python notebook instead of an agent, see [Jupyter notebooks](python.md#jupyter-notebooks): the same Lapin can be driven by both at once.
