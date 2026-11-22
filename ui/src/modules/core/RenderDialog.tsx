/**
 * View → Render settings (docs/plan-render.md, S4): how the 3D view's path tracer renders and saves an image. The size
 * (the window's, 1080p, 4K or one's own), when it is done (samples or time), denoising, depth of field (the focus picked
 * by a click, the aperture), the exposure and white balance (the view's, or the image's own); the sky is the view's (the
 * project's sun on its day and hour). Kept on this computer, as the view's profile is; nothing goes into the project.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { backdropClose } from '../../shell/Dialog';
import { notify, useUi } from '../../app/store';
import { resolutionOf, type ResolutionChoice } from '../../viewport/pathTrace';
import { F_STOPS, SAMPLE_STEPS, setRenderSettings, setRendering, useRenderApi, useRenderMode, useRenderSettings } from '../../viewport/renderMode';
import { useCameraLook, useRenderProfile } from '../../viewport/render3d';

const SIZES: { id: ResolutionChoice; label: string }[] = [
  { id: 'window', label: 'The window' },
  { id: '1080p', label: '1080p (1920 wide)' },
  { id: '4k', label: '4K (3840 wide)' },
  { id: 'custom', label: 'Custom' },
];

export function RenderDialog() {
  const st = useRenderSettings();
  const mode = useRenderMode();
  const look = useCameraLook();
  const profile = useRenderProfile((s) => s.profile);
  const denoiser = useRenderApi((s) => s.denoiser);
  const api = useRenderApi((s) => s.api);
  const [saving, setSaving] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  // The dialog opened from the ribbon with the view not rendering: it starts, so what is set shows.
  useEffect(() => { if (profile !== 'drawing' && !useRenderMode.getState().on) setRendering(true); }, [profile]);
  const view = (() => {
    const el = document.querySelector('[data-testid="view3d"]') as HTMLElement | null;
    const dpr = window.devicePixelRatio || 1;
    return { width: Math.round((el?.clientWidth ?? 1280) * dpr), height: Math.round((el?.clientHeight ?? 720) * dpr) };
  })();
  const size = resolutionOf(st.resolution, view, { width: st.width, height: st.height });
  const save = async () => {
    if (!api) { notify('Open a 3D view to render it.'); return; }
    setSaving(true);
    try {
      const saved = await api.saveImage();
      if (saved) notify(`Saved ${saved}.`);
    } catch (e) {
      notify(`The image could not be made: ${e instanceof Error ? e.message : String(e)}`);
    } finally { setSaving(false); }
  };
  const busy = saving || mode.phase === 'still' || mode.phase === 'denoising';
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog render-dialog" role="dialog" aria-label="Render" data-testid="render-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Render</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {profile === 'drawing' && <p className="muted small">Rendering needs the medium or realistic view: View → Display quality.</p>}
          <h4 className="eyebrow">Image</h4>
          <dl className="fields">
            <dt>Size</dt>
            <dd className="row">
              <select value={st.resolution} onChange={(e) => setRenderSettings({ resolution: e.target.value as ResolutionChoice })} aria-label="Size">
                {SIZES.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
              </select>
              {st.resolution === 'custom'
                ? <>
                  <input type="number" min={16} max={8192} step={2} value={st.width} onChange={(e) => setRenderSettings({ width: Number(e.target.value) })} aria-label="Width" style={{ width: 72 }} />
                  <span>×</span>
                  <input type="number" min={16} max={8192} step={2} value={st.height} onChange={(e) => setRenderSettings({ height: Number(e.target.value) })} aria-label="Height" style={{ width: 72 }} />
                  <span className="muted small">px</span>
                </>
                : <span className="num muted" data-no-i18n>{`${size.width} × ${size.height}`}</span>}
            </dd>
            <dt title="The picture refines sample by sample: it is done after so many, or after so long">Done after</dt>
            <dd className="row">
              <select value={st.limit} onChange={(e) => setRenderSettings({ limit: e.target.value as 'samples' | 'time' })} aria-label="Done after">
                <option value="samples">Samples</option>
                <option value="time">Time</option>
              </select>
              {st.limit === 'samples'
                ? <select value={st.samples} onChange={(e) => setRenderSettings({ samples: Number(e.target.value) })} aria-label="Samples">
                  {[...new Set([...SAMPLE_STEPS, st.samples])].sort((a, b) => a - b).map((n) => <option key={n} value={n} data-no-i18n>{n}</option>)}
                </select>
                : <>
                  <input type="number" min={5} max={3600} step={5} value={st.seconds} onChange={(e) => setRenderSettings({ seconds: Number(e.target.value) })} aria-label="Seconds" style={{ width: 72 }} />
                  <span className="muted small">s</span>
                </>}
            </dd>
            <dt title="Clears the last grain from the picture, guided by its colours, normals and depth">Denoise</dt>
            <dd className="row">
              <label className="row"><input type="checkbox" checked={st.denoise} onChange={(e) => setRenderSettings({ denoise: e.target.checked })} /> <span>On</span></label>
              {st.denoise && <span className="muted small">{denoiser === 'oidn' ? 'Intel Open Image Denoise for the saved image; Lapin’s own in the view.' : 'Lapin’s own denoiser.'}</span>}
            </dd>
          </dl>
          <h4 className="eyebrow" style={{ marginTop: 12 }}>Camera</h4>
          <dl className="fields">
            <dt title="Near and far go soft, as through a camera's lens: what is at the focus distance stays sharp">Depth of field</dt>
            <dd className="row">
              <label className="row"><input type="checkbox" checked={st.dof} onChange={(e) => setRenderSettings({ dof: e.target.checked })} /> <span>On</span></label>
              {st.dof && <>
                <select value={st.fStop} onChange={(e) => setRenderSettings({ fStop: Number(e.target.value) })} aria-label="Aperture" title="The aperture: a smaller f-number blurs more">
                  {[...new Set([...F_STOPS, st.fStop])].sort((a, b) => a - b).map((f) => <option key={f} value={f} data-no-i18n>{`f/${f}`}</option>)}
                </select>
              </>}
            </dd>
            {st.dof && <>
              <dt>Focus</dt>
              <dd className="row">
                <input type="number" min={0.1} step={0.1} value={st.focus} onChange={(e) => setRenderSettings({ focus: Number(e.target.value) })} aria-label="Focus distance" style={{ width: 72 }} />
                <span className="muted small">m</span>
                <button type="button" className="btn" aria-pressed={mode.pickingFocus} onClick={() => { useRenderMode.setState({ pickingFocus: true }); close(); }} title="Click in the 3D view what should be sharp">Pick in the view</button>
              </dd>
            </>}
            <dt title="Stops over the view's automatic exposure (View → Display quality)">Exposure</dt>
            <dd className="row">
              <select value={st.ev === null ? 'view' : 'own'} onChange={(e) => setRenderSettings({ ev: e.target.value === 'view' ? null : look.ev })} aria-label="Exposure">
                <option value="view">The view’s</option>
                <option value="own">Its own</option>
              </select>
              {st.ev !== null
                ? <>
                  <input type="range" min={-6} max={6} step={0.25} value={st.ev} onChange={(e) => setRenderSettings({ ev: Number(e.target.value) })} style={{ flex: 1 }} aria-label="Exposure compensation" />
                  <span className="num" style={{ width: 56 }} data-no-i18n>{`${st.ev > 0 ? '+' : ''}${st.ev} EV`}</span>
                </>
                : <span className="num muted" data-no-i18n>{`${look.ev > 0 ? '+' : ''}${look.ev} EV`}</span>}
            </dd>
            <dt title="The light that comes out white">White balance</dt>
            <dd className="row">
              <select value={st.wb === 'view' ? 'view' : st.wb === 'auto' ? 'auto' : 'fixed'} onChange={(e) => setRenderSettings({ wb: e.target.value === 'view' ? 'view' : e.target.value === 'auto' ? 'auto' : 5000 })} aria-label="White balance">
                <option value="view">The view’s</option>
                <option value="auto">Automatic</option>
                <option value="fixed">Colour temperature</option>
              </select>
              {typeof st.wb === 'number' && <>
                <input type="range" min={2000} max={10000} step={100} value={st.wb} onChange={(e) => setRenderSettings({ wb: Number(e.target.value) })} style={{ flex: 1 }} aria-label="Colour temperature" />
                <span className="num" style={{ width: 56 }} data-no-i18n>{`${st.wb} K`}</span>
              </>}
            </dd>
          </dl>
          <p className="muted small">The sky and the sun are the view’s: the project’s place, day and hour (View → Sun and shadows). After sunset the light fixtures light the rooms.</p>
        </div>
        <footer className="dialog-foot">
          {mode.phase === 'still' && mode.still && <span className="muted small" data-testid="render-progress">{`Image ${mode.still.width} × ${mode.still.height}: ${Math.round(mode.still.progress * 100)} %`}</span>}
          <span className="grow" />
          {busy
            ? <button type="button" className="btn" onClick={() => api?.cancel()}>Stop</button>
            : <button type="button" className="btn primary" disabled={profile === 'drawing'} onClick={() => void save()} data-testid="render-save">Save image…</button>}
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
