/** The types of `@lapin/sdk` v1 for a plug-in's TypeScript (the object is ui/src/platform/sdk.ts's `sdk`). */
declare module '@lapin/sdk' {
  type Sdk = import('../src/platform/sdk').LapinSdk;
  export type ModuleExports = import('../src/platform/sdk').ModuleExports;
  export const version: Sdk['version'];
  export const versionParts: Sdk['versionParts'];
  export const execute: Sdk['execute'];
  export const query: Sdk['query'];
  export const useSession: Sdk['useSession'];
  export const useUi: Sdk['useUi'];
  export const select: Sdk['select'];
  export const notify: Sdk['notify'];
  export const focusPane: Sdk['focusPane'];
  export const useLang: Sdk['useLang'];
  export const t: Sdk['t'];
  export const promptText: Sdk['promptText'];
  export const FieldPicker: Sdk['FieldPicker'];
  export const PendingDialog: Sdk['PendingDialog'];
  export const saveFile: Sdk['saveFile'];
  export const safeFileName: Sdk['safeFileName'];
  export const openImportTable: Sdk['openImportTable'];
  export const module: Sdk['module'];
  const sdk: Sdk;
  export default sdk;
}
