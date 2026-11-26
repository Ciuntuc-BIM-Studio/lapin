import { describe, expect, it } from 'vitest';
import { acceptTerms, FULL_ACCESS, moduleStatus, type EngineExtension } from './moduleKind';

const e = (o: Partial<EngineExtension>): EngineExtension => ({ id: 'ro.test.x', status: 'loaded', enabled: true, pendingRemoval: false, ...o });

describe('an extension module in Installed', () => {
  it('says nothing when it runs, and why when it was refused or its interface left out', () => {
    expect(moduleStatus(e({}))).toBeNull();
    expect(moduleStatus(e({ status: 'failed', problem: 'It needs „planning”.' }))).toEqual({ text: 'Not loaded:', detail: 'It needs „planning”.', problem: true });
    expect(moduleStatus(e({}), 'it asks for SDK 2.x')).toEqual({ text: 'Its interface was left out:', detail: 'it asks for SDK 2.x', problem: true });
    expect(moduleStatus(undefined, 'it has no interface code')?.problem).toBe(true);
    expect(moduleStatus(undefined)).toBeNull();
  });

  it('says what the next start changes', () => {
    expect(moduleStatus(e({ pendingRemoval: true }))?.text).toBe('Removed when Lapin restarts.');
    expect(moduleStatus(e({ status: 'pending' }))?.text).toBe('Loads when Lapin restarts.');
    expect(moduleStatus(e({ nextVersion: '1.1.0' }))).toMatchObject({ detail: '1.1.0', problem: false });
    expect(moduleStatus(e({ status: 'disabled', enabled: false }))?.text).toBe('Off.');
    expect(moduleStatus(e({ status: 'disabled', enabled: true }))?.text).toBe('Turns on when Lapin restarts.');
    expect(moduleStatus(e({ enabled: false }))?.text).toBe('Turns off when Lapin restarts.');
  });
});

describe('the terms at install', () => {
  it('shows the permission and the licence, then the agreement to accept; no at either step installs nothing', () => {
    const asked: string[] = [];
    const yes = (q: string) => { asked.push(q); return true; };
    expect(acceptTerms({ name: 'Planning+' }, { license: 'LicenseRef-X', licenseText: 'Terms.', eula: { version: '2', text: 'Agreement.' } }, yes)).toBe(true);
    expect(asked[0]).toContain(FULL_ACCESS);
    expect(asked[0]).toContain('LicenseRef-X');
    expect(asked[1]).toContain('Agreement.');
    expect(asked[1]).toContain('I accept');
    expect(acceptTerms({ name: 'P' }, { license: 'L', eula: { version: '1' } }, (q) => !q.includes('agreement'))).toBe(false);
    expect(acceptTerms({ name: 'P' }, { license: 'L' }, () => false)).toBe(false);
    let n = 0;
    expect(acceptTerms({ name: 'P' }, { license: 'L' }, () => { n++; return true; })).toBe(true);
    expect(n).toBe(1);
  });
});
