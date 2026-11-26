# The engine without its window: `Lapin.Host --run`

A program that needs Lapin's own results without the interface (a CDE server checking a commit, a script, a test) runs the engine as a process with a **job**: it opens a project (or starts a new one), runs the job's steps through the engine's protocol, writes the answer and ends. It is the same executable, the same product and modules and the same dispatcher (`EngineDispatcher`) as the desktop, so the results are the desktop's. Atrium Server runs a commit's clash tests this way (its `docs/plan-server-analysis.md`); nothing of Lapin's code is copied into it.

```sh
dotnet Lapin.Host.dll --product product.json --modules-dir modules --run job.json --out answer.json   # or --run - (standard input), no --out (standard output)
```

No port is opened, no recovery journal or project copy is kept, and nothing is written but what the job asks (`saveAs`); the run's temporary folder is removed after it. Exit code 0 when the job ran (each step says whether it went through), 1 when it could not (unreadable, a project that does not open, a save that failed). The code: `engine/src/Host/Lapin.Host/HeadlessRun.cs`, its tests `engine/tests/Lapin.Engine.Tests/HeadlessRunTests.cs`.

## The job

```json
{
  "open": "/path/project.lapin",
  "plain": false,
  "steps": [
    {"method": "doc.execute", "params": {"command": "clash.run", "args": {}}},
    {"method": "doc.query", "params": {"name": "clash.report", "args": {}}}
  ],
  "saveAs": "/path/out.lapin"
}
```

- `open`: a `.lapin` file, opened as the desktop opens it (an older format brought up to date). Without it a new project starts (`plain`: one ground floor).
- `steps`: calls of the engine's protocol, one of `doc.execute` (a command: one undo step, as ever), `doc.query`, `session.info`, `session.catalogue`. They run **as a guest editor** (`Caller("headless", "editor")`), so the dispatcher's guest rules apply on top: no file of the computer named in the arguments, no project script, no module that reaches the network. A step that fails does not stop the others.
- A step takes an earlier step's answer with `{"$step": <n>, "path": "a.b.0"}` anywhere in its params (members by name, array items by number); it fails when that step failed.
- `saveAs`: the project written there after the steps (tools and tests that make projects); a server's check never passes it.

## The answer

```json
{"ok": true, "engine": {"version": "0.1.2223", "product": "lapin", "modules": ["core", "architecture", "…"]},
 "open": {"notes": [], "absent": []}, "steps": [{"method": "doc.execute", "result": […]}, {"method": "doc.query", "error": "…"}],
 "saved": "/path/out.lapin", "milliseconds": 1123}
```

`open.absent` names the modules the project uses that this product lacks (their content is kept as it is, decision 484).

## For a server: `clash.report` and the server's engine

- `clash.report` (the clash module's query): every clash test with its clashes in one answer, `{hash, tests: [test as clash.list gives it, with its clashes as clash.results gives them]}`, the resolved ones left out unless `resolved: true`. Elements are named by their ids, which are their IFC GlobalIds, with their names, classes, storeys and links.
- `node scripts/build-server-engine.mjs <product | product.json> [--rid linux-x64]` builds one product's engine for a server: `build/<product>-server-engine/` with `engine/` (Lapin.Host, framework-dependent, with manifold's native library for the runtime id), `modules/` (only the product's) and `product.json`. For `linux-x64` the booleans need `engine/native/linux-x64/libmanifoldc.so` (built on Linux by `node scripts/build-manifold.mjs`, kept by `scripts/build-linux-docker.sh`); the script refuses without it.
- The CDE web shows what a server answers: a submission's clash report in its checks (`ui/src/modules/core/CdeClash.tsx`, lazy) when the server offers `clash`, and a Sun study tab under Publishing (`CdeSunStudy.tsx`, lazy) when it offers `sunStudy`. The server decides what it offers and to whom.
