/**
 * The small pictures of the recent projects on the start screen: when a project is saved, the 3D view's picture is shrunk to a
 * small JPEG and kept here by the project's path (a convenience of this computer: kept by the browser, like the recent list).
 * Nothing here may fail or slow a save: every read and write is guarded, and a missing picture is a plain icon.
 */

const KEY = 'lapin.recentThumbs';
/** The pictures kept: the latest ones; the oldest go first. */
export const MAX_THUMBS = 20;
/** The widest a kept picture is, in pixels. */
export const THUMB_WIDTH = 240;

type Thumbs = Record<string, string>;

/** The pictures as stored (oldest first); an unreadable store is an empty one. */
function read(): Thumbs {
  try {
    const v: unknown = JSON.parse(localStorage.getItem(KEY) ?? '{}');
    if (!v || typeof v !== 'object' || Array.isArray(v)) return {};
    return Object.fromEntries(Object.entries(v).filter(([, d]) => typeof d === 'string'));
  } catch { return {}; }
}

/** The picture kept for a project's path, if any. */
export function thumbOf(path: string): string | null {
  return read()[path] ?? null;
}

/** Keeps a picture for a path: it becomes the newest, and only the last MAX_THUMBS stay. */
export function keepThumb(path: string, dataUrl: string): void {
  try {
    const all = read();
    delete all[path];
    all[path] = dataUrl;
    const keys = Object.keys(all);
    for (const k of keys.slice(0, Math.max(0, keys.length - MAX_THUMBS))) delete all[k];
    localStorage.setItem(KEY, JSON.stringify(all));
  } catch { /* no storage, or it is full: no picture */ }
}

/** A picture (a data URL) shrunk to a JPEG at most THUMB_WIDTH wide; null when it cannot be made. */
export function shrink(src: string): Promise<string | null> {
  return new Promise((resolve) => {
    try {
      const img = new Image();
      img.onload = () => {
        try {
          const scale = Math.min(1, THUMB_WIDTH / Math.max(img.width, 1));
          const c = document.createElement('canvas');
          c.width = Math.max(1, Math.round(img.width * scale));
          c.height = Math.max(1, Math.round(img.height * scale));
          const g = c.getContext('2d');
          if (!g) return resolve(null);
          // JPEG has no alpha: the empty parts are put on the view's own dark ground, not black.
          g.fillStyle = '#20242b';
          g.fillRect(0, 0, c.width, c.height);
          g.drawImage(img, 0, 0, c.width, c.height);
          resolve(c.toDataURL('image/jpeg', 0.7));
        } catch { resolve(null); }
      };
      img.onerror = () => resolve(null);
      img.src = src;
    } catch { resolve(null); }
  });
}

/** After a save: the 3D view's picture, when one is mounted, kept for the project's path. Never throws; not waited for. */
export function rememberThumb(path: string, snapshot: () => string | null | undefined): void {
  try {
    const shot = snapshot();
    if (!shot) return;
    void shrink(shot).then((small) => { if (small) keepThumb(path, small); }).catch(() => undefined);
  } catch { /* no picture */ }
}
