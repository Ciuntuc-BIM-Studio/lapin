/**
 * A material's own image (Materials): the user's PNG or JPEG, laid on the 3D
 * model at a size in metres, kept in the project and carried by the 3D
 * exports (GLB, glTF, USDZ). An image larger than 2048 px is brought down to
 * that on its longer side before it goes in.
 */

import { useState } from 'react';
import { ImagePlus, Trash2 } from 'lucide-react';
import { notify } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { LengthField } from '../../shell/cells';
import { reliefArgs } from '../../viewport/textures';
import { fetchBytes, mapUrl, setArgs, setBytes, setKey, setOf, TEXTURE_SETS, type TextureSet } from '../../viewport/textureLibrary';
import { commitDecimalField } from '../../shell/parseDecimal';

const MAX = 2048;

/** The file as base64, its longer side at most 2048 px (a PNG stays a PNG, so its transparency stays). */
export async function prepareImage(file: File): Promise<{ mime: string; data: string; aspect: number }> {
  const mime = file.type === 'image/png' ? 'image/png' : 'image/jpeg';
  const bitmap = await createImageBitmap(file);
  const aspect = bitmap.height / Math.max(1, bitmap.width);
  const scale = Math.min(1, MAX / Math.max(bitmap.width, bitmap.height));
  if (scale === 1 && (file.type === 'image/png' || file.type === 'image/jpeg')) {
    const bytes = new Uint8Array(await file.arrayBuffer());
    let bin = '';
    for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
    return { mime, data: btoa(bin), aspect };
  }
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  const url = canvas.toDataURL(mime, 0.9);
  return { mime, data: url.slice(url.indexOf(',') + 1), aspect };
}

export function OwnImage({ materialId }: { materialId: string }) {
  const nodes = useSession((s) => s.nodes);
  const mat = nodes.get(materialId);
  const imageId = typeof mat?.props.textureImage === 'string' ? mat.props.textureImage : null;
  const image = imageId ? nodes.get(imageId) : undefined;
  const size = Array.isArray(mat?.props.textureSize) ? (mat!.props.textureSize as number[]) : [1, 1];
  const set = (args: Record<string, unknown>) => void execute('material.update', { id: materialId, ...args });

  const choose = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const { mime, data, aspect } = await prepareImage(file);
        const r = await execute('image.add', { name: file.name, mime, data }) as { id?: string } | null;
        if (!r?.id) return;
        // Its width as before (1 m for a first image), its height as the picture's proportion.
        const w = imageId ? size[0] : 1;
        set({ textureImage: r.id, textureSize: [w, Math.round(w * aspect * 1000) / 1000] });
      } catch (e) {
        notify(`The image could not be read: ${e instanceof Error ? e.message : String(e)}`);
      }
    };
    input.click();
  };

  return (
    <div className="own-image">
      <span className="eyebrow" title="Your own picture on the 3D model, kept in the project and carried by the GLB, glTF and USDZ exports">Own image (3D, exports)</span>
      <div className="row">
        {image && typeof image.props.data === 'string'
          ? <img className="own-image-thumb" alt={image.name} src={`data:${String(image.props.mime)};base64,${image.props.data}`} />
          : <span className="own-image-thumb empty muted small">none</span>}
        <button type="button" className="btn compact" onClick={choose}><ImagePlus size={13} />{image ? 'Replace…' : 'Import image…'}</button>
        {image && <>
          <label className="small" title="Width: how wide one copy of the picture lies on the model">W <LengthField metres={size[0]} min={0.01} noUnit style={{ width: 64 }} onCommit={(v) => { if (v != null) set({ textureSize: [v, size[1]] }); }} /></label>
          <label className="small" title="Height: how high one copy of the picture lies on the model">H <LengthField metres={size[1]} min={0.01} style={{ width: 64 }} onCommit={(v) => { if (v != null) set({ textureSize: [size[0], v] }); }} /></label>
          <button type="button" className="icon" title="Take the image off" onClick={() => set({ textureImage: null })}><Trash2 size={13} /></button>
        </>}
      </div>
    </div>
  );
}

/** The relief and surface maps a material may have (report #76; docs/plan-render.md, S1): what each is, its attribute and its strength's, and the strength's most. */
export const RELIEF = [
  { key: 'normalImage', strength: 'normalStrength', max: 10, label: 'Normal map', title: 'A normal map (tangent space, green up, as glTF): the faces shade as if they had its relief. Kept in the project; carried by GLB, glTF, USD/USDZ and OBJ.' },
  { key: 'bumpImage', strength: 'bumpStrength', max: 10, label: 'Bump map', title: 'A bump map (white high, black low): the faces shade as if raised where it is light. Kept in the project; carried by OBJ (map_Bump).' },
  { key: 'roughnessImage', strength: null, max: 0, label: 'Roughness map', title: 'A roughness map (white rough, black glossy): Roughness below multiplies it, as glTF does. Shown in the Medium and Realistic views; carried by GLB, glTF, USD/USDZ, OBJ and IFC.' },
  { key: 'aoImage', strength: 'aoStrength', max: 1, label: 'Occlusion map', title: 'An ambient occlusion map (white open, black shut in): the light from round about reaches its dark parts less, as in a joint between bricks. Shown in the Medium and Realistic views; carried by GLB, glTF, USD/USDZ and IFC.' },
] as const;

