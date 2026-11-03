/**
 * A parameter's value typed in a schedule goes into the type (decision 251): a fire rating, a manufacturer, a link —
 * the type's own parameter, which its elements inherit. Read back as the schedule writes it: a number as typed, a length
 * in mm, yes or no; empty removes it from the type.
 */

import { execute, useSession } from '../engine/session';

interface StoredParam { name: string; kind?: string; pset?: string; value?: unknown }

/** What the text is for a parameter of this kind; undefined when it does not read as one. */
export function paramValue(kind: string, text: string): unknown {
  const t = text.trim();
  if (kind === 'bool') return /^(yes|y|true|1|da|ja|oui|sì|si|sí)$/i.test(t) ? true : /^(no|n|false|0|nu|nein|non|nej)$/i.test(t) ? false : undefined;
  if (kind === 'number' || kind === 'length' || kind === 'area' || kind === 'angle') {
    const n = Number(t.replace(',', '.'));
    if (!Number.isFinite(n)) return undefined;
    return kind === 'length' ? n / 1000 : n;
  }
  return t;
}

export async function setTypeParam(typeId: string, name: string, text: string): Promise<unknown> {
  const type = useSession.getState().nodes.get(typeId);
  const params = (Array.isArray(type?.props.params) ? type!.props.params : []) as unknown as StoredParam[];
  const existing = params.find((p) => p.name.toLowerCase() === name.toLowerCase());
  if (text.trim() === '') return existing ? execute('element.removeParam', { ids: [typeId], name: existing.name, ...(existing.pset ? { pset: existing.pset } : {}) }) : null;
  const kind = existing?.kind ?? 'text';
  const value = paramValue(kind, text);
  if (value === undefined) { useSession.setState({ error: `${name} takes ${kind === 'bool' ? 'yes or no' : 'a number'}.` }); return null; }
  return execute('element.setParam', { ids: [typeId], name: existing?.name ?? name, value, ...(existing ? {} : { kind: 'text' }), ...(existing?.pset ? { pset: existing.pset } : {}) });
}
