/**
 * The resource histogram (decision 772; a view under the model, as the Gantt): for each resource the units asked for on
 * each day of the schedule as bars, those over its limit red, the limit dashed, the days off shaded; Level and Clear
 * levelling over it.
 */

import { BarChart3, Eraser, Scale, Users } from 'lucide-react';
import { clearLevelling, level, money, openResources, showDay, useResources, type ResourceDto, type ResourcesDto } from './resources';

const W = 900, H = 110, X0 = 44, X1 = W - 12, Y0 = 10, Y1 = H - 22;

/** One resource's days as bars (red where it is asked for more than it has), its limit dashed. */
export function ResourceBars({ r, d }: { r: ResourceDto; d: ResourcesDto }) {
  const days = r.demand.length;
  const top = Math.max(1, r.capacity, r.peak) * 1.15;
  const x = (i: number) => X0 + (i * (X1 - X0)) / Math.max(1, days);
  const y = (v: number) => Y1 - (v / top) * (Y1 - Y0);
  const w = Math.max(1, (X1 - X0) / Math.max(1, days) - 1);
  const off = new Set(d.nonWorking);
  const every = Math.max(1, Math.ceil(days / 15));
  const from = new Date(`${d.from}T00:00:00`);
  const label = (i: number) => { const t = new Date(from); t.setDate(t.getDate() + i); return `${String(t.getDate()).padStart(2, '0')}.${String(t.getMonth() + 1).padStart(2, '0')}`; };
  return (
    <svg className="cost-chart res-histogram" viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`Histogram ${r.name}`}>
      {Array.from({ length: days }, (_, i) => off.has(i) ? <rect key={`o${i}`} className="res-off" x={x(i)} y={Y0} width={w + 1} height={Y1 - Y0} /> : null)}
      {r.demand.map((v, i) => v > 0 && (
        <rect key={i} className={v > r.capacity + 1e-9 ? 'res-bar-over' : 'bar-planned'} data-over={v > r.capacity + 1e-9 ? '1' : undefined} x={x(i)} y={y(v)} width={w} height={Y1 - y(v)}><title>{`${label(i)}: ${v} / ${r.capacity} ${r.unit}`}</title></rect>
      ))}
      <line className="res-limit" x1={X0} x2={X1} y1={y(r.capacity)} y2={y(r.capacity)} />
      <text className="tick" x={X0 - 6} y={y(r.capacity) + 4} textAnchor="end" data-no-i18n>{r.capacity}</text>
      <line className="axis" x1={X0} x2={X1} y1={Y1} y2={Y1} />
      {Array.from({ length: days }, (_, i) => i % every === 0 ? <text key={`l${i}`} className="tick" x={x(i) + w / 2} y={Y1 + 14} textAnchor="middle" data-no-i18n>{label(i)}</text> : null)}
    </svg>
  );
}

export function HistogramView() {
  const data = useResources();
  if (!data) return <div className="estimate-pane"><p className="muted estimate-empty">Reading the resources…</p></div>;
  return (
    <div className="estimate-pane" data-testid="resource-histogram">
      <div className="view-toolbar estimate-toolbar">
        <span className="panel-title"><BarChart3 size={14} /> Resource histogram</span>
        <span className="group-rule" aria-hidden />
        <button type="button" className="icon" title="Resources…" onClick={openResources}><Users size={14} /><span className="small">Resources…</span></button>
        <button type="button" className="icon" title="Place the tasks not started so no resource is asked for more than it has" data-testid="histogram-level" onClick={() => void level()}><Scale size={14} /><span className="small">Level resources</span></button>
        {data.levelled > 0 && <button type="button" className="icon" title="Clear levelling" data-testid="histogram-clear" onClick={clearLevelling}><Eraser size={14} /><span className="small">Clear levelling</span></button>}
        <span className="muted small" data-no-i18n>{`${showDay(data.from)} – ${showDay(data.to)}`}</span>
      </div>
      <div className="estimate-scroll">
        {data.resources.length === 0 && <p className="muted estimate-empty">No resources yet: Construction → Resources… adds them and puts them on tasks.</p>}
        {data.resources.map((r) => (
          <section key={r.id} className="res-resource">
            <h4 className="eyebrow"><span data-no-i18n>{r.name}</span> · <span data-no-i18n>{`${r.capacity} ${r.unit}`}</span>
              <span className="muted" data-no-i18n> · </span>
              {r.over > 0 ? <span className="cde-warn"><span data-no-i18n>{r.over}</span> days short</span> : <span className="muted">enough</span>}
              {r.cost > 0 && <span className="muted" data-no-i18n>{` · ${money(r.cost)}`}</span>}
            </h4>
            <ResourceBars r={r} d={data} />
          </section>
        ))}
      </div>
    </div>
  );
}
