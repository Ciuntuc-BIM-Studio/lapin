/**
 * The type configurators' preview (decision 529): the type being edited, unsaved, in a sample the engine stands it up in
 * (a wall: 3 m long, a wall joined to it in T), drawn as the drawings draw it — Plan · Section · 3D. The plan and the
 * section are the views' own drawings (`types.preview` runs them through the same pipeline: Visibility / Graphics, the
 * view style, the hatches and pens at the scale, the level of detail), drawn here by the drawing views' renderer
 * (DrawingLayer); the 3D is the sample's geometry, turned with the mouse. A hovered layer (`highlight`, its row) lights up
 * in all three. Asked again a moment after the last edit; the last answer stays while the next is on its way.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { useUi } from '../../app/store';
import { t, useLang } from '../../i18n';
import { query, useSession } from '../../engine/session';
import { LengthField } from '../../shell/cells';
import { DrawingLayer } from '../../viewport/DrawingLayer';
import { drawsOnDark } from '../../viewport/drawing';
import { faceLetters } from '../../viewport/faceMarks';
import { lookOf } from '../../viewport/look';
import { reflections, standardMaterial } from '../../viewport/materialLook';
import {
  PREVIEW_DETAILS, PREVIEW_PANES, PREVIEW_SCALES, fitBounds, keyMatches, previewArgs, shownPanes, useTypePreview,
  type PreviewDrawing, type PreviewMeshPart, type PreviewPane, type TypePreviewDto,
} from './typePreviewState';

const PANE_H = 230;
const PANE_LABEL: Record<PreviewPane, string> = { plan: 'Plan', section: 'Section', '3d': '3D' };

/**
 * <code>faces</code>: the wall's faces named on the drawings (R · exterior on top in plan, as the table's layer 1, and on
 * the right in section; L · interior opposite).
 */
export function TypePreview({ typeClass, type, highlight = null, faces = false }: { typeClass: string; type: Record<string, unknown>; highlight?: number | null; faces?: boolean }) {
  const prefs = useTypePreview();
  const lang = useLang((s) => s.lang);
  const root = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(720);
  const [data, setData] = useState<TypePreviewDto | null>(null);
  const [busy, setBusy] = useState(false);
  // Asked again only when what is asked changes (the dialog makes a new object at every render).
  const typeJson = JSON.stringify(type);
  const { scale, detail, length, height } = prefs;
  const args = useMemo(() => previewArgs(typeClass, JSON.parse(typeJson) as Record<string, unknown>, { scale, detail, length, height, pane: 'plan' }, { storey: t('Storey'), above: t('Storey above'), section: 'A' }),
    [typeClass, typeJson, scale, detail, length, height, lang]);

  // The engine's preview a moment after the last edit; an answer to an older question is dropped.
  const asked = useRef(0);
  useEffect(() => {
    const n = ++asked.current;
    setBusy(true);
    const timer = setTimeout(() => {
      void query<TypePreviewDto>('types.preview', args).then((r) => { if (n === asked.current) { setData(r ?? { problem: t('The preview could not be drawn.') }); setBusy(false); } });
    }, 220);
    return () => clearTimeout(timer);
  }, [args]);

  useEffect(() => {
    const el = root.current;
    if (!el || typeof ResizeObserver === 'undefined') return;
    const o = new ResizeObserver((es) => setWidth(es[0].contentRect.width));
    o.observe(el);
    return () => o.disconnect();
  }, []);

  const panes = shownPanes(width, prefs.pane);
  const paneW = Math.max(180, Math.floor((width - (panes.length - 1) * 8) / panes.length));
  const set = (patch: Partial<typeof prefs>) => useTypePreview.setState(patch);
  return (
    <section className="type-preview" ref={root} data-testid="type-preview" aria-busy={busy}>
      <div className="row type-preview-bar">
        <span className="eyebrow">Preview</span>
        {panes.length === 1 && (
          <span className="tabs" role="tablist">
            {PREVIEW_PANES.map((p) => <button key={p} type="button" role="tab" aria-selected={prefs.pane === p} className={`tab${prefs.pane === p ? ' on' : ''}`} onClick={() => set({ pane: p })}>{PANE_LABEL[p]}</button>)}
          </span>
        )}
        <span className="grow" />
        <label title="View scale"><select data-testid="type-preview-scale" value={prefs.scale} onChange={(e) => set({ scale: Number(e.target.value) })}>
          {PREVIEW_SCALES.map((s) => <option key={s} value={s}>1:{s}</option>)}
        </select></label>
        <label title="Detail level"><select data-testid="type-preview-detail" value={prefs.detail} onChange={(e) => set({ detail: e.target.value as typeof prefs.detail })}>
          {PREVIEW_DETAILS.map((d) => <option key={d} value={d}>{d}</option>)}
        </select></label>
        <span className="muted small">Length</span>
        <LengthField className="type-preview-size" testId="type-preview-length" metres={prefs.length} min={1} title="The sample wall's length" onCommit={(m) => { if (m != null && m >= 1) set({ length: m }); }} />
        <span className="muted small">Height</span>
        <LengthField className="type-preview-size" testId="type-preview-height" metres={prefs.height} blank min={0} placeholder="—" title="The sample wall's height; empty: as the type's top says, the storey above" onCommit={(m) => set({ height: m != null && m > 0 ? m : null })} />
      </div>
      {data?.problem
        ? <p className="muted small type-preview-problem" role="status">{data.problem}</p>
        : (
          <div className="type-preview-panes">
            {panes.map((p) => (
              <figure key={p} className="type-preview-pane" data-pane={p} style={{ width: paneW }}>
                {p === '3d'
                  ? <MeshPane parts={data?.mesh ?? null} highlight={highlight} width={paneW} height={PANE_H} />
                  : <DrawingPane drawing={data?.[p] ?? null} kind={p} highlight={highlight} faces={faces} lang={lang} width={paneW} height={PANE_H} />}
                {panes.length > 1 && <figcaption className="muted small">{PANE_LABEL[p]}</figcaption>}
              </figure>
            ))}
          </div>
        )}
      <p className="muted small">Drawn by the engine, as the drawings draw it: a sample wall of the type with another joined to it in T. Hover a layer's row to light it up in all three.</p>
    </section>
  );
}

