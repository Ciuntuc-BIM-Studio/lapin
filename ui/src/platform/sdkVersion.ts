/**
 * The version of `@lapin/sdk`, the façade an extension's interface plug-in is built against (docs/plan-extension-modules.md
 * §1.3, §3.2). Minor goes up when the SDK gains something; Major when something a plug-in may use changes or goes away.
 * A plug-in says what it needs in its manifest (`"sdk": "1.x"`, `"1"`, `"1.2"`): a Major this app does not have, or a Minor
 * newer than its own, is refused. Kept apart from sdk.ts, which pulls the whole app in, so the loader and its tests do not.
 */

export const SDK_VERSION = { major: 1, minor: 0 } as const;
export const SDK_VERSION_TEXT = `${SDK_VERSION.major}.${SDK_VERSION.minor}`;

/** Why a plug-in needing `range` cannot run on this SDK; null when it can. */
export function sdkProblem(range: unknown, have: { major: number; minor: number } = SDK_VERSION): string | null {
  if (typeof range !== 'string' || !range.trim()) return 'it does not say which SDK version it needs';
  const m = /^\^?(\d+)(?:\.(\d+|x|\*))?(?:\.(\d+|x|\*))?$/.exec(range.trim());
  if (!m) return `its SDK version "${range}" cannot be read`;
  const major = Number(m[1]);
  const minor = m[2] && /^\d+$/.test(m[2]) ? Number(m[2]) : 0;
  if (major !== have.major || minor > have.minor) return `it needs SDK ${range.trim()}, and this Lapin has SDK ${have.major}.${have.minor}`;
  return null;
}
