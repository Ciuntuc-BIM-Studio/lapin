/**
 * Manage → Hatch patterns (docs/plan-material-graphics.md, stage 7): the built-in patterns (read-only, Duplicate) and the project's
 * (kind, units, uses), a line-family editor with a live preview (`hatch.preview` with the pattern inline, at a scale chosen), rename
 * (the places that follow it are counted), delete (asking `hatch.usage` first), Export and Import .pat, and My patterns (this
 * computer's files and the firm folders').
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Copy, FileDown, FileUp, Plus, Trash2, X } from 'lucide-react';
import { Dialog } from '../../shell/Dialog';
import { confirmAction, phrase } from '../../shell/prompts';
import { notify } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { FirmFolderFiles } from './FirmFolderFiles';
import { forgetHatchPreviews } from './hatchCache';
import { leaveHatchPatterns, useHatchEditor } from './hatchEditorOpen';
import { UNITS_HELP, UNITS_LABEL, type HatchUnits } from './hatchUnits';
import {
  MAX_DASHES, MAX_FAMILIES, MODEL_SCALES, PAPER_SCALES, addDash, addFamily, createArgs, dashKind, deletePattern, draftOf, draftProblem, freeName, newDraft, parseNum, patchFamily,
  previewArgs, realSizeNote, removeDash, removeFamily, renameImpact, setDashKind, setDashLength, skippedNote, updateArgs, withOrigin,
  type DashKind, type Draft, type Family, type PatternDef, type PatternKind,
} from './hatchPatternModel';
import { forgetPatterns, keepPatterns, myPatterns } from './myPatterns';

export interface PatternRow { name: string; units?: HatchUnits; description?: string; solid?: boolean; project?: boolean; kind?: PatternKind }

/** The .pat files brought in: the project gets the patterns, the collection on this computer keeps the file. */
export async function importPatText(name: string, text: string): Promise<boolean> {
  const r = await execute('hatch.import', { text }) as { count: number; errors: string[] } | null;
  if (!r) return false;
  forgetHatchPreviews();
  keepPatterns(name, text);
  notify(phrase('{n} hatch pattern(s) loaded{skipped}, and kept in My patterns on this computer.', { n: r.count, skipped: r.errors.length ? phrase(', {n} line(s) skipped', { n: r.errors.length }) : '' }));
  return true;
}

const SIZE = 60;
const KIND_LABEL: Record<PatternKind, string> = { solid: 'Solid', lines: 'Lines', tile: 'Tile', batt: 'Batt' };

