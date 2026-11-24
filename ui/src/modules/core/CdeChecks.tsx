/**
 * Checks on foreign documents (Atrium docs/plan-cde-web.md W5; the server's docs/atrium-server.md § Checks on foreign documents),
 * shared by the CDE start screen in the browser and File → Collaborate… on the desktop: a submission's checks report (its summary,
 * each file's findings by severity, a page with its findings drawn over it), the project's document register (owners import a CSV or
 * XLSX, members read it), and the check settings in the Rules (the naming rule with a live test, the project's references, foreign
 * keywords, expected values, the checks run and the title block). The words are engine/checks.ts's; the page is drawn by pdf.js,
 * loaded the first time a page is shown (pdfPage.ts); a DXF, or a PDF pdf.js cannot draw, shows the page's outline and the title
 * block as the server read it.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, AlertTriangle, Info, Loader2, Minus, Plus, Trash2, Upload, X } from 'lucide-react';
import { notify } from '../../app/store';
import { confirmAction } from '../../shell/prompts';
import { when } from './format';
import {
  AtriumError, deleteRegister, getRegister, putRegister, putRules, serverBytes,
  type Register, type Rules,
} from '../../engine/atrium';
import {
  CHECKS, DEFAULT_FIELDS, DEFAULT_SETTINGS, FIELD_KINDS, FIELD_LABELS, SEVERITIES, badMember, bboxToFractions, bboxToPixels, counts, describeFinding,
  expectedText, fileLevel, findingsBySeverity, fitScale, parseExpected, splitLines, splitList, tryNaming,
  type Anchor, type Bbox, type CheckSettings, type CheckSummary, type CheckedFile, type CheckedPage, type ChecksReport, type FieldKind, type Finding, type GateChecks, type Severity,
} from '../../engine/checks';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const P = (project: string) => `lapin-remote/1.0/projects/${encodeURIComponent(project)}`;

// ------------------------------------------------------------ counts and the summary

const WORDS: Record<Severity, [string, string]> = { error: ['error', 'errors'], warning: ['warning', 'warnings'], info: ['note', 'notes'] };
const SEVERITY_TITLE: Record<Severity, string> = { error: 'Errors', warning: 'Warnings', info: 'Notes' };

function SeverityIcon({ s, size = 12 }: { s: Severity; size?: number }) {
  return s === 'error' ? <AlertCircle size={size} /> : s === 'warning' ? <AlertTriangle size={size} /> : <Info size={size} />;
}

/** "2 errors, 1 warning": each count with its word, the words translated. */
export function CountsText({ c }: { c: CheckSummary }) {
  const parts: [Severity, number][] = [['error', c.errors], ['warning', c.warnings], ['info', c.infos]];
  const shown = parts.filter(([, n]) => n > 0);
  if (shown.length === 0) return <span>nothing found</span>;
  return <>{shown.map(([s, n], i) => <span key={s} className={`cde-count sev-${s}`}>{i > 0 ? ', ' : ''}<span data-no-i18n>{n}</span> <span>{WORDS[s][n === 1 ? 0 : 1]}</span></span>)}</>;
}

/** The checks in one line: running, failed (and why), or what they found. */
export function ChecksLine({ checks, compact = false }: { checks: ChecksReport | GateChecks | null | undefined; compact?: boolean }) {
  if (!checks) return null;
  if (checks.status === 'pending') return <span className="small muted cde-checks-line" data-cde-checks="pending"><Loader2 size={12} className="spin" /> <span>Checking the documents…</span></span>;
  if (checks.status === 'failed') {
    const why = 'error' in checks ? checks.error : null;
    return <span className="small cde-warn cde-checks-line" data-cde-checks="failed"><AlertTriangle size={12} /> <span>The checks could not run.</span>{why && !compact && <span className="muted" data-no-i18n> {why}</span>}</span>;
  }
  const c = counts(checks);
  const bad = (c?.errors ?? 0) > 0;
  return (
    <span className={`small cde-checks-line ${bad ? 'cde-block' : c && c.warnings > 0 ? 'cde-warn' : 'cde-ok'}`} data-cde-checks="done" data-cde-check-errors={c?.errors ?? 0}>
      {bad ? <AlertCircle size={12} /> : c && c.warnings > 0 ? <AlertTriangle size={12} /> : <Info size={12} />} <span>Checks:</span> {c && <CountsText c={c} />}
    </span>
  );
}

