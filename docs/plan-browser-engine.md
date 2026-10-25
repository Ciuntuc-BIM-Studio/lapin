# The engine in the browser: plan

Asked on 25.10.2026 (pending decision 124, decided the same day: go, the 3× speed budget kept). The engine compiled to WebAssembly and run in a Web Worker, so a tester opens Lapin from a URL, with the project staying in the browser. This plan starts from what the code does today and is bound by one rule, stated first because everything else bends to it.

## The rule: local first stays as it is

The desktop app (Tauri shell + `Lapin.Host` on loopback) remains the product and the reference. The browser build is a second host over the same engine, never a fork of it, and never a reason to make the desktop slower, bigger or different. Measurable form of the rule, checked on every pull request of this plan:

1. `pnpm build:product <id>` produces the same bundle it does today: same files in `Resources/modules`, same `Lapin.Host` binary, same `product.json`. The browser build is a separate command (`pnpm build:web <id>`) that the desktop pipeline never calls.
2. No module (`engine/src/Modules/*`), no platform behaviour and no test changes because of this plan. Where the browser needs a seam (a stream instead of a path, a static module list instead of a folder), the seam is added beside the existing code, and the desktop host keeps calling the existing code. The existing 437 engine tests and 87 UI tests pass unchanged; new tests cover the seams.
3. The UI keeps talking to the engine through `EngineClient` and the same JSON-RPC/binary-frame protocol. The transport (WebSocket to the local engine, or `postMessage` to a worker) is chosen at build time by the environment; the desktop and `pnpm dev` keep the WebSocket, with no code path of theirs touched.
4. Nothing of the desktop's is taken away for the web's sake: the recovery journals on disk, the MCP bridge through `host.json`, the detached view windows, LegacyImport from paths, all stay. The web either has its own version (journals in OPFS) or does without (MCP, detached windows), stated as a limit.
5. No engine process ever runs on a server for this plan, and no project ever leaves the browser. Hosting is static files only.

If a step cannot be done within these five points, the step is dropped, not the rule.

## What exists today (the facts the plan builds on)

- `Lapin.Host/Program.cs` starts Kestrel on loopback with one `Session` per process; `EngineHub.Handle(text)` takes one JSON-RPC call and returns the reply as a binary frame, broadcasting the diff first. The session is single-threaded by design (a semaphore gates the calls). The only `Task.Run` is the parent-process watch.
- Files are paths: `project.open`/`project.save` through `ProjectFiles` (`PackageFiles.Open/Save`), `project.importIfc` through `IfcImport.Run(session, path)`, `project.exportIfc` through `IfcExport.Write(session, path)` (into a temp file when no path is given), the recovery journal under `<user-dir>/recovery`. Exports already come back as base64 when no path is given (`Deliver`), and `ui/src/engine/files.ts` downloads them in a browser.
- The only native code is manifold (`Lapin.Booleans`, `DllImport("manifoldc")`, probed with `NativeLibrary.TryLoad`); the engine runs without it, with diagnostics. The only NuGet package in the engine is `Apache.Arrow` (managed). PDF, DXF and XLSX are written by `Lapin.Drawing` itself.
- Modules are loaded at start from a folder by `ModuleLoader` (`AssemblyLoadContext.LoadFromAssemblyPath`, `Activator.CreateInstance`); `products/<id>.json` lists them and `scripts/product.mjs` completes the list with what they require.
- The geometry kernel is Lapin's own code (`engine/src/Geometry`, MIT) since c71fe5f: a clean clone builds alone.
- `ui/src/engine/client.ts`: `EngineClient(resolveUrl)` opens a WebSocket, sends JSON text, receives binary frames (`decodeFrame`), reconnects by itself. `engineUrl()` reads `VITE_ENGINE_URL`, else `ws://127.0.0.1:47810/ws`.

## Stage 0: the spike (2–3 days, on a branch, nothing merged but what passes)

Three questions, in this order; each has a pass/fail answer and the next starts only when the previous passed.

