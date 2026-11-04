/**
 * Planning → Generate schedule… (docs/plan-4d5d.md, stage 3), a configurator like the roof's and the hall's: the
 * templates on the left (the project's own styles, then the catalogue's: concrete frame, load-bearing masonry, steel hall,
 * timber frame; the one the model asks for marked), the spec in tabs in the middle (Start, Sequence, Durations, Calendar)
 * and the engine's preview on the right: the schedule it would make as bars, how many elements it links (those left
 * without a task shown, or given a step of their own), how many durations came from norms, the temporaries tied to their
 * hosts' tasks, the demolitions. Apply is one engine command, one step to undo; generated again, what was edited by hand
 * stays. The norms are the user's (CSV or XLSX), or Lapin's few indicative values, marked as such.
 */

import { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, CalendarDays, FileDown, FileUp, ListPlus, MousePointer2, Pencil, RotateCcw, Save, Trash2, TriangleAlert, X } from 'lucide-react';
import { focusPane, notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { pickFile, saveFile, toBase64 } from '../../engine/files';
import { t, useLang } from '../../i18n';
import { showDate } from './gantt';
import { bars, DEMOLITION, moveStep, normLabel, quantityText, stepForLeftovers, toggleClass, translateNames, withStep, type GeneratorOptions, type GenSpec, type GenStep, type PreviewDto, type Template } from './generator';
import { openGantt, useSchedule } from './schedule';

const TABS = [['start', 'Start'], ['sequence', 'Sequence'], ['durations', 'Durations'], ['calendar', 'Calendar']] as const;
type Tab = (typeof TABS)[number][0];

interface ClassOption { id: string; name: string; count: number }

const bytes = (b64: string) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));

