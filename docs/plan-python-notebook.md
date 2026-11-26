# Notebook Python în Lapin (celule Cod, Markdown, Prompt AI Ollama, Terminal și conversie la Graf): plan

Cerut de utilizator (25.11.2026): trecerea editorului Python la o arhitectură de tip **Jupyter notebook** cu celule de cod Python, celule de prompt AI (conectate la modelele locale Ollama) care alimentează celulele de cod, celule de text (Markdown), păstrarea terminalului (consolă REPL + output + erori + Recorder) și posibilitatea de a transforma celulele de cod în **grafuri vizuale** (`Lapin.Graphs`).

## 1. Principii și decizii de arhitectură

1. **Un singur format de fișier: `.py` cu delimitatori `# %%` (stil Jupytext / VS Code).**
   - `# %%` (sau `# %% [code]`) — celulă de cod Python.
   - `# %% [markdown]` — celulă de text Markdown (liniile comentate cu `# `).
   - `# %% [prompt]` — celulă de prompt AI (liniile comentate cu `# `), asociată celulei de cod imediat următoare.
   - Un fișier `.py` fără `# %%` se deschide automat ca o singură celulă de cod și se salvează fără `# %%` (compatibilitate 100% înapoi cu scripturile existente, `LapinScript`, `scripts/<nume>.py` în `.lapin`, trust pe SHA-256, `lapin.ui.tool`, `Open in external editor` și rulare externă).
   - Output-urile celulelor sunt efemere (în sesiunea curentă) și nu se scriu în `.py`, ca să nu invalideze hash-ul de încredere.

2. **Vedere de modul (`views.scripting`), ca `Graphs`.**
   - Se deschide în split lateral lângă 3D/plan (și poate fi detașată în fereastră Tauri separată), oferind înălțime verticală pentru celule.
   - **Terminalul** rămâne mereu disponibil în partea de jos a vederii (redimensionabil): *Console* (output global cronologic + tracebacks + REPL `>>>` pe același `namespace` al kernelului) și *Recorder* (cu buton **Insert as cell**).

3. **Kernel persistent + `echo` per celulă + undo atomic.**
   - `lapinbim.kernel` păstrează deja un `namespace` comun între rulări; adăugăm `echo: true` pentru celule ca ultima expresie să se afișeze automat (ca în Jupyter).
   - *Run cell* (`Shift+Enter`) = 1 pas de undo; *Run all* = 1 singur pas de undo (toate celulele de cod combinate într-o singură tranzacție `doc.begin`/`doc.commit`).
   - Doar celula de cod activă montează CodeMirror 6; cele inactive afișează cod static, montând editorul instant la clic.

4. **Celula Prompt AI cu Ollama (refolosire `ui/src/assistant/ollama.ts`).**
   - Refolosește transportul local (`fetchTransport` + `shellTransport` Rust pe Windows) și setările Ollama, cu setare dedicată `codeModel` (implicit primul instalat din `qwen3-coder:30b`, `qwen2.5-coder`, `gemma4:12b`) schimbabilă direct din bara notebook-ului.
   - Mini-loop (max 3 pași) cu 2 tool-uri read-only (`search_catalogue`, `model_overview`) pentru a preveni halucinațiile de API `lapinbim`, plus contextul celulelor de deasupra și ultima eroare a celulei țintă (**Fix with AI**).
   - Streaming direct în celula de cod țintă; **nu execută niciodată cod automat** — utilizatorul verifică și rulează.

5. **Conversia Celulă Cod → Graf (`Lapin.Graphs`, format 2).**
   - Convertor determinist (parser de sintaxă Python → `GraphJson` format 2): variabile de intrare (`number`, `integer`, `text`, `point` cu `palette`), `range(...)` → `range`, expresii aritmetice → `formula`, apeluri `lapin.<mod>.<cmd>(...)` → `command:<mod>.<cmd>` cu fire legate automat și layout topologic.
   - Salvează un `LapinGraph` nou și îl deschide direct în panoul **Graphs** cu previzualizare fantomă (graful rezultat este C# nativ și merge și pe Web).
   - Dacă celula conține logică ne-convertibilă mecanic (`if/else` complex, biblioteci externe), raportează clar liniile nesuportate și oferă fallback **Convert with Ollama** validat automat prin `graphs.evaluate`.

## 2. Etape de implementare

| Faza | Conținut | Fișiere |
|---|---|---|
| **1. Notebook + Kernel + Terminal** | Parser/serializer `# %%` (`notebook.ts`), `echo` în `kernel.py` + `ScriptHost.cs`, `Markdown.tsx` extins, `ScriptPanel` reproiectat ca `views.scripting` cu celule Cod/Markdown, *Run cell* / *Run all* / *Restart & Run all*, Terminalul jos + Recorder *Insert as cell*. | `ui/src/modules/scripting/notebook.ts`, `ScriptPanel.tsx`, `store.ts`, `index.tsx`, `ScriptDock.tsx`, `python/lapinbim/kernel.py`, `engine/src/Host/Lapin.Host/ScriptHost.cs`, `ui/src/assistant/Markdown.tsx`, `ui/src/theme.css` |
| **2. Celula Prompt AI (Ollama)** | `codeModel` în `ollama.ts`, generator AI `ai.ts` cu `search_catalogue` + `model_overview` + context celule anterioare, streaming în celula de cod țintă, buton **Fix with AI** pe erorile de celulă. | `ui/src/modules/scripting/ai.ts`, `ui/src/assistant/ollama.ts`, `ScriptPanel.tsx` |
| **3. Celulă Cod → Graf** | Convertor determinist Python → `GraphJson` v2 (`toGraph.ts`) cu auto-layout topologic + fallback opțional Ollama validat cu `graphs.evaluate` + salvare `graphs.save` și deschidere în `Graphs`. | `ui/src/modules/scripting/toGraph.ts`, `ScriptPanel.tsx` |
| **4. Teste și verificare** | Teste Vitest pentru `notebook.ts`, `ai.ts`, `toGraph.ts`, `Markdown.tsx`, plus `pnpm typecheck`, `pnpm test` și testele Python. | `ui/src/modules/scripting/*.test.ts`, `python/tests/test_kernel.py` |
