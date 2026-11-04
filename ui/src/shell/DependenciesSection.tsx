/**
 * What the selected element depends on, and what depends on it, as the engine
 * knows it (core.dependents, docs/dependencies.md): the elements built from it
 * change when it changes, or when it is deleted. Elements are picked by a
 * click; its type and materials are named.
 */

import { Fragment, useEffect, useState } from 'react';
import { query, useSession } from '../engine/session';
import { select } from '../app/store';
import type { NodeDto, SessionInfo } from '../engine/protocol';

interface Answer { dependents: string[]; dependencies?: string[] }

/** At most this many elements listed in a row; the rest counted. */
const SHOWN = 30;

const kindOf = (info: SessionInfo | null, n: NodeDto) => info?.nodeTypes.find((t) => t.type === n.type)?.category;

function Elements({ ids }: { ids: string[] }) {
  const nodes = useSession((s) => s.nodes);
  const info = useSession((s) => s.info);
  const elements = ids.map((id) => nodes.get(id)).filter((n): n is NodeDto => !!n && kindOf(info, n) === 'element');
  if (elements.length === 0) return null;
  return (
    <span data-no-i18n>
      {elements.slice(0, SHOWN).map((n, i) => (
        <Fragment key={n.id}>{i > 0 && ', '}<button type="button" className="link" title="Select it" onClick={() => select(n.id)}>{n.name || n.type}</button></Fragment>
      ))}
      {elements.length > SHOWN && ` +${elements.length - SHOWN}`}
    </span>
  );
}

export function DependenciesSection({ node }: { node: NodeDto }) {
  const version = useSession((s) => s.version);
  const nodes = useSession((s) => s.nodes);
  const info = useSession((s) => s.info);
  const [answer, setAnswer] = useState<Answer | null>(null);
  useEffect(() => {
    let live = true;
    void query<Answer>('core.dependents', { id: node.id }).then((r) => { if (live) setAnswer(r); });
    return () => { live = false; };
  }, [node.id, version]);
  if (!answer) return null;
  const from = (answer.dependencies ?? []).map((id) => nodes.get(id)).filter((n): n is NodeDto => !!n);
  const types = from.filter((n) => kindOf(info, n) === 'type');
  const materials = from.filter((n) => n.type === 'IfcMaterial');
  const elements = from.filter((n) => kindOf(info, n) === 'element').map((n) => n.id);
  if (answer.dependents.length === 0 && from.length === 0) return null;
  return (
    <section className="sect">
      <h3 className="eyebrow">Dependencies</h3>
      <dl className="fields">
        <dt title="Built from this element: they change when it changes, or is deleted">Used by</dt>
        <dd>{answer.dependents.length ? <Elements ids={answer.dependents} /> : <span className="muted">Nothing depends on it.</span>}</dd>
        {elements.length > 0 && <><dt title="The elements this one is built from">Built from</dt><dd><Elements ids={elements} /></dd></>}
        {types.length > 0 && <><dt>Type</dt><dd data-no-i18n>{types.map((t) => t.name).join(', ')}</dd></>}
        {materials.length > 0 && <><dt>Materials</dt><dd data-no-i18n>{materials.map((m) => m.name).join(', ')}</dd></>}
      </dl>
    </section>
  );
}