function DrawingPane({ drawing, kind, highlight, faces, lang, width, height }: { drawing: PreviewDrawing | null; kind: 'plan' | 'section'; highlight: number | null; faces: boolean; lang: Parameters<typeof faceLetters>[0]; width: number; height: number }) {
  const onDark = drawsOnDark(useUi((s) => s.theme));
  const fitted = useMemo(() => fitBounds(drawing?.bounds, width, height, faces ? 22 : 14), [drawing, width, height, faces]);
  const lit = useMemo(() => {
    if (!drawing?.parts || highlight == null) return '';
    let d = '';
    for (const [key, loops] of Object.entries(drawing.parts)) {
      if (!keyMatches(key, highlight)) continue;
      for (const loop of loops) {
        for (let k = 0; k + 1 < loop.length; k += 2) { const s = fitted.toScreen({ x: loop[k], y: loop[k + 1] }); d += `${k === 0 ? 'M' : 'L'}${s.x.toFixed(1)} ${s.y.toFixed(1)}`; }
        d += 'Z';
      }
    }
    return d;
  }, [drawing, highlight, fitted]);
  const letters = faceLetters(lang);
  const right = `${letters.right} · ${t('exterior')}`, left = `${letters.left} · ${t('interior')}`;
  return (
    <svg className="drawing-svg type-preview-svg" width={width} height={height} style={{ width, height }} preserveAspectRatio="none" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={PANE_LABEL[kind]} data-testid={`type-preview-${kind}`}>
      <rect className="paper" x={0} y={0} width={width} height={height} />
      {drawing && <DrawingLayer drawing={drawing} toScreen={fitted.toScreen} pxPerMetre={fitted.k} onDark={onDark} minText={4} />}
      {lit && <path className="type-preview-lit" d={lit} fillRule="evenodd" data-testid="type-preview-lit" />}
      {faces && drawing && (kind === 'plan'
        ? <>
          <text x={width / 2} y={10} className="ref-face" data-face="right">{right}</text>
          <text x={width / 2} y={height - 9} className="ref-face" data-face="left">{left}</text>
        </>
        : <>
          <text x={6} y={height / 2} className="ref-face side" data-face="left" transform={`rotate(-90 6 ${height / 2})`}>{left}</text>
          <text x={width - 6} y={height / 2} className="ref-face side" data-face="right" transform={`rotate(90 ${width - 6} ${height / 2})`}>{right}</text>
        </>)}
    </svg>
  );
}

interface Stage { renderer: THREE.WebGLRenderer; scene: THREE.Scene; camera: THREE.PerspectiveCamera; controls: OrbitControls; model: THREE.Group; framed: string; draw: () => void }

function dispose(o: THREE.Object3D) {
  o.traverse((x) => {
    const m = x as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    for (const one of Array.isArray(mat) ? mat : mat ? [mat] : []) one.dispose();
  });
}

