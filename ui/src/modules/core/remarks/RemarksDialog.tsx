/**
 * Manage → Remarks…: the remarks on the model as BCF topics (docs/plan-cde.md,
 * stage 2). The list with its filters (status, priority, assignee, open only,
 * a text); a remark's fields, its snapshot and its comments; a remark opened
 * at its viewpoint in the 3D view (camera, selection, section planes); a
 * comment, with or without the view as it is now; a new remark from the 3D
 * view with a snapshot taken from it; Import BCF… and Export BCF… (all, the
 * open ones, the one selected; 2.1 or 3.0) through the file pickers the IFC
 * import and export use.
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Camera, Download, Eye, MessageSquare, Plus, Trash2, Upload, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { useCamera } from '../../../viewport/camera';
import {
  ANY, assigneesOf, author, commentRemark, createRemark, deleteRemarks, exportBcf, filterRemarks, found, importBcf, listRemarks, noFilter, openViewpoint,
  retakeViewpoint, setAuthor, snapshotOf, updateRemark, viewpointNow, when, type Filter, type NewRemark, type RemarkList,
} from './remarks';

const blank: NewRemark = { title: '', description: '', type: 'Issue', status: 'Open', priority: 'Normal', assignedTo: '', dueDate: '', labels: [] };

/** A select over the engine's choices, keeping a value the list lacks (a file's own status) as a choice of its own. */
function Choice({ value, choices, onChange, label, any }: { value: string; choices: string[]; onChange: (v: string) => void; label: string; any?: string }) {
  const all = value && !choices.includes(value) ? [value, ...choices] : choices;
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} aria-label={label}>
      {any != null && <option value={ANY}>{any}</option>}
      {all.map((c) => <option key={c} value={c}>{c}</option>)}
    </select>
  );
}

