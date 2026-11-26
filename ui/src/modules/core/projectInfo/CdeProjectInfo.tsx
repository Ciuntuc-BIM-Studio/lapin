/**
 * Publishing → Project information (docs/plan-archicad.md A1b): the server project's information record, read by everyone, changed by
 * editors and owners (only the fields changed are sent, with the record's version; a change made meanwhile is read again and the edits
 * kept to save again); the open proposals from Archicad, IFC models and by hand, each change shown current → proposed, applied by owners
 * in part (the keys ticked) or whole, dismissed by owners or their author; and what the models said that no field takes ("From the
 * models", read-only). The CDE web reaches the server itself; File → Collaborate on the desktop goes through the engine (remote.atrium),
 * and then syncs the open project with the record after a change.
 */

import { Fragment, useCallback, useEffect, useMemo, useState } from 'react';
import { Check, RefreshCw, X } from 'lucide-react';
import { notify } from '../../../app/store';
import { account, getRules } from '../../../engine/atrium';
import type { Role } from '../../../engine/cdeServer';
import { syncProjectInfo } from '../../../engine/remote';
import { when } from '../format';
import {
  INFO_FIELDS, INFO_GROUPS, SOURCE_NAMES, applyProposal, changeLabel, dismissProposal, fieldPatch, getInfo, infoPerms, isFieldChange, listProposals, patchInfo, sourceRows,
  type InfoProposal, type InfoRecord,
} from './serverInfo';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const MULTILINE = new Set(['clientAddress', 'designerAddress', 'address']);