export function HatchPatternsDialog() {
  const nodes = useSession((s) => s.nodes);
  const open = useHatchEditor();
  const [list, setList] = useState<PatternRow[]>([]);
  const [selected, setSelected] = useState<string | null>(open.select);
  const [def, setDef] = useState<PatternDef | null>(null);
  const [draft, setDraft] = useState<Draft | null>(open.fresh ? newDraft() : null);
  const [fresh, setFresh] = useState(open.fresh);
  const [filter, setFilter] = useState('');
  const [mine, setMine] = useState(myPatterns);
  const [bump, setBump] = useState(0);

  const close = leaveHatchPatterns;

  useEffect(() => { void query<PatternRow[]>('hatch.list').then((r) => r && setList(r)); }, [nodes, bump]);

  // The pattern chosen, read in full (a new pattern has none).
  useEffect(() => {
    if (!selected || fresh) return;
    let live = true;
    void query<PatternDef>('hatch.get', { name: selected }).then((r) => { if (live && r) { setDef(r); setDraft(draftOf(r)); } });
    return () => { live = false; };
  }, [selected, fresh, nodes, bump]);

  const taken = useMemo(() => list.map((p) => p.name), [list]);
  const shown = list.filter((p) => !filter.trim() || p.name.toLowerCase().includes(filter.trim().toLowerCase()));
  const builtIns = shown.filter((p) => !p.project), project = shown.filter((p) => p.project);
  const editable = fresh || (def?.project ?? false);

  const choose = (name: string) => { setFresh(false); setSelected(name); setDef(null); setDraft(null); };
  const startNew = () => { setFresh(true); setSelected(null); setDef(null); setDraft(newDraft()); };

  const refresh = () => { forgetHatchPreviews(); setBump((b) => b + 1); };

  const save = async () => {
    if (!draft) return;
    if (fresh) {
      const r = await execute('hatch.create', createArgs(draft)) as { name: string } | null;
      if (r) { refresh(); setFresh(false); setSelected(r.name); notify('Pattern made.'); }
      return;
    }
    if (!def) return;
    const r = await execute('hatch.update', updateArgs(def.name, draft)) as { name: string; repointed?: number } | null;
    if (!r) return;
    refresh();
    setSelected(r.name);
    notify(r.repointed ? phrase('Pattern saved: {n} place(s) now name {name}.', { n: r.repointed, name: r.name }) : 'Pattern saved.');
  };

  const duplicate = async () => {
    if (!def) return;
    const copy: Draft = { ...draftOf(def), name: freeName(`${def.name}-COPY`, taken) };
    if (!copy.tile && copy.families.length === 0) { notify('This pattern has no line families to copy.'); return; }
    const r = await execute('hatch.create', createArgs(copy)) as { name: string } | null;
    if (r) { refresh(); setFresh(false); setSelected(r.name); }
  };

  const remove = async () => {
    if (!def) return;
    const result = await deletePattern(def.name, {
      usage: async (n) => ((await query<{ count: number }>('hatch.usage', { name: n }))?.count ?? null),
      confirm: (text, vars) => confirmAction(text, vars),
      remove: async (n, force) => (await execute('hatch.delete', { name: n, ...(force ? { force: true } : {}) })) != null,
    });
    if (result === 'deleted') { refresh(); setSelected(null); setDef(null); setDraft(null); }
    else if (result === 'failed') notify(useSession.getState().error ?? 'The pattern could not be deleted.');
  };

  const exportPat = async () => {
    const names = project.map((p) => p.name);
    if (!names.length) { notify('The project has no pattern of its own to export.'); return; }
    const r = await query<{ text: string; skipped: string[] }>('hatch.exportPat', { names });
    if (!r) return;
    const note = skippedNote(r.skipped);
    if (note) notify(phrase(note.text, note.vars));
    const wrote = await saveFile(new TextEncoder().encode(r.text), `${safeFileName('Hatch patterns')}.pat`, 'text/plain');
    if (wrote) notify('Patterns exported.');
  };

  const importPat = async () => {
    const f = await pickFile('AutoCAD hatch patterns', ['pat']);
    if (!f) return;
    if (await importPatText(f.name, new TextDecoder().decode(f.bytes))) { setMine(myPatterns()); refresh(); }
  };

  const problem = draft ? draftProblem(draft, taken, fresh ? undefined : def?.name) : null;
  const dirty = !!draft && (fresh || !def || JSON.stringify(draft) !== JSON.stringify(draftOf(def)));

  return (
    <Dialog title="Hatch patterns" onClose={close} className="wide hatch-patterns-dialog" dataDialog="hatchPatterns" testId="hatch-patterns">
      <div className="hatch-patterns">
        <div className="hp-list" data-testid="hp-list">
          <input aria-label="Search patterns" placeholder="Search" value={filter} onChange={(e) => setFilter(e.target.value)} />
          <div className="row">
            <button type="button" className="btn compact" data-testid="hp-new" onClick={startNew}><Plus size={13} />New pattern</button>
            <button type="button" className="btn compact" onClick={() => void importPat()} data-testid="hp-import"><FileUp size={13} />Import .pat</button>
            <button type="button" className="btn compact" onClick={() => void exportPat()} data-testid="hp-export"><FileDown size={13} />Export .pat</button>
          </div>
          <span className="eyebrow">This project</span>
          <ul className="plain hp-items">
            {project.length === 0 && <li className="muted small">None yet: make one, or import a .pat file.</li>}
            {project.map((p) => <PatternItem key={p.name} p={p} on={!fresh && selected === p.name} onPick={() => choose(p.name)} />)}
          </ul>
          <span className="eyebrow">Built in</span>
          <ul className="plain hp-items">{builtIns.map((p) => <PatternItem key={p.name} p={p} on={!fresh && selected === p.name} onPick={() => choose(p.name)} />)}</ul>
          <MyPatterns mine={mine} onChange={() => { setMine(myPatterns()); refresh(); }} />
        </div>

        <div className="hp-editor" data-testid="hp-editor">
          {!draft ? <p className="muted small">Choose a pattern, or make a new one.</p> : (
            <>
              <div className="row hp-head">
                <label className="grow">
                  <span className="muted small">Name</span>
                  <input aria-label="Pattern name" data-field="hp-name" data-no-i18n value={draft.name} disabled={!editable}
                    onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
                </label>
                {!fresh && def && <span className="units-badge muted small">{KIND_LABEL[def.kind]}{def.project ? '' : ' · built in'}</span>}
                <span className="grow" />
                {!fresh && def && !def.project && <button type="button" className="btn" data-testid="hp-duplicate" onClick={() => void duplicate()}><Copy size={13} />Duplicate</button>}
                {!fresh && def?.project && <button type="button" className="btn" data-testid="hp-delete" onClick={() => void remove()}><Trash2 size={13} />Delete</button>}
              </div>
              {!fresh && def?.project && <RenameNote was={def.name} name={draft.name} uses={def.uses ?? 0} />}
              <label>
                <span className="muted small">Description</span>
                <input aria-label="Description" value={draft.description} disabled={!editable} onChange={(e) => setDraft({ ...draft, description: e.target.value })} />
              </label>
              <div className="row">
                <span className="muted small">Units</span>
                <select aria-label="Units" data-field="hp-units" value={draft.units} disabled={!editable} onChange={(e) => setDraft({ ...draft, units: e.target.value as HatchUnits })} title={UNITS_HELP[draft.units]}>
                  <option value="paper" title={UNITS_HELP.paper}>{UNITS_LABEL.paper}</option>
                  <option value="model" title={UNITS_HELP.model}>{UNITS_LABEL.model}</option>
                </select>
                {!fresh && def && def.kind !== 'tile' && <span className="muted small">{phrase('Named in {n} place(s).', { n: def.uses ?? 0 })}</span>}
              </div>

              {def?.kind === 'solid' && !fresh && <p className="muted small">The solid fill has no lines.</p>}
              {def?.kind === 'batt' && !fresh && <p className="muted small">The batt insulation is drawn by Lapin as loops along its band: it has no line families.</p>}
              {draft.tile && <p className="muted small" data-testid="hp-tile-note">{phrase('A tile pattern drawn in a cell of {w} × {h} mm. Draw it again and Save as hatch pattern to change its lines.', { w: +draft.tile.width.toFixed(3), h: +draft.tile.height.toFixed(3) })}</p>}
              {!draft.tile && def?.kind !== 'solid' && def?.kind !== 'batt' && <Families draft={draft} editable={editable} onChange={(families) => setDraft({ ...draft, families })} />}

              <HatchPreview draft={draft} />

              <div className="row hp-foot">
                {problem && <span className="muted small hp-problem" data-testid="hp-problem">{problem}</span>}
                <span className="grow" />
                {editable && <button type="button" className="btn primary" data-testid="hp-save" disabled={!!problem || !dirty} onClick={() => void save()}>{fresh ? 'Make pattern' : 'Save'}</button>}
              </div>
            </>
          )}
        </div>
      </div>
    </Dialog>
  );
}

