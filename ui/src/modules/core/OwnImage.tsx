/**
 * A material's own image (Materials): the user's PNG or JPEG, laid on the 3D
 * model at a size in metres, kept in the project and carried by the 3D
 * exports (GLB, glTF, USDZ). An image larger than 2048 px is brought down to
 * that on its longer side before it goes in.
 */

import { ImagePlus, Trash2 } from 'lucide-react';
import { notify } from '../../app/store';
import { execute, useSession } from '../../engine/session';

const MAX = 2048;

/** The file as base64, its longer side at most 2048 px (a PNG stays a PNG, so its transparency stays). */
async function prepare(file: File): Promise<{ mime: string; data: string; aspect: number }> {
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
        const { mime, data, aspect } = await prepare(file);
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

  const num = (t: string) => Number(t.replace(',', '.'));
  return (
    <div className="own-image">
      <span className="eyebrow" title="Your own picture on the 3D model, kept in the project and carried by the GLB, glTF and USDZ exports">Own image (3D, exports)</span>
      <div className="row">
        {image && typeof image.props.data === 'string'
          ? <img className="own-image-thumb" alt={image.name} src={`data:${String(image.props.mime)};base64,${image.props.data}`} />
          : <span className="own-image-thumb empty muted small">none</span>}
        <button type="button" className="btn compact" onClick={choose}><ImagePlus size={13} />{image ? 'Replace…' : 'Import image…'}</button>
        {image && <>
          <label className="small" title="Width: how wide one copy of the picture lies on the model">W <input className="num" type="number" step={0.05} min={0.01} defaultValue={size[0]} key={`w${size[0]}`}
            onBlur={(e) => { const v = num(e.target.value); if (v > 0 && v !== size[0]) set({ textureSize: [v, size[1]] }); }} /> m</label>
          <label className="small" title="Height: how high one copy of the picture lies on the model">H <input className="num" type="number" step={0.05} min={0.01} defaultValue={size[1]} key={`h${size[1]}`}
            onBlur={(e) => { const v = num(e.target.value); if (v > 0 && v !== size[1]) set({ textureSize: [size[0], v] }); }} /> m</label>
          <button type="button" className="icon" title="Take the image off" onClick={() => set({ textureImage: null })}><Trash2 size={13} /></button>
        </>}
      </div>
    </div>
  );
}
