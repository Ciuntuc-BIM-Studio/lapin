/**
 * A curtain wall's members, one by one (a tester's request, #128): its elevation with every mullion and transom, the ones
 * clicked taking a profile of their own (from the libraries, drawn in Manage → Profile libraries), or a plain box, turned,
 * mirrored, aligned and offset on the member; back to the type's with one button. The profile stays a copy in the project:
 * updated from its library, every member made of it follows, in this wall and in any other.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { PendingDialog } from '../core/PendingDialog';
import { ProfilePickerField } from '../core/profileLibraries/ProfilePickerField';
import { isDefaultPlace, placeJson, type Place, type Ring } from './memberPlace';
import { PlacedSection, PlaceFields } from './PlaceFields';

interface Member { key: string; role: 'mullion' | 'transom'; x0: number; x1: number; z0: number; z1: number; profile: string | null; place: Place; own: boolean; outline?: [number, number][] }
interface Members { length: number; height: number; u: number[]; pieces: Member[] }
interface Picked { name: string; section: Record<string, unknown> & { width: number; depth: number }; rings: Ring[] }

/** The keys to send for a selection: a role's key when every member of the role is in it (so it keeps to what the grid becomes), else the pieces'. */
export function keysFor(selected: ReadonlySet<string>, pieces: readonly Pick<Member, 'key' | 'role'>[]): string[] {
  const out: string[] = [];
  for (const role of ['mullion', 'transom'] as const) {
    const of = pieces.filter((p) => p.role === role);
    if (of.length > 0 && of.every((p) => selected.has(p.key))) out.push(role);
    else out.push(...of.filter((p) => selected.has(p.key)).map((p) => p.key));
  }
  return out;
}

