/**
 * What a Lapin interface plug-in shares with the app (docs/plan-extension-modules.md §3.2): these imports are not
 * bundled into plugin.js but read, when the plug-in loads, from `globalThis.__lapin.libs`, which the app publishes
 * (ui/src/platform/extensions/hostLibs.ts, SHARED_LIBS: the two lists are kept equal by extensionKit.test.ts).
 */

/** The imports the app shares, by the name a plug-in imports. */
export const SHARED = ['react', 'react-dom', 'react-dom/client', 'react/jsx-runtime', 'zustand', 'lucide-react', 'three', '@lapin/sdk'];

/** What `@lapin/sdk` v1 exports (ui/src/platform/sdk.ts, `sdk`; kept equal by sdk.test.ts). */
export const SDK_EXPORTS = [
  'version', 'versionParts',
  'execute', 'query', 'useSession',
  'useUi', 'select', 'notify', 'focusPane',
  'useLang', 't',
  'promptText',
  'FieldPicker', 'PendingDialog',
  'saveFile', 'safeFileName',
  'openImportTable',
  'module',
];

const PREFIX = '\0lapin-shared:';
const IDENT = /^[A-Za-z_$][\w$]*$/;
const RESERVED = new Set(['default', '__esModule', 'await', 'break', 'case', 'catch', 'class', 'const', 'continue', 'debugger', 'delete', 'do', 'else', 'enum', 'export', 'extends', 'false', 'finally', 'for', 'function', 'if', 'import', 'in', 'instanceof', 'let', 'new', 'null', 'return', 'static', 'super', 'switch', 'this', 'throw', 'true', 'try', 'typeof', 'var', 'void', 'while', 'with', 'yield', 'implements', 'interface', 'package', 'private', 'protected', 'public', 'arguments', 'eval']);

/** The named exports of a shared library, read from the copy the plug-in's project has (its version is the app's at build time). */
async function namesOf(id, importer) {
  if (id === '@lapin/sdk') return SDK_EXPORTS;
  const ns = await importer(id);
  return Object.keys(ns).filter((k) => IDENT.test(k) && !RESERVED.has(k));
}

/** The module that stands for a shared import in plugin.js. */
export function sharedModuleCode(id, names) {
  const lib = `globalThis.__lapin && globalThis.__lapin.libs && globalThis.__lapin.libs[${JSON.stringify(id)}]`;
  const lines = [
    `const lib = ${lib};`,
    `if (!lib) throw new Error(${JSON.stringify(`This Lapin does not share "${id}" with extensions.`)});`,
    'export default (lib.default !== undefined ? lib.default : lib);',
  ];
  // One declaration a name, so the names a plug-in does not use are dropped (vite.mjs: property reads have no side effects).
  for (const n of names) lines.push(`export const ${n} = lib.${n};`);
  return lines.join('\n') + '\n';
}

/**
 * The Vite / Rollup plug-in: each shared import becomes a module reading the app's copy. `importer` loads a library to
 * list its names (default: Node's import from the kit's folder, which finds the app's node_modules).
 */
export function lapinShared({ importer = (id) => import(id) } = {}) {
  return {
    name: 'lapin-shared',
    enforce: 'pre',
    resolveId(source) { return SHARED.includes(source) ? PREFIX + source : null; },
    async load(id) {
      if (!id.startsWith(PREFIX)) return null;
      const name = id.slice(PREFIX.length);
      return sharedModuleCode(name, await namesOf(name, importer));
    },
  };
}
