/**
 * Web Mercator tile math, AWS Terrarium elevation tile decoder, satellite/OSM orthophoto stitcher,
 * and grayscale PNG heightmap decoder for Lapin's Site → Import workflows.
 */

const EARTH_R = 6378137;
const MAX_LAT = 85.0511287798;
const ORIGIN_SHIFT = Math.PI * EARTH_R;

export type BasemapKind = 'satellite' | 'osm';

export interface GeoBounds {
  minLon: number;
  minLat: number;
  maxLon: number;
  maxLat: number;
}

/** WGS 84 (lon, lat in degrees) → Web Mercator EPSG:3857 (x, y in metres). */
export function lonLatToMercator(lon: number, lat: number): [number, number] {
  const clamped = Math.max(-MAX_LAT, Math.min(MAX_LAT, lat));
  const x = (lon * ORIGIN_SHIFT) / 180;
  const y = Math.log(Math.tan(((90 + clamped) * Math.PI) / 360)) * EARTH_R;
  return [x, y];
}

/** Web Mercator EPSG:3857 (x, y in metres) → WGS 84 (lon, lat in degrees). */
export function mercatorToLonLat(x: number, y: number): [number, number] {
  const lon = (x / ORIGIN_SHIFT) * 180;
  const lat = (Math.atan(Math.exp(y / EARTH_R)) * 360) / Math.PI - 90;
  return [lon, lat];
}

/** Fractional slippy-map tile coordinates (x, y) at integer zoom z. */
export function lonLatToTile(lon: number, lat: number, z: number): [number, number] {
  const n = 2 ** z;
  const clamped = Math.max(-MAX_LAT, Math.min(MAX_LAT, lat));
  const rad = (clamped * Math.PI) / 180;
  const tx = ((lon + 180) / 360) * n;
  const ty = ((1 - Math.log(Math.tan(rad) + 1 / Math.cos(rad)) / Math.PI) / 2) * n;
  return [tx, ty];
}

/** Ground width & height in real metres of a WGS 84 bounding box (accounting for cos(lat)). */
export function boundsMetricSize(b: GeoBounds): { widthM: number; heightM: number } {
  const midLat = ((b.minLat + b.maxLat) / 2) * (Math.PI / 180);
  const [x0, y0] = lonLatToMercator(b.minLon, b.minLat);
  const [x1, y1] = lonLatToMercator(b.maxLon, b.maxLat);
  const scale = Math.cos(midLat);
  return { widthM: Math.abs(x1 - x0) * scale, heightM: Math.abs(y1 - y0) * scale };
}

/** Four corners in EPSG:3857 (top-left, top-right, bottom-right, bottom-left) for affine raster mapping. */
export function mercatorCorners(b: GeoBounds): [[number, number], [number, number], [number, number], [number, number]] {
  const [x0, y0] = lonLatToMercator(b.minLon, b.minLat);
  const [x1, y1] = lonLatToMercator(b.maxLon, b.maxLat);
  return [[x0, y1], [x1, y1], [x1, y0], [x0, y0]];
}

export function basemapTileUrl(kind: BasemapKind, z: number, x: number, y: number): string {
  if (kind === 'satellite')
    return `https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/${z}/${y}/${x}`;
  return `https://tile.openstreetmap.org/${z}/${x}/${y}.png`;
}

export function terrariumTileUrl(z: number, x: number, y: number): string {
  return `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/${z}/${x}/${y}.png`;
}

/** Encodes a Float32Array as little-endian base64 for civil.importDem (heightsF32). */
export function float32ToBase64(arr: Float32Array): string {
  const bytes = new Uint8Array(arr.buffer, arr.byteOffset, arr.byteLength);
  let s = '';
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

/** Loads a CORS-enabled image tile into an HTMLImageElement. */
function loadTileImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error(`Failed to load tile: ${url}`));
    img.src = url;
  });
}