// ------------------------------------------------------------ one finding

function FindingRow({ f, page, onShow, active }: { f: Finding; page?: CheckedPage; onShow?: () => void; active?: boolean }) {
  const words = describeFinding(f);
  const read = f.field && page?.titleBlock?.fields?.[f.field]?.value;
  return (
    <li className={`cde-finding sev-${f.severity}${active ? ' active' : ''}`} data-cde-finding={f.code} data-cde-severity={f.severity}>
      <SeverityIcon s={f.severity} />
      <span className="cde-finding-body">
        <span><code data-no-i18n>{f.code}</code> <span>{words.text}</span></span>
        {(words.facts.length > 0 || read) && (
          <span className="cde-facts small">
            {words.facts.map((x, i) => <span key={i} className="cde-fact"><span className="muted">{x.label}</span> {x.translate ? <span>{x.value}</span> : <span data-no-i18n>{x.value}</span>}</span>)}
            {read && <span className="cde-fact"><span className="muted">Found</span> <span data-no-i18n>{read}</span></span>}
          </span>
        )}
      </span>
      {f.page != null && (onShow
        ? <button type="button" className="link small" onClick={onShow} title="Show it on the page" data-cde-show-page={f.page}><span>page</span> <span data-no-i18n>{f.page}</span></button>
        : <span className="muted small"><span>page</span> <span data-no-i18n>{f.page}</span></span>)}
    </li>
  );
}

// ------------------------------------------------------------ the report

/**
 * A submission's checks report: the summary, then each file with the findings on the whole file, its findings by severity, and the
 * page shown with its findings drawn over it.
 */
export function ChecksReportView({ server, project, checks }: { server: string; project: string; checks: ChecksReport | null | undefined }) {
  if (!checks) return null;
  return (
    <div className="cde-checks" data-cde-checks-report={checks.status}>
      <p className="small"><ChecksLine checks={checks} />
        {checks.finished && <span className="muted small" data-no-i18n> · {checks.requestedBy?.name ?? ''} · {when(checks.finished)}</span>}</p>
      {checks.files?.map((f) => <CheckedFileView key={f.sha256 + f.name} server={server} project={project} file={f} />)}
    </div>
  );
}

function CheckedFileView({ server, project, file }: { server: string; project: string; file: CheckedFile }) {
  const [shown, setShown] = useState<{ page: number; finding: Finding | null } | null>(null);
  const groups = useMemo(() => findingsBySeverity(file.findings), [file.findings]);
  const whole = fileLevel(file.findings);
  const pageOf = (n: number | null) => file.pages.find((p) => p.page === n);
  return (
    <section className="cde-checked-file" data-cde-checked-file={file.name}>
      <p className="small cde-checked-name"><b data-no-i18n>{file.name}</b>
        {file.kind && <span className="chip" data-no-i18n>{file.kind.toUpperCase()}</span>}
        {!file.checked && <span className="muted"> <span>not checked</span></span>}
        {file.code && <span className="muted" data-no-i18n> · {file.code}{file.revision ? ` · ${file.revision}` : ''}</span>}
        {file.checked && file.findings.length === 0 && <span className="cde-ok"> <span>nothing found</span></span>}
      </p>
      {whole.length > 0 && (
        <div className="cde-file-level" data-cde-file-level>
          <p className="eyebrow small">On the whole file</p>
          <ul className="cde-findings-list">{whole.map((f, i) => <FindingRow key={`w${i}`} f={f} />)}</ul>
        </div>
      )}
      {SEVERITIES.map((s) => {
        const list = groups[s].filter((f) => f.page != null);
        if (list.length === 0) return null;
        return (
          <div key={s} className="cde-severity-group" data-cde-severity-group={s}>
            <p className="eyebrow small"><SeverityIcon s={s} /> <span>{SEVERITY_TITLE[s]}</span> <span className="muted" data-no-i18n>{list.length}</span></p>
            <ul className="cde-findings-list">
              {list.map((f, i) => <FindingRow key={`${s}${i}`} f={f} page={pageOf(f.page)} active={shown?.finding === f}
                onShow={file.kind && file.pages.length > 0 ? () => setShown({ page: f.page!, finding: f }) : undefined} />)}
            </ul>
          </div>
        );
      })}
      {file.kind && file.pages.length > 0 && (
        <div className="cde-actions small">
          <span className="muted">Show a page:</span>
          {file.pages.slice(0, 40).map((p) => (
            <button key={p.page} type="button" className={`link small${shown?.page === p.page ? ' active' : ''}`} data-cde-page-button={p.page}
              onClick={() => setShown(shown?.page === p.page && !shown.finding ? null : { page: p.page, finding: null })}><span data-no-i18n>{p.page}</span></button>
          ))}
          {file.pages.length > 40 && <span className="muted" data-no-i18n>… {file.pages.length}</span>}
        </div>
      )}
      {shown && pageOf(shown.page) && (
        <PageWithFindings server={server} project={project} file={file} page={pageOf(shown.page)!} highlight={shown.finding} onClose={() => setShown(null)} />
      )}
    </section>
  );
}

