/**
 * The properties panel's Light section (docs/plan-render.md, S2): what a light fixture, a lamp or a glowing material
 * gives off — its lumens, colour temperature, kind (a point, a spot with its beam and field, a panel with its size), its
 * distribution (a built-in profile or an IES file brought into the project), where in the fixture it comes from and
 * which way it points. The data go on the element's type when it has one (every element of the type lights so), on
 * the element otherwise; on and off is the element's own. One undo step each (light.set).
 */

import { useEffect, useState } from 'react';
import { Lightbulb, Upload } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import { kelvinRgb } from '../../viewport/lights3d';

interface LightData { kind?: string; lumens?: number; kelvin?: number; beam?: number; field?: number; width?: number | null; length?: number | null; profile?: string | null; on?: boolean; at?: string; aim?: string }
interface ProfileChoice { key: string; name: string; beam: number; file: boolean }

const DEFAULTS: Required<Omit<LightData, 'width' | 'length' | 'profile'>> = { kind: 'point', lumens: 1000, kelvin: 3500, beam: 60, field: 90, on: true, at: 'bottom', aim: 'down' };

const lightOf = (n: NodeDto | null | undefined): LightData | null => (n?.props.light && typeof n.props.light === 'object' ? (n.props.light as LightData) : null);

/** Whether the selected entity is a light, or may be made one: a light fixture, a lamp, or anything with light data (its own or its type's). */
export function isLightish(n: NodeDto): boolean {
  if (/^IfcLightFixture/.test(n.type) || lightOf(n)) return true;
  const t = typeOf(useSession.getState(), n.id);
  if (lightOf(t)) return true;
  const said = `${n.name} ${String(n.props.objectType ?? '')} ${t?.name ?? ''}`;
  return /^IfcFurniture/.test(n.type) && /lamp|light|lumin|veioz|lustr|lampa/i.test(said);
}

/** A colour temperature's swatch: the light's colour as sRGB. */
function swatch(kelvin: number): string {
  const [r, g, b] = kelvinRgb(kelvin).map((c) => Math.round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055)));
  return `rgb(${r}, ${g}, ${b})`;
}