export function CurtainMembersDialog() {
  const id = useUi((s) => s.selection);
  const wall = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const version = useSession((s) => s.version);
  const close = () => useUi.setState({ dialog: null });
  const [data, setData] = useState<Members | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [picked, setPicked] = useState<Picked | null>(null);
  const [mode, setMode] = useState<'keep' | 'profile' | 'plain'>('keep');
  const [place, setPlace] = useState<Place>({});
  const [busy, setBusy] = useState(false);
  const loaded = useRef<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let live = true;
    void query<Members>('architecture.curtainMembers', { id }).then((r) => { if (live && r) setData(r); });
    return () => { live = false; };
  }, [id, version]);

  const pieces = data?.pieces ?? [];
  const chosen = useMemo(() => pieces.filter((p) => selected.has(p.key)), [pieces, selected]);
  // The first member chosen fills the fields: its place and whether it has a profile.
  useEffect(() => {
    const first = chosen[0];
    const sig = first ? `${first.key}:${JSON.stringify(first.place)}:${first.profile}` : null;
    if (loaded.current === sig) return;
    loaded.current = sig;
    if (!first) return;
    setPlace(first.place ?? {});
    setMode('keep');
    setPicked(null);
  }, [chosen]);

  if (!wall || wall.type !== 'IfcCurtainWall' || !data) return <PendingDialog title="Curtain wall members" />;

  const W = 560, H = 300, pad = 18;
  const k = Math.min((W - 2 * pad) / Math.max(data.length, 0.1), (H - 2 * pad) / Math.max(data.height, 0.1));
  const sx = (x: number) => pad + x * k, sy = (z: number) => H - pad - z * k;
  const click = (key: string, e: React.MouseEvent) => {
    setSelected((cur) => {
      const next = new Set(e.shiftKey || e.ctrlKey || e.metaKey ? cur : []);
      if (cur.has(key) && (e.shiftKey || e.ctrlKey || e.metaKey)) next.delete(key); else next.add(key);
      return next;
    });
  };
  const only = (role: Member['role']) => setSelected(new Set(pieces.filter((p) => p.role === role).map((p) => p.key)));
  const pickProfile = async (profile: unknown, library: string | null) => {
    setBusy(true);
    try {
      const used = await execute('profiles.use', { profile, library }) as { section?: Picked['section'] } | null;
      const props = await query<{ rings?: Ring[] }>('profiles.properties', { profile });
      if (used?.section) { setPicked({ name: String(used.section.name ?? (profile as { name?: string }).name ?? ''), section: used.section, rings: props?.rings ?? (used.section.rings as Ring[] | undefined) ?? [] }); setMode('profile'); }
    } finally { setBusy(false); }
  };
  const apply = async () => {
    if (!chosen.length) return;
    const args: Record<string, unknown> = { id: wall.id, keys: keysFor(selected, pieces), place: placeJson(place) ?? {} };
    if (mode === 'profile' && picked) args.profile = picked.section;
    else if (mode === 'plain') args.plain = true;
    await execute('architecture.setCurtainMembers', args);
  };
  const reset = async () => { if (chosen.length) await execute('architecture.setCurtainMembers', { id: wall.id, keys: keysFor(selected, pieces), clear: true }); };
  const first = chosen[0];
  const shownRings = mode === 'profile' ? picked?.rings : undefined;
  const size = mode === 'profile' && picked ? { w: picked.section.width, d: picked.section.depth } : { w: first ? first.x1 - first.x0 : 0.05, d: 0.15 };
  const own = pieces.filter((p) => p.own).length;
  const colorOf = (p: Member) => (selected.has(p.key) ? 'var(--accent)' : p.own ? '#2f7f7a' : '#5b6b7c');
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Curtain wall members" data-testid="curtain-members" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Curtain wall members · <span data-no-i18n>{wall.name}</span></span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Click a mullion or a transom (Shift or Ctrl/⌘ adds more). The profile and place below go to the ones chosen; the rest keep their type&apos;s.</p>
          <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} className="wall-profile-svg" aria-label="Elevation">
            <rect x={sx(0)} y={sy(data.height)} width={data.length * k} height={data.height * k} fill="var(--wash)" stroke="none" />
            {pieces.map((p) => p.outline && p.outline.length >= 3 ? (
              <polygon key={p.key} points={p.outline.map(([u, z]) => `${sx(u)},${sy(z)}`).join(' ')} fill={colorOf(p)} stroke="#1e2a3a" strokeWidth={0.3} style={{ cursor: 'pointer' }} data-member={p.key} onClick={(e) => click(p.key, e)} />
            ) : (
              <rect key={p.key} x={sx(p.x0)} y={sy(p.z1)} width={Math.max(2, (p.x1 - p.x0) * k)} height={Math.max(2, (p.z1 - p.z0) * k)}
                fill={colorOf(p)} stroke="#1e2a3a" strokeWidth={0.3} style={{ cursor: 'pointer' }} data-member={p.key} onClick={(e) => click(p.key, e)} />
            ))}
          </svg>
          <div className="row wrap" style={{ gap: 6, margin: '6px 0' }}>
            <button type="button" className="btn small" data-action="members-mullions" onClick={() => only('mullion')}>All mullions</button>
            <button type="button" className="btn small" data-action="members-transoms" onClick={() => only('transom')}>All transoms</button>
            <button type="button" className="btn small" onClick={() => setSelected(new Set(pieces.map((p) => p.key)))}>Every member</button>
            <button type="button" className="btn small" onClick={() => setSelected(new Set())}>None</button>
            <span className="muted small"><span data-no-i18n>{chosen.length}</span> <span>chosen</span> · <span data-no-i18n>{own}</span> <span>with a style of their own</span></span>
          </div>
          <div className="row" style={{ alignItems: 'flex-start', gap: 16 }}>
            <div className="grow" style={{ opacity: chosen.length ? 1 : 0.5 }}>
              <label className="field-line"><span className="eyebrow">Profile</span>
                <ProfilePickerField testId="curtain-member-library" value={mode === 'profile' ? picked?.name ?? null : mode === 'plain' ? null : first?.profile ?? null} onPick={(p, library) => void pickProfile(p, library)} />
              </label>
              <div className="row" style={{ gap: 6 }}>
                <button type="button" className={`btn small${mode === 'keep' ? ' on' : ''}`} onClick={() => setMode('keep')} title="Keeps the profile the members have now, and changes only their place">Keep the profile</button>
                <button type="button" className={`btn small${mode === 'plain' ? ' on' : ''}`} data-action="members-plain" onClick={() => setMode('plain')} title="A plain box of the type's size">Plain box</button>
              </div>
              <PlaceFields place={place} onChange={setPlace} />
            </div>
            <div style={{ textAlign: 'center' }}>
              <PlacedSection rings={shownRings} width={size.w} depth={size.d} place={place} />
              <p className="muted small">{isDefaultPlace(place) ? 'centred on the member' : 'as placed'}</p>
            </div>
          </div>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" data-action="members-reset" disabled={!chosen.some((p) => p.own)} onClick={() => void reset()} title="The chosen members go back to their type's profile and place">Back to the type&apos;s</button>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Close</button>
          <button type="button" className="btn primary" data-action="members-apply" disabled={!chosen.length || busy || (mode === 'profile' && !picked)} onClick={() => void apply()}>Apply to the chosen</button>
        </footer>
      </div>
    </div>
  );
}
