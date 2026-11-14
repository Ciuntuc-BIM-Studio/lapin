/**
 * A material's live preview (report #76): a sphere in its look, lit by a soft
 * room it reflects, before a checkered backdrop so glass shows what is behind
 * it (three.js's physical material: glass refracts through its transmission).
 * Its texture, drawn or the user's image, lies at its size in metres on a
 * sphere one metre across, its normal and bump maps with it. Drawn when the material changes, not every frame.
 */

import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import type { NodeDto } from '../../engine/protocol';
import { useSession } from '../../engine/session';
import { lookOf } from '../../viewport/look';
import { previewMaterial } from '../../viewport/materialLook';
import { imageOf, imageTexture, reliefOf, sizeOf, textureFor, textureOf, tileOf, TILE, whenImagesLoad, withRelief } from '../../viewport/textures';

interface Stage { renderer: THREE.WebGLRenderer; scene: THREE.Scene; camera: THREE.PerspectiveCamera; sphere: THREE.Mesh; env: THREE.Texture }

function checker(): THREE.Texture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  for (let y = 0; y < 8; y++) for (let x = 0; x < 8; x++) { g.fillStyle = (x + y) % 2 ? '#d9d6cf' : '#f4f2ec'; g.fillRect(x * 8, y * 8, 8, 8); }
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  t.magFilter = THREE.NearestFilter;
  return t;
}

export function MaterialPreview({ material, size = 168 }: { material: NodeDto; size?: number }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const stage = useRef<Stage | null>(null);
  const nodes = useSession((s) => s.nodes);

  useEffect(() => {
    if (!canvas.current) return;
    let renderer: THREE.WebGLRenderer;
    try { renderer = new THREE.WebGLRenderer({ canvas: canvas.current, antialias: true, alpha: true }); } catch { return; }
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(size, size, false);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    const pmrem = new THREE.PMREMGenerator(renderer);
    const env = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();
    const scene = new THREE.Scene();
    scene.environment = env;
    const backdrop = new THREE.Mesh(new THREE.PlaneGeometry(3, 3), new THREE.MeshBasicMaterial({ map: checker() }));
    backdrop.position.set(0, 0, -1.2);
    scene.add(backdrop);
    const light = new THREE.DirectionalLight(0xffffff, 1.6);
    light.position.set(2, 3, 4);
    scene.add(light, new THREE.AmbientLight(0xffffff, 0.25));
    const sphere = new THREE.Mesh(new THREE.SphereGeometry(0.5, 96, 64), new THREE.MeshPhysicalMaterial());
    scene.add(sphere);
    const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 20);
    camera.position.set(0, 0.35, 2.4);
    camera.lookAt(0, 0, 0);
    stage.current = { renderer, scene, camera, sphere, env };
    return () => {
      stage.current = null;
      scene.traverse((o) => {
        const m = o as THREE.Mesh;
        m.geometry?.dispose();
        const mat = m.material as THREE.MeshPhysicalMaterial | undefined;
        if (mat) { for (const t of [mat.map, mat.normalMap, mat.bumpMap]) if (t && !t.userData.shared) t.dispose(); mat.dispose(); }
      });
      env.dispose();
      renderer.dispose();
    };
  }, [size]);

  useEffect(() => {
    const s = stage.current;
    if (!s) return;
    const draw = () => {
      const look = lookOf(material);
      const color = typeof material.props.color === 'string' ? material.props.color : '#c9ced6';
      const own = imageOf(material, nodes);
      const kind = own ? null : textureOf(material, 'side');
      // A copy of the shared texture, repeated on the sphere as the material's tile lies on the model (the sphere is π m round, π/2 m high).
      const base = own ? imageTexture(own) : kind ? textureFor(kind, color) : null;
      const map = base?.clone() ?? null;
      if (map) {
        const [tw, th] = own ? [own.width, own.height] : tileOf(material) ?? [TILE[kind!], TILE[kind!]];
        map.userData = {};
        map.wrapS = map.wrapT = THREE.RepeatWrapping;
        map.repeat.set(Math.PI / tw, (Math.PI / 2) / th);
        map.center.set(0.5, 0.5);
        map.rotation = (look.textureRotation * Math.PI) / 180;
        map.needsUpdate = true;
      }
      const old = s.sphere.material as THREE.MeshPhysicalMaterial;
      if (old.map) old.map.dispose();
      if (old.normalMap) old.normalMap.dispose();
      if (old.bumpMap) old.bumpMap.dispose();
      old.dispose();
      // Its relief on the sphere, repeated as its images lie on the model (its own size, else 1 × 1 m) and turned alike.
      const [rw, rh] = own ? [own.width, own.height] : sizeOf(material);
      const copy = (t: THREE.Texture) => {
        const c = t.clone();
        c.userData = {};
        c.wrapS = c.wrapT = THREE.RepeatWrapping;
        c.repeat.set(Math.PI / rw, (Math.PI / 2) / rh);
        c.center.set(0.5, 0.5);
        c.rotation = (look.textureRotation * Math.PI) / 180;
        c.needsUpdate = true;
        return c;
      };
      s.sphere.material = withRelief(previewMaterial(map ? 0xffffff : color, map, look, s.env), reliefOf(material, nodes), look.normalStrength, look.bumpStrength, copy);
      s.renderer.render(s.scene, s.camera);
    };
    draw();
    // An image still loading draws again when it comes.
    return whenImagesLoad(draw);
  }, [material, nodes]);

  return <canvas ref={canvas} className="material-preview" data-testid="material-preview" width={size} height={size} style={{ width: size, height: size }} />;
}