export function LightSection({ node }: { node: NodeDto }) {
  const type = useSession((s) => (node.type.endsWith('Type') ? null : typeOf(s, node.id)));
  const target = type ?? node;
  const own = lightOf(node);
  const data: LightData = { ...DEFAULTS, ...(lightOf(type) ?? {}), ...(own ?? {}) };
  const has = !!(own || lightOf(type));
  const [profiles, setProfiles] = useState<ProfileChoice[]>([]);
  const error = useSession((s) => s.error);
  useEffect(() => { void query<ProfileChoice[]>('light.profiles', {}).then((p) => setProfiles(p ?? [])).catch(() => undefined); }, [data.profile]);
  const set = (patch: Partial<LightData>, on: NodeDto = target) => void execute('light.set', { ids: [on.id], ...patch });
  const num = (key: 'lumens' | 'kelvin' | 'beam' | 'field' | 'width' | 'length', label: string, unit: string, min: number, max: number, step = 1, title?: string) => (
    <>
      <dt title={title}>{label}</dt>
      <dd className="row">
        <input key={`${key}:${String(data[key])}`} type="number" min={min} max={max} step={step} defaultValue={data[key] ?? ''} data-testid={`light-${key}`}
          onBlur={(e) => { const v = Number(e.target.value); if (e.target.value !== '' && Number.isFinite(v) && v !== data[key]) set({ [key]: v }); }}
          onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} style={{ width: 90 }} />
        <span className="muted small">{unit}</span>
      </dd>
    </>
  );
  const importIes = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.ies,.IES';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      const r = await execute('light.addProfile', { name: file.name, text: await file.text() }) as { key?: string } | null;
      if (r?.key) set({ profile: r.key, kind: data.kind === 'area' ? 'spot' : data.kind });
    };
    input.click();
  };
  if (!has) {
    return (
      <div className="light-section" data-testid="light-section">
        <p className="muted small">It gives no light in the medium and realistic views.</p>
        <button type="button" className="btn small" onClick={() => set({ ...DEFAULTS, at: /^IfcFurniture/.test(node.type) ? 'top' : 'bottom' })} data-testid="light-make"><Lightbulb size={13} />Make it a light</button>
      </div>
    );
  }
  return (
    <div className="light-section" data-testid="light-section">
      <label className="row"><input type="checkbox" checked={data.on !== false} onChange={(e) => set({ on: e.target.checked }, node)} data-testid="light-on" /> On</label>
      {type && <p className="muted small">{'The light\'s data are its type\'s: every element of the type lights so.'}</p>}
      <dl className="fields">
        <dt>Kind</dt>
        <dd>
          <select value={data.kind} onChange={(e) => set({ kind: e.target.value })} data-testid="light-kind">
            <option value="point">Point (every way)</option>
            <option value="spot">Spot (a cone)</option>
            <option value="area">Panel (out of its face)</option>
          </select>
        </dd>
        {num('lumens', 'Luminous flux', 'lm', 0, 1_000_000, 50, 'The light it gives, as its maker states it')}
        <dt title="1000–12000 K: 2700 K a warm lamp, 4000 K neutral, 6500 K daylight">Colour temperature</dt>
        <dd className="row">
          <input key={`kelvin:${data.kelvin}`} type="number" min={1000} max={12000} step={100} defaultValue={data.kelvin} data-testid="light-kelvin"
            onBlur={(e) => { const v = Number(e.target.value); if (Number.isFinite(v) && v !== data.kelvin) set({ kelvin: v }); }} style={{ width: 90 }} />
          <span className="muted small">K</span>
          <span style={{ width: 16, height: 16, borderRadius: 8, background: swatch(data.kelvin ?? 3500), border: '1px solid var(--line, #aaa)' }} />
        </dd>
        {data.kind === 'spot' && !data.profile && <>
          {num('beam', 'Beam angle', '°', 1, 179, 1, 'Full light inside this angle')}
          {num('field', 'Field angle', '°', 1, 179, 1, 'No light past this angle')}
        </>}
        {data.kind === 'area' && <>
          {num('width', 'Width', 'm', 0.01, 20, 0.05, 'Empty: the fixture\'s')}
          {num('length', 'Length', 'm', 0.01, 20, 0.05, 'Empty: the fixture\'s')}
        </>}
        <dt title="How the light spreads: a built-in distribution, or a maker's IES file (LM-63, type C) brought into the project">Distribution</dt>
        <dd className="row">
          <select style={{ maxWidth: 170 }} value={data.profile ?? ''} onChange={(e) => set({ profile: e.target.value || null })} data-testid="light-profile">
            <option value="">The kind's own</option>
            {profiles.map((p) => <option key={p.key} value={p.key} data-no-i18n={p.file || undefined}>{p.name}</option>)}
          </select>
          <button type="button" className="icon" title="Import an IES file…" onClick={importIes}><Upload size={13} /></button>
        </dd>
        <dt>From</dt>
        <dd>
          <select value={data.at} onChange={(e) => set({ at: e.target.value })}>
            <option value="bottom">Under the fixture</option>
            <option value="centre">The fixture's middle</option>
            <option value="top">Near the fixture's top (a shade)</option>
          </select>
        </dd>
        <dt>Aimed</dt>
        <dd>
          <select value={data.aim} onChange={(e) => set({ aim: e.target.value })}>
            <option value="down">Down</option>
            <option value="up">Up</option>
          </select>
        </dd>
      </dl>
      <button type="button" className="btn small" onClick={() => void execute('light.set', { ids: own && target !== node ? [target.id, node.id] : [target.id], clear: true })}>No light</button>
      {error && <p className="error-text" role="alert">{error}</p>}
    </div>
  );
}
