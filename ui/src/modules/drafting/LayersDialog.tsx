/** Drafting layers: their colour, pen width, line type, whether they show and can be edited; a new one, renaming, removing (its shapes go to layer 0). */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { useCad } from '../../viewport/cad/session';

interface Layer { name: string; color: string; weight: number; lineType: string; visible: boolean; locked: boolean }
const TYPES = ['continuous', 'dashed', 'dotted', 'dashdot'];

export function LayersDialog() {
  const nodes = useSession((s) => s.nodes);
  const current = useCad((s) => s.layer);
  const [layers, setLayers] = useState<Layer[]>([]);
  const [fresh, setFresh] = useState('');
  useEffect(() => { void query<Layer[]>('drafting.layers').then((l) => l && setLayers(l)); }, [nodes]);
  const close = () => useUi.setState({ dialog: null });
  const set = (name: string, patch: Partial<Layer> & { newName?: string }) => void execute('drafting.setLayer', { name, ...patch });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Drafting layers" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Drafting layers</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
        <table className="layer-table">
          <thead><tr><th>Current</th><th>Name</th><th>Colour</th><th>Pen (mm)</th><th>Line</th><th>Shown</th><th>Locked</th><th /></tr></thead>
          <tbody>
            {layers.map((l) => (
              <tr key={l.name}>
                <td><input type="radio" checked={current === l.name} onChange={() => useCad.setState({ layer: l.name })} /></td>
                <td><input defaultValue={l.name} disabled={l.name === '0'} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== l.name) set(l.name, { newName: v }); }} /></td>
                <td><input type="color" value={l.color} onChange={(e) => set(l.name, { color: e.target.value })} /></td>
                <td><input type="number" step={0.05} min={0.05} max={2} defaultValue={l.weight} onBlur={(e) => set(l.name, { weight: Number(e.target.value) })} /></td>
                <td><select value={l.lineType} onChange={(e) => set(l.name, { lineType: e.target.value })}>{TYPES.map((t) => <option key={t}>{t}</option>)}</select></td>
                <td><input type="checkbox" checked={l.visible} onChange={(e) => set(l.name, { visible: e.target.checked })} /></td>
                <td><input type="checkbox" checked={l.locked} onChange={(e) => set(l.name, { locked: e.target.checked })} /></td>
                <td>{l.name !== '0' && <button type="button" className="icon" title="Remove (its shapes go to layer 0)" onClick={() => void execute('drafting.removeLayer', { name: l.name })}><X size={13} /></button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
        <footer className="dialog-foot">
          <input placeholder="New layer's name" value={fresh} onChange={(e) => setFresh(e.target.value)} />
          <button type="button" disabled={!fresh.trim()} onClick={() => { set(fresh.trim(), {}); useCad.setState({ layer: fresh.trim() }); setFresh(''); }}>Add</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
