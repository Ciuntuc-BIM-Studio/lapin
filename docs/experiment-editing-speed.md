# Experiment: editing speed and memory

> **Taken to main (09.11.2026, decisions 423–426).** The experiment ran on the branch `experiment/editing-speed` behind switches; the user then approved four things, implemented for real without switches: **(1)** the cutter search once per model revision and class (decision 423), **(2)** the recovery journal's `fsync` off the session's thread (424), **(3)** undo and redo taking the builds they had, found again by exact versions and kept to a budget (425), and **(4)** the seam plane tables kept only while a diff is made, and the geometry of storeys nobody works on let go above a size and built again when asked for (426). **Not taken:** `sendcut`, the geometry cache, compact attributes, the boolean memo, workstation GC. What is in the code is `SessionTuning` (`Session.Builds.cs`), `Entity.Version` and `ModelStore.ReadStamp`, `ModuleCatalog.CandidatesOf`, and the `Writer` in `JournalStore.cs`; the check and the benchmark are the opt-in tests in `EditingSpeedTests.cs`; the numbers in the sections below are the experiment's, and the section "Taken to main" at the end gives what the real code measures.

Branch `experiment/editing-speed` (local, from origin/main at 46b2821e). Research only: nothing here goes to main until the user decides. The prototypes are behind `LAPIN_EXP=<list>` (`engine/src/Geometry/Lapin.Geometry/Exp.cs`); off is today's code path (the whole engine suite passes with them off).

## What was measured, and how reliable it is

- Harness: `engine/tests/Lapin.Engine.Tests/EditingSpeedBench.cs`, opt-in by environment variables; stage timers and per-stage allocation in `EditProf`; run by `scripts/edit-bench-run.sh`, summarised by `scripts/edit-bench-ab.mjs`. Release build, .NET 10, Server GC (except where said), a Mac with 8 performance cores.
- Edits go through the host's dispatcher exactly as the interface does (`doc.execute`, journal on, the diff encoded as the wire frame), and the time includes the encoding. Each repetition is a different edit (values differ), so no repetition is served from a memo it just filled. Undo and redo are measured too.
- Models: `testLapin.lapin` (3 walls, too small to tell anything), **bloc** (Bloc-P-4 recognised into native: 411 walls, 1,644 built elements), **gen-10** (generated, 10 storeys, 4,440 walls with 3,930 doors and 680 windows, 18,161 entities, 13,670 built elements). None of these files is in the repository.
- **Load.** The machine was quiet (load 2.6) for the first run only. Then other sessions built and ran, load around 15 to 40, and in round 3 up to about 100. So absolute times are inflated and noisy. The comparison is **base against each configuration, three rounds, configurations run one after another within a round** (not interleaved edit by edit). Ratios below are the median of the three rounds' ratios; the round values are in brackets in `build/speed/ab.txt` and where they disagree widely I say so. A ratio between 0.85 and 1.15 here means "no measurable difference".
- Memory is the managed heap after a forced GC; working set follows the GC mode and the other processes, so treat it as indicative.

## Where the time goes today (baseline)

gen-10, changing one wall type's core (3,240 walls follow, 7,560 elements rebuilt, 9,060 sent): about 4.6 s, 19.7 GB allocated, a 177 MB frame. CPU time summed over the cores was 41 s for the walls:

| Stage | CPU | Share |
|---|---|---|
| Booleans (manifold), openings, lintels, priorities, roof | 23 s | 55% |
| Who may cut a wall (`ModuleCatalog.For`) | 11 s | 28% |
| Joins | 1.5 s | under 4% |
| Seams, tessellation, filters | about 3 s | under 8% |

For small edits (move a window, 5 elements rebuilt, 18 sent): 32 to 38 ms on gen-10 and 9 ms on bloc, of which about 5 ms is the recovery journal's `fsync` on the session's thread, 2 to 3 ms the box grid built again for every diff, about 5 ms emitting. Opening gen-10 takes 4 to 5 s and allocates 14 to 20 GB; bloc about 0.2 to 0.6 s.