/** Picks the highest zoom in [minZ..maxZ] such that the tile grid covering bounds has at most maxTiles tiles. */
function chooseZoom(b: GeoBounds, minZ: number, maxZ: number, maxTiles: number): number {
  for (let z = maxZ; z >= minZ; z--) {
    const [tx0, ty0] = lonLatToTile(b.minLon, b.maxLat, z);
    const [tx1, ty1] = lonLatToTile(b.maxLon, b.minLat, z);
    const cols = Math.floor(tx1) - Math.floor(tx0) + 1;
    const rows = Math.floor(ty1) - Math.floor(ty0) + 1;
    if (cols * rows <= maxTiles) return z;
  }
  return minZ;
}

/** Stitches tiles covering `bounds` into an exact cropped canvas (width × height capped to `maxDim`). */
async function stitchBounds(
  bounds: GeoBounds,
  urlFor: (z: number, x: number, y: number) => string,
  minZ: number,
  maxZ: number,
  maxTiles: number,
  maxDim: number,
): Promise<{ canvas: HTMLCanvasElement; zoom: number }> {
  const z = chooseZoom(bounds, minZ, maxZ, maxTiles);
  const [tx0, ty0] = lonLatToTile(bounds.minLon, bounds.maxLat, z);
  const [tx1, ty1] = lonLatToTile(bounds.maxLon, bounds.minLat, z);
  const xMin = Math.floor(tx0), xMax = Math.floor(tx1);
  const yMin = Math.floor(ty0), yMax = Math.floor(ty1);
  const n = 2 ** z;

  const stitch = document.createElement('canvas');
  stitch.width = (xMax - xMin + 1) * 256;
  stitch.height = (yMax - yMin + 1) * 256;
  const sctx = stitch.getContext('2d')!;

  const jobs: Promise<void>[] = [];
  for (let y = yMin; y <= yMax; y++) {
    for (let x = xMin; x <= xMax; x++) {
      const wx = ((x % n) + n) % n;
      const wy = Math.max(0, Math.min(n - 1, y));
      const dx = (x - xMin) * 256;
      const dy = (y - yMin) * 256;
      jobs.push(loadTileImage(urlFor(z, wx, wy)).then((img) => { sctx.drawImage(img, dx, dy); }));
    }
  }
  await Promise.all(jobs);

  const sx = (tx0 - xMin) * 256;
  const sy = (ty0 - yMin) * 256;
  const sw = Math.max(2, (tx1 - tx0) * 256);
  const sh = Math.max(2, (ty1 - ty0) * 256);
  const scale = Math.min(1, maxDim / Math.max(sw, sh));
  const outW = Math.max(2, Math.round(sw * scale));
  const outH = Math.max(2, Math.round(sh * scale));

  const out = document.createElement('canvas');
  out.width = outW;
  out.height = outH;
  const octx = out.getContext('2d')!;
  octx.drawImage(stitch, sx, sy, sw, sh, 0, 0, outW, outH);
  return { canvas: out, zoom: z };
}

/**
 * Downloads AWS Terrarium elevation tiles for `bounds` and decodes RGB pixels into metres:
 *   height = (R * 256 + G + B / 256) - 32768
 */
export async function fetchTerrariumGrid(bounds: GeoBounds, maxDim = 256): Promise<{
  width: number;
  height: number;
  heightsF32: string;
  corners: [[number, number], [number, number], [number, number], [number, number]];
  zoom: number;
}> {
  const { canvas, zoom } = await stitchBounds(bounds, terrariumTileUrl, 8, 14, 16, maxDim);
  const ctx = canvas.getContext('2d')!;
  const img = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
  const count = canvas.width * canvas.height;
  const arr = new Float32Array(count);
  for (let i = 0; i < count; i++) {
    const r = img[i * 4];
    const g = img[i * 4 + 1];
    const b = img[i * 4 + 2];
    arr[i] = (r * 256 + g + b / 256) - 32768;
  }
  return {
    width: canvas.width,
    height: canvas.height,
    heightsF32: float32ToBase64(arr),
    corners: mercatorCorners(bounds),
    zoom,
  };
}

