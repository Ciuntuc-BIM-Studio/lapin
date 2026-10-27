/**
 * A scaffold's or a formwork's type parameters: what its configurator sets
 * (the system, the bays and lifts, the plywood, the spacings, the article),
 * shown under Type parameters on every element of the type and edited there.
 * A change is the type's, so every scaffold or formwork of it follows. The
 * tables (a floor system's spacings, the props) stay in the configurator.
 */

import { Cell, LengthCell } from '../../shell/cells';
import { parseNumber } from '../../shell/cellKeys';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { MmList } from './fields';
import { setSiteWorks, type FormworkKind } from './prefs';

interface Field { key: string; label: string; kind: 'length' | 'lengths' | 'bool' | 'text' | 'number' | 'choice'; int?: boolean; suffix?: string; options?: [string, string][] }
type Spec = Record<string, unknown>;

const ARTICLE: Field[] = [
  { key: 'reuses', label: 'Reuses', kind: 'number', int: true },
  { key: 'article', label: 'Article code', kind: 'text' },
  { key: 'articleText', label: 'Article text', kind: 'text' },
];

const SCAFFOLD: Field[] = [
  { key: 'system', label: 'System', kind: 'choice', options: [['modular', 'modular'], ['frame', 'frames']] },
  { key: 'bays', label: 'Bay lengths (mm)', kind: 'lengths' },
  { key: 'width', label: 'Width', kind: 'length' },
  { key: 'lift', label: 'Lift', kind: 'length' },
  { key: 'wallGap', label: 'From the wall', kind: 'length' },
  { key: 'decks', label: 'Decks', kind: 'choice', options: [['every', 'on every lift'], ['top', 'on the top lift only']] },
  { key: 'deckBoard', label: 'Deck board', kind: 'length' },
  { key: 'guardrail', label: 'Guardrail', kind: 'length' },
  { key: 'toeBoard', label: 'Toe boards', kind: 'bool' },
  { key: 'reach', label: 'Top deck within', kind: 'length' },
  { key: 'anchorSpacing', label: 'Anchors every', kind: 'length' },
  { key: 'anchorEveryLifts', label: 'on every n-th lift', kind: 'number', int: true },
  { key: 'jack', label: 'Base jacks take', kind: 'length' },
  { key: 'accessEvery', label: 'An access bay every', kind: 'number', suffix: 'm' },
  { key: 'net', label: 'Protective net', kind: 'bool' },
];

const slab = (spec: Spec): Field[] => [
  { key: 'system', label: 'System', kind: 'choice', options: [['beams', 'plywood on beams'], ['panels', 'panels']] },
  ...(spec.system === 'panels'
    ? [
      { key: 'panelLength', label: 'Panel length', kind: 'length' },
      { key: 'panelWidth', label: 'Panel width', kind: 'length' },
      { key: 'panelThickness', label: 'Panel depth', kind: 'length' },
    ] as Field[]
    : [
      { key: 'plywood', label: 'Plywood', kind: 'length' },
      { key: 'secondaryHeight', label: 'Secondary beams', kind: 'length' },
      { key: 'primaryHeight', label: 'Primary beams', kind: 'length' },
      { key: 'beamWidth', label: 'Beam width', kind: 'length' },
      { key: 'lengths', label: 'Beam lengths (mm)', kind: 'lengths' },
    ] as Field[]),
  { key: 'edgeInset', label: 'First beam in from the edge', kind: 'length' },
  { key: 'upstand', label: 'Edge upstand', kind: 'length' },
  ...ARTICLE,
];

const BEAM: Field[] = [
  { key: 'plywood', label: 'Plywood', kind: 'length' },
  { key: 'bearers', label: 'Bearers', kind: 'number', int: true },
  { key: 'bearerWidth', label: 'Bearer width', kind: 'length' },
  { key: 'bearerHeight', label: 'Bearer height', kind: 'length' },
  { key: 'propSpacing', label: 'Props every', kind: 'length' },
  { key: 'chamfer', label: 'Chamfer fillet', kind: 'length' },
  ...ARTICLE,
];

