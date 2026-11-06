/**
 * Transport (docs/plan-elevators.md, testers' reports #54 and #97): elevators in Architecture → Elevators. The Elevator
 * tool puts one down at a click in the style the bar says (the catalogue's twelve kinds or the project's own), from the
 * plan's storey to the highest, and opens its configurator; the Elevator schedule; Shaft openings for floors drawn after.
 */

import { ListOrdered, SquareDashed } from 'lucide-react';
import { ElevatorIcon } from '../../icons';
import type { LapinModule, ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { CLASS, elevatorGrips, groupStyles, styleAbout, useElevatorPrefs, type ElevatorStyle, type OwnStyle } from './elevator';
import { activate } from '../../platform/modules';
import { ElevatorDialog } from './ElevatorDialog';
import { ElevatorSection } from './ElevatorSection';
import { openConfigurator } from './openConfigurator';

function useStyles(): { catalogue: ElevatorStyle[]; own: OwnStyle[] } {
  const info = useSession((s) => s.info);
  const nodes = useSession((s) => s.nodes);
  const project = [...nodes.values()].find((n) => n.type === 'IfcProject');
  return {
    catalogue: (info?.catalogs['transport.styles'] as unknown as ElevatorStyle[] | undefined) ?? [],
    own: (project?.props.elevatorStyles as OwnStyle[] | undefined) ?? [],
  };
}

function StyleSelect({ testId }: { testId?: string }) {
  const { catalogue, own } = useStyles();
  const style = useElevatorPrefs((s) => s.style);
  return (
    <select value={style} data-testid={testId} title="The style it puts down" onChange={(e) => useElevatorPrefs.setState({ style: e.target.value })}>
      {own.length > 0 && <optgroup label="Project">{own.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.label}</option>)}</optgroup>}
      {groupStyles(catalogue).map(([g, list]) => <optgroup key={g} label={g}>{list.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}</optgroup>)}
    </select>
  );
}

function ElevatorOptions() {
  const rotation = useElevatorPrefs((s) => s.rotation);
  return (
    <>
      <span className="cad-layer on"><StyleSelect testId="elevator-style" /></span>
      <span className="cad-layer">
        <select value={rotation} data-testid="elevator-rotation" title="Its turn: 0° its doors face down the plan" onChange={(e) => useElevatorPrefs.setState({ rotation: Number(e.target.value) })}>
          {[0, 90, 180, 270].map((a) => <option key={a} value={a}>{a}°</option>)}
        </select>
      </span>
    </>
  );
}

function ElevatorNew() {
  const { catalogue, own } = useStyles();
  const style = useElevatorPrefs((s) => s.style);
  const picked = catalogue.find((s) => s.id === style);
  const mine = own.find((s) => s.id === style);
  return (
    <div className="pad new-element" data-field="new-element" data-i18n-ctx="transport">
      <label className="field-line"><span className="eyebrow">Style</span><StyleSelect /></label>
      {picked && <p className="small">{picked.description}</p>}
      {(picked || mine) && <p className="small" data-no-i18n>{styleAbout(picked?.values ?? mine!.attrs)}</p>}
      <p className="muted small">A click puts the elevator's shaft centre there, from this storey to the highest; its configurator opens.</p>
    </div>
  );
}

const elevator: ToolDef = {
  id: 'transport.elevator', label: 'Elevator', icon: ElevatorIcon, shortcut: 'LF',
  gesture: { kind: 'point' },
  options: ElevatorOptions,
  properties: ElevatorNew,
  complete: async ({ points: [p] }, ctx) => {
    const { style, rotation } = useElevatorPrefs.getState();
    const r = (await ctx.execute('transport.place', { storeyId: ctx.storeyId, x: p.x, y: p.y, rotation, style })) as { id?: string } | null;
    if (r?.id) {
      useUi.setState({ selected: [r.id], selection: r.id, tool: null });
      openConfigurator(r.id);
    }
  },
};

const schedule: ToolDef = {
  id: 'transport.schedule', label: 'Elevator schedule', icon: ListOrdered,
  run: () => void execute('architecture.createSchedule', { kind: 'elevators' }).then((r) => { if (r) notify('Elevator schedule made: it is in the project browser, under Schedules.'); }),
};

const openings: ToolDef = {
  id: 'transport.syncOpenings', label: 'Shaft openings', icon: SquareDashed,
  run: () => void execute('transport.syncOpenings', {}),
};

const transport: LapinModule = {
  id: 'transport',
  name: 'Transport',
  summary: 'Elevators from a configurator of twelve kinds after EN 81-20 and EN 81-70: the shaft over the storeys it serves with its pit, headroom and machine room, the car, the landing doors, the openings in the floors, plans, schedule, cost and IFC.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 20,
    groups: [{ label: 'Elevators', tools: [elevator], small: [schedule, openings] }],
  }],
  propertySections: [
    { id: 'elevator', label: 'Elevator', classes: [CLASS], component: ElevatorSection },
  ],
  dialogs: { elevator: ElevatorDialog },
  grips: {
    [CLASS]: (node: NodeDto) => {
      const w = Number(node.props.cabinWidth ?? 1.1), d = Number(node.props.cabinDepth ?? 1.4);
      return elevatorGrips(node, Math.max(w, d) / 2 + 0.9);
    },
  },
  similar: {
    [CLASS]: (node: NodeDto) => {
      if (typeof node.props.style === 'string') useElevatorPrefs.setState({ style: node.props.style, rotation: Number(node.props.rotation ?? 0) });
      activate(elevator);
    },
  },
};

export default transport;
