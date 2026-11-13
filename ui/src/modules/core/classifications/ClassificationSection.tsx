/**
 * Properties → Classification: the codes an element or a type carries, one for each system (Uniclass Ss_25_10, a keynote of the
 * office's own), with a searchable picker to give or change one and a button to take one off. The codes of a type are the type's;
 * a tag reads the element's own, else its type's. Changes go to every selected element of the kind.
 */

import { useState } from 'react';
import { Plus, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import type { NodeDto } from '../../../engine/protocol';
import { selectionWith } from '../../../shell/params';
import { ItemTree } from './ItemTree';
import { assignCode, unassignCode, useSystems } from './classifications';
import { refsOf, refsText } from './classModel';

export function ClassificationSection({ node, ids: who }: { node: NodeDto; ids?: () => string[] }) {
  const systems = useSystems();
  const refs = refsOf(node.props.classification);
  const [adding, setAdding] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const ids = () => who?.() ?? selectionWith(node);
  const system = systems.find((s) => s.id === adding);
  const pick = (code: string) => { if (system) void assignCode({ ids: ids(), system: system.id, code }).then(() => { setAdding(null); setSearch(''); }); };
  // A code the system lacks becomes an item of it (a keynote of the office's own), with the title typed after a space.
  const addNew = () => {
    if (!system) return;
    const [code, ...rest] = search.trim().split(/\s+/);
    if (code) void assignCode({ ids: ids(), system: system.id, code, add: true, ...(rest.length ? { title: rest.join(' ') } : {}) }).then(() => { setAdding(null); setSearch(''); });
  };
  return (
    <div className="classification-section" data-testid="classification-section">
      {refs.length > 0 && (
        <dl className="fields">
          {refs.map((r) => {
            const s = systems.find((x) => x.id === r.system);
            return (
              <span key={r.system} className="qrow" data-class-ref={s?.name ?? r.system}>
                <dt data-no-i18n>{s?.name ?? '?'}</dt>
                <dd><span className="mono" data-no-i18n>{r.code}</span>
                  <button type="button" className="icon mini row-x" title="Take the code off" onClick={() => void unassignCode(ids(), r.system)}><X size={10} /></button></dd>
              </span>
            );
          })}
        </dl>
      )}
      {systems.length === 0 ? (
        <p className="muted small">No classification in the project yet: <button type="button" className="link-button" onClick={() => useUi.setState({ dialog: 'classifications' })}>Manage → Classifications…</button></p>
      ) : adding == null ? (
        <div className="param-add">
          <select value="" data-field="class-add-system" title="Give a code of a system" onChange={(e) => { if (e.target.value) { setAdding(e.target.value); setSearch(''); } }}>
            <option value="">Classify…</option>
            {systems.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
          </select>
          {refs.length > 0 && <span className="muted small" data-no-i18n>{refsText(refs, systems)}</span>}
        </div>
      ) : (
        <div className="class-picker" data-testid="class-picker">
          <div className="row">
            <strong className="small" data-no-i18n>{system?.name}</strong>
            <input autoFocus type="search" placeholder="Find a code or a title" value={search} data-field="class-pick-search" onChange={(e) => setSearch(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') setAdding(null); }} />
            <button type="button" className="icon" title="Close" onClick={() => setAdding(null)}><X size={13} /></button>
          </div>
          <div className="class-tree-box small">{system && <ItemTree system={system.id} search={search} onPick={(i) => pick(i.code)} picked={refs.find((r) => r.system === system.id)?.code ?? null} />}</div>
          {search.trim() && (
            <button type="button" className="btn small" data-field="class-add-new" title="The code is added to the system as a keynote of your own (a title may follow the code)" onClick={addNew}><Plus size={12} />Use “{search.trim()}” as a new code</button>
          )}
        </div>
      )}
    </div>
  );
}
