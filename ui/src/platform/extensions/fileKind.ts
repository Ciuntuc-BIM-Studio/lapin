/**
 * The library kinds of extension that are one file each (docs/plan-extensions.md §3): line types, drafting standards, rule
 * packs, bar shapes, shared parameters, families. The package is read and checked without the engine (packs.ts), kept on
 * this computer under the extension's name (the library's own store of this kind: platform/libraries.ts), and brought into
 * the project open on request (Installed → Add to this project, the Catalog) through the library's own importer.
 * Uninstalling forgets the file; what a project already took from it stays. Pure: the stores are given.
 */

import type { ContentInstaller } from './install';
import type { ExtensionKind } from './manifest';

export interface FileKindDeps {
  kind: ExtensionKind;
  /** The file's extension. */
  ext: string;
  /** Reads and checks the package; throws a sentence when it is not this kind's file. */
  read: (bytes: Uint8Array) => { text: string; names: string[] };
  files: {
    keep: (ext: string, name: string, text: string, extension: string) => Promise<unknown>;
    forget: (ext: string, name: string) => Promise<void>;
    get: (ext: string, name: string) => Promise<string | null>;
  };
  /** The file brought into the open project; null when it refused. */
  bring?: (text: string, file: string) => Promise<string | null>;
}

export function fileKindInstaller(deps: FileKindDeps): ContentInstaller {
  return {
    kind: deps.kind,
    install: async (file, m) => {
      const r = deps.read(file.bytes);
      await deps.files.keep(deps.ext, m.name, r.text, m.id);
      return { file: m.name, ext: deps.ext, names: r.names };
    },
    uninstall: async (r) => { if (typeof r.file === 'string') await deps.files.forget(deps.ext, r.file); },
    ...(deps.bring ? {
      open: {
        label: 'Add to this project',
        run: (x) => { void deps.files.get(deps.ext, String(x.receipt.file)).then((text) => (text ? deps.bring!(text, `${String(x.receipt.file)}.${deps.ext}`) : null)).catch(() => null); },
      },
    } : {}),
  };
}
