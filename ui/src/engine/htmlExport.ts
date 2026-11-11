/**
 * What the interface gives the engine for a page exported for the browser (decision 482, docs/html-export.md): the
 * viewer's code (ui/src/standalone, built on its own by vite.config.ts and fetched only here), the phrases in the
 * user's language, the edition's name and build, the theme. The engine writes the page with them (project.exportHtml).
 */

import { usePrefs, resolveTheme } from '../app/prefs';
import { phraseBook, useLang, type Lang } from '../i18n';
import { brand } from '../platform/brand';
import { isDesktop } from './files';

export interface HtmlChoice { drawings?: string[]; model3d?: boolean; tables?: boolean; project?: boolean; viewId?: string }

export async function htmlExportArgs(choice: HtmlChoice, lang: Lang = useLang.getState().lang): Promise<Record<string, unknown>> {
  const { default: viewer } = await import('virtual:lapin-viewer');
  let app = brand.name;
  if (isDesktop()) {
    // Tauri's app plugin, through the core API the app already loads (a new module would make Vite reload the page in dev).
    try { app = `${brand.name} ${await (await import('@tauri-apps/api/core')).invoke<string>('plugin:app|version')}`; } catch { /* the name alone */ }
  }
  return {
    ...choice, viewer, lang, strings: phraseBook(lang),
    edition: brand.name, app, theme: { name: resolveTheme(usePrefs.getState().theme) },
  };
}
