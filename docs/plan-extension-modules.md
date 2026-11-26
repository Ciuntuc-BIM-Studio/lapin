# Extension modules: plan

The user decided (26.11.2026) that the planning work of this round (the location-based schedule, level of effort and ALAP, the PBS tools, procurement chains and cash flow; the unmerged branches `feat/plan-lbs`, `feat/plan-loe-alap`, `feat/plan-pbs`, `feat/plan-procurement` and `feat/plan-cashflow`) ships as an **extension** in Lapin Extensions. It is free for now (price 0), under its **own licence**, and written with Yoram Mass PhD (his credit waits on his answer). Today the store takes content packs and Python scripts only. Kinds 3 (JS interface plug-ins) and 4 (C# modules) are still "to be built" (`docs/plan-extensions.md` §4, §11, §12.8). This plan builds them, as one kind: an **extension module**, with an engine half, an interface half, or both.

Status: **proposed**. Nothing here is built yet.

## 1. What an extension module is

### 1.1 The package (`.lapinext`, a zip)

```
manifest.json            the catalogue's manifest (kind "module"), the same file as in lapin-extensions/extensions/<id>/
module.json              the engine module's manifest, as today plus "contract" (§1.3)
engine/<assembly>.dll    managed assemblies only (any CPU); its own NuGet dependencies beside it
engine/Lapin.catalogue.json   (embedded in the assembly in practice: CommandCatalogue.ModuleResource)
ui/plugin.js             one ES module, nothing outside it imported except the host's shared libraries (§3.2)
ui/plugin.css            optional
ui/i18n/<lang>.json      the phrase tables, one per language of ui/src/i18n
LICENSE.txt              the licence text (shown at install, §5)
FILES.sha256             every other file's SHA-256 and size
```

- The store's signature stays as it is (`package.signature` over id, version, SHA-256 and size of the zip; `ui/src/platform/extensions/signature.ts`, `lapin-extensions/tools/sign.mjs`). `FILES.sha256` is inside the signed zip. It lets the engine check the unpacked files on every start without keeping the zip open (§2.1).
- Native libraries (`.dylib`, `.so`, a native `.dll`) are **refused** in stage 2. The validator reads each `.dll`'s PE header for a CLI header. The reasons are in §2.4.
- Size: the 64 MB limit stays (`MAX_PACKAGE`, plan-extensions §9.8).

### 1.2 Where it is installed

`<user-dir>/extensions/<id>/<version>/`, unpacked, with `<user-dir>/extensions/modules.json` beside it: `{id, version, enabled, pendingRemoval?}` for each one. `<user-dir>` is the folder the shell passes as `--user-dir`, which is Tauri's `app_data_dir()` (`desktop/src-tauri/src/lib.rs`, `spawn_engine`). **The engine owns this file.** The extension list the interface keeps today (`lapin.extensions` in settings) is not enough here, because the engine must know what to load before any interface connects. One folder per version means an update unpacks next to the running version, and the switch happens at the next start.

### 1.3 Compatibility: a contract version (new)

There is no contract version today. `ILapinModule` (`engine/src/Platform/Lapin.Platform/Module.cs`) has only `SchemaVersion`, which versions the module's part of the project file. `ProductManifest.Extensions` (`Wire.cs`) exists but nothing reads it. Two versions are added:

- **Engine:** `Lapin.Platform.LapinContract { Major, Minor }`, starting at 1.0. Minor goes up when the platform's public surface gains something; Major goes up when a public type an extension may use changes or goes away. `module.json` gets `"contract": "1.x"`. The loader refuses a Major it does not have and a Minor newer than its own.
- **Interface:** `SDK_VERSION` in the new `@lapin/sdk` (§3.2), with the same rule. The manifest has `"sdk": "1.x"`.
- **Module-to-module APIs:** planning-plus uses `Lapin.Planning`'s public types (`Plan`, `Cpm`, `CpmResult`, `TaskElements`, `PlanningModule.Solve`, `ReadResolved`). Each open module that extensions may build on gets an `"api": N` in its `module.json`, raised when its public surface breaks. An extension's `module.json` says `"requires": {"planning": "^1", "cost": "^1"}`. A bare list `["planning"]` still means "any".
- The manifest's `lapin` range (`>=0.1.X`) stays, and both checks apply. Lapin's version is `0.1.<commit count>`, so a range alone does not show a break. The contract and api numbers do.

### 1.4 `requires`

`requires` is checked as `ModuleLoader.Load` checks it today ("Module „x” needs „y”, which the product does not have."). The difference: a failed check **disables the extension** with a notice in Extensions → Installed, and the engine still starts. planning-plus needs `planning` and `cost`, and through them `phases` and `architecture`. Editions without those (lapin-architecture, lapin-urban) show it as "needs Planning and Cost".

## 2. Engine loading

### 2.1 At start (desktop host)

`engine/src/Host/Lapin.Host/Program.cs` calls `ModuleLoader.Load(--modules-dir, product.Modules)`. A second step comes after it: `ExtensionLoader.Load(userDir, product, loadedModules)`. For each enabled entry in `modules.json` it:

1. Reads the stored manifest and checks its `package.signature` against the store keys. **The engine gets its own copy of `STORE_KEYS`** (today only the interface has them, in `ui/src/platform/extensions/keys.ts`). A test keeps the two lists equal, as `manifest.ts` and `tools/lib.mjs` are kept equal now. The engine verifies with .NET's Ed25519 if the target framework has it, otherwise with a port of the interface's RFC 8032 verifier. To check at S2.
2. Hashes every file against `FILES.sha256`. A wrong file disables the extension. Hashing a few MB is a few milliseconds.
3. Checks `contract`, the `api` requirements and `requires`.
4. Loads the assembly into **its own `AssemblyLoadContext`** (`ExtensionLoadContext`, not collectible). Its `Load(name)` returns the Default context's copy of any assembly the host or a product module has already loaded: `Lapin.Model`, `Lapin.Platform`, `Lapin.Planning` and the BCL. This keeps `ILapinModule` one type and works whatever version the extension was built against. Everything else is loaded from the extension's own folder. The extension's folder is **not** added to `ModuleLoader.SearchDirs`, so its private dependencies never reach other modules, and two extensions can carry different versions of the same NuGet package.
5. Runs `Register` against a **recording registry** and commits the recorded calls to the real `ModuleCatalog` only if `Register` returned without throwing. A `TypeLoadException` or `MissingMethodException` (an extension built against an API that has changed) then disables that one extension and the engine still starts. Today a module's `Register` writes straight into the catalog (`ModuleCatalog.cs`, `m.Register(new Registry(c, m.Id))`).

The collectible option was rejected. A module's delegates sit in the catalog, the session and its caches, so unloading would need all of them dropped. Install, update and uninstall therefore **take effect at the next start**. The shell can restart the engine and reload the window on request ("Restart Lapin now"); `run_engine` already restarts the engine.

Commands the extension registers are named under its id (`planning-plus.lbs`). They get one undo step each, `CommandEntry.Module`, and they are not in `EngineDispatcher.AllowedRemote`, so a shared session's guest cannot run them. Its commands reach `session.catalogue` through the catalogue embedded in its assembly (`CommandCatalogue.ModuleResource`, which `OfModules` reads for every loaded module's assembly). That mechanism already serves premium modules. The extension's build runs `scripts/command-catalogue.mjs --root <its engine/src> --out …`.

