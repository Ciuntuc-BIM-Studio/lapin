// The catalogue scanner (scripts/command-catalogue.mjs): node --test scripts/command-catalogue.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { catalogueFromSource } from './command-catalogue.mjs';

const args = (c, name) => c.commands.find((x) => x.name === name)?.args.map((a) => a.name);

test('keys read by a helper that takes the object after other arguments, beside an out parameter, in a class after a nested type', () => {
  const dir = mkdtempSync(join(tmpdir(), 'cat-'));
  writeFileSync(join(dir, 'T.cs'), `
public static partial class T
{
    public sealed class Span
    {
        public int X;
    }

    static JsonObject Take(IModelView m, JsonObject a, double z, out JsonObject? source)
    {
        source = null;
        var level = Json.Number(a["level"]) ?? z;
        return new JsonObject();
    }

    static JsonObject Linked(JsonObject source, JsonObject a) => a["linked"] is JsonValue ? source : source;

    public static void Register(IModuleRegistry r)
    {
        r.Command("add", "Add", (ctx, a) =>
        {
            var attrs = Take(ctx.Model, a, 0, out var source);
            Linked(source!, a);
            return new JsonObject { ["name"] = (string?)a["name"] };
        });
    }
}
`);
  const c = catalogueFromSource(dir);
  assert.deepEqual(args(c, 'core.add')?.sort(), ['level', 'linked', 'name']);
});

test('the tunnel lining commands list axis, level and linked', () => {
  const c = catalogueFromSource();
  for (const n of ['civil.addTunnelLining', 'civil.updateTunnelLining']) {
    for (const k of ['axis', 'level', 'linked']) assert.ok(args(c, n)?.includes(k), `${n} lacks ${k}`);
  }
});
