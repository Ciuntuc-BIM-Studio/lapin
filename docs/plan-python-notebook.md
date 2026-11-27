# The Python notebook: plan

Asked by the user on 25.11.2026 (in Romanian): the Script panel's editor to become a **Jupyter-like notebook**, with Python cells, AI prompt cells (on the local Ollama models) that write the code cells, Markdown text cells, the terminal kept (the console, its output and errors, and the recorder), and a way to turn a code cell into a **visual graph** (`graphs`, decision 527). Decision 897 (`docs/decisions-pending.md`).

Open, MIT, in the `scripting` module (`ui/src/modules/scripting`), still a developer extra (Preferences → Developer extras), desktop only for Python and Ollama. It builds on the Python scripting of decision 489 (`docs/plan-python-scripting.md`) and changes none of its contracts: a script is still a `.py` file, a project's script still a `LapinScript` with its SHA-256 trust, a run still one undo step.

**Status (27.11.2026): stages 1–4 done**, written by the user on 26.11.2026 and brought onto main with the interface's strings in every language, the coding models made a setting, and tests for the kernel's and the host's new calls. What is left is in §5.

## 1. Decisions

1. **One file format: `.py` with `# %%` markers** (as Jupytext and Visual Studio Code write them).
   - `# %%` (or `# %% [code]`): a Python cell.
   - `# %% [markdown]`: a Markdown cell, its lines commented with `# `.
   - `# %% [prompt]`: an AI prompt cell, its lines commented with `# `, tied to the code cell right after it.
   - A `.py` file without `# %%` opens as one code cell and is saved without markers, byte for byte. Every script written before, a `LapinScript`, `scripts/<name>.py` in a `.lapin` package, the trust on its SHA-256, `lapin.ui.tool` headers, Open in external editor and running from a terminal all work as they did.
   - What the cells print is kept for the session only and never written into the `.py`, so a run does not change the file or its trust hash.
2. **A view of the module (`views.scripting`), like Graphs.** View → Script opens it beside the plan or the 3D (the window split in two) or focuses it when it is open; like any view it can go into a window of its own. The library (Mine, Project's) folds away on the left. The **terminal** stays at the bottom, resizable and hideable: Terminal (every run's output, the tracebacks, and the `>>>` line on the same kernel namespace) and Recorder (with **Insert as cell**).
3. **One kernel, echo per cell, one undo step.**
   - `lapinbim.kernel` already keeps one namespace between runs. A run may now ask for `echo`, and a cell's last expression is shown as in Jupyter.
   - **Run** on a cell (Shift+Enter or Ctrl+Enter) is one undo step; **Run all** is also one undo step (the code cells joined into one run, named after the notebook). **Restart and run all** checks that the notebook runs from the top on its own.
   - Each run of a cell gets its number (`[1]`, `[2]`…); a cell edited since its run is marked `•`. Only the active code cell mounts CodeMirror 6; the others show their code until clicked.
4. **The AI prompt cell, on Ollama** (reusing `ui/src/assistant/ollama.ts`: its transports, including the Rust shell transport on Windows, and the assistant's settings).
   - The model is the setting `codeModel`, picked in the notebook's header among the installed ones. The first time, it is the first installed of the **Coding models** list (the assistant's settings, `codeModels`; empty: the built-in default `qwen3-coder:30b`, `qwen2.5-coder:32b/14b/7b/latest`, `deepseek-coder-v2:latest`, `gemma4:12b`), else any model named `coder`, else the assistant's own choice.
   - A **context pack** (`contextPack.ts`, a few thousand tokens) instead of the whole catalogue: the catalogue's commands that fit the request (English and Romanian building terms expanded), two or three tested `lapinbim` recipes, the model's storeys, classes, types and selected elements, the code cells above, and the names the kernel holds (`script.inspect`).
   - Up to 3 rounds with two read-only tools (`search_catalogue`, `model_overview`), so the model looks up what it is not sure of instead of inventing it.
   - What it writes is checked against `session.catalogue` (`validate.ts`: unknown commands and unknown keyword arguments, with suggestions); when something is wrong, the model is asked once to repair it.
   - The code streams into the target cell and is **never run by itself**: the user reads it and runs it. **Fix with AI** on a cell that failed sends its code and its error.