export function RemarksDialog() {
  const nodes = useSession((s) => s.nodes);
  const view = useUi((s) => s.view);
  const hasCamera = useCamera((s) => s.api != null);
  const [data, setData] = useState<RemarkList | null>(null);
  const [filter, setFilter] = useState<Filter>(noFilter);
  const [current, setCurrent] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [draft, setDraft] = useState<NewRemark>(blank);
  const [withView, setWithView] = useState(true);
  const [preview, setPreview] = useState<string | null>(null);
  const [snapshot, setSnapshot] = useState<string | null>(null);
  const [comment, setComment] = useState('');
  const [commentWithView, setCommentWithView] = useState(false);
  const [version, setVersion] = useState<'2.1' | '3.0'>('2.1');
  const [me, setMe] = useState(author());
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  const reload = useCallback(async () => { const r = await listRemarks(); if (r) setData(r); }, []);
  useEffect(() => { void reload(); }, [reload, nodes]);

  const shown = useMemo(() => (data ? filterRemarks(data.remarks, filter) : []), [data, filter]);
  const remark = useMemo(() => data?.remarks.find((r) => r.id === current) ?? null, [data, current]);
  const viewpoint = remark?.viewpoints[0];

  // The snapshot of the remark in front, asked for when shown (it stays out of the list).
  useEffect(() => {
    setSnapshot(null);
    if (!remark || !viewpoint?.hasSnapshot) return;
    let live = true;
    void snapshotOf(remark.id, viewpoint.guid).then((d) => { if (live) setSnapshot(d); });
    return () => { live = false; };
  }, [remark, viewpoint]);

  const startNew = () => {
    setCreating(true);
    setCurrent(null);
    setDraft(blank);
    const can = view === '3d' && hasCamera;
    setWithView(can);
    setPreview(can ? useCamera.getState().api?.snapshot(640) ?? null : null);
  };

  const run = async (f: () => Promise<unknown>, after?: () => void) => {
    setBusy(true);
    try { await f(); after?.(); await reload(); } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };

  const create = () => run(async () => {
    if (!draft.title.trim()) { notify('A remark has a title.'); return; }
    const r = await createRemark({ ...draft, title: draft.title.trim() }, withView && view === '3d' && hasCamera ? viewpointNow() : null);
    setCreating(false);
    setCurrent(r.id);
  });

  const field = (key: keyof NewRemark, value: string) => remark && run(() => updateRemark(remark.id, { [key]: value }));
  const labels = (s: string) => remark && run(() => updateRemark(remark.id, { labels: s.split(',').map((x) => x.trim()).filter(Boolean) }));

  const send = () => remark && comment.trim() && run(() => commentRemark(remark.id, comment.trim(), commentWithView && view === '3d' && hasCamera), () => { setComment(''); setCommentWithView(false); });

  const remove = () => remark && window.confirm(`Delete the remark “${remark.title}”?`) && run(() => deleteRemarks([remark.id]), () => setCurrent(null));

  const show = () => { if (viewpoint) void openViewpoint(viewpoint).catch((e) => notify(e instanceof Error ? e.message : String(e))); };

  const nameMe = (v: string) => { setMe(v); setAuthor(v); };

  if (!data) return null;
  const inThreeD = view === '3d' && hasCamera;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide remarks" role="dialog" aria-label="Remarks" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Remarks</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="remarks-bar">
          <button type="button" className="btn" onClick={startNew} disabled={busy} title="A new remark, with the 3D view as it is now when it is in front"><Plus size={13} /> New remark</button>
          <button type="button" className="btn" onClick={() => void run(() => importBcf())} disabled={busy} title="Read a BCF file (2.1 .bcfzip or 3.0 .bcf): its topics become remarks, known ones updated"><Upload size={13} /> Import BCF…</button>
          <span className="remarks-export">
            <Download size={13} />
            <button type="button" className="link" onClick={() => void exportBcf({}, version)} disabled={busy || data.remarks.length === 0} title="Every remark as a BCF file">Export all</button>
            <button type="button" className="link" onClick={() => void exportBcf({ open: true }, version)} disabled={busy || !data.remarks.some((r) => r.open)} title="The remarks not closed">Export open</button>
            <button type="button" className="link" onClick={() => remark && void exportBcf({ ids: [remark.id] }, version)} disabled={busy || !remark} title="The remark in front alone">Export selected</button>
            <select value={version} onChange={(e) => setVersion(e.target.value as '2.1' | '3.0')} aria-label="BCF version" title="BCF 2.1 (.bcfzip) is what most tools read; 3.0 (.bcf) is the newer one">
              <option value="2.1">BCF 2.1</option>
              <option value="3.0">BCF 3.0</option>
            </select>
          </span>
          <span className="grow" />
          <label className="row small"><span className="muted">Your name</span><input value={me} onChange={(e) => nameMe(e.target.value)} placeholder="On what you write" aria-label="Your name" style={{ width: 120 }} /></label>
        </div>
        <div className="remarks-filters">
          <input value={filter.text} onChange={(e) => setFilter({ ...filter, text: e.target.value })} placeholder="Search…" aria-label="Search remarks" />
          <Choice value={filter.status} choices={data.statuses} onChange={(v) => setFilter({ ...filter, status: v })} label="Status filter" any="Any status" />
          <Choice value={filter.priority} choices={data.priorities} onChange={(v) => setFilter({ ...filter, priority: v })} label="Priority filter" any="Any priority" />
          <Choice value={filter.assignee} choices={assigneesOf(data.remarks)} onChange={(v) => setFilter({ ...filter, assignee: v })} label="Assignee filter" any="Anyone" />
          <label className="row small"><input type="checkbox" checked={filter.open} onChange={(e) => setFilter({ ...filter, open: e.target.checked })} /> Open only</label>
        </div>
        <div className="dialog-body split remarks-body">
          <ul className="remarks-list" data-testid="remarks-list">
            {data.remarks.length === 0 && <li className="muted small">No remark yet. New remark makes one from the 3D view; Import BCF… reads a reviewer's file.</li>}
            {data.remarks.length > 0 && shown.length === 0 && <li className="muted small">Nothing matches the filter.</li>}
            {shown.map((r) => (
              <li key={r.id} className={r.id === current ? 'current' : undefined}>
                <button type="button" onClick={() => { setCreating(false); setCurrent(r.id); }}>
                  <span className="remark-title">{r.title}</span>
                  <span className="muted small">
                    <span className={`remark-status${r.open ? '' : ' closed'}`}>{r.status ?? 'Open'}</span>
                    {r.priority && <> · {r.priority}</>}
                    {r.assignedTo && <> · {r.assignedTo}</>}
                    {r.comments.length > 0 && <> · <MessageSquare size={10} /> {r.comments.length}</>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <div className="remarks-detail" data-testid="remarks-detail">
            {creating && (
              <div className="remark-form">
                <h4>New remark</h4>
                <label className="row"><span className="muted small loc-label">Title</span><input value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} aria-label="Title" autoFocus /></label>
                <label className="row"><span className="muted small loc-label">Description</span><textarea value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} aria-label="Description" rows={3} /></label>
                <div className="remark-fields">
                  <label className="row"><span className="muted small loc-label">Type</span><Choice value={draft.type} choices={data.types} onChange={(v) => setDraft({ ...draft, type: v })} label="Type" /></label>
                  <label className="row"><span className="muted small loc-label">Status</span><Choice value={draft.status} choices={data.statuses} onChange={(v) => setDraft({ ...draft, status: v })} label="Status" /></label>
                  <label className="row"><span className="muted small loc-label">Priority</span><Choice value={draft.priority} choices={data.priorities} onChange={(v) => setDraft({ ...draft, priority: v })} label="Priority" /></label>
                  <label className="row"><span className="muted small loc-label">Assigned to</span><input value={draft.assignedTo} onChange={(e) => setDraft({ ...draft, assignedTo: e.target.value })} aria-label="Assigned to" /></label>
                  <label className="row"><span className="muted small loc-label">Due</span><input type="date" value={draft.dueDate.slice(0, 10)} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value ? `${e.target.value}T00:00:00Z` : '' })} aria-label="Due date" /></label>
                  <label className="row"><span className="muted small loc-label">Labels</span><input value={draft.labels.join(', ')} onChange={(e) => setDraft({ ...draft, labels: e.target.value.split(',').map((x) => x.trim()).filter(Boolean) })} placeholder="Comma-separated" aria-label="Labels" /></label>
                </div>
                <label className="row small"><input type="checkbox" checked={withView && inThreeD} disabled={!inThreeD} onChange={(e) => setWithView(e.target.checked)} /> With the 3D view: its camera, the selection, the section box, a snapshot</label>
                {!inThreeD && <p className="muted small">Open the 3D view first to keep a viewpoint with the remark.</p>}
                {preview && withView && inThreeD && <img className="remark-snapshot" src={preview} alt="Snapshot" />}
                <div className="row">
                  <button type="button" className="btn primary" onClick={() => void create()} disabled={busy || !draft.title.trim()}>Create</button>
                  <button type="button" className="btn" onClick={() => setCreating(false)}>Cancel</button>
                </div>
              </div>
            )}
            {!creating && !remark && <p className="muted">Pick a remark, or make a new one.</p>}
            {!creating && remark && (
              <div className="remark">
                <input className="remark-heading" value={remark.title} onChange={(e) => setData({ ...data, remarks: data.remarks.map((r) => (r.id === remark.id ? { ...r, title: e.target.value } : r)) })} onBlur={(e) => e.target.value.trim() && e.target.value !== remark.title && void field('title', e.target.value.trim())} aria-label="Title" />
                <p className="muted small">{remark.creationAuthor} · {when(remark.creationDate)}{remark.modifiedDate && <> · <span>edited</span> {remark.modifiedAuthor} {when(remark.modifiedDate)}</>}</p>
                <div className="remark-fields">
                  <label className="row"><span className="muted small loc-label">Status</span><Choice value={remark.status ?? 'Open'} choices={data.statuses} onChange={(v) => void field('status', v)} label="Status" /></label>
                  <label className="row"><span className="muted small loc-label">Priority</span><Choice value={remark.priority ?? ''} choices={data.priorities} onChange={(v) => void field('priority', v)} label="Priority" /></label>
                  <label className="row"><span className="muted small loc-label">Type</span><Choice value={remark.type ?? ''} choices={data.types} onChange={(v) => void field('type', v)} label="Type" /></label>
                  <label className="row"><span className="muted small loc-label">Assigned to</span><input defaultValue={remark.assignedTo ?? ''} key={`a${remark.id}${remark.modifiedDate}`} onBlur={(e) => e.target.value !== (remark.assignedTo ?? '') && void field('assignedTo', e.target.value)} aria-label="Assigned to" /></label>
                  <label className="row"><span className="muted small loc-label">Due</span><input type="date" defaultValue={remark.dueDate?.slice(0, 10) ?? ''} key={`d${remark.id}${remark.modifiedDate}`} onChange={(e) => void field('dueDate', e.target.value ? `${e.target.value}T00:00:00Z` : '')} aria-label="Due date" /></label>
                  <label className="row"><span className="muted small loc-label">Labels</span><input defaultValue={remark.labels.join(', ')} key={`l${remark.id}${remark.modifiedDate}`} onBlur={(e) => e.target.value !== remark.labels.join(', ') && void labels(e.target.value)} aria-label="Labels" /></label>
                </div>
                <textarea className="remark-description" defaultValue={remark.description ?? ''} key={`t${remark.id}${remark.modifiedDate}`} onBlur={(e) => e.target.value !== (remark.description ?? '') && void field('description', e.target.value)} placeholder="Description" aria-label="Description" rows={3} />
                <div className="remark-view">
                  {snapshot && <img className="remark-snapshot" src={`data:image/png;base64,${snapshot}`} alt="Snapshot" />}
                  <div className="row">
                    {viewpoint
                      ? <button type="button" className="btn" onClick={show} title="The 3D view at this remark's camera, with its elements selected and its section planes"><Eye size={13} /> Show in 3D</button>
                      : <span className="muted small">No viewpoint.</span>}
                    {viewpoint && <span className="muted small">{found(viewpoint).length} of {viewpoint.selection.length} elements found</span>}
                    <button type="button" className="btn" onClick={() => void run(() => retakeViewpoint(remark.id))} disabled={busy || !inThreeD} title="The 3D view as it is now becomes this remark's viewpoint (the comments that pointed at the old one follow it)"><Camera size={13} /> Retake from the 3D view</button>
                  </div>
                </div>
                <h4>Comments</h4>
                <ul className="remark-comments">
                  {remark.comments.length === 0 && <li className="muted small">No comment yet.</li>}
                  {remark.comments.map((c) => {
                    const vp = c.viewpoint ? remark.viewpoints.find((v) => v.guid === c.viewpoint) : undefined;
                    return (
                      <li key={c.guid}>
                        <span className="muted small">{c.author} · {when(c.date)}</span>
                        <span>{c.text}</span>
                        {vp && <button type="button" className="link small" onClick={() => void openViewpoint(vp)} title="The 3D view as this comment saw it"><Eye size={11} /> Show</button>}
                      </li>
                    );
                  })}
                </ul>
                <div className="remark-reply">
                  <textarea value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Answer…" aria-label="Comment" rows={2} />
                  <div className="row small">
                    <label className="row small"><input type="checkbox" checked={commentWithView && inThreeD} disabled={!inThreeD} onChange={(e) => setCommentWithView(e.target.checked)} /> With the 3D view</label>
                    <span className="grow" />
                    <button type="button" className="btn primary" onClick={() => void send()} disabled={busy || !comment.trim()}>Comment</button>
                  </div>
                </div>
                <div className="row">
                  <span className="grow" />
                  <button type="button" className="btn" onClick={remove} disabled={busy} title="Delete this remark (one undo step)"><Trash2 size={13} /> Delete</button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