function PatternItem({ p, on, onPick }: { p: PatternRow; on: boolean; onPick: () => void }) {
  return (
    <li>
      <button type="button" className={`hp-item${on ? ' on' : ''}`} data-pattern={p.name} onClick={onPick} title={p.description}>
        <span className="grow" data-no-i18n>{p.name}</span>
        <span className="muted small">{p.kind ? KIND_LABEL[p.kind] : ''}{p.units === 'model' ? ' · ' : ''}{p.units === 'model' ? UNITS_LABEL.model : ''}</span>
      </button>
    </li>
  );
}

function RenameNote({ was, name, uses }: { was: string; name: string; uses: number }) {
  const r = renameImpact(was, name, uses);
  if (!r.renamed) return null;
  return <p className="muted small" data-testid="hp-rename-note">{r.uses > 0 ? phrase('Renaming moves {n} place(s) to the new name.', { n: r.uses }) : 'No place names it yet: nothing else changes.'}</p>;
}

// ------------------------------------------------------------ the line-family editor

function NumCell({ value, onCommit, label, className }: { value: number; onCommit: (v: number) => void; label: string; className?: string }) {
  return (
    <input className={`num ${className ?? ''}`} aria-label={label} inputMode="decimal" key={value} defaultValue={String(value)}
      onBlur={(e) => { const v = parseNum(e.target.value); if (v === null) { e.target.value = String(value); return; } if (v !== value) onCommit(v); }}
      onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
  );
}

