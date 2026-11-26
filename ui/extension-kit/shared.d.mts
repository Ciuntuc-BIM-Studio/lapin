export const SHARED: readonly string[];
export const SDK_EXPORTS: readonly string[];
export function sharedModuleCode(id: string, names: readonly string[]): string;
export function lapinShared(options?: { importer?: (id: string) => Promise<Record<string, unknown>> }): unknown;
