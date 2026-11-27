import { createHash, createPrivateKey, createPublicKey, sign } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { isNewer, readRevokedList, revocationOf, revokedAddress, revokedProblem, verifyRevoked, type RevokedList } from './revoked';
import type { StoreKey } from './signature';

const enc = (s: string) => new TextEncoder().encode(s);

/** The engine's test seed (ExtensionLoaderTests.Seed: bytes 7, 14, …): the same key signs the vector both sides check. */
const SEED = Buffer.from(Array.from({ length: 32 }, (_, i) => ((i + 1) * 7) & 255));
const privateKey = createPrivateKey({ key: Buffer.concat([Buffer.from('302e020100300506032b657004220420', 'hex'), SEED]), format: 'der', type: 'pkcs8' });
const raw = Buffer.from(createPublicKey(privateKey).export({ format: 'jwk' }).x!, 'base64url');
const KEY: StoreKey = { id: createHash('sha256').update(raw).digest('hex').slice(0, 16), publicKey: raw.toString('base64') };
const sigFile = (bytes: Uint8Array, keyId = KEY.id) => enc(JSON.stringify({ format: 'lapin-signature', version: 1, keyId, value: sign(null, bytes, privateKey).toString('base64') }));

const list = (generated: string, revoked: unknown[]) => enc(JSON.stringify({ format: 'lapin-extensions-revoked', version: 1, generated, revoked }, null, 2) + '\n');
const entry = (o: Record<string, unknown>) => ({ id: 'ro.test.fixture', reason: 'It sends the project away.', date: '2026-11-27', ...o });

/** ExtensionLoaderTests.VectorText and VectorSignature: the engine signs these bytes to this value. */
const VECTOR = '{"format":"lapin-extensions-revoked","version":1,"generated":"2026-11-27T10:00:00.000Z","revoked":[{"id":"ro.test.fixture","versions":["1.0.0"],"reason":"Broken.","date":"2026-11-27"}]}\n';
const VECTOR_SIGNATURE = '62Ys/HHjWfa6PvDytwzCAXxiR39+VO/ub7Kur7DJvmADskPnn3vEPzW0xGjSg5h+IPtdKYRBiLz4XQJRkN0qDw==';

describe('the revocation list', () => {
  it('verifies the vector the engine signs, and refuses it tampered, unsigned or by another key', async () => {
    const bytes = enc(VECTOR);
    const sig = enc(JSON.stringify({ format: 'lapin-signature', version: 1, keyId: KEY.id, value: VECTOR_SIGNATURE }));
    expect(sign(null, bytes, privateKey).toString('base64')).toBe(VECTOR_SIGNATURE);
    const got = await verifyRevoked([KEY], bytes, sig);
    expect(got?.revoked).toEqual([{ id: 'ro.test.fixture', versions: ['1.0.0'], reason: 'Broken.', date: '2026-11-27' }]);
    expect(await verifyRevoked([KEY], enc(VECTOR.replace('1.0.0', '1.0.1')), sig)).toBeNull();
    expect(await verifyRevoked([KEY], bytes, null)).toBeNull();
    expect(await verifyRevoked([KEY], bytes, enc('{}'))).toBeNull();
    expect(await verifyRevoked([KEY], bytes, sigFile(bytes, 'ffffffffffffffff'))).toBeNull();
    expect(await verifyRevoked([], bytes, sig)).toBeNull();
  });

  it('refuses a list that is not one, even signed: no generated, no reason, nothing named', async () => {
    const noTime = enc(JSON.stringify({ format: 'lapin-extensions-revoked', version: 1, revoked: [] }));
    expect(await verifyRevoked([KEY], noTime, sigFile(noTime))).toBeNull();
    expect(readRevokedList(list('2026-11-27T10:00:00Z', [entry({ versions: ['*'], reason: ' ' })]))).toBeNull();
    expect(readRevokedList(list('2026-11-27T10:00:00Z', [entry({})]))).toBeNull();
    expect(readRevokedList(list('2026-11-27T10:00:00Z', [entry({ sha256: ['ABC'] })]))).toBeNull();
    expect(readRevokedList(list('2026-11-27T10:00:00Z', []))?.revoked).toEqual([]);
  });

  it('takes only a newer list than the one kept', () => {
    const a: RevokedList = { generated: '2026-11-20T10:00:00Z', revoked: [] };
    const b: RevokedList = { generated: '2026-11-27T10:00:00Z', revoked: [] };
    expect(isNewer(a, null)).toBe(true);
    expect(isNewer(b, a)).toBe(true);
    expect(isNewer(a, b)).toBe(false);
    expect(isNewer(b, b)).toBe(false);
  });

  it('matches a version, every version ("*"), or a package’s SHA-256', () => {
    const sha = 'c'.repeat(64);
    const l = readRevokedList(list('2026-11-27T10:00:00Z', [entry({ id: 'a.one', versions: ['1.0.0'] }), entry({ id: 'a.all', versions: ['*'] }), entry({ id: 'a.sha', sha256: [sha] })]))!;
    expect(revocationOf(l, { id: 'a.one', version: '1.0.0' })?.reason).toBe('It sends the project away.');
    expect(revocationOf(l, { id: 'a.one', version: '1.0.1' })).toBeNull();
    expect(revocationOf(l, { id: 'a.all', version: '9.9.9' })).not.toBeNull();
    expect(revocationOf(l, { id: 'a.sha', version: '2.0.0', package: { sha256: sha } })).not.toBeNull();
    expect(revocationOf(l, { id: 'a.sha', version: '2.0.0', package: { sha256: 'd'.repeat(64) } })).toBeNull();
    expect(revocationOf(l, { id: 'a.one', version: '3.0.0', package: { sha256: sha } })).toBeNull();
    expect(revocationOf(null, { id: 'a.one', version: '1.0.0' })).toBeNull();
  });

  it('is read beside the index, and says why an install is refused', () => {
    expect(revokedAddress('https://lapinbim.com/extensions/index.json')).toBe('https://lapinbim.com/extensions/revoked.json');
    expect(revokedAddress('/Users/me/cat/index.json')).toBe('/Users/me/cat/revoked.json');
    expect(revokedAddress('index.json')).toBe('revoked.json');
    expect(revokedProblem('Planning+', '1.2.0', { id: 'x.y', versions: ['1.2.0'], reason: 'Broken.', date: '2026-11-27' }))
      .toBe('„Planning+” 1.2.0 is not installed: Lapin Extensions withdrew this version on 2026-11-27: Broken.');
  });
});

describe('the revocation list’s sentences', () => {
  it('are in every language of the interface', async () => {
    const { LANGUAGES, loadLanguage, translate } = await import('../../i18n');
    const refused = revokedProblem('Planning+', '1.2.0', { id: 'x.y', versions: ['1.2.0'], reason: 'Broken.', date: '2026-11-27' });
    for (const { id } of LANGUAGES) {
      if (id === 'en') continue;
      await loadLanguage(id);
      for (const s of ['Withdrawn by Lapin Extensions:', 'Withdrawn by Lapin Extensions; it is turned off when Lapin restarts:', 'Withdrawn by Lapin Extensions; what is installed stays:'])
        expect(translate(s, id), `${id}: ${s}`).toBeTruthy();
      const t = translate(refused, id);
      expect(t, `${id}: the refusal`).toContain('Broken.');
      expect(t).toContain('2026-11-27');
      expect(t).not.toBe(refused);
    }
  }, 60_000);
});
