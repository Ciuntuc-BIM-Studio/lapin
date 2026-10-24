/** The command line under a 2D view: the last lines, the prompt and what is being typed; snap, ortho and polar switches; the current layer. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import { useCad } from './session';

interface LayerDto { name: string; color: string; visible: boolean; locked: boolean }

export function CommandLine() {
  const { history, buffer, step, command, osnap, ortho, polar, layer } = useCad();
  const nodes = useSession((s) => s.nodes);
  const [layers, setLayers] = useState<LayerDto[]>([]);
  useEffect(() => { void query<LayerDto[]>('drafting.layers').then((l) => l && setLayers(l)); }, [nodes]);
  const lines = history.slice(-2);
  return (
    <div className="cad-line" data-testid="command-line">
      <div className="cad-history">{lines.map((l, i) => <div key={i}>{l || ' '}</div>)}</div>
      <div className="cad-prompt">
        <span className="cad-label">{step ? `${command}: ${step.prompt}` : 'Command'}</span>
        <span className="cad-buffer">{buffer}<span className="caret" /></span>
        <span className="grow" />
        <button type="button" className={`cad-toggle${osnap ? ' on' : ''}`} title="Object snap (F3)" onClick={() => useCad.setState({ osnap: !osnap })}>SNAP</button>
        <button type="button" className={`cad-toggle${ortho ? ' on' : ''}`} title="Ortho (F8; hold Shift)" onClick={() => useCad.setState({ ortho: !ortho })}>ORTHO</button>
        <button type="button" className={`cad-toggle${polar ? ' on' : ''}`} title="Polar tracking every 45° (F10)" onClick={() => useCad.setState({ polar: !polar })}>POLAR</button>
        <select className="cad-layer" value={layer} title="The layer new shapes go on" onChange={(e) => useCad.setState({ layer: e.target.value })}>
          {(layers.length ? layers : [{ name: '0', color: '#18181b', visible: true, locked: false }]).map((l) => <option key={l.name} value={l.name}>{l.name}</option>)}
        </select>
      </div>
    </div>
  );
}