// ------------------------------------------------------------ a page with its findings drawn over it

/** A file's bytes, read once from the server per session (an issued file never changes: its id is its SHA-256). */
const bytesCache = new Map<string, Promise<Uint8Array>>();
const fileBytes = (server: string, project: string, sha256: string) => {
  const key = `${server} ${project} ${sha256}`;
  let b = bytesCache.get(key);
  if (!b) { b = serverBytes(server, `${P(project)}/objects/${sha256}`); bytesCache.set(key, b); b.catch(() => bytesCache.delete(key)); }
  return b;
};
type Pdf = Awaited<ReturnType<typeof import('./pdfPage').openPdf>>;
const pdfCache = new Map<string, Promise<Pdf>>();

const SHOWN_WIDTH = 760;

function PageWithFindings({ server, project, file, page, highlight, onClose }: { server: string; project: string; file: CheckedFile; page: CheckedPage; highlight: Finding | null; onClose: () => void }) {
  const [zoom, setZoom] = useState(1);
  const [state, setState] = useState<'loading' | 'drawn' | 'outline'>(file.kind === 'pdf' ? 'loading' : 'outline');
  const [why, setWhy] = useState<string | null>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const scale = fitScale(page.width, SHOWN_WIDTH) * zoom;
  const here = file.findings.filter((f) => f.page === page.page);
  const drawn = here.filter((f) => f.bbox);
  useEffect(() => {
    if (file.kind !== 'pdf') return;
    let live = true;
    setState('loading');
    void (async () => {
      try {
        const key = `${server} ${project} ${file.sha256}`;
        let doc = pdfCache.get(key);
        if (!doc) {
          doc = Promise.all([import('./pdfPage'), fileBytes(server, project, file.sha256)]).then(([m, b]) => m.openPdf(b));
          pdfCache.set(key, doc);
          doc.catch(() => pdfCache.delete(key));
        }
        const [m, d] = await Promise.all([import('./pdfPage'), doc]);
        if (!live || !canvas.current) return;
        await m.drawPage(d, page.page, canvas.current, scale);
        if (live) setState('drawn');
      } catch (e) { if (live) { setWhy(message(e)); setState('outline'); } }
    })();
    return () => { live = false; };
  }, [server, project, file.sha256, file.kind, page.page, scale]);
  const w = page.width * (72 / 25.4) * scale, h = page.height * (72 / 25.4) * scale;
  const box = (b: Bbox) => bboxToPixels(b, scale);
  const tb = page.titleBlock;
  return (
    <div className="cde-page-view" data-cde-page-view={page.page} data-cde-page-state={state}>
      <div className="cde-actions small">
        <span><span>Page</span> <span data-no-i18n>{page.page}</span> <span className="muted" data-no-i18n>· {Math.round(page.width)} × {Math.round(page.height)} mm</span></span>
        <button type="button" className="icon" title="Smaller" onClick={() => setZoom((z) => Math.max(0.5, z / 1.5))}><Minus size={12} /></button>
        <button type="button" className="icon" title="Larger" onClick={() => setZoom((z) => Math.min(4, z * 1.5))}><Plus size={12} /></button>
        <span className="grow" />
        <button type="button" className="icon" title="Close" onClick={onClose}><X size={12} /></button>
      </div>
      {state === 'outline' && (
        <p className="muted small">{file.kind === 'dxf'
          ? 'A DXF is shown as its page and the title block the checks read, not its drawing.'
          : 'The page could not be drawn here: its outline and the title block the checks read are shown.'}{why && <span data-no-i18n> ({why})</span>}</p>
      )}
      <div className="cde-page-scroll">
        <div className="cde-page" style={{ width: w, height: h }}>
          {file.kind === 'pdf' && <canvas ref={canvas} className="cde-page-canvas" style={{ display: state === 'drawn' ? 'block' : 'none' }} />}
          {state === 'loading' && <p className="muted small cde-page-loading"><Loader2 size={12} className="spin" /> <span>Drawing the page…</span></p>}
          {state === 'outline' && tb?.found && tb.bbox && <div className="cde-box titleblock" style={box(tb.bbox)} title="Title block" />}
          {state === 'outline' && tb && Object.entries(tb.fields ?? {}).map(([name, x]) => x.bbox && (
            <div key={name} className="cde-box field" style={{ ...box(x.bbox), fontSize: Math.max(8, Math.min(14, box(x.bbox).height)) }} title={FIELD_LABELS[name] ?? name}><span data-no-i18n>{x.value}</span></div>
          ))}
          {drawn.map((f, i) => {
            const words = describeFinding(f);
            return (
              <div key={i} className={`cde-box finding sev-${f.severity}${highlight === f ? ' active' : ''}`} style={box(f.bbox!)} data-cde-box={f.code}
                data-cde-box-fraction={JSON.stringify(Object.values(bboxToFractions(f.bbox!, page)).map((v) => Math.round(v * 1000) / 1000))}>
                <span className="cde-box-tag" data-no-i18n>{f.code}</span>
                <span className="cde-box-tip">{words.text}</span>
              </div>
            );
          })}
        </div>
      </div>
      {here.filter((f) => !f.bbox).length > 0 && (
        <ul className="cde-findings-list">{here.filter((f) => !f.bbox).map((f, i) => <FindingRow key={i} f={f} page={page} />)}</ul>
      )}
    </div>
  );
}