const LIT = new THREE.Color('#e8590c');

function MeshPane({ parts, highlight, width, height }: { parts: PreviewMeshPart[] | null; highlight: number | null; width: number; height: number }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const stage = useRef<Stage | null>(null);
  const [failed, setFailed] = useState(false);
  const nodes = useSession((s) => s.nodes);

  // One renderer while the pane is shown.
  useEffect(() => {
    if (!canvas.current) return;
    let renderer: THREE.WebGLRenderer;
    try { renderer = new THREE.WebGLRenderer({ canvas: canvas.current, antialias: true, alpha: true }); } catch { setFailed(true); return; }
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(width, height, false);
    const scene = new THREE.Scene();
    scene.environment = reflections(renderer);
    const sun = new THREE.DirectionalLight(0xffffff, 1.5);
    sun.position.set(-2, 4, 5);
    const fill = new THREE.DirectionalLight(0xffffff, 0.5);
    fill.position.set(3, -4, 2);
    scene.add(sun, fill, new THREE.HemisphereLight(0xffffff, 0xb8b2a6, 0.9));
    const camera = new THREE.PerspectiveCamera(35, width / height, 0.05, 200);
    camera.up.set(0, 0, 1);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = false;
    const model = new THREE.Group();
    scene.add(model);
    const draw = () => renderer.render(scene, camera);
    controls.addEventListener('change', draw);
    stage.current = { renderer, scene, camera, controls, model, framed: '', draw };
    return () => {
      stage.current = null;
      controls.removeEventListener('change', draw);
      controls.dispose();
      dispose(scene);
      renderer.dispose();
    };
  }, [width, height]);

  // The sample's parts into the scene; framed from the R side (outside), above, the first time and when its size changes.
  useEffect(() => {
    const s = stage.current;
    if (!s || !parts) return;
    for (const c of [...s.model.children]) { s.model.remove(c); dispose(c); }
    for (const p of parts) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.Float32BufferAttribute(p.positions, 3));
      if (p.normals?.length === p.positions.length) g.setAttribute('normal', new THREE.Float32BufferAttribute(p.normals, 3));
      if (p.indices.length) g.setIndex(p.indices);
      if (!p.normals?.length) g.computeVertexNormals();
      const node = p.material ? nodes.get(p.material) : undefined;
      const color = typeof node?.props.color === 'string' ? node.props.color : p.color;
      const mesh = new THREE.Mesh(g, standardMaterial(color, null, lookOf(node)));
      mesh.userData = { key: p.key, color };
      s.model.add(mesh);
      if (p.edges.length) {
        const e = new THREE.BufferGeometry();
        e.setAttribute('position', new THREE.Float32BufferAttribute(p.edges, 3));
        s.model.add(new THREE.LineSegments(e, new THREE.LineBasicMaterial({ color: 0x3a3d42, transparent: true, opacity: 0.55 })));
      }
    }
    const box = new THREE.Box3().setFromObject(s.model);
    const size = box.getSize(new THREE.Vector3()), mid = box.getCenter(new THREE.Vector3());
    const key = `${size.x.toFixed(2)} ${size.y.toFixed(2)} ${size.z.toFixed(2)}`;
    if (!box.isEmpty() && s.framed !== key) {
      s.framed = key;
      const r = size.length() * 0.5;
      s.controls.target.copy(mid);
      const d = r / Math.tan((s.camera.fov * Math.PI) / 360) * 1.15;
      s.camera.position.copy(mid).add(new THREE.Vector3(-0.55, 1, 0.6).normalize().multiplyScalar(d));
      s.controls.minDistance = r * 0.3;
      s.controls.maxDistance = d * 4;
      s.controls.update();
    }
    s.draw();
  }, [parts, nodes]);

  // The hovered layer lit, the others as they are.
  useEffect(() => {
    const s = stage.current;
    if (!s) return;
    s.model.traverse((o) => {
      const m = o as THREE.Mesh;
      if (!m.isMesh || !(m.material instanceof THREE.MeshStandardMaterial)) return;
      const on = keyMatches(m.userData.key as string | null, highlight);
      m.material.emissive.copy(on ? LIT : new THREE.Color(0x000000));
      m.material.emissiveIntensity = on ? 0.55 : 0;
    });
    s.draw();
  }, [highlight, parts]);

  if (failed) return <p className="muted small">3D</p>;
  return <canvas ref={canvas} width={width} height={height} style={{ width, height }} className="type-preview-3d" aria-label="3D: drag to turn, wheel to zoom" title="3D: drag to turn, wheel to zoom" data-testid="type-preview-3d" />;
}