### 2.2 Trimming, single-file, AOT

The desktop host is published untrimmed (`Lapin.Host.csproj`: `PublishTrimmed=false`, "Modules are found by reflection"), as a single file (`scripts/build-product.mjs`: `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`). It loads its own modules from a folder with `LoadFromAssemblyPath` already, so extension assemblies load the same way. The whole framework ships with it, so an extension can use any BCL or ASP.NET API. AOT is not used and must stay off while extension modules exist. Extensions target `net10.0` and reference the contract assemblies with `Private="false"`, as `../lapin-planning`'s `Lapin.PlanningPro.csproj` does.

### 2.3 The web edition

The browser engine (`engine/src/Host/Lapin.Browser`) is trimmed and builds its modules in (`scripts/product.mjs`, `engineModules`). Its `Resolving` hook loads assemblies from bytes for its lazy parts, but an extension would reach APIs the trimmer removed. **Extension modules with an engine half are desktop only** (`desktopOnly`, as Speckle is). In the browser, Extensions shows them with "Desktop app only" and no Install button, as for scripts. A project that holds their content opens in the browser as in any edition without the module (§4.4). Extensions with only an interface half could later run in the browser too; that is not in this plan.

### 2.4 macOS, Windows, Linux

- **macOS.** The bundle is signed with the hardened runtime (`build-product.mjs`: `hardenedRuntime: true, entitlements: 'entitlements.plist'`). `desktop/src-tauri/entitlements.plist` **already has** `com.apple.security.cs.allow-jit`, `allow-unsigned-executable-memory` and `disable-library-validation`; the last is there for manifold's native library, which the single-file host extracts. Managed assemblies are not covered by library validation in any case: the runtime reads them as data and JIT-compiles them, and code signing does not check them. **So a signed or unsigned managed extension loads in today's notarized app with no entitlement change.** Native libraries would load too, because library validation is off. That is why stage 2 refuses them: Lapin's own check (the store signature and `FILES.sha256`) would be the only gate, and a native library can do things the managed runtime cannot. Recommendation: keep the entitlements as they are (manifold needs them). If an extension ever needs a native library, the store signs it with Lapin's Developer ID at publication (`codesign`, on the Mac, like the updater key). Quarantine: the engine downloads files itself (`extensions.fetch`) and writes them; that does not set `com.apple.quarantine` the way a browser download does. To verify at S7 on a clean machine.
- **Windows.** No signing requirement for managed DLLs loaded by the process. To test at S7: Smart App Control and Defender with an unsigned extension DLL under `%APPDATA%`, the installer build and the portable zip.
- **Linux.** No checks. The AppImage mounts read-only, but extensions live in the user's data folder, so that does not matter. To test at S7 on the .deb, .rpm and AppImage.