The sampling profile agrees: about 20% of the sampled time of a type change is inside manifold, another large share is garbage-collection suspension and waiting on the parallel build.

## Results per hypothesis

### Found by the baseline, not in the list: the cutter search (`LAPIN_EXP=cutters`)

Every wall built re-sorts all slabs and rebuilds their envelopes to see what may cut it. Doing that once per model revision and class (same candidates, same order, same reads recorded) gives:

- gen-10: type change x0.69 (4.6 s to 3.0 s; allocation 19.7 GB to 11.7 GB), wall height x0.69, drag x0.67, move window x0.77.
- bloc: type change x0.36, height x0.69, drag x0.74.
- Open of gen-10: 5.1 s to 3.5 s; allocation 20 to 12 GB.
- Cost: small (one cache in the catalog), but it makes each wall's read set larger (bloc 109,000 to 151,000 ids), which is memory and a risk to the rebuild rules; the equality check and the tests pass.

This is the best result of the experiment and it is not part of any of the four hypotheses.

### Journal (`journal`)

The `fsync` after every journal line moved off the session's thread (up to 100 ms exposed on power loss, none on a crash of Lapin). Saves a fixed 3 to 5 ms: bloc move window x0.33, height x0.49, drag x0.59; on gen-10 it is 5 to 10% of an edit (x0.93 to 0.95) and lost in noise on big edits. A durability trade-off the user should decide.

### Hypothesis 1, stepwise rebuild (`boolmemo`, `elemmemo`)

- The premise is mostly wrong on the "do" side. **Joins are under 4% of a wall's build, so keeping the plan joins when a height changes saves almost nothing.** The cost is the booleans (55%) and they take new input geometry whenever the edit changes the wall.
- **Booleans memoized by content hash (`boolmemo`)**: x0.77 to 0.99 on small edits, x0.61 (gen-10 height; rounds disagree 0.85/0.61/0.51), x0.83 drag, no effect on the type change (x1.01). It saves what a joined neighbour rebuilt with unchanged inputs would cut again. Costs memory (a table of solids, 128-bit hashes of every solid; gen-10 heap +70 MB).
- **Earlier builds kept per element and found again when what they read is as it was (`elemmemo`)**: nothing on "do" (x0.75 to 1.14), but it makes **undo and redo much faster**, because an undo puts back the same immutable entities: gen-10 type change undo/redo 5.0 s to 0.9 s (x0.16), height x0.20 to 0.27, move window x0.48 to 0.58; bloc x0.33 to 0.68 (drag only x0.85). The prototype prints by object identity, not exactly; a real one needs exact versions. **It costs memory**: 3 builds per element kept, gen-10 working peaks +10 to 20%, and it **defeats releasing unseen storeys** (hypothesis 3, below). It also needed a fix for shared-body instances and cannot cover elements reading a provision's cells without extra rules.

### Hypothesis 2, compact attributes

Measured on the side, not put behind the `Entity` contract. On the real models:

| | gen-10 | bloc |
|---|---|---|
| Attributes as a JSON tree after reading and walking them | 39.5 MB (15.3 MB while still lazy) | 24.6 MB (8.0 MB lazy) |
| Same as an interned binary blob with identical blobs shared | 1.8 MB | 17.5 MB (stored triangle arrays dominate) |
| Every number read (22,595 / 2,173 reads) | tree 0.6 ms, blob scan 0.3 ms | 0.07 / 0.4 ms |
| Clone all, compare all | clone 2 ms; tree compare 1.6 ms, blob 0.4 ms | |