type Strengths = { normalStrength: number; bumpStrength: number; aoStrength: number };

/**
 * The texture sets Lapin ships (docs/plan-render.md, S1): a picture of each; a click lays the set's maps on the material in one
 * step (its colour, normal, roughness, occlusion and height maps, at the set's real tile size), copied into the project.
 */
export function TextureSets({ materialId }: { materialId: string }) {
  const nodes = useSession((s) => s.nodes);
  const mat = nodes.get(materialId);
  const chosen = typeof mat?.props.textureSet === 'string' ? mat.props.textureSet : null;
  const [busy, setBusy] = useState<string | null>(null);
  const apply = async (s: TextureSet) => {
    setBusy(s.id);
    try { await execute('material.textures', await setArgs(s, [materialId], fetchBytes)); }
    catch (e) { notify(`The texture set could not be read: ${e instanceof Error ? e.message : String(e)}`); }
    finally { setBusy(null); }
  };
  return (
    <div className="own-image texture-sets" data-testid="texture-sets">
      <span className="eyebrow" title="Photographed materials with their relief, roughness and occlusion (ambientCG, CC0): a click lays one on this material at its real size, copied into the project">Texture sets (3D, exports)</span>
      <div className="texture-set-grid">
        {TEXTURE_SETS.map((s) => (
          <button key={s.id} type="button" className={`texture-set${chosen === setKey(s) ? ' on' : ''}`} data-set={s.id} disabled={busy != null}
            title={`${s.name}: ${s.size[0]} × ${s.size[1]} m a tile, ${Math.round(setBytes(s) / 1024)} KB in the project (ambientCG ${s.id}, CC0)`} onClick={() => void apply(s)}>
            <img alt="" src={mapUrl(s, 'color') ?? ''} loading="lazy" />
            <span className="small">{busy === s.id ? '…' : s.name}</span>
          </button>
        ))}
      </div>
      {chosen && (
        <div className="row">
          <span className="small muted">{setOf(chosen)?.name ?? chosen}</span>
          <button type="button" className="btn compact" data-action="texture-set-off" title="Take the set's maps off this material (its colour and drawn texture show again)" onClick={() => void execute('material.textures', { id: materialId, maps: {} })}><Trash2 size={13} />Take the maps off</button>
        </div>
      )}
    </div>
  );
}

/** A material's relief and surface maps: each its image, its strength, taken off with the bin. Laid on at the texture's size and turn. */
export function ReliefMaps({ materialId, strengths }: { materialId: string; strengths: Strengths }) {
  const nodes = useSession((s) => s.nodes);
  const mat = nodes.get(materialId);
  const set = (args: Record<string, unknown>) => void execute('material.update', { id: materialId, ...args });

  const choose = (key: string) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const { mime, data, aspect } = await prepareImage(file);
        const r = await execute('image.add', { name: file.name, mime, data }) as { id?: string } | null;
        if (r?.id) set(reliefArgs(key, r.id, aspect, mat?.props.textureSize));
      } catch (e) {
        notify(`The image could not be read: ${e instanceof Error ? e.message : String(e)}`);
      }
    };
    input.click();
  };

  return (
    <div className="own-image" data-testid="material-relief">
      {RELIEF.map(({ key, strength, max, label, title }) => {
        const imageId = typeof mat?.props[key] === 'string' ? mat.props[key] as string : null;
        const image = imageId ? nodes.get(imageId) : undefined;
        return (
          <div key={key} className="row" data-field={`material-${key}`}>
            <span className="eyebrow" title={title}>{label}</span>
            {image && typeof image.props.data === 'string'
              ? <img className="own-image-thumb" alt={image.name} src={`data:${String(image.props.mime)};base64,${image.props.data}`} />
              : <span className="own-image-thumb empty muted small">none</span>}
            <button type="button" className="btn compact" onClick={() => choose(key)}><ImagePlus size={13} />{image ? 'Replace…' : 'Import image…'}</button>
            {image && <>
              {strength && <label className="small" title="How strongly it shows: 1 as the image says, 0 flat">Strength <input className="num" data-field={`material-${strength}`} key={`${materialId}${strength}${strengths[strength]}`} defaultValue={strengths[strength]}
                onBlur={(e) => commitDecimalField(e.target, strengths[strength], { min: 0, max }, (v) => set({ [strength]: v }))} /></label>}
              <button type="button" className="icon" title="Take the image off" onClick={() => set({ [key]: null })}><Trash2 size={13} /></button>
            </>}
          </div>
        );
      })}
    </div>
  );
}