## 3. Interface loading

### 3.1 When

Interface modules are compiled into the bundle (`virtual:lapin-modules`, `ui/vite.config.ts`), and `ui/src/platform/modules.ts` computes `modules` once, when it is imported. Loading them at runtime and adding them to a live interface would touch every caller of that list. It is not needed: `ui/src/main.tsx` already awaits `restoreUserSettings()` before `import('./boot')`. **The plug-ins load there**, before `modules.ts` is evaluated:

1. A small first call to the engine, `extensions.modules`, returns the extensions the engine loaded: `{id, version, sdk, js, css, phrases}`, the texts read from the files it has just verified.
2. Each `js` is imported from a `blob:` URL. Its default export is a `LapinModule` (`ui/src/platform/module.ts`, the same contract as built-in modules), which goes into a registry that `modules.ts` appends to its list. Ribbon tabs, tools, `views` (the LBS pane is `views.lbs`, as `gantt` is), `dialogs`, `propertySections`, `planOverlays` and the rest work unchanged.
3. A plug-in that throws while loading, or asks for an `sdk` the app does not have, is left out, and Extensions → Installed shows why.

Why the engine serves the files: the engine is then **the only verifier**, for both halves. The webview's CSP (`tauri.conf.json`: `script-src 'self'`) needs `blob:` added to `script-src` and nothing else: no new Rust code, no custom protocol, no asset-protocol scope over the user's folder. The cost is that a plug-in must be **one file** (no chunks of its own; Vite `inlineDynamicImports`). That is fine at planning-plus's size, a few hundred KB. A Tauri custom protocol (`lapin-ext://`) serving verified files is the alternative if plug-ins grow. Deferred.

### 3.2 Shared libraries: `@lapin/sdk` and a global registry

Premium modules today import whatever they need by path (`@lapin/app/store`, `@lapin/engine/session`, `@lapin/modules/core/PendingDialog`, `@lapin/modules/planning/schedule`), because they are compiled with the app. A plug-in cannot do that, and it must not ship its own React: two React copies break hooks.

