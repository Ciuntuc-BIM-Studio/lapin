/**
 * File → Optimize IFC… (docs/ifc-optimize.md): IFC files cleaned and lightened into copies named with an ending, one
 * after the other, each with its stages and a report (size, entities, triangles, bodies shared, orphans removed, the
 * largest deviation measured, the time). No project needed: it opens from the start screen too. The original is never
 * written; a copy already there is overwritten only when the user says so.
 */

import { useEffect, useState } from 'react';
import { FilePlus2, FolderOpen, ExternalLink, X } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { engine, engineInBrowser } from '../engine/session';
import { LONG_TIMEOUT_MS } from '../engine/client';
import { download, isDesktop, toBase64 } from '../engine/files';
import { t } from '../i18n';
import {
  addSources, change, cleanSuffix, copyName, fileName, folderOf, optionsOf, rememberChoice, rememberedChoice, sizeText, toleranceOf, useOptimize,
  type OptimizeChoice, type OptimizeItem, type OptimizeReport, type OptimizePreset, type OptimizeSource,
} from '../engine/ifcOptimize';

/** IFC files the user picks: paths on the desktop, bytes in a browser. */
async function pickSources(): Promise<OptimizeSource[]> {
  if (isDesktop()) {
    const dialog = await import('@tauri-apps/plugin-dialog');
    const r = await dialog.open({ multiple: true, directory: false, filters: [{ name: 'IFC', extensions: ['ifc', 'ifczip'] }] });
    const paths = Array.isArray(r) ? r : typeof r === 'string' ? [r] : [];
    return paths.map((path) => ({ name: fileName(path), path }));
  }
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    input.accept = '.ifc,.ifczip';
    input.onchange = async () => {
      const files = [...(input.files ?? [])];
      resolve(await Promise.all(files.map(async (f) => ({ name: f.name, data: toBase64(new Uint8Array(await f.arrayBuffer())) }))));
    };
    input.oncancel = () => resolve([]);
    input.click();
  });
}

async function confirmOverwrite(target: string): Promise<boolean> {
  const question = `${t('The copy already exists. Overwrite it? The original stays as it is.')}\n\n${target}`;
  if (isDesktop()) {
    const dialog = await import('@tauri-apps/plugin-dialog');
    return dialog.ask(question, { title: t('Optimize IFC'), kind: 'warning' });
  }
  return window.confirm(question);
}

const PRESETS: [OptimizePreset, string][] = [['fast', 'Fast (5 mm)'], ['balanced', 'Balanced (1 mm)'], ['precise', 'Precise (0.1 mm)'], ['custom', 'Custom tolerance']];

