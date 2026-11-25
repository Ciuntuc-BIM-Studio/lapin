/**
 * The Structural (load-bearing) tick in the properties (docs/plan-structural-usage.md, decision 866): ticked or cleared it is
 * said, on every selected element that may bear load at once; unset, the rule's answer shows greyed with where it came from.
 * A second row sets it on their type (every element of the type that says nothing itself).
 */

import { useEffect, useState } from 'react';
import { RotateCcw } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { byRule, isStructuralClass, setArgs, sourceText, type StructuralRow } from './structural';

export function StructuralSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const version = useSession((s) => s.version);
  const selected = useUi((s) => s.selected);
  const ids = [...new Set([node.id, ...selected])].filter((id) => { const n = nodes.get(id); return !!n && isStructuralClass(n.type); });
  const [rows, setRows] = useState<StructuralRow[]>([]);
  const key = ids.join(',');
  useEffect(() => {
    let live = true;
    void query<StructuralRow[]>('element.structural', { ids }).then((r) => { if (live) setRows((r ?? []).filter((x) => x.applies)); });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, version]);
  const row = rows.find((r) => r.id === node.id) ?? rows[0];
  if (!row) return null;
  const on = rows.filter((r) => r.effective).length;
  const mixed = on > 0 && on < rows.length;
  const ruled = rows.every(byRule);
  const isType = node.type.endsWith('Type');
  const typed = !isType && rows.some((r) => r.typeSaid != null);
  const typeOn = rows.filter((r) => r.typeSaid === true).length;
  return (
    <dl className="fields" data-testid="structural">
      <dt title="Structural: it carries loads. Ticked or cleared here, it is said; unset, the rule decides (shown greyed).">Structural (load-bearing)</dt>
      <dd>
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, opacity: ruled ? 0.55 : 1 }}>
          <input type="checkbox" data-action="structural" checked={on === rows.length}
            ref={(el) => { if (el) el.indeterminate = mixed; }}
            onChange={(e) => void execute('element.setStructural', setArgs(ids, e.target.checked))} />
          <span className="muted small" data-testid="structural-source">{sourceText(row)}</span>
        </label>
        {!ruled && (
          <button type="button" className="icon mini" data-action="structural-rule" title="Back to the rule" onClick={() => void execute('element.setStructural', setArgs(ids, null))}><RotateCcw size={11} /></button>
        )}
        {rows.some((r) => r.conflict) && <div className="muted small" data-testid="structural-conflict">Its LoadBearing property says otherwise; the tick wins.</div>}
      </dd>
      {!isType && (
        <>
          <dt title="The tick on the type: every element of the type that says nothing itself takes it">Type structural</dt>
          <dd>
            <input type="checkbox" data-action="structural-type" checked={typed && typeOn === rows.length}
              ref={(el) => { if (el) el.indeterminate = typeOn > 0 && typeOn < rows.length; }}
              onChange={(e) => void execute('element.setStructural', setArgs(ids, e.target.checked, true))} />
            {typed && <button type="button" className="icon mini" title="Back to the rule" onClick={() => void execute('element.setStructural', setArgs(ids, null, true))}><RotateCcw size={11} /></button>}
          </dd>
        </>
      )}
    </dl>
  );
}
