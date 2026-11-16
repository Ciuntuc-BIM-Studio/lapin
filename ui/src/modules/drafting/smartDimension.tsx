/**
 * One Dimension tool (audit 2026-11-15, S9): DIMENSION picks the kind from what is clicked and offers the rest in its bar.
 * **Smart**: two points make an aligned (or linear) dimension; a drafted line clicked away from its snaps is measured end to
 * end, and a second line clicked then makes the angle between them; a circle or an arc its radius (or diameter, or arc length,
 * as the bar says); Enter after the first point writes that point's level. The bar also chooses a kind outright (aligned,
 * linear, angle, radius, diameter, arc length, ordinate, slope, level, spot coordinate), how a string goes on (single, chain or
 * baseline) and holds the generated dimensions (rooms, exterior) and a string's points. Each kind is the command it was, with the
 * same engine command; the old tools are aliases that start this one set to their kind.
 */
import { create } from 'zustand';
import { startCommand, useCad } from '../../viewport/cad/session';
import { stringMode, StringModePicker, StringTail, useDimTool } from './dimensionTool';

export type DimKind = 'smart' | 'aligned' | 'linear' | 'angular' | 'radial' | 'diameter' | 'arc' | 'ordinate' | 'slope' | 'level' | 'coordinate';
/** What a circle or an arc clicked in the smart mode is given. */
export type RoundAs = 'radial' | 'diameter' | 'arc';

/** The kinds, in the bar's order, with the old tool each was (its id stays an alias) and its typed command. */
export const DIM_KINDS: readonly { kind: DimKind; label: string; tool?: string; command?: string }[] = [
  { kind: 'smart', label: 'Smart' },
  { kind: 'aligned', label: 'Aligned', command: 'DIMALIGNED' },
  { kind: 'linear', label: 'Linear', tool: 'cad.dimLinear', command: 'DIMLINEAR' },
  { kind: 'angular', label: 'Angle', tool: 'cad.dimAngular', command: 'DIMANGULAR' },
  { kind: 'radial', label: 'Radius', tool: 'cad.dimRadius', command: 'DIMRADIUS' },
  { kind: 'diameter', label: 'Diameter', tool: 'cad.dimDiameter', command: 'DIMDIAMETER' },
  { kind: 'arc', label: 'Arc length', tool: 'cad.dimArc', command: 'DIMARC' },
  { kind: 'ordinate', label: 'Ordinate', tool: 'cad.dimOrdinate', command: 'DIMORDINATE' },
  { kind: 'slope', label: 'Slope', tool: 'cad.dimSlope', command: 'DIMSLOPE' },
  { kind: 'level', label: 'Level', tool: 'level', command: 'DIMLEVEL' },
  { kind: 'coordinate', label: 'Spot coordinate', tool: 'cad.dimCoord', command: 'DIMCOORD' },
];

interface SmartDim { kind: DimKind; roundAs: RoundAs; linear: boolean }
export const useSmartDim = create<SmartDim>(() => ({ kind: 'smart', roundAs: 'radial', linear: false }));

/** Whether a kind draws a string of points (and so takes the single, chain or baseline choice). */
export const takesString = (kind: DimKind): boolean => kind === 'smart' || kind === 'aligned' || kind === 'linear';

/** The kind the tool draws next; a DIMENSION running starts over in it (what a chain drew so far is kept, as Esc keeps it). */
export function setDimKind(kind: DimKind): void {
  useSmartDim.setState({ kind });
  if (useCad.getState().command === 'DIMENSION') void startCommand('DIMENSION');
}

/** The Dimension tool set to a kind: the main button starts it smart, an old tool at its own kind. */
export function startDimension(kind: DimKind = 'smart'): void {
  useSmartDim.setState({ kind });
  void startCommand('DIMENSION');
}

/** A button of the bar that runs something else: the generated dimensions and a string's points (set by the module). */
export interface DimExtra { label: string; title: string; run: () => void; testId: string }
let extras: DimExtra[] = [];
export const setDimensionExtras = (list: DimExtra[]): void => { extras = list; };

/** DIMENSION's bar: the kind, the string's mode, what a circle or arc is given and two points' kind (smart), and the extras. */
export function SmartDimensionOptions() {
  const { kind, roundAs, linear } = useSmartDim();
  const dim = useDimTool();
  const mode = stringMode(dim);
  const linearNow = kind === 'linear' || (kind === 'smart' && linear);
  return (
    <>
      <select className="cad-layer on" value={kind} data-dim-kind title="What it dimensions. Smart: two points, a line (a second line: the angle), a circle or an arc; Enter after the first point: its level"
          onChange={(e) => setDimKind(e.target.value as DimKind)}>
          {DIM_KINDS.map((k) => <option key={k.kind} value={k.kind}>{k.label}</option>)}
        </select>
      {kind === 'smart' && (
        <>
          <select className="cad-layer" value={linear ? 'linear' : 'aligned'} data-dim-two title="Two points, or a line, make an aligned dimension or a linear one (horizontal or vertical)"
              onChange={(e) => useSmartDim.setState({ linear: e.target.value === 'linear' })}>
              <option value="aligned">Aligned</option>
              <option value="linear">Linear</option>
            </select>
          <select className="cad-layer" value={roundAs} data-dim-round title="What a circle or an arc clicked is given"
              onChange={(e) => useSmartDim.setState({ roundAs: e.target.value as RoundAs })}>
              <option value="radial">Arcs: radius</option>
              <option value="diameter">Arcs: diameter</option>
              <option value="arc">Arcs: arc length</option>
            </select>
        </>
      )}
      {takesString(kind) && <StringModePicker mode={mode} />}
      {takesString(kind) && <StringTail orient={linearNow ? dim.orient : null} count={mode === 'chain' ? dim.count : 0} />}
      {extras.map((x) => (
        <button key={x.testId} type="button" className="cad-toggle" data-dim-extra={x.testId} title={x.title} onClick={x.run}>{x.label}</button>
      ))}
    </>
  );
}
