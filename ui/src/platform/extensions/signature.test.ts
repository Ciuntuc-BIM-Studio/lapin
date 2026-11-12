import { createHash, generateKeyPairSync, sign } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { download, sha256Hex } from './install';
import { furniturePack } from './fixtures';
import { catalogueTrust, packageMessage, packageProblem, readSignatureFile, trustProblem, verifyEd25519, verifyEd25519Own, verifySignature, type StoreKey } from './signature';
import { validateManifest, type Manifest } from './manifest';

const hex = (s: string) => Uint8Array.from(s.match(/../g)!.map((b) => parseInt(b, 16)));
const enc = (s: string) => new TextEncoder().encode(s);

/** A throwaway store key, as scripts/store-key.mjs makes the real one, and its signatures as lapin-extensions' tools/sign.mjs writes them. */
function storeKey() {
  const { privateKey, publicKey } = generateKeyPairSync('ed25519');
  const raw = Buffer.from(publicKey.export({ format: 'jwk' }).x!, 'base64url');
  const key: StoreKey = { id: createHash('sha256').update(raw).digest('hex').slice(0, 16), publicKey: raw.toString('base64') };
  const signBytes = (bytes: Uint8Array) => ({ keyId: key.id, value: sign(null, bytes, privateKey).toString('base64') });
  return { key, raw: new Uint8Array(raw), signBytes };
}

describe('Ed25519, the platform’s and Lapin’s own', () => {
  // RFC 8032 §7.1, TEST 1 (the empty message) and TEST 2 (one byte).
  const vectors = [
    { pub: 'd75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a', msg: '', sig: 'e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b' },
    { pub: '3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c', msg: '72', sig: '92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00' },
  ];

  it('accepts the RFC’s signatures and refuses them changed', async () => {
    for (const v of vectors) {
      const msg = v.msg ? hex(v.msg) : new Uint8Array(0);
      expect(await verifyEd25519Own(hex(v.pub), hex(v.sig), msg)).toBe(true);
      expect(await verifyEd25519(hex(v.pub), hex(v.sig), msg)).toBe(true);
      const bad = hex(v.sig); bad[10] ^= 1;
      expect(await verifyEd25519Own(hex(v.pub), bad, msg)).toBe(false);
      expect(await verifyEd25519Own(hex(v.pub), hex(v.sig), enc('x'))).toBe(false);
    }
  });

  it('agrees with Node’s signer on random keys and messages; an S past the group order is refused', async () => {
    for (let i = 0; i < 4; i++) {
      const { raw, signBytes } = storeKey();
      const msg = enc(`index ${i} ${'é'.repeat(i * 50)}`);
      const sig = Uint8Array.from(Buffer.from(signBytes(msg).value, 'base64'));
      expect(await verifyEd25519Own(raw, sig, msg)).toBe(true);
      const other = enc(`index ${i}!`);
      expect(await verifyEd25519Own(raw, sig, other)).toBe(false);
      const high = sig.slice(); high[63] |= 0xf0;
      expect(await verifyEd25519Own(raw, high, msg)).toBe(false);
    }
    expect(await verifyEd25519Own(new Uint8Array(32).fill(255), new Uint8Array(64), enc('x'))).toBe(false);
  });

  it('leaves the bytes it is given as they were (a Node Buffer’s slice is a view)', async () => {
    for (let i = 0; i < 8; i++) {
      const { raw, signBytes } = storeKey();
      const msg = enc(`m${i}`);
      const sig = Buffer.from(signBytes(msg).value, 'base64');
      const pub = Buffer.from(raw);
      const before = Buffer.concat([sig, pub]).toString('hex');
      expect(await verifyEd25519Own(pub, sig, msg)).toBe(true);
      expect(Buffer.concat([sig, pub]).toString('hex')).toBe(before);
    }
  });
});