/** Downloads satellite or OSM basemap tiles for `bounds` and returns a JPEG base64 + EPSG:3857 corners. */
export async function fetchOrthophoto(bounds: GeoBounds, kind: BasemapKind, maxDim = 1536): Promise<{
  data: string;
  mime: string;
  corners: [[number, number], [number, number], [number, number], [number, number]];
}> {
  const { canvas } = await stitchBounds(bounds, (z, x, y) => basemapTileUrl(kind, z, x, y), 10, 17, 25, maxDim);
  const dataUrl = canvas.toDataURL('image/jpeg', 0.88);
  const data = dataUrl.slice(dataUrl.indexOf(',') + 1);
  return { data, mime: 'image/jpeg', corners: mercatorCorners(bounds) };
}

/** Decodes a grayscale heightmap image (PNG/JPEG) into a Float32Array mapped to [minZ, maxZ]. */
export async function decodeHeightmapImage(file: File, minZ: number, maxZ: number, maxDim = 512): Promise<{
  width: number;
  height: number;
  heightsF32: string;
}> {
  const url = URL.createObjectURL(file);
  try {
    const img = await new Promise<HTMLImageElement>((resolve, reject) => {
      const el = new Image();
      el.onload = () => resolve(el);
      el.onerror = () => reject(new Error('Could not read the heightmap image.'));
      el.src = url;
    });
    const scale = Math.min(1, maxDim / Math.max(img.naturalWidth, img.naturalHeight));
    const w = Math.max(2, Math.round(img.naturalWidth * scale));
    const h = Math.max(2, Math.round(img.naturalHeight * scale));
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d')!;
    ctx.drawImage(img, 0, 0, w, h);
    const px = ctx.getImageData(0, 0, w, h).data;
    const arr = new Float32Array(w * h);
    const span = maxZ - minZ;
    for (let i = 0; i < w * h; i++) {
      const lum = (0.299 * px[i * 4] + 0.587 * px[i * 4 + 1] + 0.114 * px[i * 4 + 2]) / 255;
      arr[i] = minZ + lum * span;
    }
    return { width: w, height: h, heightsF32: float32ToBase64(arr) };
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** Parses direct "lat, lon" input or searches OpenStreetMap Nominatim. */
export async function searchLocation(queryText: string): Promise<{ lat: number; lon: number; label: string } | null> {
  const m = queryText.trim().match(/^(-?\d+(?:\.\d+)?)\s*[,;\s]\s*(-?\d+(?:\.\d+)?)$/);
  if (m) {
    const a = Number(m[1]), b = Number(m[2]);
    if (Math.abs(a) <= 90 && Math.abs(b) <= 180) return { lat: a, lon: b, label: `${a.toFixed(5)}, ${b.toFixed(5)}` };
  }
  const res = await fetch(`https://nominatim.openstreetmap.org/search?format=json&limit=1&q=${encodeURIComponent(queryText)}`, {
    headers: { Accept: 'application/json' },
  });
  if (!res.ok) return null;
  const rows = await res.json() as { lat: string; lon: string; display_name: string }[];
  if (!rows.length) return null;
  return { lat: Number(rows[0].lat), lon: Number(rows[0].lon), label: rows[0].display_name };
}

export interface OsmBuilding {
  name?: string;
  footprint: [number, number][];
  height?: number;
  levels?: number;
  properties?: Record<string, string>;
}

const OVERPASS_ENDPOINTS = [
  'https://overpass.private.coffee/api/interpreter',
  'https://overpass-api.de/api/interpreter',
  'https://overpass.kumi.systems/api/interpreter',
];

/** Parses OSM height tag ("12", "12.5 m", "40 ft") into metres, or undefined. */
export function parseOsmHeight(raw: string | undefined): number | undefined {
  if (!raw) return undefined;
  const s = raw.trim().toLowerCase().replace(',', '.');
  const m = s.match(/^(\d+(?:\.\d+)?)\s*(m|ft|')?$/);
  if (!m) return undefined;
  const v = Number(m[1]);
  if (!Number.isFinite(v) || v <= 0) return undefined;
  return m[2] === 'ft' || m[2] === "'" ? v * 0.3048 : v;
}

/** Downloads building footprints and heights inside `bounds` (fast OSM map.json API first, Overpass fallback for large areas). */
export async function fetchOsmBuildings(bounds: GeoBounds): Promise<OsmBuilding[]> {
  const dLon = Math.abs(bounds.maxLon - bounds.minLon);
  const dLat = Math.abs(bounds.maxLat - bounds.minLat);

  // Fast path: official OSM cgimap API returns small site boxes (< ~2 km) in < 1 s with no Overpass queue.
  if (dLon <= 0.025 && dLat <= 0.025) {
    try {
      const osmUrl = `https://api.openstreetmap.org/api/0.6/map.json?bbox=${bounds.minLon.toFixed(6)},${bounds.minLat.toFixed(6)},${bounds.maxLon.toFixed(6)},${bounds.maxLat.toFixed(6)}`;
      const res = await fetch(osmUrl, { signal: AbortSignal.timeout(12_000) });
      if (res.ok) {
        const json = await res.json() as {
          elements?: { type: string; id: number; lat?: number; lon?: number; nodes?: number[]; tags?: Record<string, string> }[];
        };
        const nodes = new Map<number, [number, number]>();
        for (const el of json.elements ?? [])
          if (el.type === 'node' && el.lon != null && el.lat != null) nodes.set(el.id, [el.lon, el.lat]);
        const out: OsmBuilding[] = [];
        for (const el of json.elements ?? []) {
          if (el.type !== 'way' || !el.nodes || el.nodes.length < 3) continue;
          const tags = el.tags ?? {};
          if (!tags.building && !tags['building:part']) continue;
          const pts: [number, number][] = [];
          for (const nid of el.nodes) { const p = nodes.get(nid); if (p) pts.push(p); }
          if (pts.length < 3) continue;
          out.push(osmWayToBuilding(pts, tags));
        }
        return out;
      }
    } catch { /* fall through to Overpass */ }
  }

  const bbox = `${bounds.minLat.toFixed(6)},${bounds.minLon.toFixed(6)},${bounds.maxLat.toFixed(6)},${bounds.maxLon.toFixed(6)}`;
  const ql = `[out:json][timeout:12];(way["building"](${bbox});way["building:part"](${bbox}););out body geom;`;
  let data: { elements?: { type: string; id: number; tags?: Record<string, string>; geometry?: { lat: number; lon: number }[] }[] } | null = null;
  let lastErr: unknown = null;

  for (const url of OVERPASS_ENDPOINTS) {
    try {
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8' },
        body: `data=${encodeURIComponent(ql)}`,
        signal: AbortSignal.timeout(15_000),
      });
      if (res.ok) { data = await res.json(); break; }
      lastErr = new Error(`Overpass HTTP ${res.status}`);
    } catch (e) {
      lastErr = e;
    }
  }
  if (!data) throw lastErr instanceof Error ? lastErr : new Error('Could not reach OpenStreetMap API.');

  const out: OsmBuilding[] = [];
  for (const el of data.elements ?? []) {
    if (!el.geometry || el.geometry.length < 3) continue;
    const pts: [number, number][] = el.geometry.map((g) => [g.lon, g.lat]);
    out.push(osmWayToBuilding(pts, el.tags ?? {}));
  }
  return out;
}

function osmWayToBuilding(pts: [number, number][], tags: Record<string, string>): OsmBuilding {
  const height = parseOsmHeight(tags.height) ?? parseOsmHeight(tags['building:height']);
  const lvlsRaw = Number((tags['building:levels'] ?? '').replace(',', '.'));
  const levels = Number.isFinite(lvlsRaw) && lvlsRaw > 0 ? lvlsRaw : undefined;
  const name = tags.name || tags['addr:housenumber'] ? [tags.name, tags['addr:street'], tags['addr:housenumber']].filter(Boolean).join(' ') : undefined;
  const properties: Record<string, string> = {};
  if (tags.building) properties.building = tags.building;
  if (tags['building:levels']) properties.levels = tags['building:levels'];
  return { name, footprint: pts, ...(height ? { height } : {}), ...(levels ? { levels } : {}), properties };
}