function Families({ draft, editable, onChange }: { draft: Draft; editable: boolean; onChange: (f: Family[]) => void }) {
  const fs = draft.families;
  return (
    <div className="hp-families" data-testid="hp-families">
      <table className="layer-table">
        <thead><tr><th>Angle</th><th>Origin x</th><th>Origin y</th><th title="Shift of the next line along the line">Delta x</th><th title="Distance to the next line, across it">Delta y</th><th>Dashes</th><th /></tr></thead>
        <tbody>
          {fs.map((f, i) => (
            <tr key={i} data-family={i}>
              <td><NumCell label="Angle" value={f.angle} onCommit={(v) => onChange(patchFamily(fs, i, { angle: v }))} /></td>
              <td><NumCell label="Origin x" value={f.origin[0]} onCommit={(v) => onChange(patchFamily(fs, i, { origin: withOrigin(f, 0, v) }))} /></td>
              <td><NumCell label="Origin y" value={f.origin[1]} onCommit={(v) => onChange(patchFamily(fs, i, { origin: withOrigin(f, 1, v) }))} /></td>
              <td><NumCell label="Delta x" value={f.deltaX} onCommit={(v) => onChange(patchFamily(fs, i, { deltaX: v }))} /></td>
              <td><NumCell label="Delta y" value={f.deltaY} onCommit={(v) => onChange(patchFamily(fs, i, { deltaY: v }))} /></td>
              <td><DashList dashes={f.dashes} onChange={(dashes) => onChange(patchFamily(fs, i, { dashes }))} /></td>
              <td className="row-tools">{editable && fs.length > 1 && <button type="button" className="icon mini" title="Remove this family" onClick={() => onChange(removeFamily(fs, i))}><X size={12} /></button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {editable && <button type="button" className="btn compact" data-testid="hp-add-family" disabled={fs.length >= MAX_FAMILIES} onClick={() => onChange(addFamily(fs))}><Plus size={13} />Add a family</button>}
    </div>
  );
}

/** The dashes of one family as a list: each a dash, a gap or a dot with its length; empty is a continuous line. */
function DashList({ dashes, onChange }: { dashes: number[]; onChange: (d: number[]) => void }) {
  const add = (k: DashKind) => onChange(addDash(dashes, k));
  return (
    <div className="hp-dashes">
      {dashes.length === 0 && <span className="muted small">continuous</span>}
      {dashes.map((d, i) => (
        <span key={i} className="hp-dash" data-kind={dashKind(d)}>
          <select aria-label="Dash kind" value={dashKind(d)} onChange={(e) => onChange(setDashKind(dashes, i, e.target.value as DashKind))}>
            <option value="dash">dash</option><option value="gap">gap</option><option value="dot">dot</option>
          </select>
          {d !== 0 && <NumCell label="Length" className="hp-dash-len" value={Math.abs(d)} onCommit={(v) => onChange(setDashLength(dashes, i, v))} />}
          <button type="button" className="icon mini" title="Remove" onClick={() => onChange(removeDash(dashes, i))}><X size={10} /></button>
        </span>
      ))}
      <span className="hp-dash-add">
        <button type="button" className="btn compact" disabled={dashes.length >= MAX_DASHES} onClick={() => add('dash')}>+ dash</button>
        <button type="button" className="btn compact" disabled={dashes.length >= MAX_DASHES} onClick={() => add('gap')}>+ gap</button>
        <button type="button" className="btn compact" disabled={dashes.length >= MAX_DASHES} onClick={() => add('dot')}>+ dot</button>
      </span>
    </div>
  );
}

// ------------------------------------------------------------ the live preview

/** The draft drawn by the engine (`hatch.preview` with the pattern inline), at a scale chosen. */
function HatchPreview({ draft }: { draft: Draft }) {
  const [scale, setScale] = useState(1);
  const [segs, setSegs] = useState<number[]>([]);
  const choices = draft.units === 'model' ? MODEL_SCALES : PAPER_SCALES;
  const shownScale = choices.includes(scale) ? scale : draft.units === 'model' ? 50 : 1;
  const key = JSON.stringify([draft.units, draft.tile, draft.families, shownScale]);
  const latest = useRef('');
  const ask = useCallback(() => {
    latest.current = key;
    void query<{ segments: number[] }>('hatch.preview', previewArgs(draft, shownScale, SIZE)).then((r) => { if (latest.current === key) setSegs(r?.segments ?? []); });
  }, [key]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { const t = window.setTimeout(ask, 120); return () => window.clearTimeout(t); }, [ask]);
  const px = 4;
  const note = realSizeNote(draft.units, shownScale);
  return (
    <div className="hp-preview" data-testid="hp-preview">
      <svg width={SIZE * px} height={SIZE * px} viewBox={`0 0 ${SIZE * px} ${SIZE * px}`}>
        <rect x={0.5} y={0.5} width={SIZE * px - 1} height={SIZE * px - 1} fill="var(--paper, #fff)" stroke="var(--ink-muted)" />
        <path d={Array.from({ length: Math.floor(segs.length / 4) }, (_, i) => {
          const [ax, ay, bx, by] = segs.slice(i * 4, i * 4 + 4).map((v) => v * px);
          return ax === bx && ay === by ? `M${ax} ${SIZE * px - ay}h1` : `M${ax} ${SIZE * px - ay}L${bx} ${SIZE * px - by}`;
        }).join('')} stroke="var(--ink)" strokeWidth={0.8} fill="none" />
      </svg>
      <div className="hp-preview-side">
        <label className="row">
          <span className="muted small">{draft.units === 'model' ? 'View scale' : 'Scale (×)'}</span>
          <select aria-label="Preview scale" data-field="hp-scale" value={shownScale} onChange={(e) => setScale(Number(e.target.value))}>
            {choices.map((s) => <option key={s} value={s}>{draft.units === 'model' ? `1:${s}` : `×${s}`}</option>)}
          </select>
        </label>
        <span className="muted small">{phrase('A square of {n} mm on paper.', { n: SIZE })}</span>
        {note && <span className="muted small" data-testid="hp-real-size">{phrase(note.text, note.vars)}</span>}
      </div>
    </div>
  );
}

// ------------------------------------------------------------ My patterns: this computer's files and the firm folders'

function MyPatterns({ mine, onChange }: { mine: ReturnType<typeof myPatterns>; onChange: () => void }) {
  const choices = ['All my patterns', ...mine.map((f) => f.name)];
  return (
    <div className="my-patterns" data-testid="hp-mine">
      <span className="eyebrow" title="The .pat files you imported, kept on this computer and offered from the firm folders">My patterns</span>
      <ul className="plain">
        {mine.map((f) => (
          <li key={f.name} className="row">
            <span className="grow small" title={f.names.join(', ')} data-no-i18n>{f.name} <span className="muted">· {f.names.length}</span></span>
            <button type="button" className="btn compact" onClick={() => void execute('hatch.import', { text: f.text }).then(() => { forgetHatchPreviews(); onChange(); })}>Add to this project</button>
            <button type="button" className="icon mini" title="Forget this file (projects keep what they use)" onClick={() => { forgetPatterns(f.name); onChange(); }}><Trash2 size={12} /></button>
          </li>
        ))}
      </ul>
      <FirmFolderFiles ext="pat" onPick={async (f) => { if (await importPatText(f.name, new TextDecoder().decode(f.bytes))) onChange(); }}
        save={{ disabled: mine.length === 0, choices, get: async (c) => {
          const files = c && c !== choices[0] ? mine.filter((f) => f.name === c) : mine;
          return files.length ? { text: files.map((f) => f.text).join('\n'), name: files.length === 1 ? files[0].name.replace(/\.pat$/i, '') : 'My patterns' } : null;
        } }} />
    </div>
  );
}

