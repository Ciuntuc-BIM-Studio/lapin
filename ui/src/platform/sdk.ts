/**
 * `@lapin/sdk` v1 (docs/plan-extension-modules.md §3.2): the curated façade an extension's interface plug-in uses, the
 * same object published as `globalThis.__lapin.sdk` (host.ts), to which the extension kit maps `import … from '@lapin/sdk'`.
 * A plug-in cannot import the app's files by path, as the modules compiled with it do, so what it may use is listed here
 * and versioned by SDK_VERSION: something added raises the Minor, something changed or gone raises the Major.
 * The names are kept equal with the kit's list (ui/extension-kit/shared.mjs, `SDK_EXPORTS`) by sdk.test.ts.
 *
 * Its first contents are what planning-plus imports (measured on its branches), plus `module(id)`: what an open module
 * exports for extensions (`LapinModule.exports`) and that export's `api` number. Call `module` inside the plug-in's
 * functions and components, not while it loads: the plug-ins load before the module list is complete.
 */

import loaded from 'virtual:lapin-modules';
import { execute, query, useSession } from '../engine/session';
import { focusPane, notify, select, useUi } from '../app/store';
import { t, useLang } from '../i18n';
import { promptText } from '../shell/prompts';
import { FieldPicker } from '../shell/FieldPicker';
import { PendingDialog } from '../modules/core/PendingDialog';
import { saveFile, safeFileName } from '../engine/files';
import { openImportTable } from '../modules/core/tables/tables';
import { extensionModules } from './extensions/plugins';
import { SDK_VERSION, SDK_VERSION_TEXT } from './sdkVersion';

/** What an open module offers extensions: its `exports` and their `api` number (null: the module is not in this edition). */
export interface ModuleExports { id: string; api: number; exports: Readonly<Record<string, unknown>> }

/** An open module's exports for extensions, or null when this edition does not have the module. */
export function module(id: string): ModuleExports | null {
  const m = [...loaded, ...extensionModules()].find((x) => x.id === id);
  return m ? { id: m.id, api: m.api ?? 1, exports: Object.freeze({ ...(m.exports ?? {}) }) } : null;
}

export const sdk = {
  version: SDK_VERSION_TEXT,
  versionParts: SDK_VERSION,
  execute, query, useSession,
  useUi, select, notify, focusPane,
  useLang, t,
  promptText,
  FieldPicker, PendingDialog,
  saveFile, safeFileName,
  openImportTable,
  module,
};

export type LapinSdk = typeof sdk;