- The host publishes `globalThis.__lapin = { version: 1, libs: { react, 'react-dom', 'react/jsx-runtime', zustand, 'lucide-react', three }, sdk: {...} }` before plug-ins load.
- `@lapin/extension-kit` (a Vite preset, in the open repo) builds a plug-in as ESM, rewriting those imports to reads from `__lapin`, and gives the TypeScript types. An import map was considered and rejected: it needs an inline script under the CSP, and whether WebKitGTK on Ubuntu 22.04 supports import maps is unverified. The registry works in every webview.
- **`@lapin/sdk` v1** is a curated façade, `ui/src/platform/sdk.ts`, versioned by `SDK_VERSION`. Its first contents are what planning-plus imports, measured on the branches: `execute`, `query`, `useSession` (engine/session); `useUi`, `select`, `notify`, `focusPane` (app/store); `useLang`, `t` (i18n); `promptText` (shell/prompts); `FieldPicker`, `PendingDialog`; `saveFile`, `safeFileName` (engine/files); `openImportTable`. Plus `module(id)`, which returns what an open module exports for extensions (§6.3), and its `api` number.
- CSS: the plug-in's CSS goes in one `<style data-ext="<id>">`. Its class names must start with its id. The validator checks this; the tokens of `theme.css` are the styling API.

### 3.3 i18n

The translator loads languages lazily (`ui/src/i18n/index.ts`, `LOADERS`, `loadLanguage`). planning-pro adds its phrases by mutating each language's table when it is imported (`../lapin-planning/ui/src/modules/planning-pro/i18n.ts`). A plug-in ships `ui/i18n/<lang>.json`: `{phrases: {en: tr}, patterns: [[regexSource, template]]}`, where a template uses `{1}`, `{2}` for the groups, because JSON cannot hold planning-pro's functions. A new `addPhrases(lang, table)` in `i18n/index.ts` merges them when the language loads. As planning-pro does it, a phrase the host already translates keeps the host's translation. The validator requires one table per language of `ui/src/i18n` (15 besides English today), following the standing rule that new strings come in every language.

### 3.4 Sandboxing: decided, no sandbox

An `iframe` sandbox (what plan-extensions §5 expected for JS) would cut the plug-in off from what makes it useful: panes in the layout, the Gantt's selection, plan and 3D overlays, the ribbon and the properties panel. Its engine half is unrestricted C# in any case, so sandboxing only the JS would give **no real protection**. **Decision: plug-ins run in the app's context with full power, like the C# half. Trust rests on review, the signature, permissions shown at install, and revocation (§4).** Plug-ins with an interface half only, for anyone, would get sandboxing in a later stage if ever wanted. Not here.

## 4. Trust and security

### 4.1 Review and signature

- **Who may publish one:** in stage 2, kind `module` is open only to authors the maintainer lists (`MODULE_AUTHORS` in `tools/lib.mjs`), starting with Ciuntuc BIM Studio. Partners come later.
- **The review:** "the code is read by Claude and approved by the user" means, for a module, its **full source** at a tagged commit. The pull request names the repository and the commit. CI builds the package **reproducibly** from that commit: `dotnet build -p:Deterministic=true -p:ContinuousIntegrationBuild=true`, a pinned SDK, and Vite with no timestamps. The SHA-256 must equal the submitted package's. Claude's review is a written checklist in the pull request: what it reads and writes, network, processes, files outside the project, and reflection or dynamic loading. Then the user approves. Only then does `publish.mjs` sign it.
- **The signature check:** in the interface at install (as now) and in the engine at **every start** (§2.1). A package that is not signed, or whose signature is wrong, never loads. A user's own catalogue may list unsigned content packs (plan-extensions §10), but **never unsigned modules**. A developer mode (`LAPIN_DEV_EXTENSIONS=<folder>`, the engine started by `pnpm dev`) loads a folder unsigned, never in a release build.

### 4.2 Permissions

The manifest's `permissions` gets one new value: `full` ("Runs code with everything you can do on this computer: read and change your files, use the network, start programs"). It is required for kind `module`, and the install dialog shows it in plain words. It is honest: nothing restricts a C# module. The other permissions remain as descriptions of what the module does.

### 4.3 Revocation, updates, uninstall

