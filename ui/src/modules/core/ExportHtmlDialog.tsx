/**
 * File → Lapin for the browser (.html)… (decision 482, docs/html-export.md): the project as one .html file that any
 * browser opens by double-click, with no network — the 3D model, the drawings chosen (the plans, sections, elevations
 * and sheets, as the sheets print them), the estimate where the edition has the cost module, every element's
 * properties — and, unless left out, the project itself, which File → Open takes back. The page speaks the user's
 * language and has Lapin's look. The viewer is built from Lapin's own code (ui/src/standalone) and handed to the engine,
 * which writes the page; the model is not changed.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, useSession } from '../../engine/session';
import { exportFile, projectTitle } from '../../engine/files';
import { htmlExportArgs } from '../../engine/htmlExport';

interface PlanDrawing { id: string; name: string; kind: string; number?: string }
interface Plan { drawings: PlanDrawing[]; tables: boolean; sizes: { project: number; model3d: number; drawing: number; sheet: number } }

const GROUPS: [string, string][] = [['plan', 'Floor plans'], ['section', 'Sections'], ['elevation', 'Elevations'], ['sheet', 'Sheets']];
/** The viewer's code with three.js inside: about 630 kB, counted before it is built. */
const VIEWER_BYTES = 640_000;

/** Bytes as the dialog shows them: kB under a megabyte, MB above. */
function sizeText(bytes: number): string {
  if (bytes < 1_000_000) return `${Math.max(1, Math.round(bytes / 1000))} kB`;
  return `${(bytes / 1_000_000).toFixed(bytes < 10_000_000 ? 1 : 0)} MB`;
}

/** What the page would weigh: what the engine measured (the project, the 3D model) and a guess for the drawings. */
function estimate(plan: Plan, chosen: PlanDrawing[], model3d: boolean, project: boolean): number {
  const drawings = chosen.reduce((s, d) => s + (d.kind === 'sheet' ? plan.sizes.sheet : plan.sizes.drawing), 0);
  return VIEWER_BYTES + drawings + (model3d ? plan.sizes.model3d : 0) + (project ? plan.sizes.project : 0);
}

export function ExportHtmlDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const view3dId = useUi((s) => s.view3dId);
  const [plan, setPlan] = useState<Plan | null>(null);
  const [off, setOff] = useState<Set<string>>(new Set());
  const [model3d, setModel3d] = useState(true);
  const [tables, setTables] = useState(true);
  const [project, setProject] = useState(true);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    let live = true;
    engine.call<Plan>('project.htmlPlan', { viewId: view3dId ?? '3d' }, { timeoutMs: 120_000 })
      .then((p) => { if (live) setPlan(p); })
      .catch(() => { if (live) setPlan({ drawings: [], tables: false, sizes: { project: 0, model3d: 0, drawing: 0, sheet: 0 } }); });
    return () => { live = false; };
  }, [view3dId]);

  const chosen = useMemo(() => (plan?.drawings ?? []).filter((d) => !off.has(d.id)), [plan, off]);
  const toggle = (id: string) => setOff((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n; });

  const run = async () => {
    if (!plan) return;
    setBusy(true);
    try {
      // The viewer, built on its own (vite.config.ts), fetched only now: the app carries it in a lazy chunk alone.
      const args = await htmlExportArgs({ drawings: chosen.map((d) => d.id), model3d, tables: plan.tables && tables, project, viewId: view3dId ?? '3d' });
      await exportFile('project.exportHtml', args, `${projectTitle(path)}.html`, 'html');
    } finally {
      setBusy(false);
    }
    close();
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Lapin for the browser" onClick={(e) => e.stopPropagation()} style={{ width: 'min(560px, 96vw)' }}>
        <header className="dialog-head"><span className="panel-title">Lapin for the browser</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small" style={{ marginTop: 0 }}>One .html file that opens by double-click in any browser, with no internet: the 3D model, the drawings, the estimate and every element's properties. With the project inside, File → Open in Lapin takes it back.</p>
          {!plan ? <p className="muted">Reading the project…</p> : (
            <>
              <div className="eyebrow" style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <span>Drawings</span><span className="grow" />
                <button type="button" className="link" onClick={() => setOff(new Set())}>All</button>
                <button type="button" className="link" onClick={() => setOff(new Set(plan.drawings.map((d) => d.id)))}>None</button>
              </div>
              <div className="html-export-drawings" data-testid="html-export-drawings" style={{ maxHeight: 220, overflow: 'auto', border: '1px solid var(--rule)', padding: '4px 8px', margin: '4px 0 10px' }}>
                {plan.drawings.length === 0 && <p className="muted small">The project has no drawing yet.</p>}
                {GROUPS.map(([kind, label]) => {
                  const items = plan.drawings.filter((d) => d.kind === kind);
                  if (!items.length) return null;
                  return (
                    <div key={kind}>
                      <div className="muted small" style={{ margin: '6px 0 2px' }}>{label}</div>
                      {items.map((d) => (
                        <label key={d.id} className="check-line" style={{ display: 'flex', gap: 6 }}>
                          <input type="checkbox" checked={!off.has(d.id)} onChange={() => toggle(d.id)} />
                          <span>{d.kind === 'sheet' && d.number ? `${d.number} · ` : ''}{d.name}</span>
                        </label>
                      ))}
                    </div>
                  );
                })}
              </div>
              <label className="check-line" style={{ display: 'flex', gap: 6 }}><input type="checkbox" data-testid="html-export-3d" checked={model3d} onChange={(e) => setModel3d(e.target.checked)} /> The 3D model, as the 3D view shows it</label>
              {plan.tables && <label className="check-line" style={{ display: 'flex', gap: 6 }}><input type="checkbox" data-testid="html-export-tables" checked={tables} onChange={(e) => setTables(e.target.checked)} /> The estimate, worked out from the model now</label>}
              <label className="check-line" style={{ display: 'flex', gap: 6 }} title="Without it the page only shows the model: Lapin cannot open it"><input type="checkbox" data-testid="html-export-project" checked={project} onChange={(e) => setProject(e.target.checked)} /> The project itself, so Lapin can open the page again</label>
              <p className="small" data-testid="html-export-size" style={{ marginBottom: 0 }}>About {sizeText(estimate(plan, chosen, model3d, project))}</p>
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy || !plan} onClick={() => void run()}>{busy ? 'Exporting…' : 'Export'}</button>
        </footer>
      </div>
    </div>
  );
}
