# Lapin from Python: the `lapinbim` package

Every Lapin tool is an engine command with JSON arguments and one undo step. `lapinbim` (in `python/`, MIT) sends those commands to the Lapin that is open on this computer, so what a script makes:
- shows at once in the window;
- undoes with Ctrl+Z, one step per command (or per batch).

It is stages 1–3 of `docs/plan-python-scripting.md` (decision 489): the client, the Script panel in the app, and the recorder with the project's scripts (below). Script tools and dialogs come in stage 4.

- **Python 3.11 or newer**, the standard library only: its own WebSocket client over `socket`, the engine's LPF1 frames read with `struct` and `memoryview`, JSON-RPC.
- **127.0.0.1 only**: it drives the engine of this computer, never another's (decision 489, §7). A guest of a shared session has no way in.
- **No dependency**, and none to build it: the package has its own build backend (`python/build_backend/lapin_build.py`).

## Install

```sh
pip install lapinbim                 # from PyPI
pip install ./python                 # from a checkout of lapin-pro
pip install -e ./python              # the checkout's own code, as you edit it
```

## Connect

```python
import lapinbim as lapin

print(lapin.info().product.name)     # the module's own connection, made at the first call
```

The engine is found as the MCP bridge finds it (docs/mcp.md):
1. `LAPIN_PORT`, when set;
2. else the first live `host.json` the engine writes while it runs, in `LAPIN_USER_DIR`, then the desktop app's user folder (`~/Library/Application Support/ro.ciuntucbimstudio.lapin` on macOS, `~/.local/share/Lapin` on Linux, `%LOCALAPPDATA%\Lapin` on Windows); a file whose process is gone is skipped;
3. else port 47810, the one `pnpm dev` uses (its user folder is the repo's `.artifacts/user`).

`lapin.connect(port)` makes the module's connection to a given port; `lapin.Lapin(port)` makes another one (`with lapin.Lapin() as l: …` closes it).

## Calls

| Call | What it does |
|---|---|
| `lapin.architecture.add_wall(x1=0, y1=0, x2=5, y2=0, height=2.8)` | A command by its namespace and name, typed from the catalogue (below): one undo step. Returns what the command returns (often `{"id": …}`, read as `r.id`). |
| `lapin.execute("architecture.addWall", {"x1": 0, …})` | The same, by the engine's names. |
| `lapin.query("schedule.csv", {"id": …})` | A question that changes nothing; typed too: `lapin.schedule.csv(id=…)`. |
| `lapin.undo()`, `lapin.redo()` | One step back or forward. |
| `lapin.save()`, `lapin.save("/abs/path/house.lapin")` | The project saved, to its own file or to the path. |
| `lapin.info()` | `session.info`: the product, its modules, the commands (name, label), the classes and their fields. |
| `lapin.catalogue()` | The commands and queries with their arguments, and the classes (below). |
| `lapin.quantities(id)` | An element's quantity sets. |
| `lapin.snapshot()`, `lapin.state()` | The whole model as a window gets it (nodes, relations, render); the document's state (`undo_label`, `can_undo`, `dirty`, `path`). |
| `lapin.batch(label)` | `with lapin.batch("Facades"):` the commands inside as one `edit.batch`: one undo step named `label`. |
| `lapin.step(label)` | `with lapin.step("Facades"):` a step held open on the engine: one undo step, each command answered at once (below). |
| `lapin.selection()` | What the window has selected now, as elements (the desktop app with the scripting module). |
| `lapin.on("diff", f)` | `f(diff)` for every change the engine sends (an edit, an undo, the window's own); returns what removes it. |
| `lapin.model` | The model's entities (below). |
| `lapin.call(method, params)` | Any protocol call (`doc.execute`, `project.new`, …). |

A refused command raises `lapinbim.LapinError` with the engine's own message (the one the user reads in the window); `e.name` is the command.

### Names, arguments and units

- A command's **namespace** is what comes before its first dot, in snake_case: `architecture.addWall` is `lapin.architecture.add_wall`, `cutOrder.reset` is `lapin.cut_order.reset`. `lapin.ns("cutOrder")` takes the engine's spelling, `lapin.command("architecture.addWall")` a full name.
- **Arguments** are named only, in snake_case (`storey_id`) or as the engine spells them (`storeyId`); a Python keyword takes a trailing `_` (`from_`). A top-level `None` is not sent (so a default stays the engine's); a dict passed first goes as it is, for what keywords cannot say: `lapin.element.update({"id": w, "attrs": {"name": None}})`.
- An `Element` passed as an argument goes as its id: `lapin.element.delete(ids=[wall])`.
- **Units** are the engine's: metres and degrees (decision 112); no conversion.
- A query named like a command of the same namespace takes `_query`: `lapin.project.location_query()`.

### The command catalogue

What arguments each command reads is not declared in C#: it is found in the engine's source when Lapin is built. `scripts/command-catalogue.mjs` reads every handler (`r.Command("addWall", "Wall", (ctx, a) => …)`): its `a["key"]`, `P(a, "x", "y")`, and the keys of the helpers it hands `a` to (`Args.Ids(a)`, `WallsAlong.Add(ctx, a, …)`, two levels deep), with a type guessed from how each is read (`(string?)` → str, `Json.Number` → float, `TryGetValue<bool>` → bool, `as JsonArray` → list, `as JsonObject` → dict). It writes `engine/src/Platform/Lapin.Protocol/catalogue.json`, which the Lapin.Protocol build regenerates before compiling (when node is there) and embeds.

The engine serves it as **`session.catalogue`** (`CommandCatalogue.cs`): the product's own commands with their label and module, and its queries, each with the arguments from the file. So an installed app, with no source, still has them. The classes and their fields come from `session.info`. The MCP bridge reads the same catalogue (docs/mcp.md).

Keys read further away than the catalogue looks are not listed; the command still takes them (`**kwargs` go as they are), and its error names what is missing. A premium module built elsewhere has its commands listed with no arguments. A command added to the engine shows here after the next build; `CommandCatalogueTests` fails when a registered command is missing from the file.

### Editor completion

The published package carries `__init__.pyi` and `catalogue.json` for the Lapin it was made with: an editor completes `lapin.architecture.add_wall(` with its arguments and their types, and `lapin.model.walls`. For the Lapin open now (newer, or an edition with more modules):

```sh
python -m lapinbim stubs                     # beside the installed package (--out DIR elsewhere)
python -m lapinbim catalogue --out c.json    # the catalogue itself
python -m lapinbim info                      # what the open Lapin has
```

## The model

```python
lapin.model.walls                    # IfcWall: a class by the plural of its label (lapin.model.classes lists them)
lapin.model.floors                   # IfcSlab ("Floor")
lapin.model.of("IfcWall", "IfcCurtainWall")
lapin.model[id]                      # one entity; KeyError when the model has none
lapin.model.storey("Ground floor", 0) # a storey by its name, else its elevation (1 mm), else (no arguments) the ground floor
lapin.model.find("IfcWallType", "Brick 250")   # the first entity of a class with that name
len(lapin.model), list(lapin.model)
```

The entities are read from `doc.snapshot` (JSON) once and kept until the engine sends a diff; then the next read asks again. An `Element` has `id` (its GlobalId), `type` (its IFC class), `name`, `storey` (the storey's id), `module`, `parent` and `attrs`; its attributes read by name too (`wall.height`, `wall.type_id`), and an assignment changes one through `element.update` (`wall.height = 3`: one undo step, or part of the batch or step it is in; the engine refuses a field the class lacks or keeps read-only). It is what the model was when read (an assignment updates its own copy); `refresh()` reads it again, `fields` names its class's fields.

| On an element | What it does |
|---|---|
| `quantities()` | Its quantity sets, as the properties panel shows them. |
| `quantity("NetVolume")` | One quantity's value by its IFC name. |
| `update(height=3)` | `element.update`: its fields changed, one undo step. |
| `rename(name)`, `set_param(name, value, kind=None, pset=None)`, `delete()` | `element.rename`, `element.setParam`, `element.delete`. |

## Batches: one undo step for many commands

```python
with lapin.batch("Facades") as b:
    for x in range(0, 30, 6):
        lapin.architecture.add_wall(x1=x, y1=0, x2=x + 6, y2=0)
print(b.results)                     # each command's result, in order
```

- The commands go together as one `edit.batch {steps, label}` when the block ends: one undo step named by the label (the engine's own "Edit" without one), all or nothing (a step refused, none is kept).
- Inside the block a command returns a `Deferred`, filled when the block ends (`d.id` before then raises). A step cannot use an id made by an earlier step of the same batch; queries go at once and see the model before the batch.
- An exception in the block sends nothing. A batch inside a batch joins the outer one. `undo` and `redo` are refused inside.
- Thousands of single commands are thousands of undo steps and a few seconds; a batch is one call.

## Steps: one undo step, each command answered at once

```python
with lapin.step("Room") as s:
    w = lapin.architecture.add_wall(x1=0, y1=0, x2=5, y2=0)
    lapin.element.rename(id=w.id, name="South")     # the id is there at once, unlike in a batch
print(s.kept)
```

- `doc.begin {label}` as the block starts, `doc.commit` when it ends well, `doc.rollback` when it raises (the exception goes on): the model as it was. The engine's side is below (`doc.begin`, `doc.commit`, `doc.rollback`).
- While it is open no other connection edits (*A script is editing the model…*); undo and redo are refused inside it.
- A step inside a step joins it (`doc.begin` answers `{joined: true}`; `s.joined`): only the outermost commit or rollback ends it, so an inner step whose exception the script catches keeps what it did in the outer one. A Run in the Script panel is a step, so `lapin.step` inside a script joins its Run.
- A batch may go inside a step; a step inside a batch is refused.

## Events

```python
off = lapin.on("diff", lambda d: print(len(d.nodes), "changed,", len(d.removed_nodes), "removed"))
lapin.on("*", lambda name, data: print(name))      # every event: diff, progress, presence, delivery
off()
```

Handlers run one at a time, on a thread of their own (so they may call the engine). A diff carries the window's meshes too: its arrays are `memoryview`s on the frame (`numpy.frombuffer(part.positions, numpy.float32)` takes one as it is).

## Examples

Four walls round a room, one undo step:

```python
import lapinbim as lapin

storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (10, 0), (10, 8), (0, 8)]
with lapin.batch("Room walls"):
    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
        lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
```

The volume of every wall, and the total:

```python
total = 0.0
for wall in lapin.model.walls:
    v = wall.quantity("NetVolume") or 0
    total += v
    print(f"{wall.name:20} {v:8.3f} m³")
print(f"{'total':20} {total:8.3f} m³")
```

A door schedule, and its CSV written to a file:

```python
sched = lapin.architecture.create_schedule(kind="door")        # door, window, all, lintel, or a module's table
open("/tmp/doors.csv", "w", encoding="utf-8").write(lapin.schedule.csv(id=sched.id))
```

Every wall 20 cm higher, one undo step:

```python
with lapin.batch("Walls +20 cm"):
    for w in lapin.model.walls:
        w.update(height=w.height + 0.2)
```

## The Script panel (stage 2)

The open module `scripting` (in the free edition, `products/lapin.json`) puts Python in the desktop app, behind **Preferences → Developer extras** (off by default; user guide: `docs/user/python.md`). Its engine half is the host's (`engine/src/Host/Lapin.Host/ScriptHost.cs`), its interface half `ui/src/modules/scripting` (the panel, CodeMirror 6 fetched with it on first open).

- **Python is the user's own**, 3.11 or newer: the one named in Preferences (a path or a command), else the first that answers of `LAPIN_PYTHON`, `py -3` (Windows), `python3`, `python`, and the usual places (python.org's, Homebrew's, pyenv's; a GUI app on macOS has a short PATH). Each is asked its version; an older one is named in the message. Lapin ships no Python and downloads none.
- **The kernel**: `python -I -B -u -c "<bootstrap>" <package dir>`, started at the first Run, one per engine, kept between runs (the console's names stay) until Restart. Isolated (no current folder, no `PYTHON*` variables; the user's own site-packages are put back, so `pip install --user` packages import), no bytecode written (the app's resources are signed). The package is `python/lapinbim`, copied by `build:product` to `resources/python/lapinbim` where the product has `scripting` (so no `pip install` is needed), and found by the host beside its modules, else up from the engine (a checkout's `python/`: `pnpm dev`, the guide's scenes, the tests); `--python-dir` names another. The kernel binds `lapin` to it, sets `LAPIN_PORT` to the engine's and connects back over the protocol like any script: what a script does is ordinary commands.
- **Kernel protocol** (`python/lapinbim/kernel.py`), one JSON object a line: in, `{"op": "run", "id", "code", "name", "console"}`, `{"op": "interrupt"}`, `{"op": "exit"}`; out, `ready` (versions), `out`/`err` (what the code prints, with its run's id), `done` (`ok`, `interrupted`, `kept`, `error`, `line`, `traceback` with the kernel's frames left out, `seconds`). A console run shows its last expression's value. `input()` is refused rather than hanging.
- **Host methods**, the host's own (refused to a guest: `Only the host can do that.`), answered past the call queue so Stop reaches a script whose commands hold it: `script.run {code, name?, console?, python?}` (one run at a time), `script.stop` (an interrupt, a real SIGINT on macOS and Linux, `interrupt_main` on Windows; the process killed after 2 s), `script.restart`, `script.status {python?, refresh?}`, and Mine: `script.files`, `script.load {name}`, `script.save {name, code}`, `script.delete {name}`, `script.reveal {name, editor?}` (plain `.py` files in `<user-dir>/scripts`, a name never a path; the external editor named, else `open -t`, Notepad or xdg-open, never the system's action for `.py`, which may run it). The kernel's lines go to the computer's own windows as the event `script.output`. An edition without `scripting` refuses them (`script.status` says so).

### One Run, one undo step: `doc.begin`, `doc.commit`, `doc.rollback`

The kernel opens a step on the engine round each run: `doc.begin {label: <the script's name>}`, then `doc.commit` when the code ends well, `doc.rollback` when it raises or is stopped. Any client may use them (`lapin.call("doc.begin", {"label": "…"})`); the step is its connection's (`Session.Step.cs`):

- its commands (a batch too) join one transaction (`History.Begin/Commit/Rollback`): one undo step named by the label, journalled once at the commit; nothing changed, no step;
- each command's diff still goes to every window as it is made, so the window shows the script at work;
- while it is open, another connection's edit, undo or redo, a new or opened project, a save, is refused with *A script is editing the model: wait for it to finish, or stop it.*; reads go on; the step's own connection cannot undo, redo, save or open another project inside it either; a module's background results wait for it to end;
- a command refused inside it takes back only itself; the step stays open;
- the connection lost (the kernel killed, crashed, a terminal script closed) rolls it back, and every window gets the diff;
- a guest cannot open one (`doc.begin`, `doc.commit` and `doc.rollback` are not in `AllowedRemote`).

A step begun by the connection that holds one joins it (only the outermost ends it; above).

### The kernel's own connections (stage 3)

The engine gives each kernel it starts a token of its own (32 random bytes, `LAPIN_SCRIPT_TOKEN` in its environment, new with each Python process). `lapinbim` takes it out of the environment as it loads (a process the script starts does not inherit it) and every connection it opens to that engine shows it: `script.join {token}` (the host's, refused to a guest). The hub then gives that connection the kernel's step key (`EngineHub.Client.StepKey`, `ScriptHost.KernelKey`), the connection the session sees (`Session.Connection`): so the module's own connection (`lapin`) and any a script opens itself (`lapinbim.Lapin()`, `l.joined` is true) edit in the run's step, and every other connection (a terminal script, the MCP bridge, the window) is still refused while it is open. A wrong token, or a kernel's from before a Restart, is refused (*That is not the Script panel's Python…*) and the connection stays its own. The step is rolled back when the last connection of its key goes.

### The project's scripts, trust, the selection (stage 3)

- **`LapinScript`**, the open engine module `scripting` (`engine/src/Modules/Scripting/Lapin.Scripting`): a resource with its name (a file name ending `.py`, unique whatever its case), `code`, `language` (`python`), `description` and `sha256` (of the code, UTF-8, lowercase hex). Commands, one undo step each: `scripting.save {id?, name, code, description?}` (new, or by id changed and maybe renamed), `scripting.rename {id, name}`, `scripting.delete {id}`; queries `scripting.list` (no code), `scripting.get {id | name}`. The core's own edits (`element.update`, `element.rename`, `setParam`, moves) are refused on a script (its code and hash change together); `element.delete` takes it like any content. A guest is refused every `scripting.*` command, alone or in a batch (`EngineDispatcher.HostOnlyCommands`).
- **In the .lapin**: each script is also written as `scripts/<name>` (UTF-8), a part listed in the manifest with `module: "scripting"` (`IPackageTexts`, `LapinPackage.TextFolders`); Lapin reads the entity, never the text. Another tool's files in `scripts/` are foreign parts, kept byte for byte beside them (decision 345); a project script named as one of them is what the package holds from then on. An edition without the module keeps the entities (decision 484) and writes their `scripts/` parts back as they came (`PackageForeign.ModuleParts`). Versions keep the scripts with the model.
- **Trust** (`docs/plan-python-scripting.md` §7): a project's script never runs as it opens; the panel asks before its first run and after its code changed (Run once / Trust / Cancel). Trust is kept on this computer by the code's SHA-256 in `<user-dir>/scripting-trust.json`: `script.trusted {sha256}`, `script.trust {sha256}`. Code the user saves into the project is trusted as it is saved.
- **The selection**: the window tells the host what it has selected as it changes (`script.selected {ids}`, a call with no id, with Developer extras on); `script.selection` answers it, `lapin.selection()` reads it as elements.

## The recorder (stage 3)

The Script panel's **Recorder** tab (`ui/src/modules/scripting/recorder.ts`) hears every call the window makes (`EngineClient.onCall`, where every tool's edit passes, overridden or not) and writes each `doc.execute` as one line from the catalogue: `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, …)`.

- What a command made is bound to a variable (`{id}` → `wall1`, then `wall1.id`; `{ids}` → `walls1.ids[0]`); a storey is looked up by name and elevation (`storey1 = lapin.model.storey("Ground floor", 0)`), a type or resource by class and name (`lapin.model.find(…)`), another element by its id (`lapin.model["…"]`); with *Selection as lapin.selection()*, an `ids` that is the window's selection is `lapin.selection()`.
- An `edit.batch` is a `with lapin.batch("…"):` block. An Undo takes the last recorded step back out of the script and a Redo puts it back, so the script holds the end result and runs as one undo step in the panel; an Undo of what came before the recording is `lapin.undo()`.
- Keywords where lapinbim maps them back to the engine's key (`camel(snake(k)) == k`); other keys, and a `None` (which a keyword would leave out), go in the dict passed first. A command the catalogue lacks is `lapin.execute("…", {…})`; a namespace named as one of the module's own names is `lapin.ns("…")`.
- Not recorded: queries, the panel's own `scripting.*`, what a script does (its own connection's).
- Record / Stop, Copy, Save as script (into Mine, opened in the editor). `python/tests/fixtures/recorded_walls.py` is what `recorder.test.ts` makes of a session and what `python/tests/test_steps.py` runs on a new project.

## Tests

`pnpm test:python` (`scripts/test-python.mjs`, part of `pnpm test`) runs `python/tests` with the standard library's `unittest` on the oldest Python found from 3.11 and the newest, each against an engine of its own: the built Debug host on a free port with a temporary user folder (`python/tests/engine.py`), as `scripts/lapin-mcp-check.mjs` starts it, never the engine of `pnpm dev`. They cover the WebSocket client against a small server of the test's own (fragments, ping, a 64-bit length, masking, close, a wrong handshake), the LPF1 reader, names and arguments, and against the engine: connecting by `LAPIN_PORT` and by `host.json`, typed commands, a batch as one named undo step (and all or nothing), undo and redo, a refused command, binary frames, quantities, element edits, diff events, saving, the module's own connection, the catalogue and the stubs. No Python 3.11 or newer: the run says so and passes; no built engine, no dotnet or no node: the engine's tests are skipped, saying why. `--all` runs every Python found.

## Publishing on PyPI

`lapinbim` is published on PyPI **only with an app release** (the user's decision, 12.11.2026): when a build of Lapin is published for users (`scripts/publish-update.mjs`), the same commit's package is built and uploaded, so a version on PyPI always matches an app users have; never between releases. The release steps: publish the app; then `pnpm build:engine` and `node scripts/publish-python.mjs --upload` from the same commit.

The package's version is Lapin's build version, `0.1.<commit count>`, the desktop app's: a published `lapinbim` carries the catalogue and the stubs of the Lapin built from the same commit. A newer Lapin still works with it (it reads the open engine's catalogue); an engine from before `session.catalogue` uses the copy the package carries.

```sh
pnpm build:engine
node scripts/publish-python.mjs                  # build/python/dist: the sdist and the wheel, with their SHA-256; nothing uploaded
python3 -m pip install --user twine              # once
node scripts/publish-python.mjs --upload         # the same, then twine uploads them (it asks for the PyPI token)
node scripts/publish-python.mjs --upload --test  # to TestPyPI first
```

The script stages a copy of `python/` with the version written in, starts an engine of its own to write `lapinbim/catalogue.json` and `lapinbim/__init__.pyi`, builds the sdist and the wheel with the package's own backend, checks that the wheel imports with its version, stubs and catalogue, and with `--upload` runs `python3 -m twine upload`. The token is the user's own (in `~/.pypirc` as `__token__`, the keyring, or typed when twine asks); it never goes through the script or the repo. `--python <cmd>` picks the Python, `--product <id>` the edition whose catalogue it carries (`lapin` by default).

## A premium module's commands

A module built in another repository (`../lapin-mep`, `../lapin-urban`, …; docs/overrides.md) is not in Lapin's catalogue, which is made from this repository's source. Its repository makes its own and embeds it in its module:

```sh
node ../lapin-pro/scripts/command-catalogue.mjs --root engine/src --out engine/src/Modules/<Group>/Lapin.<X>/catalogue.json
```

```xml
<ItemGroup>
  <EmbeddedResource Include="catalogue.json" LogicalName="Lapin.catalogue.json" />
</ItemGroup>
```

`session.catalogue` reads `Lapin.catalogue.json` from every loaded module's assembly (`CommandCatalogue.ModuleResource`) and fills in the commands and queries Lapin's own catalogue does not have; Lapin's own entries win. The Python package, the panel's completion and the MCP bridge then have that module's arguments, in an installed app too. Run it again when the module's commands change (a step in its build, as Lapin.Protocol's is here).