- **Revocation list:** `revoked.json` (signed like the index, `lapinbim.com/extensions/revoked.json`) lists `{id, versions, reason}`. It is read with the catalogue and kept on the computer. A revoked module is **disabled at the next start**, with its reason shown. Content packs keep today's rule: what is installed stays (plan-extensions §5, §12.5). Code is different.
- **Update:** download, verify, unpack beside the running version, switch `modules.json` at the next start. The old version's folder is removed the start after that.
- **Uninstall:** `pendingRemoval`. The folder is deleted at the next start, before anything loads; Windows locks loaded DLLs.

### 4.4 Projects that use an extension that is not there

The core already keeps the content of an absent module (decision 484, `docs/overrides.md` "Content of an absent module"):

- the package records the module that wrote each class (`PackageManifest.Classes`), and an edition without that module keeps its entities and writes them back as they came (`Session.ModuleOf`, `_foreignClasses`, `Session.AbsentModules()`);
- an entity of a platform class the module owns carries `ownerModule` and is kept as it is (`ModuleOwned`, `AbsentModules`);
- its text parts are written back unchanged (`PackageForeign.ModuleParts`).

An extension module is just another absent module there. Two gaps:

1. `AbsentModules()` counts classes and owned entities only. Cash flow's terms are an attribute on the estimate (`CashFlowSettings.Attr`), so a project with only those would not be reported. Fix: `PackageModule` gets `Extension: {id, version}?`, written for every extension module loaded at save, and `project.open` returns `absentExtensions`. The interface then says "This project uses Planning+ 1.2 (extension). Install it from Extensions…" with a button that opens that extension's page in Extensions.
2. Attributes owned by an extension on core entities (a task's procurement tag) are kept because `CloneAttrs` keeps unknown keys. A test (`AbsentExtensionTests`) checks that a round trip through an edition without planning-plus keeps them all.

## 5. Licence

- `license` stays required. For planning-plus it is `LicenseRef-PlanningPlus` (a placeholder name), with its text in `LICENSE.txt` in the package and on the store page. `isSpdx` already accepts `LicenseRef-…` (plan-extensions §9.1).
- New optional manifest field `eula: {file, version}`. Installing then shows the text with **I accept** / Cancel (a click-through). The acceptance is kept as `{id, eulaVersion, at}` in `lapin.extensions`; a new `eula.version` asks again at update.
- Price 0: no `purchase`. `PAID_KINDS` stays without `module` until the user decides; the machinery of §12 (Gumroad, `/api/licensed/…`) works for it then unchanged.
- **The source and the MIT rule:** the five branches were made in this MIT repository but never merged. The extension's code moves to a **separate repository** (`Ciuntuc-BIM-Studio/lapin-planning-plus`) and **never lands on `main`**. Only the core hooks (§6.1) do, under MIT. The branches are deleted after the move.
- **What the user must decide** (no legal text is written here): the licence type (proprietary freeware, source-available, or an open licence after all); the copyright holders (Ciuntuc BIM Studio SRL alone, or with Yoram Mass); his credit line and role (co-author, scientific adviser); whether it allows commercial use, redistribution and modification; warranty and liability wording (indicative schedules, financial figures in cash flow); whether the EULA is required or the licence alone is enough; the law and jurisdiction (Romania); whether a future paid version keeps the free one.

## 6. The split: core hooks and the extension

The principle: **a project's numbers and links must not depend on whether an extension is installed.** What changes a schedule's dates, a task's elements or an estimate's totals stays in the open modules. What is a derived view, a dialog or an extra tool goes to the extension.

### 6.1 Stays in the open `planning` / `cost` (merged to `main`, MIT)

| From | What | Why |
|---|---|---|
| loe-alap | Level of effort and ALAP in `Cpm` (`IsLoe`, `Leaves`, the ALAP pass), `PlanTask.Loe/EffortFrom/EffortTo`, `constraint: "alap"`, the MS Project and IFC reading and writing, the Gantt's LOE bar and Properties fields | They change every date: inseparable from CPM |
| loe-alap | Time-related estimate items (`TimeRelated`, `EstimateLine.Per/Span`, the chapter, `MeasureUnits` additions, `ScheduleTimes.Length`) | They change the estimate's total |
| lbs | `planning.updateTasks` and the shared `UpdateTask` | A generic command |
| procurement | The `chart` attribute (`Plan.ChartKey`, `InCharts`), `phase` in the exports, the `Task` order as a double | Generic task data |
| pbs | The rule's `products` and `locations` terms in `TaskRule`, their resolution (`Breakdowns.Pbs`, `CodeOf`, `InLocations`), which classification is the PBS (read), `byProduct` in the unlinked report, TaskCheck naming products, MS Project Text2 | They decide a task's elements |
| pbs | The IFC classification-import fix (`IfcImport.cs`: entity numbers read as such; codes of a system the project lacks give way) | A core bug fix |
| cashflow | `ICertifiedProgress` and `CertifiedValue` (`Lapin.Platform/CashFlowProgress.cs`) | The hook planning-pro fills |

### 6.2 Moves to the extension `planning-plus`

| From | Engine | Interface |
|---|---|---|
| lbs | `Lbs.cs`, `PlanningLbs.cs` (the `lbs` query, `lbsView`, `lbsSheet`, `LbsSheet : ISheetContent`, its class `Lbs.Class` now owned by planning-plus) | `LbsView.tsx`, `lbs.ts`, the ribbon tools, its CSS |
| pbs | `setPbs`, `pbsStarter` (indicative), `rulePreview` counts (`Breakdowns.Counted`, `Locations` for pickers) | `BreakdownDialog.tsx`, `BreakdownPickers.tsx`, `breakdown.ts` |
| procurement | `Procurement.cs` and the chain code from `Generator.cs`, behind the new contributor hook (§6.3) | the generator's Procurement section |
| cashflow | `CashFlow.cs`, `CashFlowSettings.cs`, `setCashFlow`, `cashFlow`, `cashFlowXlsx`, `cashFlowCsv`, the "Cash flow" `IScheduleSource` | `CashFlowView.tsx`, `cashFlow.ts`, Construction → Cash flow |

Command ids change with the move (`cost.cashFlow` → `planning-plus.cashFlow`). This costs nothing because the branches are unmerged: no saved project or script uses them yet. A project made while the module was absent does not have its ids changed later.

### 6.3 Registration points the core needs

1. **Generator contributors** (engine): `IGeneratorContributor { string Key; void Contribute(GenContext ctx, JsonObject? spec) }`, registered with `r.Service`. It is called after the steps, with a `Task` factory and the step tasks, and returns tasks and links. `GenSpec` keeps unknown keys (`procurement`) as they came. Tasks a contributor made carry `gen: "<key>/…"`. **Regeneration without that contributor keeps them** rather than removing them as no longer generated. That keeps the schedule independent of what is installed.
2. **Generator sections** (interface): `planning` exports `addGeneratorSection({id, label, component, read, write})`, which GenerateDialog shows as a section (procurement).
3. **Module exports for extensions** (interface): a new `LapinModule.exports?: Record<string, unknown>` plus `api`, read through `sdk.module(id)`. Planning exports `gantt` helpers (`dateOf`, `dayOf`, `ticks`, `showDate`, `namesOf`, `dragDays`), `openGantt` and the task types. Cost exports `money`, `shortMoney`, `axisOf`, `niceStep`, `useEstimateData`, `formOnSheet` and `openCostTime`. Today's extension files import these by path.
4. **Views, ribbon, dialogs, sheets:** no new hook. `views`, tabs merged by id (planning-plus adds groups to the Construction and Planning tabs), `dialogs`, `ISheetContent` and `IScheduleSource` already take another module's additions.
5. **Product files:** products list engine modules only. An extension is never in `products/*.json`; it comes from the user's folder. `ProductManifest.Extensions` is removed, because nothing reads it.

## 7. Stages

Each stage can ship on its own. Sizes are rough (S under a day, M 1–3 days, L 3–6 days).

| Stage | What | Size | Tests |
|---|---|---|---|
| **S1** Manifest | kind `module` in `manifest.ts`, `tools/lib.mjs` and `schema/manifest.schema.json` (`engine`, `ui`, `contract`, `sdk`, `full`, `eula`, `MODULE_AUTHORS`); `validate.mjs` checks the zip's layout, `FILES.sha256`, no native code, one phrase table per language, CSS prefix; `LapinContract`, `api` in module.json | M | `manifest.test.ts`, `lib.test.mjs` (good and bad packages) |
| **S2** Engine loader | `ExtensionLoader` (signature, hashes, ALC, recording registry, `requires`/`contract`/`api`), `modules.json`, `extensions.install/uninstall/modules` in `EngineHub`, the engine's `STORE_KEYS`; Extensions → Installed: Enable, Disable, Restart now | L | `ExtensionLoaderTests`: a test module in a fixture zip: loads; wrong hash, wrong signature, newer contract, missing requirement, `Register` that throws → disabled, engine runs; two extensions with different versions of one dependency |
| **S3** Interface loader | `extensions.modules` before boot, blob import, `__lapin` registry, `@lapin/sdk` v1, `addPhrases`, CSS, CSP `blob:`; `@lapin/extension-kit` | L | vitest: a fixture plug-in registers a tab, a view and phrases; a throwing plug-in is left out; SDK version refused |
| **S4** Core hooks | §6.1 merged from the branches (MIT), `IGeneratorContributor` with keep-when-absent, `addGeneratorSection`, `LapinModule.exports`, `PackageModule.Extension`, `absentExtensions` | M | port the branches' core tests (`PlanningEffortTests`, `CostTimeRelatedTests`, the pbs rule tests); `AbsentExtensionTests` |
| **S5** planning-plus | the new repo, the §6.2 code moved, renamed ids, built with the kit, its tests (`PlanningLbsTests`, `CashFlowTests`, `PlanningProcurementTests`, `PlanningBreakdownTests` and the vitest files) run against a lapin-pro checkout, as `../lapin-planning`'s do | L | its own; a round trip without it |
| **S6** Store and docs | reproducible build in the catalogue's CI, `publish.mjs` for modules, `revoked.json`, the store page (licence, "Runs code with full access", "Desktop app only"); the user guide: Extensions page and the planning-plus pages (EN, RO) with scenes run against the installed extension; `docs/history.md`, `docs/decisions-pending.md` | M | `catalogue.test.mjs`, `site/build.test.mjs`, guide scenes |
| **S7** Platforms | install, run, update, uninstall on the notarized macOS app, the Windows installer and portable zip, .deb/.rpm/AppImage; quarantine, Smart App Control | M | a manual checklist in `docs/release-windows.md` and its siblings |

Order: S1 → S2 → S3 → S4 (S4 can run beside S2 and S3) → S5 → S6 → S7. Until S5, the five branches stay unmerged.

## 8. Risks

1. **API drift.** planning-plus leans on planning's internals, which change often. Mitigations: `api` numbers, the recording registry (no crash, only a disabled extension), and a release check that loads every catalogue module and runs its smoke test before a Lapin build is published.
2. **Full power.** A malicious or careless module can do anything the user can. The answer is people and process: author allow-list, full source review, reproducible build, signature, revocation.
3. **Restart to apply.** Install, update and uninstall need a restart. That is a small cost for a desktop app, and it is said in the dialog.
4. **The engine's Ed25519.** It must match the interface's byte for byte, which the shared test vectors check.
5. **The SDK surface.** Too small and plug-ins copy code; too large and every refactor breaks them. It starts from what planning-plus measured and grows by Minor versions.
6. **Licence on code born in an MIT repo.** The extension code must never be merged, and its history goes to the new repository. Whether a commit authored there needs a statement of ownership is for the user and Yoram Mass to settle.
7. **Webview differences.** Blob module imports in WKWebView, WebView2 and WebKitGTK 2.36+ are expected to work. To check at S3 on all three.

## 9. Open questions for the user

1. The licence of planning-plus and its EULA (§5, the list there), and Yoram Mass's credit.
2. Its name: "Planning+" (`ro.ciuntucbimstudio.planning-plus`), or another.
3. Is time-related cost in the core (recommended, §6) or in the extension?
4. Are partners' modules allowed later, or only ours? (`MODULE_AUTHORS`)
5. Should the extension also be available as a built-in module of a private edition (products with `"external"`), or only through the store?
6. Should it stay free with no limit in time, or does a paid version come later (then `module` joins `PAID_KINDS`)?
7. The source repository: private, or public under its own licence?