1. **Does the engine compile and run for `browser-wasm`?** A throwaway `Lapin.Browser` project referencing `Lapin.Platform`, `Lapin.Package`, `Lapin.Ifc` and the modules of `products/lapin.json` as plain project references (no `ModuleLoader`), interpreted (no AOT), without manifold. One `[JSExport]` that takes a JSON-RPC string and returns the reply frame bytes. Test page: copies `modele/testLapin.lapin` into Emscripten's in-memory file system and calls `project.open` with that virtual path, then `doc.snapshot`. **Pass:** every element has a body and the part volumes match the native engine's (minus the openings, which need manifold), no exception from `Apache.Arrow` or `System.IO.Compression`.
2. **Do AOT and trimming hold?** Same project with `RunAOTCompilation` and `PublishTrimmed`. **Pass:** builds; the test page runs without a `MissingMethodException`/reflection failure (the known suspects: `ModuleLoader`'s `Activator.CreateInstance`, `JsonSerializer.Deserialize<ModuleManifest|ProductManifest|LintelProduct>`, `JsonSerializer.SerializeToNode` of the roof catalogue); rebuilding the test house (`architecture.updateRoof` with a new pitch) takes under 3× the native time; the compressed download is under 15 MB.
3. **Does manifold work from npm?** `manifold-3d` loaded in the worker, an `ISolidBooleans` implementation in `Lapin.Browser` that marshals the mesh arrays through `[JSImport]`. **Pass:** the openings of `testLapin.lapin` are cut and the volumes now match the native engine's exactly.

Deliverable: a short section at the end of this file with the three answers and the measured numbers (download size, start time, rebuild time, memory at peak). If 1 fails, the plan stops here and the file says why.

## Stage 1: the seams (desktop unchanged, proven by tests)

Small, behaviour-preserving refactors that both hosts then share. Each is one commit with its own test, mergeable on its own, useful even if the browser never ships.

- **`Lapin.Protocol`** (new class library under `engine/src/Platform`): `BinaryFrame` and the dispatcher (`EngineHub.Handle`/`Dispatch` without the WebSocket parts) move there as `EngineDispatcher`. `Lapin.Host` keeps `Program.cs`, the Kestrel loop, `Recovery`, `ExitGuard`, `LegacyImport`, `Support`, and calls the dispatcher. Test: the protocol tests run against the dispatcher directly (they already drive `Handle`).
- **Streams beside paths.** `LapinPackage.Read(Stream)`/`Write(Stream, …)`, `IfcExport.Write(session, Stream, …)`, `IfcImport.Run(session, Stream, name, …)`; the path overloads become thin wrappers that open the file and call the stream ones, so every existing caller and test is untouched. `ProjectFiles` gains a bytes-based pair (`OpenBytes(name, bytes)`, `SaveBytes()`), the desktop keeps `Open(path)`/`Save(path)`.
- **`IJournalStore`** behind `Recovery`: the class keeps its logic (start lines, tx lines, grace, exclusive hold) over an interface with `Open(name)`, `Append(line)`, `List()`, `Read(name)`, `Delete(name)`. `FileJournalStore` is today's code under `<user-dir>/recovery`; nothing changes for the desktop.
- **Static module registry.** A generated file `Modules.g.cs` per product (written by `scripts/product.mjs` at build, from the same resolution the desktop uses) that returns `new ArchitectureModule()`, `new StructureModule()`, … in order. `ModuleLoader` stays the desktop's way; the browser host uses the generated list. The product resolution stays one function, so a module added to a product reaches both hosts.
- **Manifold behind the service, not the class.** `ISolidBooleans` is already the contract; `CoreModule.Register` registers `ManifoldBooleans` when `Available`. The registration moves to the host (desktop: `ManifoldBooleans`; browser: the npm-backed one), so `Lapin.Platform` no longer references `Lapin.Booleans` directly. The desktop still gets the native library; the tests still get it through the fixture.

## Stage 2: the browser host and its UI transport

- **`engine/src/Host/Lapin.Browser`** (`<TargetFramework>net10.0</TargetFramework>`, `<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>`): the generated module list, `EngineDispatcher`, `BytesProjectFiles`, `OpfsJournalStore` (through `[JSImport]` to OPFS), the npm manifold `ISolidBooleans`. `[JSExport] static byte[] Handle(string json)` plus `[JSExport] static void Subscribe()` for diffs delivered as frames. Nothing else of the desktop host is in it.
- **The worker** (`ui/src/engine/worker/engine.worker.ts`): loads `dotnet.js`, forwards messages, posts frames back as transferable `ArrayBuffer`s (zero copy; the existing `decodeFrame` reads them).
- **The transport** (`ui/src/engine/transport.ts`): `EngineTransport { send(text), onFrame(cb), onStatus(cb) }` with `WebSocketTransport` (today's `open()` logic moved, byte for byte) and `WorkerTransport`. `EngineClient` takes a transport; `session.ts` picks it from `import.meta.env.VITE_ENGINE` (`ws`, the default and what `pnpm dev` and the desktop use; `worker` for the web build). The client's tests cover both transports.
- **Files in the browser** (`files.ts` already branches on `isDesktop()`): open through `showOpenFilePicker` where it exists, else `<input type=file>`; the bytes go to `project.openBytes`. Save through `showSaveFilePicker` where it exists (the handle kept for the session so "Save" writes in place), else download; `project.saveBytes` returns the package. The recent list keeps names, not paths, on the web.
- **The web build**: `pnpm build:web <id>` publishes `Lapin.Browser` for that product (AOT, trimmed), runs the Vite build with `VITE_ENGINE=worker` and the product's UI modules, and writes one static folder (`build/<id>/web`). The Bubbles plugins tab is excluded as it is from every public build (`lapin-plugins-private`).
- **Deploy**: a folder on the nginx that serves `lapin.ciuntucbimstudio.ro` (static, with the COOP/COEP headers only if the threaded runtime is ever used; the single-threaded one needs none), or GitHub Pages for prereleases. `deploy/lapin/deploy.sh` gains a `web` target; the existing targets are unchanged.

## Stage 3: budgets, CI, limits

- **Budgets** written in `scripts/build-web.mjs` and enforced there: compressed download ≤ 15 MB, start to `session.info` ≤ 4 s on a 2020 laptop, rebuild of the test house ≤ 3× native. A build that breaks a budget fails, so the numbers from the spike stay true.
- **CI**: a `web` job beside `ui` and `engine` in `.github/workflows/ci.yml` (the Emscripten workload, `dotnet publish`, the budgets, a headless Chrome smoke test that opens `testLapin.lapin`). The `engine` job does not change.
- **Stated limits** on the web (in the start screen's about text, and in `docs/mcp.md`): no MCP bridge; no detached view windows; the memory ceiling (2 GB; a large imported IFC may not fit, which is in line with "Lapin is native first"); Safari and Firefox save by download.
- **What the web does without, by choice**: no server engine, no accounts, no sync. A tester's project lives in their browser (OPFS journal) and in the `.lapin` they download.

## Stage 4: parity and the desktop's gains

Only after the web runs with the budgets kept:

- LegacyImport from bytes (old format-2 projects dropped on the page).
- `support.report` from the browser (the report goes to the same Hetzner endpoint as the desktop's, `lapin-feedback-reports`).
- The seams pay back on the desktop: `project.saveBytes` lets the Tauri shell save without the engine touching the path (one less reason for the engine to know the file system), and `IJournalStore` lets the desktop keep journals elsewhere if ever wanted. Neither is required; both are free.

## Effort

Stage 0: 2–3 days. Stage 1: 2–3 days (mostly tests). Stage 2: 5–7 days. Stage 3: 2 days. Total to "a tester opens, draws, saves and exports": about two weeks after the spike. Stage 4: one more week, when wanted.

## Open questions

1. **Which products go to the web?** `lapin` only, or every edition that has no private module? (Default: `lapin` only, until the budgets are known.)
2. **Where is it served?** `lapin.ciuntucbimstudio.ro/web/` beside the site, a subdomain, or GitHub Pages for prereleases? (Default: the nginx folder, same deploy script, a `web` target.)
3. **Safari and Firefox**: is "save by download" acceptable for testers, or do we wait for them to get the File System Access API? (Default: acceptable, stated on the start screen.)
4. **Interpreter fallback**: keep a non-AOT build for debugging only, or none? (Default: debugging only, never deployed.)
5. **The plugins**: the web build excludes the Bubbles tab as every public build does; confirm nothing else is private in the `lapin` product. (Default: nothing else.)


## Progress (25.10.2026)

**Stage 1, done** (each step behaviour-preserving, proven by tests; engine 444 tests, UI 87):

- The engine's JSON is generated at build time: a context per assembly (`ModelJson`, `PackageJson`, `PlatformJson`, `DrawingJson`, `ArchitectureJson`, `ArchitectureCatalogJson`, `CivilJson`, `ProtocolJson`) with the options of `Json.Options`; `JsonContractTests` proves every moved type writes exactly what reflection wrote. Every engine library is `IsTrimmable` (in `Directory.Build.targets`, so the hosts and tests are not), which makes reflection that trimming would break a build error on the desktop too. Two patterns the analyzers flag: `JsonArray.Add(x)` with a non-node argument (typed `(JsonNode)x` now) and collection expressions into a `JsonArray` (`new JsonArray(…)` now; the trimmer, not the build-time analyzer, found those).
- `Lapin.Protocol`: the dispatcher (`EngineDispatcher`) and `BinaryFrame`, without the WebSocket; the desktop's `EngineHub` serves it as before (same constructor, same frames). `ProtocolContractTests` runs a session through the dispatcher twice, the second time writing frames by the generated contracts alone (as a trimmed engine does), and the frames are byte for byte the same.
- Streams beside paths: `LapinPackage.Read/Write(Stream)`, `IfcExport.Write(session, Stream, name)`, `IfcImport.Run(session, bytes, name)`, `StepFile.Read(bytes)`; the path overloads wrap them (a failed save or export still leaves nothing behind). The protocol gains `project.openBytes`, `project.saveBytes`, and `project.importIfc` with `data`; `project.exportIfc` without a path no longer goes through a temporary file.
- The recovery journal over `IJournalStore` (moved into `Lapin.Protocol`); `FileJournalStore` is the desktop's code as it was. `JournalStoreTests` brings work back from a store that is not files.
- The static module list: `engineModules()` in `scripts/product.mjs` (the same resolution the desktop uses) gives each module's project and its `ILapinModule` class; `build-web.mjs` writes them into the browser host.
- manifold: nothing to move. In the browser `CoreModule`'s probe sees no library to load and registers nothing; the browser host registers the desktop's `ManifoldBooleans`, linked in. `scripts/build-manifold.mjs --browser-wasm` builds it (`engine/native/browser-wasm/`, ignored by git).

**Stage 2, done:**

- `engine/src/Host/Lapin.Browser` (outside `Lapin.slnx`): AOT, **trimmed whole** (`TrimMode=full`), manifold linked in, journals through the worker. `pnpm build:web <id>` builds the engine and the interface into `build/<id>/web`: **6.8 MB** compressed for `lapin`, the engine 6.5 of it.
- The interface: `EngineClient` takes a transport (`webSocketTransport`, today's code; `workerTransport`, the engine in a worker), chosen by `VITE_ENGINE=worker` at build time; the desktop and `pnpm dev` keep the WebSocket. Projects open from a file the user picks (the File System Access API where there is one, else a file input) and save back to it or as a download; IFC import takes the file's bytes; the tab asks before leaving unsaved work.
- The worker (`ui/src/engine/worker/engine.worker.ts`) runs .NET as a **sidecar**: `globalThis.dotnetSidecar = true` before `dotnet.js` loads, or the runtime takes a worker that has a message handler for one of its own helper threads and waits forever. Journals: kept in memory (read at start from the origin private file system, folder `recovery`), each line written through in order; a journal another tab writes is held by its Web Lock.
- Checked in headless Chrome on the built folder, served statically: a new project, two walls drawn with WA (joined at their corner), the 3D view; the tab reloaded without saving offers the unsaved work on the start screen, and Recover brings the walls back. The desktop path was checked the same way from the branch (native host, WebSocket): unchanged.

**Next: stage 3** — the CI job, the start-time and speed budgets measured in the build (the speed is at 3.4× on whole-storey work, against the 3× kept: profiling the rebuild in the browser comes first), the limits written on the start screen, the deploy target.
