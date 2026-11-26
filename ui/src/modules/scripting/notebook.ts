/**
 * Python notebook format & pure operations (docs/plan-python-notebook.md):
 * stores notebooks as standard `.py` files using Jupytext / VS Code percent delimiters:
 *
 *   # %%            (or # %% [code])   → Python code cell
 *   # %% [markdown]                    → Markdown text cell (lines prefixed with `# `)
 *   # %% [prompt]                      → AI prompt cell (lines prefixed with `# `), feeds the next code cell
 *
 * Plain `.py` files without `# %%` open as a single code cell and serialize back verbatim without adding markers,
 * keeping 100% compatibility with existing scripts, LapinScript, SHA-256 trust, lapin.ui.tool, and external editors.
 */

export type CellKind = 'code' | 'markdown' | 'prompt';

export interface CellOutput {
  kind: 'out' | 'err' | 'ok';
  text: string;
}

export interface NotebookCell {
  id: string;
  kind: CellKind;
  source: string;
  /** Execution counter from the kernel (1, 2, …) for code cells; null if never run or after restart. */
  execCount?: number | null;
  /** True when the cell was edited since its last run. */
  stale?: boolean;
  /** Ephemeral per-cell outputs (never serialized into the .py file). */
  outputs?: CellOutput[];
  /** 1-based line number inside this cell where the last error occurred. */
  errorLine?: number | null;
  /** Summary of the last error in this cell (used by "Fix with AI"). */
  lastError?: string | null;
}

let nextCellSeq = 1;
export function newCellId(): string {
  return `c${nextCellSeq++}`;
}

export function createCell(kind: CellKind, source = ''): NotebookCell {
  return { id: newCellId(), kind, source, execCount: null, stale: false, outputs: [], errorLine: null, lastError: null };
}

/** Matches a percent-format cell delimiter line: `# %%`, `# %% [markdown]`, `# %% [prompt]`, `# %% [code]`. */
const DELIMITER_RE = /^#\s*%%(?:\s+\[(code|markdown|prompt)\])?(?:\s+(.*))?$/i;

/** Strip leading `# ` or `#` comment prefix from a markdown/prompt cell body. */
function uncommentBlock(lines: string[]): string {
  const out = lines.map((line) => {
    if (line.startsWith('# ')) return line.slice(2);
    if (line === '#') return '';
    if (line.startsWith('#')) return line.slice(1);
    return line;
  });
  return trimBlankEdges(out).join('\n');
}

/** Prefix every line with `# ` (or `#` for empty lines) for markdown/prompt serialization. */
function commentBlock(text: string): string {
  if (!text) return '#';
  return text.split('\n').map((line) => (line.length ? `# ${line}` : '#')).join('\n');
}

function trimBlankEdges(lines: string[]): string[] {
  let start = 0;
  let end = lines.length;
  while (start < end && lines[start].trim() === '') start++;
  while (end > start && lines[end - 1].trim() === '') end--;
  return lines.slice(start, end);
}

/** Whether raw `.py` text uses percent-format cell delimiters. */
export function hasCellMarkers(code: string): boolean {
  return code.split(/\r?\n/).some((l) => DELIMITER_RE.test(l));
}

/**
 * Parse a `.py` file into notebook cells.
 * Plain scripts without `# %%` become a single code cell with the exact source preserved.
 */