export function OptimizeIfcDialog() {
  const { items, folder, running } = useOptimize();
  const [c, setC] = useState<OptimizeChoice>(rememberedChoice);
  const close = () => { if (!running) useUi.setState({ dialog: null }); };
  const set = (patch: Partial<OptimizeChoice>) => setC((x) => ({ ...x, ...patch }));
  const suffix = cleanSuffix(c.suffix);
  const valid = suffix != null && toleranceOf(c) != null && items.length > 0;
  // The engine's stages for the file running.
  useEffect(() => engine.onEvent('progress', (d) => {
    const p = d as { stage?: string; done?: number; total?: number };
    const s = useOptimize.getState();
    if (!s.running) return;
    useOptimize.setState({ items: s.items.map((i) => (i.state === 'running' ? { ...i, stage: p.stage ?? i.stage, done: p.done ?? 0, total: p.total ?? 0 } : i)) });
  }), []);

  const update = (k: number, patch: Partial<OptimizeItem>) => useOptimize.setState((s) => ({ items: s.items.map((i, j) => (j === k ? { ...i, ...patch } : i)) }));
  const run = async () => {
    if (!valid) return;
    rememberChoice(c);
    useOptimize.setState({ running: true });
    const options = optionsOf(c);
    for (let k = 0; k < useOptimize.getState().items.length; k++) {
      const item = useOptimize.getState().items[k];
      if (item.state === 'done') continue;
      update(k, { state: 'running', stage: 'Reading the file', done: 0, total: 0, error: undefined, report: undefined });
      const src = item.source.data != null ? { name: item.source.name, data: item.source.data } : { path: item.source.path };
      const call = (overwrite: boolean) => engine.call<OptimizeReport & { exists?: boolean; target?: string }>('ifc.optimize',
        { ...src, options, suffix, ...(folder ? { folder } : {}), ...(overwrite ? { overwrite: true } : {}) }, { timeoutMs: LONG_TIMEOUT_MS });
      try {
        let r = await call(false);
        if (r.exists) {
          if (!(await confirmOverwrite(r.target ?? ''))) { update(k, { state: 'cancelled' }); continue; }
          r = await call(true);
        }
        if (r.data != null && r.name) {
          download(Uint8Array.from(atob(r.data), (ch) => ch.charCodeAt(0)), r.name, 'application/x-step');
          r = { ...r, data: undefined };
        }
        update(k, { state: 'done', report: r });
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        update(k, { state: msg.startsWith('Cancelled') ? 'cancelled' : 'failed', error: msg });
        if (msg.startsWith('Cancelled')) break;
      }
    }
    useOptimize.setState({ running: false });
  };
  const cancel = () => { void engine.call('cancel.long', {}).catch(() => undefined); };
  const chooseFolder = async () => {
    const dialog = await import('@tauri-apps/plugin-dialog');
    const r = await dialog.open({ directory: true, multiple: false });
    if (typeof r === 'string') useOptimize.setState({ folder: r });
  };
  const openFolder = async (path: string) => {
    try { const { invoke } = await import('@tauri-apps/api/core'); await invoke('open_path', { path: folderOf(path) }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const openInLapin = async (path: string) => {
    try { const { invoke } = await import('@tauri-apps/api/core'); await invoke('open_separately', { path }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const example = items[0]?.source.name ?? 'model.ifc';
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide optimize-ifc" role="dialog" aria-label="Optimize IFC" data-dialog="optimize-ifc" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Optimize IFC</span>
          <button type="button" className="icon" onClick={close} disabled={running} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">A copy of each file, cleaned and lighter. Its GlobalIds, names, property sets, quantities and relationships stay exactly as the file wrote them; only the geometry changes, and nothing is added. The original is never written.</p>
          <div className="optimize-files" data-optimize="files">
            {items.length === 0 && <p className="muted">No file yet: add one or more IFC files.</p>}
            {items.map((i, k) => <FileRow key={(i.source.path ?? i.source.name) + k} item={i} running={running}
              onRemove={() => useOptimize.setState((s) => ({ items: s.items.filter((_, j) => j !== k) }))}
              onFolder={isDesktop() ? openFolder : undefined} onOpen={isDesktop() ? openInLapin : undefined} />)}
            <button type="button" className="btn" data-optimize="add" disabled={running} onClick={() => void pickSources().then(addSources)}><FilePlus2 size={14} />Add files…</button>
          </div>
        </div>
        <div className="dialog-body exchange-options">
          <dl className="fields">
            <dt>Precision</dt>
            <dd>
              <select data-optimize="preset" value={c.preset} onChange={(e) => set({ preset: e.target.value as OptimizePreset })}>
                {PRESETS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
              </select>
            </dd>
            {c.preset === 'custom' && (
              <>
                <dt>Tolerance (mm)</dt>
                <dd><input data-optimize="tolerance" inputMode="decimal" value={c.toleranceMm} onChange={(e) => set({ toleranceMm: e.target.value })} /></dd>
              </>
            )}
            <dt>Ending</dt>
            <dd>
              <input data-optimize="suffix" style={{ width: "14ch" }} value={c.suffix} onChange={(e) => set({ suffix: e.target.value })} />
              <span className="muted small"> <span>The copy:</span> <span data-no-i18n>{copyName(example, c.suffix, c.zip)}</span></span>
            </dd>
            {isDesktop() && (
              <>
                <dt>Folder</dt>
                <dd>
                  {folder ? <span data-no-i18n title={folder}>{fileName(folder)}</span> : <span>Next to the original</span>}{' '}
                  <button type="button" className="btn mini" onClick={() => void chooseFolder()}><FolderOpen size={12} />Choose folder…</button>
                  {folder && <button type="button" className="icon mini" title="Next to the original" onClick={() => useOptimize.setState({ folder: null })}><X size={12} /></button>}
                </dd>
              </>
            )}
          </dl>
          <div className="optimize-checks">
            <Check on={c.simplify} set={(v) => set({ simplify: v })} name="simplify" label="Simplify meshes" note="fewer triangles, never further from the original than the tolerance, measured" />
            <Check on={c.clean} set={(v) => set({ clean: v })} name="clean" label="Clean meshes" note="duplicate vertices welded; degenerate and repeated triangles and unused coordinates dropped" />
            <Check on={c.share} set={(v) => set({ share: v })} name="share" label="Share identical bodies" note="written once as a representation map, each product a mapped item of it" />
            <Check on={c.orphans} set={(v) => set({ orphans: v })} name="orphans" label="Remove orphans" note="points, directions, styles and empty representations nothing references" />
            <Check on={c.round} set={(v) => set({ round: v })} name="round" label="Round coordinates to 0.01 mm" />
            <Check on={c.brepsToFaceSets} set={(v) => set({ brepsToFaceSets: v })} name="breps" label="B-reps as triangulated face sets (IFC4 and IFC4X3)" />
            <Check on={c.zip} set={(v) => set({ zip: v })} name="zip" label="Write .ifczip" />
          </div>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          {running && !engineInBrowser && <button type="button" className="btn" data-optimize="cancel" onClick={cancel}>Cancel</button>}
          {!running && <button type="button" className="btn" onClick={close}>Close</button>}
          <button type="button" className="btn primary" data-optimize="run" disabled={running || !valid} onClick={() => void run()}>{running ? 'Optimizing…' : 'Optimize'}</button>
        </footer>
      </div>
    </div>
  );
}

function Check({ on, set, name, label, note }: { on: boolean; set: (v: boolean) => void; name: string; label: string; note?: string }) {
  return (
    <label className="check-line">
      <input type="checkbox" data-optimize={name} checked={on} onChange={(e) => set(e.target.checked)} /> <span>{label}</span>
      {note && <span className="muted small"> <span>{note}</span></span>}
    </label>
  );
}

const STATES: Record<OptimizeItem['state'], string> = { waiting: 'Waiting', running: '', done: 'Done', failed: 'Failed', cancelled: 'Cancelled' };

function FileRow({ item, running, onRemove, onFolder, onOpen }: {
  item: OptimizeItem; running: boolean; onRemove: () => void; onFolder?: (path: string) => void; onOpen?: (path: string) => void;
}) {
  const r = item.report;
  const pct = item.total ? Math.round(((item.done ?? 0) / item.total) * 100) : null;
  return (
    <div className={`optimize-file ${item.state}`} data-optimize-file={item.state}>
      <div className="optimize-file-head">
        <strong data-no-i18n>{item.source.name}</strong>
        {item.source.path && <span className="muted small" data-no-i18n title={item.source.path}> {folderOf(item.source.path)}</span>}
        <span className="grow" />
        {item.state === 'running'
          ? <span className="muted small"><span>{item.stage ?? ''}</span>{pct != null && <span data-no-i18n> {pct} %</span>}</span>
          : <span className="muted small">{STATES[item.state]}</span>}
        {!running && item.state !== 'done' && <button type="button" className="icon mini" title="Remove" onClick={onRemove}><X size={12} /></button>}
      </div>
      {item.state === 'running' && <div className={`busy-bar${pct == null ? ' indeterminate' : ''}`}><span style={pct == null ? undefined : { width: `${pct}%` }} /></div>}
      {item.error && <p className="error-text small">{item.error}</p>}
      {r && (
        <>
          <dl className="fields optimize-report" data-optimize="report">
            <dt>File size</dt><dd data-no-i18n>{change(r.bytesBefore, r.bytesAfter, sizeText)}</dd>
            <dt>Entities</dt><dd data-no-i18n>{change(r.entitiesBefore, r.entitiesAfter)}</dd>
            <dt>Triangles</dt><dd data-no-i18n>{change(r.trianglesBefore, r.trianglesAfter)}</dd>
            <dt title="Identical bodies written once, as a representation map">Shared bodies</dt><dd data-no-i18n>{r.sharedBodies}</dd>
            <dt title="Products whose body is now a mapped item of a shared one">Mapped items</dt><dd data-no-i18n>{r.shared}</dd>
            {r.mapsMerged > 0 && <><dt>Identical maps merged</dt><dd data-no-i18n>{r.mapsMerged}</dd></>}
            <dt>Orphans removed</dt><dd data-no-i18n>{r.orphans.toLocaleString('en-US')}</dd>
            <dt>Largest deviation</dt><dd data-no-i18n>{r.deviationMm.toFixed(3)} mm</dd>
            {r.kept > 0 && <><dt title="with their own normals, colour or texture maps">Kept as written</dt><dd data-no-i18n>{r.kept}</dd></>}
            <dt>Time taken</dt><dd data-no-i18n>{r.seconds.toFixed(1)} s</dd>
          </dl>
          {r.target && (
            <div className="row-wrap">
              <span className="muted small" data-no-i18n title={r.target}>{fileName(r.target)}</span>
              {onFolder && <button type="button" className="btn mini" data-optimize="open-folder" onClick={() => onFolder(r.target!)}><FolderOpen size={12} />Open folder</button>}
              {onOpen && <button type="button" className="btn mini" data-optimize="open-lapin" onClick={() => onOpen(r.target!)}><ExternalLink size={12} />Open in Lapin</button>}
            </div>
          )}
          {!r.target && <p className="muted small">Downloaded.</p>}
        </>
      )}
    </div>
  );
}