export function GenerateDialog() {
  const lang = useLang((s) => s.lang);
  const version = useSession((s) => s.version);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const schedule = useSchedule();
  const templates = (info?.catalogs['planning.scheduleTemplates'] as unknown as Template[] | undefined) ?? [];
  const [tab, setTab] = useState<Tab>('start');
  const [options, setOptions] = useState<GeneratorOptions | null>(null);
  const [spec, setSpec] = useState<GenSpec | null>(null);
  const [style, setStyle] = useState<string | null>(null);
  const [start, setStart] = useState<string | null>(null);
  const [keep, setKeep] = useState(true);
  const [preview, setPreview] = useState<PreviewDto | null>(null);
  const [classes, setClasses] = useState<ClassOption[]>([]);
  const [open, setOpen] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);

  // The options follow the model (a style saved, norms imported, an undo); the draft is set once, from the last spec or the suggested template.
  useEffect(() => {
    void query<GeneratorOptions>('planning.generatorOptions').then((o) => {
      if (!o) return;
      setOptions(o);
      setSpec((cur) => cur ?? (o.generated ? o.spec : translateNames(o.spec, (s) => t(s))));
      setStart((cur) => cur ?? o.start);
    });
    void query<{ classes: ClassOption[] }>('planning.ruleOptions').then((o) => o && setClasses(o.classes));
  }, [version]);

  // The language changed before anything was touched: the catalogue's step names follow it (the tasks are named in it).
  useEffect(() => {
    if (options && !options.generated && !dirty && !style) setSpec(translateNames(options.spec, (x) => t(x)));
  }, [lang]);

  // The preview follows the draft and the model.
  useEffect(() => {
    if (!spec) return;
    let live = true;
    const id = setTimeout(() => {
      void query<PreviewDto>('planning.preview', { spec, keep }).then((p) => { if (live) setPreview(p); });
    }, 120);
    return () => { live = false; clearTimeout(id); };
  }, [spec, keep, version]);

  const close = () => useUi.setState({ dialog: null });
  const change = (next: GenSpec) => { setSpec(next); setDirty(true); };
  const pick = (id: string, next: GenSpec) => { setStyle(id); setSpec(next); setDirty(true); };
  const own = options?.styles ?? [];
  const suggested = options?.suggested;
  const norms = options?.norms;
  const summary = preview?.summary;

  const apply = async () => {
    if (!spec) return;
    const r = await execute('planning.generate', { spec, keep, ...(start ? { start } : {}) }) as { tasks: number } | null;
    if (!r || useSession.getState().error) return;
    close();
    openGantt();
    notify('The schedule is generated: one step to undo.');
  };
  const saveAs = async () => {
    if (!spec) return;
    const name = window.prompt(t('Name of the schedule style'), '');
    if (!name?.trim()) return;
    const r = await execute('planning.saveScheduleStyle', { spec, name }) as { styleId?: string } | null;
    if (r?.styleId) setStyle(r.styleId);
  };
  const ownStyle = own.find((s) => s.id === style);
  const importNorms = async () => {
    const f = await pickFile('Norms (CSV, XLSX)', ['csv', 'xlsx', 'txt']);
    if (!f) return;
    const r = await execute('planning.importNorms', { data: toBase64(f.bytes), name: f.name }) as { rows: number } | null;
    if (r && !useSession.getState().error) notify('Norms imported.');
  };
  const template = async () => {
    const b64 = await query<string>('planning.normTemplate');
    if (b64) await saveFile(bytes(b64), 'norms-template.csv', 'text/csv');
  };
  const showUnlinked = () => {
    const ids = [...(summary?.none ?? []), ...(summary?.partial ?? [])];
    if (!ids.length) return;
    close();
    const s = useUi.getState();
    const model = s.panes.findIndex((p) => p.view !== 'gantt');
    if (model >= 0) focusPane(model);
    useUi.setState({ selected: ids, selection: ids[ids.length - 1] });
  };
  const addLeftovers = () => {
    if (!spec || !summary) return;
    const next = stepForLeftovers(spec, summary.unlinkedClasses, t('Other elements'));
    if (next) { change(next); setTab('sequence'); }
  };

  const plot = useMemo(() => bars(preview?.tasks ?? []), [preview]);
  const classLabel = (c: string) => classes.find((o) => o.id === c)?.name ?? c.replace(/^Ifc|^Lapin/, '');

  const stepRow = (s: GenStep, i: number) => {
    const demolition = s.key === DEMOLITION;
    const set = (patch: Partial<GenStep>) => change(withStep(spec!, s.key, patch));
    return (
      <div key={s.key} className={`gen-step${s.on ? '' : ' off'}`} data-step={s.key}>
        <div className="row">
          <span className="gen-order">
            <button type="button" className="icon mini" title="Earlier" disabled={i === 0} onClick={() => change({ ...spec!, steps: moveStep(spec!.steps, i, i - 1) })}><ArrowUp size={11} /></button>
            <button type="button" className="icon mini" title="Later" disabled={i === spec!.steps.length - 1} onClick={() => change({ ...spec!, steps: moveStep(spec!.steps, i, i + 1) })}><ArrowDown size={11} /></button>
          </span>
          <input type="checkbox" checked={s.on} title="Make this step's tasks" onChange={(e) => set({ on: e.target.checked })} data-field={`gen-on-${s.key}`} />
          <input className="grow" value={s.name} data-no-i18n onChange={(e) => set({ name: e.target.value })} data-field={`gen-name-${s.key}`} />
          {!demolition && s.classes.length > 0 && (
            <select value={s.split} title="One task for the building, or one a storey" onChange={(e) => set({ split: e.target.value as GenStep['split'] })} data-field={`gen-split-${s.key}`}>
              <option value="once">once</option><option value="storey">per storey</option>
            </select>
          )}
          <select value={s.link} title="How it follows the step before it on the same storey" onChange={(e) => set({ link: e.target.value as GenStep['link'] })}>
            <option value="FS">FS</option><option value="SS">SS</option>
          </select>
          <input type="number" className="num gen-lag" value={s.lag} step={1} title="Lag in working days (negative: an overlap)" onChange={(e) => set({ lag: Math.round(Number(e.target.value) || 0) })} />
          {!demolition && <button type="button" className="icon mini" title="What it builds" onClick={() => setOpen(open === s.key ? null : s.key)}><Pencil size={11} /></button>}
        </div>
        <div className="muted small gen-what">
          {demolition ? <span>the elements the phases demolish</span>
            : s.classes.length === 0 ? <span>no elements (site set-up)</span>
            : <span data-no-i18n>{s.classes.map(classLabel).join(', ')}{s.part ? ` · ${t(s.part === 'core' ? 'core' : s.part === 'finishes' ? 'finishes' : s.part)}` : ''}</span>}
        </div>
        {open === s.key && (
          <div className="gen-classes">
            <label className="field-line roof-field"><span className="eyebrow" data-i18n-ctx="task">Part</span>
              <select value={s.part ?? ''} onChange={(e) => set({ part: e.target.value || undefined })}>
                <option value="">Whole elements</option>
                <option value="core">Core (structure)</option>
                <option value="finishes">Finishes (layers but the core)</option>
              </select>
            </label>
            <span className="chips">
              {[...classes.map((c) => c.id), ...s.classes.filter((c) => !classes.some((o) => o.id === c))].map((c) => (
                <button type="button" key={c} className={`chip${s.classes.includes(c) ? ' on' : ''}`} onClick={() => set(toggleClass(s, c))} data-class={c}>
                  <span>{classLabel(c)}</span>
                </button>
              ))}
            </span>
          </div>
        )}
      </div>
    );
  };

  if (!spec) return null;
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Generate schedule">
      <div className="dialog wide roof-dialog gen-dialog" data-dialog="planning-generate" data-i18n-ctx="schedule">
        <header className="dialog-head">
          <span className="panel-title">Generate schedule</span>
          <button type="button" className="icon" onClick={() => { if (!dirty || window.confirm(t('Close without applying the changes?'))) close(); }} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="type-list roof-styles">
            {options?.generated && <button type="button" className={`tree-item${!style ? ' on' : ''}`} title="The spec the schedule was last generated with"
              onClick={() => { setStyle(null); setSpec(options.spec); setDirty(false); }}>As it is</button>}
            <p className="eyebrow">Project</p>
            {own.length === 0 && <p className="muted small">Configure a schedule, then Save as style.</p>}
            {own.map((s) => (
              <div key={s.id} className="row roof-own">
                <button type="button" className={`tree-item grow${style === s.id ? ' on' : ''}`} onClick={() => pick(s.id, s.spec)}><span className="grow" data-no-i18n>{s.label}</span></button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the schedule style'), s.label); if (n?.trim()) void execute('planning.renameScheduleStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style" onClick={() => { if (window.confirm(t('Delete this schedule style?'))) void execute('planning.deleteScheduleStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            <p className="eyebrow">Templates</p>
            {templates.map((x) => (
              <button key={x.id} type="button" className={`tree-item${style === x.id ? ' on' : ''}`} data-field={`gen-template-${x.id}`} title={t(x.description)}
                onClick={() => pick(x.id, translateNames(x.spec, (s) => t(s)))}>
                <span className="grow">{x.label}</span>{x.id === suggested && <span className="muted small">suggested</span>}
              </button>
            ))}
          </div>
          <div className="roof-edit">
            <div className="tabs-inline" role="tablist">
              {TABS.map(([k, l]) => <button key={k} type="button" role="tab" data-tab={`gen-${k}`} aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{l}</button>)}
            </div>
            <div className="joinery-edit">
              <div className="joinery-form gen-form">
                {tab === 'start' && <>
                  <label className="field-line roof-field"><span className="eyebrow">Start</span>
                    <input type="date" value={start ?? ''} data-field="gen-start" onChange={(e) => { setStart(e.target.value || null); setDirty(true); }} />
                  </label>
                  <label className="field-line roof-field" title="A task a storey for the steps that say so; else a task a step"><span className="eyebrow">Split by storey</span>
                    <input type="checkbox" checked={spec.byStorey} data-field="gen-by-storey" onChange={(e) => change({ ...spec, byStorey: e.target.checked })} />
                  </label>
                  <label className="field-line roof-field" title="The elements the phases demolish, in a task before the others"><span className="eyebrow">Demolition first{summary && summary.demolished > 0 && <span data-no-i18n>{` · ${summary.demolished}`}</span>}</span>
                    <input type="checkbox" checked={spec.demolition} data-field="gen-demolition" onChange={(e) => change({ ...spec, demolition: e.target.checked })} />
                  </label>
                  <p className="eyebrow">Temporary elements</p>
                  <label className="field-line roof-field" title="Up when its host's casting starts, struck after the days its type says"><span className="eyebrow">Formwork</span>
                    <input type="checkbox" checked={spec.formwork} data-field="gen-formwork" onChange={(e) => change({ ...spec, formwork: e.target.checked })} />
                  </label>
                  <label className="field-line roof-field" title="From the first task on its walls to the last, their finishes too"><span className="eyebrow">Scaffolding</span>
                    <input type="checkbox" checked={spec.scaffolding} data-field="gen-scaffolding" onChange={(e) => change({ ...spec, scaffolding: e.target.checked })} />
                  </label>
                  <p className="eyebrow">Generated again</p>
                  <label className="field-line roof-field" title="Tasks added by hand stay; tasks edited by hand stay as edited either way"><span className="eyebrow">Keep the tasks made by hand</span>
                    <input type="checkbox" checked={keep} data-field="gen-keep" onChange={(e) => setKeep(e.target.checked)} />
                  </label>
                  <p className="muted small">Each task takes its elements by a rule, so an element drawn later joins its task by itself. A task you edit by hand is kept as it is when the schedule is generated again.</p>
                </>}
                {tab === 'sequence' && <>
                  <p className="muted small">The steps in order; a step's tasks follow the step before on the same storey, and the same step on the storey below.</p>
                  <div className="gen-steps" data-field="gen-steps">{spec.steps.map(stepRow)}</div>
                </>}
                {tab === 'durations' && <>
                  <div className="gen-norms" data-field="gen-norms">
                    <p className="eyebrow">Productivity norms</p>
                    {!norms?.count ? <p className="muted small">No norms yet: each task takes its step's days. Import your norms (a code, a unit, the man-hours per unit), or start from Lapin's template.</p>
                      : <p className="small"><span data-no-i18n>{norms.indicative ? `${norms.count}` : `${norms.count} · ${norms.source ?? ''}`}</span></p>}
                    {norms?.indicative && <p className="warn-text small" data-field="gen-indicative"><TriangleAlert size={12} /> Indicative values written by Lapin, not taken from any norm book: replace them with your own.</p>}
                    <div className="row">
                      <button type="button" className="btn" onClick={() => void importNorms()} data-field="gen-import-norms"><FileUp size={14} />Import norms…</button>
                      <button type="button" className="btn" title="The columns and a few rows of Lapin's indicative values, as CSV" onClick={() => void template()}><FileDown size={14} />Template (CSV)</button>
                    </div>
                    <div className="row">
                      <button type="button" className="link" onClick={() => void execute('planning.useIndicativeNorms', {})} data-field="gen-indicative-norms">Use Lapin's indicative values</button>
                      {!!norms?.count && <button type="button" className="link" onClick={() => void execute('planning.clearNorms', {})}>Remove the norms</button>}
                    </div>
                  </div>
                  <p className="muted small">Duration = quantity × man-hours per unit ÷ (crew × hours a day); without a norm, the step's days.</p>
                  <table className="gen-durations">
                    <thead><tr><th>Step</th><th>Norm</th><th title="0: the norm's own">Crew</th><th title="When no norm times it">Days</th></tr></thead>
                    <tbody>
                      {spec.steps.filter((s) => s.on && (s.classes.length > 0 || s.key === DEMOLITION)).map((s) => (
                        <tr key={s.key} data-step={s.key}>
                          <td data-no-i18n>{s.name}</td>
                          <td><select value={s.norm ?? ''} onChange={(e) => change(withStep(spec, s.key, { norm: e.target.value || undefined }))} data-field={`gen-norm-${s.key}`}>
                            <option value="">(none)</option>
                            {s.norm && !norms?.rows.some((n) => n.code.toLowerCase() === s.norm!.toLowerCase()) && <option value={s.norm} data-no-i18n>{`${s.norm} ?`}</option>}
                            {(norms?.rows ?? []).map((n) => <option key={n.code} value={n.code} data-no-i18n>{normLabel(n)}</option>)}
                          </select></td>
                          <td><input type="number" className="num" min={0} step={1} value={s.crew} onChange={(e) => change(withStep(spec, s.key, { crew: Math.max(0, Math.round(Number(e.target.value) || 0)) }))} /></td>
                          <td><input type="number" className="num" min={1} step={1} value={s.days} onChange={(e) => change(withStep(spec, s.key, { days: Math.max(1, Math.round(Number(e.target.value) || 1)) }))} /></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>}
                {tab === 'calendar' && schedule && <>
                  <dl className="fields roof-summary">
                    <dt>Calendar</dt><dd data-no-i18n>{schedule.calendar.name}</dd>
                    <dt>Working days</dt><dd data-no-i18n>{schedule.calendar.days.map((d) => t(['', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'][d])).join(' ')}</dd>
                    <dt>Hours a day</dt><dd data-no-i18n>{schedule.calendar.hours}</dd>
                    <dt>Holidays</dt><dd>{schedule.calendar.holidays === 'ro' ? "Romania's legal holidays" : 'None (only the exceptions below)'}</dd>
                    <dt>Winter break</dt><dd>{schedule.calendar.winter.on ? <span data-no-i18n>{`${schedule.calendar.winter.from} – ${schedule.calendar.winter.to}`}</span> : 'off'}</dd>
                  </dl>
                  <p className="muted small">The durations are working days of this calendar; the hours a day divide the norms' man-hours.</p>
                  <button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'planning.calendar' })}><CalendarDays size={14} />Calendar…</button>
                </>}
              </div>
              <div className="joinery-preview gen-preview">
                <div className="gen-bars" data-field="gen-preview">
                  {(preview?.tasks ?? []).map((x, i) => (
                    <div key={x.id} className={`gen-bar-row${x.summary ? ' summary' : ''}${x.kept ? ' kept' : ''}`} title={`${showDate(x.start)} – ${showDate(x.finish)} · ${x.duration} d`}>
                      <span className="gen-bar-name" style={{ paddingLeft: x.level * 10 }} data-no-i18n>{x.name}</span>
                      <span className="gen-bar-track">
                        <span className={`gen-bar${x.critical ? ' critical' : ''}${x.source === 'default' ? ' default' : ''}`} style={{ left: `${plot.rows[i].left * 100}%`, width: `${plot.rows[i].width * 100}%` }} />
                      </span>
                      <span className="gen-bar-q muted small" data-no-i18n>{x.summary ? '' : x.source === 'norm' ? quantityText(x.quantity, x.unit) : x.count ? `${x.duration} d` : ''}</span>
                    </div>
                  ))}
                </div>
                {preview?.problem && <p className="error-text">{preview.problem}</p>}
                {summary && (
                  <dl className="fields roof-summary" data-field="gen-summary">
                    <dt>Tasks</dt><dd className="num" data-no-i18n>{summary.tasks}</dd>
                    <dt>Working days</dt><dd className="num" data-no-i18n>{summary.workingDays}</dd>
                    <dt>Start</dt><dd className="num" data-no-i18n>{showDate(summary.start)}</dd>
                    <dt>Finish</dt><dd className="num" data-no-i18n>{showDate(summary.finish)}</dd>
                    <dt>Elements linked</dt>
                    <dd className={`num${summary.linked < summary.total ? ' warn-text' : ''}`} data-field="gen-linked" data-no-i18n>{`${summary.linked} / ${summary.total}`}</dd>
                    <dt>Durations from norms</dt><dd className="num" data-no-i18n>{summary.fromNorms}</dd>
                    <dt>Durations by default (no norm)</dt><dd className={`num${summary.byDefault ? ' warn-text' : ''}`} data-no-i18n title={summary.noNorm.join(', ')}>{summary.byDefault}</dd>
                    {(summary.formwork > 0 || summary.scaffolds > 0) && <><dt>Formwork and scaffolds on their hosts' tasks</dt><dd className="num" data-no-i18n>{`${summary.formwork} + ${summary.scaffolds}`}</dd></>}
                    {summary.kept > 0 && <><dt>Kept as edited by hand</dt><dd className="num" data-no-i18n>{summary.kept}</dd></>}
                    {summary.removed > 0 && <><dt>Tasks removed</dt><dd className="num" data-no-i18n>{summary.removed}</dd></>}
                  </dl>
                )}
                {summary?.indicative && <p className="warn-text small"><TriangleAlert size={12} /> Durations from Lapin's indicative values.</p>}
                {summary && summary.linked < summary.total && (
                  <div className="row">
                    <button type="button" className="link" onClick={showUnlinked} title="Select them in the view" data-field="gen-show-unlinked"><MousePointer2 size={12} />Show the elements without a task</button>
                    {summary.unlinkedClasses.length > 0 && <button type="button" className="link" onClick={addLeftovers} data-field="gen-add-step"><ListPlus size={12} />A step for them</button>}
                  </div>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{style ? (templates.find((x) => x.id === style)?.label ?? own.find((x) => x.id === style)?.label ?? '') : ''}</span>
                <button type="button" className="btn" disabled={!dirty} onClick={() => { if (options) { setStyle(null); setSpec(options.generated ? options.spec : translateNames(options.spec, (s) => t(s))); setDirty(false); } }}><RotateCcw size={14} />Reset</button>
                {ownStyle && <button type="button" className="btn" title="Save the spec into its style" onClick={() => void execute('planning.saveScheduleStyle', { spec, name: ownStyle.label, styleId: ownStyle.id })}><Save size={14} />Update style</button>}
                <button type="button" className="btn" title="Save the spec as a style of the project" onClick={() => void saveAs()}><Save size={14} />Save as style…</button>
                <button type="button" className="btn primary" data-field="gen-apply" disabled={Boolean(preview?.problem)} onClick={() => void apply()}>Apply</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
