/**
 * Manage → Reference documents…: the documents a project receives — urbanism certificates, permits, land-book extracts, studies,
 * a consultant's IFC — with their metadata, validity and folder (docs/plan-cde-references.md, stage R2). The folder tree on the
 * left (the derived folders greyed: what the project issued), the list in the middle with its filters, the selected document on
 * the right. Files are dropped anywhere on the dialog; documents are dragged onto a folder to move them.
 */

import { backdropClose } from '../../../shell/Dialog';
import { promptText } from '../../../shell/prompts';
import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent } from 'react';
import { ChevronDown, ChevronRight, FilePlus2, FolderSearch, FolderTree as FolderIcon, Paperclip, Settings2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { isDesktop } from '../../../engine/files';
import { isGuest } from '../../../engine/share';
import { useLang } from '../../../i18n';
import { usePhaseList } from '../format';
import { PendingDialog } from '../PendingDialog';
import { today, useReferencesOpen } from './expiry';
import { RefProvider, enginePerms, engineAdapter, type RefAdapter, type RefPerms } from './adapter';
import {
  badgeOf, dragHasFiles, DRAG_TYPE, filterRefs, flattenFolders, folderLanguage, hasNoReferenceSettings,
  NO_FILTER, nameIn, phaseChoices, readFiles, sortRefs,
  type AddResult, type FolderTree, type RefDoc, type RefFilter, type RefFolder, type SortKey, type TypeList,
} from './references';
import { ReferenceDetail } from './ReferenceDetail';
import { StatusBadge } from './StatusBadge';
import { ReferenceScan } from './ReferenceScan';
import { ReferenceSettings } from './ReferenceSettings';

const STATUSES: [string, string][] = [['requested', 'Requested'], ['received', 'Received'], ['expired', 'Expired'], ['superseded', 'Superseded'], ['withdrawn', 'Withdrawn']];
const COLUMNS: [SortKey, string][] = [['title', 'Title'], ['type', 'Type'], ['issuer', 'Issuer'], ['number', 'Number'], ['date', 'Date'], ['validUntil', 'Valid until'], ['status', 'Status']];

interface Outcome { added: number; duplicates: { title: string; of: string[] }[]; warnings: string[] }

/** Manage → Reference documents…: the open project's documents, in a dialog. */
export function ReferencesDialog() {
  const nodes = useSession((s) => s.nodes);
  const guest = isGuest();
  const close = () => useUi.setState({ dialog: null });
  const perms = useMemo(() => enginePerms(guest), [guest]);
  return <ReferencesWorkspace adapter={engineAdapter} perms={perms} reloadOn={nodes} onClose={close} />;
}

/**
 * The list, the folder tree and the document's detail over an adapter: in a dialog over the open project (onClose given), or in the
 * CDE web's Documents tab over a server (`embedded`). `reloadOn` makes it read again when it changes (the project's nodes).
 */
export function ReferencesWorkspace({ adapter, perms, reloadOn, onClose, embedded = false }: { adapter: RefAdapter; perms: RefPerms; reloadOn?: unknown; onClose?: () => void; embedded?: boolean }) {
  const error = useSession((s) => s.error);
  const ui = useLang((s) => s.lang);
  const opened = useReferencesOpen();
  const phases = usePhaseList();
  const guest = !perms.add;
  const server = adapter.kind === 'server';
  const [docs, setDocs] = useState<RefDoc[] | null>(null);
  const [tree, setTree] = useState<FolderTree | null>(null);
  const [types, setTypes] = useState<TypeList | null>(null);
  const [filter, setFilter] = useState<RefFilter>({ ...NO_FILTER, status: opened.status, within: opened.within });
  const [sort, setSort] = useState<{ key: SortKey; desc: boolean }>({ key: 'title', desc: false });
  const [selected, setSelected] = useState<string | null>(null);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [panel, setPanel] = useState<'' | 'scan' | 'settings'>('');
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [dropping, setDropping] = useState(false);
  const [overFolder, setOverFolder] = useState<string | null>(null);
  const [closed, setClosed] = useState<Set<string>>(new Set());
  const input = useRef<HTMLInputElement>(null);
  const close = onClose ?? (() => undefined);
  const now = today();

  // Only the latest read is kept (one started before it, answering late, is dropped).
  const asked = useRef(0);
  const refresh = useCallback(async () => {
    const n = ++asked.current;
    try {
      const [d, f, t] = [await adapter.list(), await adapter.folders(), await adapter.types()];
      if (n !== asked.current || !f || !t) return;
      setDocs(d); setTree(f); setTypes(t);
    } catch (e) { if (n === asked.current) { notify(e instanceof Error ? e.message : String(e)); setDocs((d) => d ?? []); } }
  }, [adapter]);
  useEffect(() => { void refresh(); }, [refresh, reloadOn]);

  // A server reads the files in the background (text, IFC header): the list is read again while any is pending, until none is or the tab closes.
  const pending = server && !!docs?.some((d) => d.textStatus === 'pending');
  useEffect(() => {
    if (!pending) return;
    const t = setTimeout(() => void refresh(), 3000);
    return () => clearTimeout(t);
  }, [pending, docs, refresh]);

  // The folders are named in the interface's language the first time, while the project has no folders of its own (Romanian or English).
  const named = useRef(false);
  useEffect(() => {
    if (named.current || !tree || guest || server) return;
    named.current = true;
    const lang = folderLanguage(ui);
    if (lang !== tree.language && hasNoReferenceSettings(useSession.getState().nodes)) void adapter.setFolders({ language: lang });
  }, [tree, ui, guest, server, adapter]);

  const shown = useMemo(() => (docs && tree ? sortRefs(filterRefs(docs, filter, tree.folders, now), sort.key, sort.desc) : []), [docs, tree, filter, sort, now]);
  const current = docs?.find((d) => d.id === selected) ?? null;
  const set = (patch: Partial<RefFilter>) => setFilter((f) => ({ ...f, ...patch }));
  const fail = (e: unknown) => notify(e instanceof Error ? e.message : String(e));
  const typeName = (id?: string) => nameIn(types?.types.find((x) => x.id === id)?.name, ui) || id || '';

  /** Documents made of the files (one each), into the folder looked at unless it is derived; the duplicates and warnings said. */
  const addFiles = async (files: File[], folder?: string) => {
    if (!files.length || guest) return;
    try {
      const target = folder ?? (filter.folder && !isDerived(tree?.folders ?? [], filter.folder) ? filter.folder : undefined);
      const read = await readFiles(files);
      const result: Outcome = { added: 0, duplicates: [], warnings: [] };
      let last: string | null = null;
      for (const f of read) {
        const r = await adapter.add({ files: [f], ...(target ? { folder: target } : {}), ...(filter.type ? { type: filter.type } : {}) }) as AddResult | null;
        if (!r) continue;
        result.added++; last = r.id;
        if (r.duplicates.length) result.duplicates.push({ title: f.name, of: r.duplicates });
        result.warnings.push(...r.warnings);
      }
      setOutcome(result);
      if (last) setSelected(last);
      await refresh();
    } catch (e) { fail(e); }
  };

  const request = async () => {
    const title = promptText('What is asked for (the title of the document)?')?.trim();
    if (!title) return;
    const folder = filter.folder && !isDerived(tree?.folders ?? [], filter.folder) ? filter.folder : undefined;
    const r = await adapter.add({ title, ...(folder ? { folder } : {}), ...(filter.type ? { type: filter.type } : {}) });
    if (r) setSelected(r.id);
  };

  const onDrop = (e: DragEvent, folder?: string) => {
    e.preventDefault();
    setDropping(false); setOverFolder(null);
    if (dragHasFiles(e)) { e.stopPropagation(); void addFiles([...e.dataTransfer.files], folder); return; }
    const raw = e.dataTransfer.getData(DRAG_TYPE);
    if (folder && raw) { e.stopPropagation(); void adapter.move(JSON.parse(raw) as string[], folder).then(() => refresh()); }
  };

  const sortBy = (key: SortKey) => setSort((s) => (s.key === key ? { key, desc: !s.desc } : { key, desc: false }));
  const pick = (d: RefDoc, e: React.MouseEvent) => {
    if (e.metaKey || e.ctrlKey) setPicked((p) => { const n = new Set(p); if (n.has(d.id)) n.delete(d.id); else n.add(d.id); return n; });
    else { setSelected(d.id); setPicked(new Set()); }
  };
  const drag = (d: RefDoc, e: DragEvent) => {
    const ids = picked.has(d.id) ? [...picked] : [d.id];
    e.dataTransfer.setData(DRAG_TYPE, JSON.stringify(ids));
    e.dataTransfer.effectAllowed = 'move';
  };

  if (!docs || !tree || !types) return embedded ? <p className="muted small">Loading…</p> : <PendingDialog title="Reference documents" />;
  const phaseList = phaseChoices(docs, phases.map((p) => p.name));
  const canScan = !!adapter.scan && isDesktop() && !guest && !!tree.projectFolder;

  const frame = {
    onDragOver: (e: DragEvent) => { if (dragHasFiles(e) && !guest) { e.preventDefault(); setDropping(true); } },
    onDragLeave: (e: DragEvent) => { if (e.currentTarget === e.target) setDropping(false); },
    onDrop: (e: DragEvent) => onDrop(e),
  };
  const shell = (children: React.ReactNode) => (embedded
    ? <div className={`refs refs-embedded${dropping ? ' refs-drop' : ''}`} data-cde-documents {...frame}>{children}{dropping && <div className="refs-drop-hint" aria-hidden="true"><span>Drop to add</span></div>}</div>
    : (
      <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
        <div className={`dialog wide refs${dropping ? ' refs-drop' : ''}`} role="dialog" aria-label="Reference documents" onClick={(e) => e.stopPropagation()} {...frame}>
          <header className="dialog-head"><span className="panel-title">Reference documents</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
          {children}
          <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
          {dropping && <div className="refs-drop-hint" aria-hidden="true"><span>Drop to add</span></div>}
        </div>
      </div>
    ));

  return (
    <RefProvider value={{ adapter, perms }}>
      {shell(
        <div className={embedded ? 'refs-body' : 'dialog-body refs-body'} data-testid="refs">
          <p className="muted small">What the project receives: urbanism certificates, permits and agreements, land-book extracts, studies, a consultant's models. Nothing is deleted outright: a document is superseded or withdrawn. Drop files anywhere on this window to add them.</p>
          <div className="row refs-bar">
            {!guest && <button type="button" className="btn" data-testid="refs-add" onClick={() => input.current?.click()}><Paperclip size={13} /> Add files…</button>}
            {!guest && <button type="button" className="btn" onClick={() => void request()} title="A document asked for and not received yet: it has no file"><FilePlus2 size={13} /> New request</button>}
            {canScan && <button type="button" className={`btn${panel === 'scan' ? ' on' : ''}`} onClick={() => setPanel(panel === 'scan' ? '' : 'scan')}><FolderSearch size={13} /> Scan folder</button>}
            <button type="button" className={`btn${panel === 'settings' ? ' on' : ''}`} onClick={() => setPanel(panel === 'settings' ? '' : 'settings')}><Settings2 size={13} /> Folders and types</button>
            <input ref={input} type="file" multiple hidden data-testid="refs-file" onChange={(e) => { const f = [...(e.target.files ?? [])]; e.target.value = ''; void addFiles(f); }} />
          </div>

          {outcome && (
            <div className="refs-outcome" data-testid="refs-outcome">
              <button type="button" className="icon mini" title="Close" onClick={() => setOutcome(null)}><X size={12} /></button>
              <p className="small"><b>{outcome.added}</b> <span>{outcome.added === 1 ? 'document added' : 'documents added'}</span></p>
              {outcome.duplicates.map((d) => (
                <p key={d.title} className="cde-warn small">{d.title}: <span>the same file is already in another document</span> ({d.of.map((id) => docs.find((x) => x.id === id)?.title ?? id).join(', ')})</p>
              ))}
              {outcome.warnings.map((w, i) => <p key={i} className="cde-warn small">{w}</p>)}
            </div>
          )}

          {panel === 'scan' && <ReferenceScan folders={tree.folders} types={types} onDone={() => void refresh()} />}
          {panel === 'settings' && <ReferenceSettings tree={tree} types={types} guest={!perms.admin} onDone={() => void refresh()} />}

          <div className="refs-grid">
            <nav className="refs-tree" aria-label="Folders" data-testid="refs-tree">
              <button type="button" className={`refs-folder${filter.folder === null ? ' on' : ''}`} onClick={() => set({ folder: null })}>
                <FolderIcon size={12} /> <span className="grow">All documents</span> <span className="refs-count">{docs.length}</span>
              </button>
              <FolderRows nodes={tree.folders} depth={0} selected={filter.folder} over={overFolder} closed={closed}
                onSelect={(id) => set({ folder: id })} onToggle={(id) => setClosed((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; })}
                onOver={setOverFolder} onDrop={onDrop} guest={guest} />
              {tree.unfiled > 0 && <p className="muted small refs-unfiled"><span>Not in a folder of the tree</span>: {tree.unfiled}</p>}
            </nav>

            <section className="refs-list">
              <div className="row refs-filters" role="search">
                <input type="search" placeholder="Search title, number, issuer" aria-label="Search" value={filter.q} onChange={(e) => set({ q: e.target.value })} />
                <select aria-label="Type" value={filter.type} onChange={(e) => set({ type: e.target.value })}>
                  <option value="">All types</option>
                  {types.types.map((x) => <option key={x.id} value={x.id}>{typeName(x.id)}</option>)}
                </select>
                <select aria-label="Phase" value={filter.phase} onChange={(e) => set({ phase: e.target.value })}>
                  <option value="">All phases</option>
                  {phaseList.map((p) => <option key={p} value={p}>{p}</option>)}
                </select>
                <select aria-label="Status" value={filter.status} onChange={(e) => set({ status: e.target.value })}>
                  <option value="">Any status</option>
                  {STATUSES.map(([id, label]) => <option key={id} value={id}>{label}</option>)}
                </select>
                <label className="row small" title="Received documents whose validity ends within this many days">
                  <input type="checkbox" aria-label="Expiring" checked={filter.within != null} onChange={(e) => set({ within: e.target.checked ? 30 : null })} />
                  <span>Expiring within</span>
                  <input type="number" min={1} max={3650} aria-label="Days" className="refs-days" value={filter.within ?? 30} disabled={filter.within == null} onChange={(e) => set({ within: Math.max(1, Number(e.target.value) || 30) })} />
                  <span>days</span>
                </label>
                {(filter.q || filter.type || filter.phase || filter.status || filter.within != null) && <button type="button" className="link" onClick={() => setFilter({ ...NO_FILTER, folder: filter.folder })}>Clear filters</button>}
              </div>
              <div className="refs-table-wrap">
                <table className="layer-table refs-table" data-testid="refs-list">
                  <thead><tr>{COLUMNS.map(([key, label]) => (
                    <th key={key}><button type="button" className="link" onClick={() => sortBy(key)}>{label}{sort.key === key ? (sort.desc ? ' ▾' : ' ▴') : ''}</button></th>
                  ))}</tr></thead>
                  <tbody>
                    {shown.map((d) => {
                      const b = badgeOf(d, now);
                      return (
                        <tr key={d.id} className={`${d.id === selected ? 'on' : ''}${picked.has(d.id) ? ' picked' : ''}${d.status === 'withdrawn' || d.status === 'superseded' ? ' refs-off' : ''}`}
                          draggable={!guest} onDragStart={(e) => drag(d, e)} onClick={(e) => pick(d, e)} data-ref={d.id}>
                          <td>{d.title}{d.files.length > 1 ? <span className="muted small"> · {d.files.length}</span> : null}</td>
                          <td>{d.typeName ?? d.type ?? ''}</td>
                          <td>{d.issuer ?? ''}</td>
                          <td>{d.number ?? ''}</td>
                          <td>{d.date ?? ''}</td>
                          <td>{d.validUntil ?? ''}</td>
                          <td><StatusBadge label={b.label} tone={b.tone} expiry={b.expiry} /></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                {shown.length === 0 && <p className="muted small refs-empty">{docs.length === 0 ? 'No reference document yet. Drop the certificate, the permits, the studies here.' : 'No document passes the filters.'}</p>}
              </div>
              {picked.size > 0 && <p className="muted small"><b>{picked.size}</b> <span>selected: drag them onto a folder to move them</span></p>}
            </section>

            <aside className="refs-detail">
              {current
                ? <ReferenceDetail key={current.id} doc={current} docs={docs} types={types} tree={tree} lang={ui} phases={phaseList} guest={!perms.canChange(current)} canDelete={perms.remove} onSelect={setSelected} onChanged={() => void refresh()} />
                : <p className="muted small">Select a document to see its details, files and links.</p>}
            </aside>
          </div>
        </div>)}
    </RefProvider>
  );
}

const isDerived = (nodes: RefFolder[], id: string): boolean => flattenFolders(nodes, true).some((f) => f.id === id && f.derived);

function FolderRows({ nodes, depth, selected, over, closed, onSelect, onToggle, onOver, onDrop, guest }: {
  nodes: RefFolder[]; depth: number; selected: string | null; over: string | null; closed: Set<string>; guest: boolean;
  onSelect: (id: string) => void; onToggle: (id: string) => void; onOver: (id: string | null) => void; onDrop: (e: DragEvent, folder?: string) => void;
}) {
  return (
    <>
      {nodes.map((n) => {
        const kids = n.children ?? [];
        const open = !closed.has(n.id);
        const derived = !!n.derived;
        return (
          <div key={n.id}>
            <div className={`refs-folder${selected === n.id ? ' on' : ''}${derived ? ' derived' : ''}${over === n.id ? ' over' : ''}`} style={{ paddingLeft: 6 + depth * 12 }} data-folder={n.id}
              title={derived ? 'Shows what this project issued: it holds no reference documents, and nothing is dropped here' : undefined}
              onDragOver={(e) => { if (!derived && !guest) { e.preventDefault(); e.stopPropagation(); onOver(n.id); } }}
              onDragLeave={() => onOver(null)}
              onDrop={(e) => { if (derived) { e.preventDefault(); e.stopPropagation(); onOver(null); } else onDrop(e, n.id); }}>
              {kids.length > 0
                ? <button type="button" className="icon mini" aria-label={open ? 'Fold' : 'Unfold'} onClick={() => onToggle(n.id)}>{open ? <ChevronDown size={11} /> : <ChevronRight size={11} />}</button>
                : <span className="refs-twisty" />}
              <button type="button" className="refs-name" onClick={() => onSelect(n.id)}>{n.text}</button>
              <span className="refs-count" title={kids.length ? 'With its subfolders' : undefined}>{derived ? (n.count || '') : n.total}</span>
            </div>
            {open && kids.length > 0 && <FolderRows nodes={kids} depth={depth + 1} selected={selected} over={over} closed={closed} onSelect={onSelect} onToggle={onToggle} onOver={onOver} onDrop={onDrop} guest={guest} />}
          </div>
        );
      })}
    </>
  );
}