The whole model (entities, relations, ids) is **74 MB of a 1,270 MB heap on gen-10 (6%)** and 17 of 104 MB on bloc. Reads are already about 27 ns each, and JSON work is about 5% of a build. Even a perfect compaction saves at most 3% of the heap and no measurable time; with 734 direct `.Attrs` uses and 446 `CloneAttrs` calls in the code, it would be a large change. **Not worth doing on this evidence.** (My first version of the measure undercounted the tree; this one parses afresh and walks.)

### Hypothesis 3, geometry off the managed heap

- Where the memory goes (gen-10, 1.27 GB managed): render float arrays about 108 MB, solids (Face, int arrays, Vec3 arrays) about 110 MB, seam plane tables about 70 MB, read sets about 37 MB, stored byte arrays about 105 MB (the harness' own frames), the rest small. 1.27 GB is mostly render data and solids of 51,320 parts and 798,644 faces, plus garbage not yet collected.
- **Releasing the geometry of unseen storeys (`ReleaseStoreys`)**: with 3 of 10 storeys kept, base heap 1,270 to 655 MB (-48%), and bringing one storey back (906 elements) took 270 to 830 ms. This works only without `elemmemo` (with it the heap stays at 1,200 MB).
- **Dropping the seam plane tables (`noplanes`)**: heap -200 MB on gen-10 (1,270 to 1,074 MB, working set 2.8 to 2.2 GB), edit speed unchanged in round 1 (x0.94 to 1.03); it recomputes the planes of neighbours on demand.
- Compact arrays instead of small objects (the real hypothesis) was **not built**. Face = 64 B, 800,000 of them is 50 MB, plus 33 MB of int arrays: a flat array layout could save about 100 MB of 1.27 GB. Moderate gain, large change (the `Solid` record is used everywhere). Not recommended before the two cheaper items above.
- **Workstation GC** (`DOTNET_gcServer=0`): managed heap 1,270 to 707 MB and working set 2.8 to 1.8 GB on gen-10 (peaks about equal), **but edits get slower**: gen-10 drag x1.21, type change x1.40 (round 3 at load 100: x3.8), bloc type change x1.24. The server GC is the right choice for speed; its memory price is about 1 GB.

### Hypothesis 4, a geometry cache in `.lapin` (`diskcache`)

A prototype sidecar `<project>.geocache` (straight solids and render items of walls, doors, windows, openings, slabs; mesh parts, instances and silhouettes left out and built on opening), each element keyed by a content hash of what it read; a stale or missing entry is just built.

| | opening, build | opening, from the cache |
|---|---|---|
| gen-10 (13,670 of 13,670 elements cached) | 5.1 s, 20 GB allocated | **0.53 s, 2.0 GB** |
| bloc (967 of 1,644 cached) | 0.17 s, 0.9 GB | 0.22 s (noise at load), **0.16 GB allocated** |

The cache file is 239 MB on gen-10 (60 MB deflated, 0.7 s to deflate) against a project of 2.2 MB, written in 0.8 s; on bloc 6.2 MB (1.5 MB deflated). It does not speed up editing, and **the diff frame (189 MB on gen-10, about 0.2 s to encode) remains**. Equality was checked on all four models, and a changed project is not served stale. It is also the only item that makes opening a 14,000-element model usable (10x), but it is a large derived file next to a 2 MB project, so it must be optional, outside the zip's SHA list, and rebuilt freely.

### `sendcut`: not sending what the interface already holds

Neighbours re-emitted for their seams, and dependents rebuilt the same, are skipped when their print is unchanged. Frames: gen-10 drag 1.5 MB to 60 KB, height 830 KB to 7 KB, bloc type change 2.0 MB to 1.36 MB (the rebuilt elements themselves still go). CPU: x0.77 to 0.99. The gain is on the wire and in the interface (not measured here, the largest unknown for 177 MB frames). **It fails 3 existing tests** (`DependencyTests.Joined_walls_and_a_door…`, `ClashFollowUpTests.Many_walls_and_runs…`, `ParallelBuildTests.An_edit_rebuilding_many…`) that read `diff.Render` to see what was rebuilt; a real version should add a separate "rebuilt" list to the diff for them.

## Combined (`all`)

gen-10: move window x0.49, height x0.33, drag x0.47, type change x0.61 (4.6 s to 3.5 s); undo/redo of the type change x0.16 (5 s to 0.9 s); open 5.1 s to 3.0 s (with the cache 0.5 s). bloc: x0.64 to 0.94 on "do" at that load (round 1: 0.24 to 0.48, three rounds disagree widely), undo/redo x0.27 to 0.6.

## Correctness

- `Switches_change_nothing_when_given`: the same 13 edits with undo, redo, undo-all and redo-all, on one session with the switches off and one with them on; after every step the geometry of every built element (parts, names, solids' vertices and faces, volumes, plan items, diagnostics, the render item as built) and a replica of what the interface would hold, fed only by the diffs, must be equal; once more after a full snapshot, then the project opened from the cache, and from a stale cache. **Identical on test, gen-small, bloc and gen-10** (67 comparisons of 13,670 elements on gen-10; total volume 24,929.190 m3). With `LAPIN_EXP=mutate` (an element taken from the memo without looking at what it read) the check fails, so it can catch a stale build. `noplanes` was not in these comparisons.
- Full engine suite (2,175 tests): switches off, 2,175 pass; `cutters,journal,boolmemo,elemmemo` on, 2,175 pass; `all` on, 2,172 pass, 3 fail (the `sendcut` tests above). The suite also found two real bugs in the prototypes, both fixed and committed (a journal handle kept open past a session's end; a shared-body instance taken from the memo, which lost its mesh on redo).

## What was not measured or built

- The cost in the interface of applying a diff (the frame is 177 MB for a type change on gen-10), and the effect of `sendcut` there.
- The browser (Web Worker, AOT): nothing here was built for it. `MemoBooleans`, the cache (file system) and the journal change are desktop prototypes; compact arrays and the memo would work in the browser, the cache would need the browser's storage.
- Models above 14,000 elements, other disciplines (roofs, MEP, Studio solids: the cutter cache and the memo were exercised only on walls, slabs, doors, windows), and a real session's mix of edits. The generated building is regular (all walls similar), so booleans are probably cheaper per wall than in a real project.
- Exact versioning for the memo (prototype uses object identity), and the cost of keeping read sets as small as today's.
- Rounds: three, under varying load; the ratios for small edits swing by a factor of two between rounds.

## Recommendation, in order

1. **Cutter search once per revision and class** (`cutters`). The largest gain (x0.7 on a type change, x0.36 on bloc, -40% allocation, open 30% faster), small and safe. Do it first, for real, with a test of its own; check the larger read sets.
2. **Journal `fsync` off the session's thread**, if the user accepts up to 100 ms of exposure on power loss. Worth it on small models (3 to 5 ms an edit); an explicit decision, not a default.
3. **Undo/redo from the kept builds** (`elemmemo`, redone with exact versions instead of identity). x0.16 to 0.5 for undo/redo and the "move a window back and forth" case; the cost is memory, so keep few builds and tie it to the heap budget. Not with storey release unless that is made to drop the memo for released storeys.
4. **Release unseen storeys' geometry and drop the plane tables** on very large models: -48% heap with 3 of 10 storeys, -200 MB for the planes at no speed cost; a storey comes back in 0.3 to 0.8 s. Do after 1 and 3.
5. **Stop sending what the interface has** (`sendcut`) once the interface's cost is measured and the diff has a separate "rebuilt" list for the tests.
6. **The geometry cache** only if opening large models matters to the users: 10x on gen-10, but a 100x file next to the project; optional and never the source of truth.
7. **Drop** compact attributes (at most 3% of the heap, no time) and the boolean memo as a general feature (x0.61 to 1.0, memory cost; the stepwise rebuild's premise did not hold: joins are under 4% of a build). Not recommended: workstation GC for editing (x1.2 to 1.4 slower), compact solid arrays before the above are done.

## Reproducing

`scripts/edit-bench-run.sh build | gen | check | tests | ab 3` (needs `modele/` files from the old Lapin: `Bloc-P-4.ifc`, `testLapin.lapin`); everything it makes goes under the ignored `build/speed`; `node scripts/edit-bench-ab.mjs build/speed/ab.jsonl` prints the tables. `LAPIN_EXP` is a comma list of `cutters, journal, boolmemo, elemmemo, sendcut, noplanes, diskcache, mutate` or `all`.

## Taken to main: what the real code measures

The code on main is the four items without switches (`SessionTuning`: `Reference` is the session as it was, kept only for the tests that compare). Two things changed on the way, both found by measuring the real code against `Reference` in alternation (three rounds, Release, a Mac, load 3 to 35; ratios are the median of the rounds' ratios):

- **Keeping earlier builds made every new edit of a small model slower** (10 to 40 ms on a model of a thousand entities: the heap they hold makes collections dearer, and the stamp is worked out at every build). So they are stamped, kept and looked up only from 5,000 entities (`MemoFromEntities`) and only on undo and redo. Below that a "do" edit costs what it did.
- **The memory estimate was 45% of the real heap**; it is now measured (a read is its set entry and its key's string, about 96 bytes; a face about 220), real/estimated 0.8 on gen-10, and the thresholds are in those terms (desktop: release above 640 MB, earlier builds 384 MB; browser: 160 and 96 MB). The seam face tables are kept for the last edit's neighbourhood only, which costs nothing in speed.

| (default / Reference, "do") | small (1,066 entities) | bloc (2,324) | gen-10 (18,161) |
|---|---|---|---|
| move a window | 0.95 | 1.06 (3.3 → 3.5 ms) | 0.88 |
| change a wall's height | 0.94 | 0.72 | 0.76 |
| drag a wall's end | 1.07 | 0.69 | 0.78 |
| change a wall type's core | 1.19 (load noise: 0.96 in an isolated 3-round run of four tunings) | 0.42 | 0.60 (4.4 s → 2.8 s) |
| undo or redo of the above | 0.8 to 1.1 | 0.4 to 0.7 | 0.13 to 0.26; type change 0.58 |
| open | 1.01 | 0.49 | 0.67 (3.9 s → 2.6 s) |
| save | 0.99 | 0.99 | 1.00 |

Heap on gen-10 (forced collections): 1,406 MB with `Reference` against 1,154 MB (seam tables trimmed); with the release threshold at 300 MB, 929 MB and 5,436 builds let go, a storey's first access after that about 0.25 s. At the default threshold (640 MB) gen-10, at about 470 MB estimated, does not release.

**Checks.** The equality check (`EditingSpeedTests`, `LAPIN_EDIT_BENCH_CHECK`) runs the same edits, undos, redos, all-undo, all-redo and a snapshot on `Reference` and on the defaults, on "kept" (earlier builds in any size) and on a squeezed tuning that releases and evicts all the time, and compares every element's geometry and the interface's copy of every render item (fed by the diffs alone): identical on small, bloc and gen-10. Four regular tests run it on a small model with every build, and test that an undo builds nothing, that a typed-again value is a new object, that the budget holds and that released storeys come back identical. The engine's 2,257 tests pass, `pnpm typecheck` and the 849 UI tests pass, and `pnpm build:web lapin` gives 13.7 MB at the first load and 14.6 MB with every lazy part (the figures it had), the engine starting in the browser in 0.8 s, `check:web` green.

**Not verified.** The interface's cost of applying a 177 MB frame; storey release inside the browser (the thresholds are scaled, not measured there); models above 18,000 entities; other disciplines' edits (roofs, MEP, Studio: they pass through the same cache and stamps but were exercised only by the existing tests).

