/**
 * What the desktop app gives extensions' interface plug-ins (docs/plan-extension-modules.md §3.2), fetched only when the
 * engine has loaded one (host.ts): its namespace imports of the shared libraries (all of three, all of lucide-react)
 * would otherwise weigh on every start.
 *
 * `globalThis.__lapin = { version: 1, libs, sdk }`: one React, one zustand, one three for the app and every plug-in (two
 * Reacts break hooks). The extension kit (ui/extension-kit) rewrites a plug-in's imports of these to reads from it. Each
 * plug-in is imported from a blob: URL (the webview's CSP allows blob: scripts, desktop/src-tauri/tauri.conf.json), its
 * phrases given to the translator, its CSS put in a `<style data-ext="<id>">`.
 */

import * as React from 'react';
import * as ReactDOM from 'react-dom';
import * as ReactDOMClient from 'react-dom/client';
import * as JsxRuntime from 'react/jsx-runtime';
import * as Zustand from 'zustand';
import * as Lucide from 'lucide-react';
import * as THREE from 'three';
import loaded from 'virtual:lapin-modules';
import { isGuest } from '../../engine/share';
import { notify } from '../../app/store';
import { addPhrases } from '../../i18n';
import { sdk } from '../sdk';
import { loadPlugins, pluginsWanted, setPlugins, type PluginFiles } from './plugins';

/** The libraries plug-ins share with the app, by the name they import (the kit's SHARED list, ui/extension-kit/shared.mjs). */
export const SHARED_LIBS: Record<string, unknown> = {
  react: React, 'react-dom': ReactDOM, 'react-dom/client': ReactDOMClient, 'react/jsx-runtime': JsxRuntime,
  zustand: Zustand, 'lucide-react': Lucide, three: THREE, '@lapin/sdk': sdk,
};

/** Publishes the shared libraries and the SDK for plug-ins (once: what is there is kept, and cannot be replaced). */
export function publishHost(target: object = globalThis): void {
  if ('__lapin' in target) return;
  Object.defineProperty(target, '__lapin', { value: Object.freeze({ version: 1, libs: Object.freeze({ ...SHARED_LIBS }), sdk }), enumerable: false, writable: false, configurable: false });
}

/** A plug-in's code evaluated as an ES module, from a blob: URL. */
async function importBlob(js: string): Promise<unknown> {
  const url = URL.createObjectURL(new Blob([js], { type: 'text/javascript' }));
  try { return await import(/* @vite-ignore */ url); } finally { URL.revokeObjectURL(url); }
}

function addStyle(id: string, css: string): void {
  const style = document.createElement('style');
  style.dataset.ext = id;
  style.textContent = css;
  document.head.appendChild(style);
}

/** Loads the plug-ins given, keeps what loaded for modules.ts, and says which were left out. */
export async function installPlugins(list: PluginFiles[]): Promise<void> {
  // A shared session's guest works in the host's engine, through the relay: no plug-ins of this computer's own.
  if (!pluginsWanted({ inBrowser: false, guest: isGuest() })) { setPlugins({ modules: [], loaded: [], left: [] }); return; }
  publishHost();
  const result = await loadPlugins(list, { importModule: importBlob, taken: loaded.map((m) => m.id), addStyle, addPhrases, sdk });
  setPlugins(result);
  for (const l of result.left) console.warn(`Extension ${l.id} ${l.version} was left out: ${l.reason}`);
  if (result.left.length) notify(`Some extensions were left out: ${result.left.map((l) => l.id).join(', ')}.`);
}
