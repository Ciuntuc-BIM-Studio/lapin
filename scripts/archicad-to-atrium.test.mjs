// The Archicad → Atrium script's pure parts: node --test scripts/archicad-to-atrium.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { candidatePorts, parseVersion, versionAtLeast, parseArgs, normaliseServer, fileNameFor, mimeFor, chunkRanges, probePort, Fail } from './archicad-to-atrium.mjs';

test('ports 19723 to 19744', () => {
  const p = candidatePorts();
  assert.equal(p[0], 19723);
  assert.equal(p.at(-1), 19744);
  assert.equal(p.length, 22);
});

test('Tapir versions', () => {
  assert.deepEqual(parseVersion('2.0.1'), [2, 0, 1]);
  assert.deepEqual(parseVersion('1.5'), [1, 5, 0]);
  assert.equal(parseVersion('x'), null);
  assert.ok(versionAtLeast([2, 0, 1], [2, 0, 0]));
  assert.ok(versionAtLeast([3, 0, 0], [2, 9, 9]));
  assert.ok(!versionAtLeast([1, 5, 8], [2, 0, 0]));
});

test('arguments', () => {
  const o = parseArgs(['--port', '19724', '--project=88bdb6e6-3ff0-4187-accb-221a5b667977', '--ifczip', '--dry-run', '--translator', 'T', '--elements', 'EntireProject', '--server', 'cde.example.org/']);
  assert.equal(o.port, 19724);
  assert.equal(o.ifczip, true);
  assert.equal(o.server, 'https://cde.example.org');
  assert.equal(o.type, 'model');
  assert.throws(() => parseArgs(['--port', '80']), (e) => e instanceof Fail && e.exitCode === 2);
  assert.throws(() => parseArgs(['--elements', 'EntireProject']), Fail);
  assert.throws(() => parseArgs(['--nope']), Fail);
  assert.throws(() => parseArgs(['--project', 'abc']), Fail);
  assert.throws(() => parseArgs(['--title']), Fail);
});

test('server addresses: https, or http on this computer only', () => {
  assert.equal(normaliseServer('http://127.0.0.1:5280'), 'http://127.0.0.1:5280');
  assert.equal(normaliseServer('https://cde.lapinbim.com/'), 'https://cde.lapinbim.com');
  assert.throws(() => normaliseServer('http://cde.example.org'), Fail);
});

test('file names', () => {
  assert.equal(fileNameFor('Casa Ionescu.pln', 'ifc'), 'Casa Ionescu.ifc');
  assert.equal(fileNameFor('a/b:c', 'ifczip'), 'a_b_c.ifczip');
  assert.equal(fileNameFor(null, 'ifc'), 'archicad-model.ifc');
  assert.equal(mimeFor('x.ifczip'), 'application/zip');
  assert.equal(mimeFor('x.ifc'), 'application/x-step');
});

test('upload chunks cover the file from the server\'s offset', () => {
  assert.deepEqual([...chunkRanges(10, 0, 4)], [[0, 4], [4, 8], [8, 10]]);
  assert.deepEqual([...chunkRanges(10, 8, 4)], [[8, 10]]);
  assert.deepEqual([...chunkRanges(10, 10, 4)], []);
});

test('a port that does not answer is not Archicad', async () => {
  assert.equal(await probePort(19999, async () => { throw new Error('refused'); }), false);
  assert.equal(await probePort(19723, async () => ({ succeeded: true, result: { isAlive: true } })), true);
  assert.equal(await probePort(19723, async () => ({ succeeded: false })), false);
});