export function parseNotebook(code: string): NotebookCell[] {
  const normalized = code.replace(/\r\n/g, '\n');
  const lines = normalized.split('\n');
  const markers: { index: number; kind: CellKind }[] = [];

  for (let i = 0; i < lines.length; i++) {
    const m = DELIMITER_RE.exec(lines[i]);
    if (m) {
      const tag = (m[1]?.toLowerCase() as CellKind | undefined) ?? 'code';
      markers.push({ index: i, kind: tag });
    }
  }

  if (markers.length === 0) {
    return [createCell('code', normalized)];
  }

  const cells: NotebookCell[] = [];
  // Any non-empty preamble before the first `# %%` (e.g. `# lapin: tool=...` header) stays as a leading code cell.
  if (markers[0].index > 0) {
    const pre = trimBlankEdges(lines.slice(0, markers[0].index));
    if (pre.length > 0) cells.push(createCell('code', pre.join('\n')));
  }

  for (let i = 0; i < markers.length; i++) {
    const start = markers[i].index + 1;
    const end = i + 1 < markers.length ? markers[i + 1].index : lines.length;
    const raw = lines.slice(start, end);
    const kind = markers[i].kind;
    const source = kind === 'code' ? trimBlankEdges(raw).join('\n') : uncommentBlock(raw);
    cells.push(createCell(kind, source));
  }

  return cells.length ? cells : [createCell('code', '')];
}

/**
 * Serialize notebook cells back to a valid `.py` file.
 * A single code cell without markers serializes as plain Python (no `# %%` added).
 */
export function serializeNotebook(cells: readonly NotebookCell[]): string {
  if (cells.length === 0) return '';
  if (cells.length === 1 && cells[0].kind === 'code' && !hasCellMarkers(cells[0].source)) {
    return cells[0].source;
  }
  const parts: string[] = [];
  for (const cell of cells) {
    if (cell.kind === 'code') {
      parts.push(cell.source.length ? `# %%\n${cell.source}` : '# %%');
    } else {
      parts.push(`# %% [${cell.kind}]\n${commentBlock(cell.source)}`);
    }
  }
  return `${parts.join('\n\n')}\n`;
}

/** Combine all non-empty code cells into a single executable Python script (for Run All in one undo step). */
export function combineCodeCells(cells: readonly NotebookCell[]): string {
  return cells
    .filter((c) => c.kind === 'code' && c.source.trim().length > 0)
    .map((c) => c.source)
    .join('\n\n');
}

/**
 * Find the target code cell index for a prompt cell at `promptIndex`:
 * returns the immediately following code cell if one exists (skipping markdown cells is not done—it must be next),
 * or null if a new code cell should be inserted right after `promptIndex`.
 */
export function targetCodeCellIndex(cells: readonly NotebookCell[], promptIndex: number): number | null {
  const next = promptIndex + 1;
  if (next < cells.length && cells[next].kind === 'code') return next;
  return null;
}

/**
 * Collect the Python code from all code cells strictly above `cellId`,
 * so the AI prompt cell knows which variables and imports are already in the notebook's namespace.
 */
export function codeContextAbove(cells: readonly NotebookCell[], cellId: string, maxChars = 4000): string {
  const idx = cells.findIndex((c) => c.id === cellId);
  const limit = idx >= 0 ? idx : cells.length;
  const chunks: string[] = [];
  for (let i = 0; i < limit; i++) {
    const c = cells[i];
    if (c.kind === 'code' && c.source.trim()) {
      chunks.push(`# Cell ${i + 1}\n${c.source.trim()}`);
    }
  }
  const joined = chunks.join('\n\n');
  return joined.length > maxChars ? joined.slice(joined.length - maxChars) : joined;
}

/** Insert a new cell at `index` (clamped). */
export function insertCellAt(cells: readonly NotebookCell[], index: number, cell: NotebookCell): NotebookCell[] {
  const at = Math.max(0, Math.min(cells.length, index));
  return [...cells.slice(0, at), cell, ...cells.slice(at)];
}

/** Move a cell from `index` by `delta` (-1 up, +1 down). */
export function moveCell(cells: readonly NotebookCell[], index: number, delta: -1 | 1): NotebookCell[] {
  const to = index + delta;
  if (index < 0 || index >= cells.length || to < 0 || to >= cells.length) return [...cells];
  const next = [...cells];
  const [item] = next.splice(index, 1);
  next.splice(to, 0, item);
  return next;
}

/** Remove a cell by id, ensuring at least one code cell remains. */
export function removeCell(cells: readonly NotebookCell[], id: string): NotebookCell[] {
  const next = cells.filter((c) => c.id !== id);
  return next.length ? next : [createCell('code', '')];
}