// ------------------------------------------------------------ the document register

/** The project's document register: owners import a CSV or XLSX (it replaces the one there) or delete it; members read it. */
export function RegisterPanel({ server, project, owner }: { server: string; project: string; owner: boolean }) {
  const [reg, setReg] = useState<Register | null | undefined>(undefined);
  const [failed, setFailed] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [filter, setFilter] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const load = () => { setFailed(null); getRegister(server, project).then(setReg).catch((e) => { setFailed(message(e)); setReg(null); }); };
  useEffect(load, [server, project]);
  const upload = async (file: File) => {
    setBusy(true);
    try {
      const r = await putRegister(server, project, new Uint8Array(await file.arrayBuffer()), file.name);
      setReg(r);
      notify('Register imported. Pending submissions are checked against it when their checks run again.');
    } catch (e) { notify(message(e)); } finally { setBusy(false); if (input.current) input.current.value = ''; }
  };
  const remove = async () => {
    if (!confirmAction('Delete the document register? The checks stop comparing codes, titles and revisions with it.')) return;
    setBusy(true);
    try { await deleteRegister(server, project); setReg(null); notify('Register deleted.'); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const rows = useMemo(() => {
    if (!reg) return [];
    const q = filter.trim().toLowerCase();
    return q ? reg.entries.filter((e) => [e.code, e.title ?? '', e.revision ?? '', ...Object.values(e.fields)].some((x) => x.toLowerCase().includes(q))) : reg.entries;
  }, [reg, filter]);
  const extra = reg?.columns.fields ?? [];
  return (
    <div className="cde-register" data-cde-register={reg ? 'yes' : reg === null ? 'none' : 'loading'}>
      <div className="cde-actions">
        {owner && <>
          <input ref={input} type="file" accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" hidden data-cde-register-file
            onChange={(e) => { const f = e.target.files?.[0]; if (f) void upload(f); }} />
          <button type="button" className="btn" disabled={busy} onClick={() => input.current?.click()} data-cde-register-upload title="A CSV or XLSX whose first row names the columns: the code (required), the title, the expected revision, and any title-block value"><Upload size={13} />{reg ? 'Replace the register…' : 'Import a register (CSV or XLSX)…'}</button>
          {reg && <button type="button" className="btn" disabled={busy} onClick={() => void remove()} data-cde-register-delete><Trash2 size={13} />Delete</button>}
        </>}
      </div>
      {reg === undefined && <p className="muted small">Loading…</p>}
      {failed && <p className="muted small">{failed}</p>}
      {reg === null && !failed && <p className="muted small">This project has no document register yet. {owner ? 'Import one: the checks then compare each document\'s code, title and revision with it.' : 'The project\'s owners import it.'}</p>}
      {reg && <>
        <p className="small"><b data-no-i18n>{reg.name ?? ''}</b> <span className="muted" data-no-i18n>· {reg.importedBy?.name ?? ''} · {when(reg.imported)} · </span><span data-no-i18n>{reg.count}</span> <span>documents</span></p>
        <p className="small muted"><span>Columns:</span> <span>code</span> <code data-no-i18n>{reg.columns.code}</code>
          {reg.columns.title && <> · <span>title</span> <code data-no-i18n>{reg.columns.title}</code></>}
          {reg.columns.revision && <> · <span>revision</span> <code data-no-i18n>{reg.columns.revision}</code></>}
          {extra.length > 0 && <> · <span>expected values</span> <span data-no-i18n>{extra.join(', ')}</span></>}</p>
        {reg.warnings.length > 0 && (
          <details className="cde-register-warnings" data-cde-register-warnings={reg.warnings.length}>
            <summary className="small cde-warn"><span data-no-i18n>{reg.warnings.length}</span> <span>import warnings</span></summary>
            <ul className="small">{reg.warnings.map((w, i) => <li key={i} data-no-i18n>{w}</li>)}</ul>
          </details>
        )}
        <div className="cde-add"><input placeholder="Filter" aria-label="Filter the register" value={filter} onChange={(e) => setFilter(e.target.value)} /></div>
        <div className="cde-register-table">
          <table className="cde-members">
            <thead><tr><th>Code</th><th>Title</th><th>Revision</th>{extra.map((c) => <th key={c} data-no-i18n>{c}</th>)}</tr></thead>
            <tbody>
              {rows.slice(0, 500).map((e) => (
                <tr key={e.code} data-cde-register-row={e.code}>
                  <td data-no-i18n><code>{e.code}</code></td><td data-no-i18n>{e.title ?? ''}</td><td data-no-i18n>{e.revision ?? ''}</td>
                  {extra.map((c) => <td key={c} data-no-i18n>{e.fields[c] ?? ''}</td>)}
                </tr>
              ))}
            </tbody>
          </table>
          {rows.length > 500 && <p className="muted small"><span>Showing the first 500 of</span> <span data-no-i18n>{rows.length}</span></p>}
        </div>
      </>}
    </div>
  );
}

// ------------------------------------------------------------ the check settings (in the Rules)

interface FieldRow { name: string; kind: FieldKind; labels: string; required: boolean; zone: string; attributes: string }
const toRow = (f: { name: string; kind: FieldKind; labels: string[]; required: boolean; zone: Bbox | null; attributes: string[] }): FieldRow =>
  ({ name: f.name, kind: f.kind, labels: f.labels.join(', '), required: f.required, zone: f.zone ? f.zone.join(' ') : '', attributes: f.attributes.join(', ') });
const zoneOf = (text: string): Bbox | null => {
  const n = text.trim().split(/[\s,;]+/).filter(Boolean).map(Number);
  return n.length === 4 && n.every(Number.isFinite) ? n as Bbox : null;
};

interface Draft {
  naming: string; enabled: string[]; projectCode: string; projectNumber: string; projectName: string; keywords: string; expected: string; futureDays: string;
  custom: boolean; region: boolean; anchor: Anchor; width: string; height: string; marginX: string; marginY: string; minLabels: string; fields: FieldRow[];
}

function draftOf(rules: Rules): Draft {
  const c = rules.checks ?? DEFAULT_SETTINGS;
  const tb = c.titleBlock;
  const r = tb?.region;
  return {
    naming: rules.naming ?? '', enabled: c.enabled, projectCode: c.projectCode ?? '', projectNumber: c.projectNumber ?? '', projectName: c.projectName ?? '',
    keywords: c.foreignKeywords.join('\n'), expected: expectedText(c.expected), futureDays: String(c.futureDays ?? 0),
    custom: tb != null, region: r != null, anchor: r?.anchor ?? 'bottom-right', width: String(r?.width ?? 180), height: String(r?.height ?? 60),
    marginX: String(r?.marginX ?? 10), marginY: String(r?.marginY ?? 10), minLabels: String(tb?.minLabels ?? 2), fields: (tb?.fields ?? DEFAULT_FIELDS).map(toRow),
  };
}

const num = (s: string) => (s.trim() === '' ? NaN : Number(s));
const orNull = (s: string) => (s.trim() ? s.trim() : null);

/** The settings the draft says, as `PUT rules` takes them (the server checks every member and names a bad one). */
export function settingsOf(d: Draft): CheckSettings {
  return {
    enabled: CHECKS.filter((c) => d.enabled.includes(c)),
    projectCode: orNull(d.projectCode), projectNumber: orNull(d.projectNumber), projectName: orNull(d.projectName),
    foreignKeywords: splitLines(d.keywords), expected: parseExpected(d.expected), futureDays: num(d.futureDays),
    titleBlock: !d.custom ? null : {
      region: d.region ? { anchor: d.anchor, width: num(d.width), height: num(d.height), marginX: num(d.marginX), marginY: num(d.marginY) } : null,
      minLabels: num(d.minLabels),
      fields: d.fields.map((f) => ({ name: f.name.trim(), kind: f.kind, labels: splitList(f.labels), required: f.required, zone: zoneOf(f.zone), attributes: splitList(f.attributes) })),
    },
  };
}

const CHECK_NAMES: Record<string, string> = {
  V1: 'V1 · names, revisions and the register', V2: 'V2 · the title block', V3: 'V3 · empty sheets, content off the page', V4: 'V4 · placeholders', V5: 'V5 · another project\'s sheets',
};
const ANCHORS: [Anchor, string][] = [['bottom-right', 'Bottom right'], ['bottom-left', 'Bottom left'], ['top-right', 'Top right'], ['top-left', 'Top left']];

/** The naming rule and the check settings: owners change them (PUT rules replaces the settings whole); the others read them. */
export function CheckSettingsEditor({ server, project, rules, onSaved }: { server: string; project: string; rules: Rules; onSaved: (r: Rules) => void }) {
  const [d, setD] = useState<Draft>(() => draftOf(rules));
  const [busy, setBusy] = useState(false);
  const [bad, setBad] = useState<{ key: string; field?: string; text: string } | null>(null);
  const [tryCode, setTryCode] = useState('');
  useEffect(() => { setD(draftOf(rules)); }, [rules]);
  const owner = rules.role === 'owner';
  const off = busy || !owner;
  const set = (change: Partial<Draft>) => setD((x) => ({ ...x, ...change }));
  const setField = (i: number, change: Partial<FieldRow>) => setD((x) => ({ ...x, fields: x.fields.map((f, j) => (j === i ? { ...f, ...change } : f)) }));
  const tried = tryCode.trim() ? tryNaming(d.naming, tryCode) : null;
  const save = async () => {
    setBusy(true); setBad(null);
    try {
      const r = await putRules(server, project, { naming: d.naming.trim() || null, checks: settingsOf(d) });
      onSaved(r);
      notify('Check settings saved. Pending submissions are checked with them when their checks run again.');
    } catch (e) {
      if (e instanceof AtriumError && e.code === 'bad_request' && e.serverMessage) {
        const m = badMember(e.serverMessage);
        setBad({ key: m?.key ?? 'checks', field: m?.field, text: e.serverMessage });
      } else notify(message(e));
    } finally { setBusy(false); }
  };
  const Bad = ({ k }: { k: string }) => (bad && bad.key === k
    ? <p className="small cde-block cde-bad" data-cde-bad={k}><span>The server did not accept this:</span> <span data-no-i18n>{bad.text}</span></p> : null);
  const badField = (name: string) => bad?.key === 'titleBlock.fields' && bad.field != null && bad.field.toLowerCase() === name.trim().toLowerCase();
  return (
    <div className="cde-check-settings" data-cde-check-settings>
      <p className="eyebrow">Checks on documents</p>
      <p className="muted small">Every PDF and DXF a submission issues is checked on the server: its name, revision and register entry, its title block, empty or shifted sheets, placeholders, and another project's sheets. Only errors stop the approval.</p>
      <table className="cde-members cde-settings-table">
        <tbody>
          <tr><td>Naming rule</td><td>
            <input className="mono" aria-label="Naming rule" placeholder="A regular expression the whole code must match, e.g. (?<project>EL24)-[A-Z]{3}-L2-[A-Z0-9]{2}-[A-Z]{2}-[A-Z]-\d{4}" value={d.naming} disabled={off} onChange={(e) => set({ naming: e.target.value })} data-cde-naming />
            <div className="cde-add"><input aria-label="Try a code" placeholder="Try a code" value={tryCode} onChange={(e) => setTryCode(e.target.value)} data-cde-naming-try />
              {tried && (tried.error
                ? <span className="small cde-block" data-cde-naming-result="error"><span>Does not compile here:</span> <span data-no-i18n>{tried.error}</span></span>
                : tried.matches
                  ? <span className="small cde-ok" data-cde-naming-result="match"><span>It matches</span>{tried.project && <> · <span>project field</span> <code data-no-i18n>{tried.project}</code></>}</span>
                  : <span className="small cde-block" data-cde-naming-result="no-match">Does not match</span>)}
            </div>
            <Bad k="naming" />
          </td></tr>
          <tr><td>Checks run</td><td className="cde-check-list">
            {CHECKS.map((c) => <label key={c} className="row small"><input type="checkbox" checked={d.enabled.includes(c)} disabled={off} data-cde-check-enabled={c}
              onChange={(e) => set({ enabled: e.target.checked ? [...d.enabled, c] : d.enabled.filter((x) => x !== c) })} /> <span>{CHECK_NAMES[c]}</span></label>)}
            <Bad k="enabled" />
          </td></tr>
          <tr><td>Project code</td><td><input aria-label="Project code" value={d.projectCode} disabled={off} onChange={(e) => set({ projectCode: e.target.value })} data-cde-project-code /><Bad k="projectCode" /></td></tr>
          <tr><td>Project number</td><td><input aria-label="Project number" value={d.projectNumber} disabled={off} onChange={(e) => set({ projectNumber: e.target.value })} data-cde-project-number /><Bad k="projectNumber" /></td></tr>
          <tr><td>Project name</td><td><input aria-label="Project name" value={d.projectName} disabled={off} onChange={(e) => set({ projectName: e.target.value })} data-cde-project-name /><Bad k="projectName" /></td></tr>
          <tr><td>Another project's keywords</td><td><textarea aria-label="Another project's keywords" rows={3} placeholder="One per line: another project's code, number or place" value={d.keywords} disabled={off} onChange={(e) => set({ keywords: e.target.value })} data-cde-keywords /><Bad k="foreignKeywords" /></td></tr>
          <tr><td>Expected values</td><td><textarea aria-label="Expected values" rows={3} placeholder="field = value, one per line (client = CNCF CFR SA)" value={d.expected} disabled={off} onChange={(e) => set({ expected: e.target.value })} data-cde-expected /><Bad k="expected" /></td></tr>
          <tr><td>A date in the future after</td><td><input type="number" min={0} max={3650} aria-label="Days" value={d.futureDays} disabled={off} onChange={(e) => set({ futureDays: e.target.value })} data-cde-future-days /> <span className="small">days</span><Bad k="futureDays" /></td></tr>
          <tr><td>Title block</td><td>
            <select aria-label="Title block" value={d.custom ? 'custom' : 'default'} disabled={off} onChange={(e) => set({ custom: e.target.value === 'custom' })} data-cde-title-block>
              <option value="default">Found by its labels (the default)</option><option value="custom">Described here</option>
            </select>
            <Bad k="titleBlock" />
          </td></tr>
        </tbody>
      </table>
      {d.custom && (
        <div className="cde-title-block" data-cde-title-block-spec>
          <label className="row small"><input type="checkbox" checked={d.region} disabled={off} onChange={(e) => set({ region: e.target.checked })} data-cde-region /> <span>In a fixed region of the page</span></label>
          {d.region && (
            <div className="cde-add cde-region">
              <select aria-label="Corner" value={d.anchor} disabled={off} onChange={(e) => set({ anchor: e.target.value as Anchor })}>{ANCHORS.map(([a, l]) => <option key={a} value={a}>{l}</option>)}</select>
              {(['width', 'height', 'marginX', 'marginY'] as const).map((k) => (
                <label key={k} className="row small"><span>{{ width: 'Width', height: 'Height', marginX: 'Margin across', marginY: 'Margin down' }[k]}</span>
                  <input type="number" min={0} aria-label={k} value={d[k]} disabled={off} onChange={(e) => set({ [k]: e.target.value } as Partial<Draft>)} data-cde-region-size={k} /> <span>mm</span></label>
              ))}
            </div>
          )}
          <Bad k="titleBlock.region" />
          <label className="row small"><span>Labels that make a title block (without a region)</span> <input type="number" min={1} max={40} aria-label="Labels" value={d.minLabels} disabled={off} onChange={(e) => set({ minLabels: e.target.value })} /></label>
          <Bad k="titleBlock.minLabels" />
          <table className="cde-members cde-fields-table">
            <thead><tr><th>Field</th><th>Kind</th><th>Labels</th><th>Required</th><th>Zone (mm)</th><th>DXF attributes</th><th /></tr></thead>
            <tbody>
              {d.fields.map((f, i) => (
                <tr key={i} className={badField(f.name) ? 'cde-bad-row' : undefined} data-cde-field-row={f.name}>
                  <td><input aria-label="Field" value={f.name} disabled={off} onChange={(e) => setField(i, { name: e.target.value })} /></td>
                  <td><select aria-label="Kind" value={f.kind} disabled={off} onChange={(e) => setField(i, { kind: e.target.value as FieldKind })}>{FIELD_KINDS.map((k) => <option key={k} value={k}>{k}</option>)}</select></td>
                  <td><input aria-label="Labels" placeholder="DRAWING NO, COD PLANȘĂ" value={f.labels} disabled={off} onChange={(e) => setField(i, { labels: e.target.value })} /></td>
                  <td><input type="checkbox" aria-label="Required" checked={f.required} disabled={off} onChange={(e) => setField(i, { required: e.target.checked })} /></td>
                  <td><input aria-label="Zone" placeholder="x0 y0 x1 y1" value={f.zone} disabled={off || !d.region} onChange={(e) => setField(i, { zone: e.target.value })} /></td>
                  <td><input aria-label="DXF attributes" value={f.attributes} disabled={off} onChange={(e) => setField(i, { attributes: e.target.value })} /></td>
                  <td><button type="button" className="icon" title="Remove" disabled={off || d.fields.length <= 1} onClick={() => set({ fields: d.fields.filter((_, j) => j !== i) })}><X size={12} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <Bad k="titleBlock.fields" />
          {owner && <div className="cde-actions">
            <button type="button" className="btn" disabled={off || d.fields.length >= 40} onClick={() => set({ fields: [...d.fields, { name: `field${d.fields.length + 1}`, kind: 'text', labels: '', required: false, zone: '', attributes: '' }] })}><Plus size={13} />Add a field</button>
            <button type="button" className="btn" disabled={off} onClick={() => set({ fields: DEFAULT_FIELDS.map(toRow) })}>Start from the default fields</button>
          </div>}
          <p className="muted small">A zone is measured from the region's top-left corner. Without a region, the labels are looked for anywhere on the page.</p>
        </div>
      )}
      <Bad k="checks" />
      {owner
        ? <div className="cde-actions">
          <button type="button" className="btn primary" disabled={busy} onClick={() => void save()} data-cde-checks-save>Save the check settings</button>
          <button type="button" className="btn" disabled={busy} onClick={() => { setD(draftOf({ ...rules, naming: d.naming, checks: { ...DEFAULT_SETTINGS } })); setBad(null); }}>Defaults</button>
          <button type="button" className="btn" disabled={busy} onClick={() => { setD(draftOf(rules)); setBad(null); }}>Undo changes</button>
        </div>
        : <p className="muted small">The project's owners change the check settings.</p>}
    </div>
  );
}
