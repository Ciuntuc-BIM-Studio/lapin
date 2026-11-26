/**
 * The reference documents of an Atrium Server project, read and written directly (docs/plan-cde-references.md §5, stage R4; the
 * server's side is docs/atrium-server.md, "Reference documents (R3)"): the CDE web's Documents tab works through this adapter, with
 * no project open. A file goes to lapin-remote's object store first (content-addressed: the SHA-256 is its name there), then the
 * record names it; the record's ETag goes back as If-Match on a change, and a 412 says the document was read again.
 */

import { api } from '../../../engine/cdeServer';
import { notify } from '../../../app/store';
import { refusalText } from '../../../engine/refusals';
import type { ModelInfo, RefAdapter, RefPerms } from './adapter';
import { today } from './expiry';
import type { AddResult, Checklist, FolderTree, RefDoc, RefFile, RefFileData, RefFolder, TypeList } from './references';

export class ServerRefError extends Error {
  constructor(public status: number, public code: string, message: string, public body: Record<string, unknown> | null = null) { super(message); }
}

// ------------------------------------------------------------ pure parts

export const bytesOfBase64 = (data: string) => Uint8Array.from(atob(data), (c) => c.charCodeAt(0));
export function base64OfBytes(bytes: Uint8Array): string {
  let bin = '';
  for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(bin);
}

/** A file's SHA-256 as lower-case hex (the name of its object on the server). */
export async function sha256Hex(bytes: Uint8Array): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', bytes as BufferSource);
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}

/** A server's document as the interface shows it: its files are all held (the server has them), a null count is none. */
export function normalizeDoc(d: RefDoc): RefDoc {
  return { ...d, files: (d.files ?? []).map((f) => ({ ...f, held: true })) };
}

/** A server's folder tree as the interface has it: the derived folders are not counted by the server (null), shown as none. */
export function normalizeTree(t: Partial<FolderTree> & { folders?: RefFolder[] }): FolderTree {
  const fix = (nodes: RefFolder[]): RefFolder[] => (nodes ?? []).map((n) => ({ ...n, count: n.count ?? 0, total: n.total ?? 0, children: fix(n.children ?? []) }));
  return {
    folders: fix(t.folders ?? []), typeFolders: t.typeFolders ?? {}, roleFolders: t.roleFolders ?? {}, unfiled: t.unfiled ?? 0, language: t.language ?? 'en',
    root: '', keepLimit: 0, projectFolder: null,
  };
}

/** Viewers read; editors add and change what they added; owners do everything, folders and types included. */
export function serverPerms(role: 'viewer' | 'editor' | 'owner', authorId: string | null): RefPerms {
  return {
    add: role !== 'viewer',
    canChange: (d) => role === 'owner' || (role === 'editor' && !!authorId && d.addedBy === authorId),
    remove: role === 'owner',
    admin: role === 'owner',
  };
}

/** The files a document names after files are added (a name already there is replaced, ignoring case) or one is taken off. */
export function mergeFiles(current: RefFile[], add: Pick<RefFile, 'name' | 'sha256' | 'size' | 'mime'>[], drop?: string): Pick<RefFile, 'name' | 'sha256' | 'size' | 'mime'>[] {
  const keep = (f: RefFile) => f.name.toLowerCase() !== drop?.toLowerCase() && !add.some((a) => a.name.toLowerCase() === f.name.toLowerCase());
  const plain = (f: RefFile) => ({ name: f.name, sha256: f.sha256, size: f.size, ...(f.mime ? { mime: f.mime } : {}) });
  return [...current.filter(keep).map(plain), ...add];
}

// ------------------------------------------------------------ the adapter

const enc = encodeURIComponent;

/**
 * The adapter for one server project. `lang` names the folders and the types.
 */
