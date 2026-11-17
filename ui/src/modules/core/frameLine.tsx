/**
 * The pen weight, colour and line type of detail callouts' boundaries and inset views' frames (issue #73): the fields a callout's
 * and an inset's properties show (their own), and the rows of Drafting standards → Project (the project's default). An empty
 * field is the next one's: the project's, then Lapin's own. The engine draws them (Callouts.cs, Insets.cs), so views, sheets and
 * PDF agree.
 */

import { useEffect, useState } from 'react';
import { RotateCcw } from 'lucide-react';
import { execute, query, useSession } from '../../engine/session';
import { ColourField } from '../../shell/ColourField';
import { useLineTypes } from './lineTypes/lineTypes';
import { lineTypeNames, withCurrent } from './lineTypes/lineTypeModel';
import { inheritedFrame, lapinFrame, readFrameLine, standardPatch, weightChoices, type FrameKind, type FrameLine } from './frameLineModel';

/** The project's default for a kind, kept fresh. */
export function useStandardFrame(kind: FrameKind): FrameLine {
  const version = useSession((s) => s.version);
  const [line, setLine] = useState<FrameLine>({});
  useEffect(() => {
    let live = true;
    void query<Record<string, unknown>>('standards.get').then((b) => { if (live && b) setLine(readFrameLine(b[kind === 'callout' ? 'calloutLine' : 'insetFrame'])); });
    return () => { live = false; };
  }, [version, kind]);
  return line;
}

interface FieldsProps {
  /** A prefix for the fields' test ids. */
  field: string;
  value: FrameLine;
  /** What draws where a field is empty. */
  base: Required<FrameLine>;
  onSet: (field: keyof FrameLine, value: number | string | null) => void;
}

/** The three fields, as rows of a field list (dt, dd). */
export function FrameLineFields({ field, value, base, onSet }: FieldsProps) {
  const lineTypes = useLineTypes();
  const names = withCurrent(lineTypeNames(lineTypes), value.lineType ?? null);
  return (
    <>
      <dt title="Its pen weight on paper (mm); default: the drafting standards', else Lapin's own">Pen weight</dt>
      <dd>
        <select data-testid={`${field}-weight`} value={value.weight != null ? String(value.weight) : ''} onChange={(e) => onSet('weight', e.target.value ? Number(e.target.value) : null)}>
          <option value="">default</option>
          {weightChoices(value.weight).map((w) => <option key={w} value={String(w)} data-no-i18n>{`${w} mm`}</option>)}
        </select>
        {value.weight == null && <span className="muted small" data-no-i18n> {base.weight} mm</span>}
      </dd>
      <dt title="Its colour; default: the drafting standards', else Lapin's own">Line colour</dt>
      <dd>
        <ColourField data-testid={`${field}-color`} value={value.color ?? base.color} onChange={(v) => onSet('color', v)} />
        {value.color != null && <button type="button" className="icon mini" title="Back to the default" onClick={() => onSet('color', null)}><RotateCcw size={12} /></button>}
      </dd>
      <dt title="Its line type, Lapin's or one of the project's own (Drafting standards → Line types); default: the drafting standards', else Lapin's own">Line type</dt>
      <dd>
        <select data-testid={`${field}-type`} value={value.lineType ?? ''} onChange={(e) => onSet('lineType', e.target.value || null)}>
          <option value="">default</option>
          {names.map((n) => <option key={n} value={n}>{n}</option>)}
        </select>
        {value.lineType == null && <span className="muted small"> {base.lineType}</span>}
      </dd>
    </>
  );
}

/** Drafting standards → Project: the callouts' boundary and the insets' frame, the project's default. */
export function FrameLinesStandards({ book }: { book: Record<string, unknown> }) {
  const rows: [FrameKind, string, string, string][] = [
    ['callout', 'calloutLine', 'Detail callout boundary', "How detail callouts' boundaries are drawn in views, sheets and PDF; a callout's own, in its properties, goes before"],
    ['inset', 'insetFrame', 'Inset view frame', "How inset views' frames are drawn; an inset's own, in its properties, goes before"],
  ];
  return (
    <>
      {rows.map(([kind, key, label, title]) => [
        <dt key={`${kind}t`} className="group-title" title={title}>{label}</dt>,
        <dd key={`${kind}d`} className="muted small">Empty fields: Lapin's own.</dd>,
        <FrameLineFields key={`${kind}f`} field={`standard-${kind}-line`} value={readFrameLine(book[key])} base={lapinFrame(kind)}
          onSet={(f, v) => void execute('standards.frameLine', standardPatch(kind, f, v))} />,
      ])}
    </>
  );
}

/** What a callout's or an inset's own fields fall back on: the project's, else Lapin's own. */
export function useInheritedFrame(kind: FrameKind, refers = true): Required<FrameLine> {
  return inheritedFrame(kind, useStandardFrame(kind), refers);
}