describe('the catalogue’s signature', () => {
  const { key, signBytes } = storeKey();
  const index = enc('{"format":"lapin-extensions-index","version":1,"extensions":[]}\n');
  const sigFile = (bytes: Uint8Array) => enc(JSON.stringify({ format: 'lapin-signature', version: 1, ...signBytes(bytes) }));

  it('reads signed by a key Lapin carries, unsigned without a file, wrongly signed otherwise', async () => {
    expect(await catalogueTrust([key], index, sigFile(index))).toBe('signed');
    expect(await catalogueTrust([key], index, null)).toBe('unsigned');
    expect(await catalogueTrust([key], enc('{"changed":1}'), sigFile(index))).toBe('bad');
    expect(await catalogueTrust([], index, sigFile(index))).toBe('bad'); // a key Lapin does not carry
    expect(await catalogueTrust([key], index, enc('not json'))).toBe('bad');
    expect(readSignatureFile(enc('{"format":"lapin-signature","version":2}'))).toBeNull();
  });

  it('Lapin’s own catalogue must be signed once Lapin carries a key; another may be unsigned, never wrongly signed', () => {
    expect(trustProblem('unsigned', { isDefault: true, keys: [] })).toBeNull();
    expect(trustProblem('unsigned', { isDefault: true, keys: [key] })).toMatch(/not signed as it should be/);
    expect(trustProblem('unsigned', { isDefault: false, keys: [key] })).toBeNull();
    expect(trustProblem('bad', { isDefault: false, keys: [] })).toMatch(/does not hold/);
    expect(trustProblem('signed', { isDefault: true, keys: [key] })).toBeNull();
  });
});

describe('a package’s signature', () => {
  const { key, signBytes } = storeKey();

  it('names one exact file: a changed SHA-256, size or version is refused before anything is downloaded', async () => {
    const m = furniturePack();
    const signed: Manifest = { ...m, package: { ...m.package, signature: signBytes(enc(packageMessage(m))) } };
    expect(validateManifest(signed)).toEqual([]);
    expect(await packageProblem(signed, { keys: [key], required: true })).toBeNull();
    for (const p of [{ sha256: 'b'.repeat(64) }, { size: 1201 }]) {
      expect(await packageProblem({ ...signed, package: { ...signed.package, ...p } }, { keys: [key], required: false })).toMatch(/does not hold/);
    }
    expect(await packageProblem({ ...signed, version: '1.0.1' }, { keys: [key], required: false })).toMatch(/does not hold/);
    expect(await packageProblem(m, { keys: [key], required: true })).toMatch(/not signed by the catalogue/);
    expect(await packageProblem(m, { keys: [key], required: false })).toBeNull();
    expect(await verifySignature([key], { keyId: key.id, value: 'A'.repeat(86) + '==' }, enc('x'))).toBe(false);
  });

  it('is checked by the download, which then tries the store’s counting address, the author’s file and the copy in turn', async () => {
    const bytes = enc('{"items":["Sofa"]}');
    const m0 = furniturePack({ package: { url: 'https://github.com/a/b/releases/download/v1/x.lapinlib', mirror: 'https://lapinbim.com/extensions/files/x/1.0.0/x.lapinlib', download: 'https://store.lapinbim.com/api/download/x/1.0.0', sha256: await sha256Hex(bytes), size: bytes.length } });
    const m: Manifest = { ...m0, package: { ...m0.package, signature: signBytes(enc(packageMessage(m0))) } };
    const fetched: string[] = [];
    const files: Record<string, Uint8Array> = { [m.package.mirror!]: bytes, [m.package.url]: enc('{"items":["Sofx"]}') };
    const fetchBytes = async (u: string) => { fetched.push(u); if (!files[u]) throw new Error(`404 ${u}`); return files[u]; };
    const desktop = { version: '0.1.900', browser: false, hasModule: () => true };
    // The store is down, the author's release was changed after review: the copy is the file.
    expect(await download(m, { fetchBytes, app: desktop, signature: { keys: [key], required: true } })).toEqual(bytes);
    expect(fetched).toEqual([m.package.download, m.package.url, m.package.mirror]);
    // Unsigned where a signature is wanted: nothing is downloaded.
    fetched.length = 0;
    await expect(download(m0, { fetchBytes, app: desktop, signature: { keys: [key], required: true } })).rejects.toThrow(/not signed/);
    expect(fetched).toEqual([]);
    // In the browser: the counting address, then the copy; never the author's release.
    fetched.length = 0;
    await download(m, { fetchBytes, app: { ...desktop, browser: true } });
    expect(fetched).toEqual([m.package.download, m.package.mirror]);
    // No address gives the right file: the wrong file is what is said.
    delete files[m.package.mirror!];
    await expect(download(m, { fetchBytes, app: desktop })).rejects.toThrow(/SHA-256 differs/);
  });
});