5. **Code cell → graph** (`toGraph.ts`, graph format 2).
   - A deterministic converter: number, integer, text, bool, point and list assignments become input nodes (with their palette), `range(…)` a range node, arithmetic over names a formula node, `lapin.<module>.<command>(…)` the command's (or query's) node, wired to what it uses, simple `for` loops a list mapping on the command's inputs; laid out by depth.
   - The graph is saved as a new `LapinGraph` and opened in the Graphs panel with its ghost preview. It runs in the C# engine, so it works in the browser edition too.
   - Lines it cannot map (an `if`, a `while`, a `def`, another library) are listed with their line: the user may open the partial graph, or ask Ollama for the whole graph, which is checked with `graphs.evaluate` before it is saved.

## 2. Stages

| Stage | What | Files |
|---|---|---|
| **1. Notebook, kernel, terminal** | The `# %%` parser and writer, `echo` in the kernel and the host, Markdown headings and code blocks in `Markdown.tsx`, the panel as `views.scripting` with Python and Markdown cells, Run, Run all, Restart and run all, the terminal at the bottom, the recorder's Insert as cell | `notebook.ts`, `ScriptPanel.tsx`, `store.ts`, `index.tsx`, `ScriptDock.tsx`, `python/lapinbim/kernel.py`, `engine/src/Host/Lapin.Host/ScriptHost.cs`, `ui/src/assistant/Markdown.tsx`, `ui/src/theme.css` |
| **2. AI prompt cell** | `codeModel` and `codeModels` in the assistant's settings, `ai.ts` with the context pack, the two tools, the catalogue check and one repair, streaming into the cell, Fix with AI, `script.inspect` | `ai.ts`, `contextPack.ts`, `validate.ts`, `ui/src/assistant/ollama.ts`, `AssistantPanel.tsx`, `ScriptHost.cs`, `kernel.py` |
| **3. Code cell → graph** | The deterministic converter with its layout, the Ollama fallback checked with `graphs.evaluate`, `graphs.save` and opening it in Graphs | `toGraph.ts`, `store.ts`, `ScriptPanel.tsx` |
| **4. Tests and checks** | Vitest for `notebook.ts`, `ai.ts`, `contextPack.ts`, `toGraph.ts`, `validate.ts`; the kernel's echo and inspect (`python/tests/test_kernel.py`) and the host's (`ScriptHostTests`); the strings in every language (`i18n.test.ts`) | `ui/src/modules/scripting/*.test.ts`, `python/tests/test_kernel.py`, `engine/tests/Lapin.Engine.Tests/ScriptHostTests.cs` |

## 3. The host and the kernel

- `script.run {…, echo?}`: `echo` is passed to the kernel, which shows the run's last expression as it does for the console. A console line always echoes; a script without `echo` does not.
- `script.inspect`: the kernel's names, `{alive, busy, vars: [{name, type, len?, preview}]}`, at most 40, each preview cut at 80 characters, without modules, `_names` and `lapin`. It never starts Python (`alive: false`), answers `busy: true` while a run is on (the kernel reads one job at a time), and gives up after 1.5 s. The kernel answers with a line `{"type": "inspect", "id", "vars"}`, which the host takes out of the output stream.

## 4. The interface's words

Lapin's own lines (the status while the model works, what the console says after a generation or a repair, the graph's notices) are whole phrases with their numbers and names, put into every language by `ui/src/i18n` patterns (`Cell written by … in … s (… tokens): review it, then Shift+Enter runs it.` and the rest). What a script prints, the code, the model's names and the badges are shown as they are (`data-no-i18n`). The textareas' placeholders go through `t()`, since the translator leaves textareas alone.

## 5. Left open

- The user guide's scenes (`python-console`, `python-recorder`, `python-tools` in `scripts/user-docs/scenes.mjs`) were written for the bottom panel. The panel keeps `data-panel="script"` and its `data-action`s, so they should still find it, but the pictures now show the notebook beside the plan; the scenes were not run here (they need the guide's run with Python). A scene for the AI cells would need Ollama on the machine that makes the guide.
- View → Script opens or focuses the notebook; it no longer closes it (the view closes as any view does).
- The old bottom panel's CSS (`.script-panel`, `.script-body`, `.script-main`, `.script-console`) is no longer used.
- The context pack's recipes and the Romanian and English term list are coded; per the standing principle they could also become the user's (a recipe saved from a cell).
- Translations other than Romanian are made by models and not reviewed by native speakers.