function ProposalView({ p, record, canApply, canDismiss, busy, onApply, onDismiss }: {
  p: InfoProposal; record: InfoRecord | null; canApply: boolean; canDismiss: boolean; busy: boolean;
  onApply: (keys: string[]) => void; onDismiss: () => void;
}) {
  const [ticked, setTicked] = useState<Set<string>>(() => new Set(p.changes.filter(isFieldChange).map((c) => c.key)));
  useEffect(() => { setTicked(new Set(p.changes.filter(isFieldChange).map((c) => c.key))); }, [p.id, p.changes.length]);
  const toggle = (k: string) => setTicked((s) => { const n = new Set(s); if (n.has(k)) n.delete(k); else n.add(k); return n; });
  return (
    <div className="cde-info-proposal" data-cde-info-proposal={p.number}>
      <p className="small"><b><span>Proposal</span> <span data-no-i18n>#{p.number}</span></b> <span className="muted">·</span> <span>{SOURCE_NAMES[p.source] ?? p.source}</span>
        {p.sourceRef?.file && <span className="muted" data-no-i18n> · {p.sourceRef.file}</span>}
        <span className="muted" data-no-i18n> · {p.createdByName ?? ''} · {when(p.created)}</span></p>
      {p.changes.length === 0 ? <p className="muted small">Nothing in it differs from the record any more.</p> : (
        <table className="cde-info-changes small">
          <thead><tr>{canApply && <th />}<th>Field</th><th>Current</th><th>Proposed</th></tr></thead>
          <tbody>
            {p.changes.map((c) => (
              <tr key={c.key} data-cde-info-change={c.key}>
                {canApply && <td><input type="checkbox" checked={ticked.has(c.key)} onChange={() => toggle(c.key)} aria-label="Take this value" /></td>}
                <td>{isFieldChange(c) ? <span>{changeLabel(c, record)}</span> : <><span className="muted">From the models:</span> <span data-no-i18n>{changeLabel(c, record)}</span></>}</td>
                <td className="muted" data-no-i18n>{c.current ?? '—'}</td>
                <td data-no-i18n>{c.proposed}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="cde-actions">
        {canApply && p.changes.length > 0 && <button type="button" className="btn primary" disabled={busy || ticked.size === 0} data-cde-info-apply onClick={() => onApply([...ticked])}><Check size={13} />Apply the ticked values</button>}
        {canDismiss && <button type="button" className="btn" disabled={busy} data-cde-info-dismiss onClick={onDismiss}><X size={13} />Dismiss</button>}
      </div>
    </div>
  );
}

export function CdeProjectInfo({ server, project, withModel = false }: { server: string; project: string; withModel?: boolean }) {
  const [who, setWho] = useState<{ role: Role; authorId: string | null } | null>(null);
  const [record, setRecord] = useState<InfoRecord | null>(null);
  const [proposals, setProposals] = useState<InfoProposal[] | null>(null);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [failed, setFailed] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    setFailed(null);
    void (async () => {
      try {
        const [a, rules, r, ps] = await Promise.all([account(server), getRules(server, project).catch(() => null), getInfo(server, project), listProposals(server, project).catch(() => [])]);
        setWho({ role: (rules?.role ?? a.projects.find((p) => p.id === project)?.role ?? 'viewer') as Role, authorId: a.authorId ?? null });
        setRecord(r); setProposals(ps);
      } catch (e) { setFailed(message(e)); }
    })();
  }, [server, project]);
  useEffect(load, [load]);

  const perms = infoPerms(who?.role ?? 'viewer', who?.authorId ?? null);
  const patch = useMemo(() => fieldPatch(record?.fields ?? {}, draft), [record, draft]);
  const dirty = Object.keys(patch).length > 0;
  const rows = sourceRows(record);
  const custom = Object.entries(record?.custom ?? {});

  /** After a change on the desktop, the open project takes the record (one undo step). */
  const followed = async () => { if (withModel) await syncProjectInfo(server, true); };

  const run = async (what: () => Promise<void>) => {
    if (busy) return;
    setBusy(true);
    try { await what(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };

  const save = () => run(async () => {
    const r = await patchInfo(server, project, { fields: patch }, record?.etag);
    setRecord(r.record);
    if (r.changed) { notify('The project information changed on the server meanwhile: it was read again. Your edits are kept: save again.'); return; }
    setDraft({});
    notify('Project information saved on the server.');
    await followed();
  });

  const apply = (p: InfoProposal, keys: string[]) => run(async () => {
    const r = await applyProposal(server, project, p.id, keys);
    notify('Proposal applied.');
    setRecord({ ...r.info, etag: r.info.etag });
    setProposals(await listProposals(server, project));
    await followed();
  });

  const dismiss = (p: InfoProposal) => run(async () => {
    await dismissProposal(server, project, p.id);
    notify('Proposal dismissed.');
    setProposals(await listProposals(server, project));
  });

  if (failed) return <p className="muted small" data-cde-info="failed" data-no-i18n>{failed}</p>;
  if (!record || !who || !proposals) return <p className="muted small">Loading…</p>;
  const value = (k: string) => (k in draft ? draft[k] : record.fields[k] ?? '');

  return (
    <div className="cde-info" data-cde-info>
      <p className="muted small">The project's information, as Lapin's Manage → Project information… has it: Lapin syncs it field by field with Save to server, Get latest and Sync issues and documents. Archicad and IFC models propose values; owners apply them.
        <button type="button" className="icon" title="Refresh" onClick={load}><RefreshCw size={12} /></button></p>
      {record.modifiedAt && <p className="muted small"><span>Last changed:</span> <span data-no-i18n>{record.modifiedByName ?? ''} · {when(record.modifiedAt)}</span></p>}

      {proposals.length > 0 && (
        <section className="cde-info-proposals" data-cde-info-proposals={proposals.length}>
          <h4 className="eyebrow">Open proposals</h4>
          {!perms.apply && <p className="muted small">The project's owners apply proposals.</p>}
          {proposals.map((p) => (
            <ProposalView key={p.id} p={p} record={record} canApply={perms.apply} canDismiss={perms.dismiss(p)} busy={busy} onApply={(keys) => void apply(p, keys)} onDismiss={() => void dismiss(p)} />
          ))}
        </section>
      )}

      <div className="doc-info">
        {INFO_GROUPS.map((g) => (
          <section key={g.key} className="doc-info-group">
            <h4 className="eyebrow">{g.label}</h4>
            {INFO_FIELDS.filter((f) => f.group === g.key).map((f) => (
              <label key={f.key} className="doc-info-row">
                <span className="muted small">{f.label}</span>
                {perms.edit
                  ? MULTILINE.has(f.key)
                    ? <textarea rows={2} value={value(f.key)} data-cde-info-field={f.key} data-no-i18n onChange={(e) => setDraft((d) => ({ ...d, [f.key]: e.target.value }))} />
                    : <input value={value(f.key)} data-cde-info-field={f.key} data-no-i18n onChange={(e) => setDraft((d) => ({ ...d, [f.key]: e.target.value }))} />
                  : <span data-cde-info-field={f.key} data-no-i18n>{record.fields[f.key] ?? '—'}</span>}
              </label>
            ))}
          </section>
        ))}
        {custom.length > 0 && (
          <section className="doc-info-group" data-cde-info-custom={custom.length}>
            <h4 className="eyebrow">The project's own parameters</h4>
            <dl className="fields small">{custom.map(([k, p]) => <Fragment key={k}><dt data-no-i18n>{p.label}</dt><dd data-no-i18n>{p.value ?? '—'}</dd></Fragment>)}</dl>
            <p className="muted small">They are changed in Lapin (Manage → Project information…).</p>
          </section>
        )}
        {rows.length > 0 && (
          <section className="doc-info-group" data-cde-info-source={rows.length}>
            <h4 className="eyebrow">From the models</h4>
            <p className="muted small">What Archicad and the IFC models said that no field takes, kept as they said it (read-only).</p>
            <dl className="fields small">{rows.map((r) => <Fragment key={`${r.source}.${r.id}`}><dt><span data-no-i18n>{r.label}</span> <span className="muted">({SOURCE_NAMES[r.source] ?? r.source})</span></dt><dd data-no-i18n>{r.value}</dd></Fragment>)}</dl>
          </section>
        )}
      </div>
      {perms.edit ? (
        <div className="cde-actions">
          <button type="button" className="btn primary" disabled={busy || !dirty} data-cde-info-save onClick={() => void save()}><Check size={13} />Save</button>
          {dirty && <button type="button" className="btn" disabled={busy} onClick={() => setDraft({})}>Discard the edits</button>}
        </div>
      ) : <p className="muted small">Viewers read the project information; editors and owners change it.</p>}
    </div>
  );
}