const UPRIGHT: Field[] = [
  { key: 'panelWidths', label: 'Panel widths (mm)', kind: 'lengths' },
  { key: 'panelHeights', label: 'Panel heights (mm)', kind: 'lengths' },
  { key: 'panelThickness', label: 'Panel depth', kind: 'length' },
  { key: 'tieAlong', label: 'Ties every, along', kind: 'length' },
  { key: 'tieUp', label: 'Ties every, up', kind: 'length' },
  { key: 'clampSpacing', label: 'Column clamps every', kind: 'length' },
  { key: 'braceSpacing', label: 'Push-pull props every', kind: 'length' },
  ...ARTICLE,
];

const CATALOG: Record<FormworkKind, string> = { slab: 'sitework.slabFormworkDefault', beam: 'sitework.beamFormworkDefault', upright: 'sitework.uprightFormworkDefault' };

function SpecFields({ type, fields, spec, command, configure }: { type: NodeDto; fields: Field[]; spec: Spec; command: string; configure: () => void }) {
  const set = (key: string, value: unknown) => void execute(command, { id: type.id, spec: { ...spec, [key]: value } });
  return (
    <dl className="fields">
      {fields.map((f) => (
        <span key={f.key} className="qrow">
          <dt title="Set by the type's configurator: every element of the type has it">{f.label}</dt>
          <dd>
            {f.kind === 'length' ? <LengthCell metres={spec[f.key] as number} onCommit={(m) => m != null && m >= 0 && set(f.key, m)} />
              : f.kind === 'lengths' ? <MmList value={(spec[f.key] as number[] | undefined) ?? []} onCommit={(v) => set(f.key, v)} />
              : f.kind === 'bool' ? <input type="checkbox" checked={spec[f.key] === true} onChange={(e) => set(f.key, e.target.checked)} />
              : f.kind === 'choice' ? <select className="cell" value={String(spec[f.key] ?? '')} onChange={(e) => set(f.key, e.target.value)}>{(f.options ?? []).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
              : f.kind === 'text' ? <Cell value={String(spec[f.key] ?? '')} onCommit={(t) => set(f.key, t.trim())} />
              : <Cell className="num" value={spec[f.key] == null ? '' : String(spec[f.key])} suffix={f.suffix}
                  onCommit={(t) => { const n = parseNumber(t); if (n != null && n >= 0) set(f.key, f.int ? Math.max(1, Math.round(n)) : n); }} />}
          </dd>
        </span>
      ))}
      <dt /><dd><button type="button" className="btn compact" onClick={configure}>Configurator…</button></dd>
    </dl>
  );
}

export function ScaffoldTypeFields({ type }: { type: NodeDto }) {
  const fallback = useSession((s) => s.info?.catalogs['sitework.scaffoldDefault']) as Spec | undefined;
  const spec = { ...(fallback ?? {}), ...((type.props.spec as Spec | undefined) ?? {}) };
  const configure = () => { setSiteWorks({ scaffoldTypeId: type.id }); useUi.setState({ dialog: 'scaffoldTypes' }); };
  return <SpecFields type={type} fields={SCAFFOLD} spec={spec} command="sitework.updateScaffoldType" configure={configure} />;
}

export function FormworkTypeFields({ type }: { type: NodeDto }) {
  const kind = ((type.props.kind as string | undefined) ?? 'slab') as FormworkKind;
  const fallback = useSession((s) => s.info?.catalogs[CATALOG[kind]]) as Spec | undefined;
  const spec = { ...(fallback ?? {}), ...((type.props.spec as Spec | undefined) ?? {}) };
  const fields = kind === 'beam' ? BEAM : kind === 'upright' ? UPRIGHT : slab(spec);
  const configure = () => { setSiteWorks({ formworkTypeId: type.id, formworkKind: kind }); useUi.setState({ dialog: 'formworkTypes' }); };
  return <SpecFields type={type} fields={fields} spec={spec} command="sitework.updateFormworkType" configure={configure} />;
}
