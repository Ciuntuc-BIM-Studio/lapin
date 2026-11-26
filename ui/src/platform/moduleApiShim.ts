/**
 * `LapinModule.exports` and `api` (docs/plan-extension-modules.md §6.3), declared here only until the interface loader's
 * branch (feat/ext-s3) is merged: it declares the same two fields, with the same types, in module.ts. Delete this file then.
 */

import type {} from './module';

declare module './module' {
  interface LapinModule {
    exports?: Record<string, unknown>;
    api?: number;
  }
}

export {};