export function serverAdapter(project: string, lang: string, name = project): RefAdapter {
  const base = `/atrium/1.0/projects/${enc(project)}/references`;
  const objects = `/lapin-remote/1.0/projects/${enc(project)}/objects`;
  const seen = new Map<string, RefDoc>();

  async function send(method: string, path: string, body?: unknown, headers: Record<string, string> = {}): Promise<{ body: Record<string, unknown> | null; etag: string | null; response: Response }> {
    const r = await api(path, {
      method,
      headers: { ...(body === undefined ? {} : { 'content-type': 'application/json' }), ...headers },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    let json: Record<string, unknown> | null = null;
    if (r.status !== 204 && (r.headers.get('content-type') ?? '').includes('json')) { try { json = await r.json() as Record<string, unknown>; } catch { json = null; } }
    if (!r.ok) {
      const code = (json?.code as string | undefined) ?? (r.status === 404 ? 'not_offered' : `http_${r.status}`);
      throw new ServerRefError(r.status, code, refusalText(code, (json?.message as string | undefined) ?? `The server answered ${r.status}.`), json);
    }
    return { body: json, etag: r.headers.get('etag'), response: r };
  }

  const remember = (d: RefDoc, etag?: string | null): RefDoc => {
    const doc = normalizeDoc(etag ? { ...d, etag } : d);
    seen.set(doc.id, doc);
    return doc;
  };

  /** Sends what puts a document's files on the server first when it says they are missing (`object_missing`), then once more. */
  async function withObjects<T>(blobs: Map<string, Uint8Array>, run: () => Promise<T>): Promise<T> {
    try { return await run(); } catch (e) {
      if (!(e instanceof ServerRefError) || e.code !== 'object_missing') throw e;
      const missing = (e.body?.missing as string[] | undefined) ?? [...blobs.keys()];
      for (const sha of missing) {
        const bytes = blobs.get(sha);
        if (!bytes) continue;
        const r = await api(`${objects}/${sha}`, { method: 'PUT', headers: { 'content-type': 'application/octet-stream' }, body: bytes as BodyInit });
        if (!r.ok) throw new ServerRefError(r.status, `http_${r.status}`, `The server did not take the file (${r.status}).`);
      }
      return run();
    }
  }

  async function read(parts: { name: string; data: string }[]) {
    const blobs = new Map<string, Uint8Array>();
    const files = await Promise.all(parts.map(async (p) => {
      const bytes = bytesOfBase64(p.data);
      const sha256 = await sha256Hex(bytes);
      blobs.set(sha256, bytes);
      return { name: p.name, sha256, size: bytes.length };
    }));
    return { blobs, files };
  }

  /** A write that said so on failure instead of throwing: the refusal as notice, a 412 as "read again". */
  async function said<T>(f: () => Promise<T>): Promise<T | null> {
    try { return await f(); } catch (e) {
      if (e instanceof ServerRefError && e.status === 412) notify('Someone changed this document at the same time: it was read again. Make your change again.');
      else notify(e instanceof Error ? e.message : String(e));
      return null;
    }
  }

  const patch = (id: string, body: Record<string, unknown>) => said(async () => {
    const etag = seen.get(id)?.etag;
    const r = await send('PATCH', `${base}/${enc(id)}`, body, etag ? { 'if-match': etag } : {});
    return remember(r.body as unknown as RefDoc, r.etag);
  });

  return {
    kind: 'server',
    project: { id: project, name },
    async model(id, file) {
      // The document read whole: its files' IDS reports come only with it, the list carrying their summaries.
      try {
        const r = await send('GET', `${base}/${enc(id)}?lang=${enc(lang)}`);
        const f = ((r.body?.files as (RefFile & Partial<ModelInfo>)[] | undefined) ?? []).find((x) => x.name === file);
        return f ? { kind: f.kind ?? null, ifc: f.ifc ?? null, ids: f.ids ?? null } : null;
      } catch { return null; }
    },
    async list() {
      const r = await send('GET', `${base}?today=${today()}&lang=${enc(lang)}`);
      seen.clear();
      return ((r.body?.references as RefDoc[] | undefined) ?? []).map((d) => remember(d));
    },
    async folders() { return normalizeTree((await send('GET', `${base}/folders?lang=${enc(lang)}`)).body as Partial<FolderTree>); },
    async types() { return (await send('GET', `${base}/types?lang=${enc(lang)}`)).body as unknown as TypeList; },
    async checklist(id) { return (await send('GET', `${base}/${enc(id)}/checklist?lang=${enc(lang)}`)).body as unknown as Checklist; },
    async file(id, name) {
      const doc = seen.get(id);
      const n = name ?? doc?.files[0]?.name;
      if (!n) return null;
      try {
        const r = await api(`${base}/${enc(id)}/files/${enc(n)}`);
        if (!r.ok) return null;
        const bytes = new Uint8Array(await r.arrayBuffer());
        const out: RefFileData = { name: n, mime: r.headers.get('content-type') ?? undefined, size: bytes.length, data: base64OfBytes(bytes) };
        return out;
      } catch { return null; }
    },
    async text(id, name, pages) {
      try {
        const r = await send('GET', `${base}/${enc(id)}/text?${name ? `name=${enc(name)}&` : ''}${pages ? `pages=${enc(pages)}` : ''}`);
        return r.body as never;
      } catch { return null; }
    },
    async add(args) {
      return said(async () => {
        const parts = (args.files as { name: string; data: string }[] | undefined) ?? [];
        const { blobs, files } = await read(parts);
        const body: Record<string, unknown> = {};
        for (const k of ['title', 'folder', 'type', 'relatesTo', 'status', 'issuer', 'number']) if (args[k] != null && args[k] !== '') body[k] = args[k];
        if (files.length) body.files = files;
        const r = await withObjects(blobs, () => send('POST', base, body));
        const doc = remember(r.body as unknown as RefDoc, r.etag);
        const result: AddResult = {
          id: doc.id, files: doc.files, duplicates: ((r.body?.duplicates as string[] | undefined) ?? []),
          warnings: ((r.body?.warnings as { message?: string }[] | undefined) ?? []).map((w) => w.message ?? '').filter(Boolean),
        };
        return result;
      });
    },
    async update(id, change) {
      const body = { ...change };
      delete body.today;
      return patch(id, body);
    },
    async move(ids, folder) { for (const id of ids) await patch(id, { folder }); return null; },
    async attach(id, parts) {
      return said(async () => {
        const { blobs, files } = await read(parts);
        const current = seen.get(id)?.files ?? [];
        const merged = mergeFiles(current, files);
        const etag = seen.get(id)?.etag;
        const r = await withObjects(blobs, () => send('PATCH', `${base}/${enc(id)}`, { files: merged }, etag ? { 'if-match': etag } : {}));
        const doc = remember(r.body as unknown as RefDoc, r.etag);
        return { files: doc.files, warnings: ((r.body?.warnings as { message?: string }[] | undefined) ?? []).map((w) => w.message ?? '').filter(Boolean) };
      });
    },
    async detach(id, name) { return patch(id, { files: mergeFiles(seen.get(id)?.files ?? [], [], name) }); },
    async supersede(id, by) { return said(() => send('POST', `${base}/${enc(id)}/supersede`, { by })); },
    async withdraw(id, reason) { return said(() => send('POST', `${base}/${enc(id)}/withdraw`, { reason })); },
    async remove(id) {
      return said(async () => {
        const etag = seen.get(id)?.etag;
        await send('DELETE', `${base}/${enc(id)}`, undefined, etag ? { 'if-match': etag } : {});
        seen.delete(id);
        return true;
      });
    },
    async setFolders(args) { return said(() => send('PUT', `${base}/folders`, args)); },
    async setTypes(types) { return said(() => send('PUT', `${base}/types`, { types })); },
  };
}
