/** The types of the kit's build (vite.mjs). */
export interface PluginBuildOptions {
  /** The file whose default export is the module. */
  entry: string;
  /** Where plugin.js, plugin.css and i18n/ go (default dist/ui). */
  outDir?: string;
  /** The folder of the phrase tables (<lang>.json), copied to <outDir>/i18n. */
  i18n?: string;
  /** The project's folder (default the current one). */
  root?: string;
  minify?: boolean;
  /** false: nothing written, the code given back. */
  write?: boolean;
  /** Loads a shared library to list its names (default: Node's import). */
  importer?: (id: string) => Promise<Record<string, unknown>>;
}

export const SHARED: readonly string[];
export const SDK_EXPORTS: readonly string[];
export function pluginConfig(options: PluginBuildOptions): Record<string, unknown>;
export function buildPlugin(options: PluginBuildOptions & { write: false }): Promise<{ js: string; css: string | null }>;
export function buildPlugin(options: PluginBuildOptions): Promise<{ js: string; css: string | null } | null>;
export function sharedModuleCode(id: string, names: readonly string[]): string;
export function lapinShared(options?: { importer?: (id: string) => Promise<Record<string, unknown>> }): unknown;
