/** A wall type's build-up, outside to inside (mock data until the engine sends it). */

const LAYERS = [
  { name: 'Plaster, exterior', mm: 20, color: '#e7e1d5' },
  { name: 'EPS insulation', mm: 100, color: '#f1e3d9' },
  { name: 'Brick 30', mm: 300, color: '#c98a6b', core: true },
  { name: 'Plaster, interior', mm: 15, color: '#efeae0' },
];

export function WallLayers() {
  const total = LAYERS.reduce((s, l) => s + l.mm, 0);
  return (
    <div className="layers">
      <div className="layer-bar">{LAYERS.map((l) => <span key={l.name} style={{ flex: l.mm, background: l.color }} title={l.name} />)}</div>
      <table>
        <tbody>
          {LAYERS.map((l) => (
            <tr key={l.name}>
              <td><span className="swatch" style={{ background: l.color }} />{l.name}{l.core && <span className="chip">core</span>}</td>
              <td className="num">{l.mm} mm</td>
            </tr>
          ))}
          <tr className="total"><td>Total</td><td className="num">{total} mm</td></tr>
        </tbody>
      </table>
    </div>
  );
}
