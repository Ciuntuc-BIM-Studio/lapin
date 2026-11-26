/**
 * A type dialog's Parameters (issue #146): the type's own section (ParamsSection) once it exists, and before that the draft
 * (DraftParams) its Create type sends with the type, so every type dialog offers shared and standard parameters from the start,
 * as Wall types has since issue #32. With useNewTypeDraft, the new type's mark typed in its editor (issue #147) goes the same way.
 */

import { useState } from 'react';
import { useSession } from '../engine/session';
import { DraftParams } from './DraftParams';
import { ParamsSection } from './ParamsSection';
import { newTypeArgs } from './typeParamDraft';
import type { Param } from './params';

/** A new type's mark and parameters, held by its dialog until Create type sends them (args()), then cleared (reset()). */
export function useNewTypeDraft() {
  const [mark, setMark] = useState('');
  const [params, setParams] = useState<Param[]>([]);
  return { mark, setMark, params, setParams, args: () => newTypeArgs(mark, params), reset: () => { setMark(''); setParams([]); } };
}

export type NewTypeDraft = ReturnType<typeof useNewTypeDraft>;

export function TypeParamsBlock({ typeClass, typeId, draft, hint }: { typeClass: string; typeId: string | null; draft: NewTypeDraft; hint?: string }) {
  const node = useSession((s) => (typeId ? s.nodes.get(typeId) : undefined));
  return (
    <div className="type-params" data-testid="type-params">
      <div className="row">
        <span className="eyebrow">Parameters</span>
        <span className="muted small">{hint ?? "every element of the type has them (FireRating, IsExternal); an instance default is each element's own copy"}</span>
      </div>
      {typeId && node
        ? <ParamsSection node={node} ids={() => [typeId]} isType />
        : <DraftParams typeClass={typeClass} params={draft.params} onChange={draft.setParams} />}
    </div>
  );
}
