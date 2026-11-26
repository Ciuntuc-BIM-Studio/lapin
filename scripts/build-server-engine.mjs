// Lapin's engine for a server, without a window (docs/headless-run.md): one product's engine modules and the host that runs
// jobs (Lapin.Host --run), laid out as a CDE server mounts it:
//   build/<product>-server-engine/
//     engine/       Lapin.Host.dll, framework-dependent (the server's ASP.NET runtime runs it: dotnet engine/Lapin.Host.dll), with
//                   manifold's native library for the runtime id
//     modules/      only the product's engine modules (as a desktop build ships them)
//     product.json  the product, resolved (scripts/product.mjs)
//
//   node scripts/build-server-engine.mjs <product | path/to/product.json> [--rid linux-x64]
//
// The runtime id is this machine's by default. For linux-x64 the booleans need engine/native/linux-x64/libmanifoldc.so, which a
// Linux x64 machine builds by itself (Lapin.Booleans.csproj) and scripts/build-linux-docker.sh keeps in its cache; elsewhere build
// it first (node scripts/build-manifold.mjs on Linux). A product's external modules (a sibling repository's) are built against this
// checkout, as pnpm build:product builds them.
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveProduct } from './product.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const productId = process.argv[2];
if (!productId || productId.startsWith('--')) { console.error('Usage: node scripts/build-server-engine.mjs <product | product.json> [--rid <runtime id>]'); process.exit(1); }
const ridArg = (() => { const i = process.argv.indexOf('--rid'); return i > 0 ? process.argv[i + 1] : undefined; })();
const here = { 'darwin-arm64': 'osx-arm64', 'darwin-x64': 'osx-x64', 'linux-x64': 'linux-x64', 'linux-arm64': 'linux-arm64', 'win32-x64': 'win-x64' }[`${process.platform}-${process.arch}`];
const rid = ridArg ?? here;
if (!rid) { console.error(`No .NET runtime id for ${process.platform}-${process.arch}: give --rid.`); process.exit(1); }

let product;
try { product = resolveProduct(root, productId); } catch (e) { console.error(e.message); process.exit(1); }
const out = join(root, 'build', `${product.id}-server-engine`);
const sh = (cmd, args) => {
  console.log(`▸ ${cmd} ${args.join(' ')}`);
  const r = spawnSync(cmd, args, { cwd: root, stdio: 'inherit' });
  if (r.error) { console.error(`Cannot run ${cmd}: ${r.error.message}`); process.exit(1); }
  if (r.status !== 0) process.exit(r.status ?? 1);
};

const native = { 'linux-x64': 'libmanifoldc.so', 'linux-arm64': 'libmanifoldc.so', 'osx-arm64': 'libmanifoldc.dylib', 'osx-x64': 'libmanifoldc.dylib', 'win-x64': 'manifoldc.dll' }[rid];
if (native && !existsSync(join(root, 'engine/native', rid, native)) && !(rid === here && rid.startsWith('linux'))) {
  console.error(`engine/native/${rid}/${native} is missing: build it on that platform first (node scripts/build-manifold.mjs), or the engine cuts nothing.`);
  process.exit(1);
}

rmSync(out, { recursive: true, force: true });
mkdirSync(join(out, 'modules'), { recursive: true });
// 1. The modules (Release), a sibling repository's against this checkout; then the host, framework-dependent.
sh('dotnet', ['build', 'engine/Lapin.slnx', '-c', 'Release', '-v', 'q', '-nologo']);
for (const csproj of product.engineProjects ?? []) sh('dotnet', ['build', csproj, '-c', 'Release', '-v', 'q', '-nologo', `-p:LapinProDir=${root}/`]);
const commits = spawnSync('git', ['rev-list', '--count', 'HEAD'], { cwd: root, encoding: 'utf8' }).stdout?.trim();
const version = process.env.LAPIN_VERSION || (commits ? `0.1.${commits}` : '0.0.0');
sh('dotnet', ['publish', 'engine/src/Host/Lapin.Host', '-c', 'Release', '-r', rid, '--self-contained', 'false', `-p:LapinNativeRid=${rid}`,
  `-p:InformationalVersion=${version}`, '-p:DebugType=none', '-o', join(out, 'engine'), '-v', 'q', '-nologo']);

// 2. Only the product's engine modules, and the product.
writeFileSync(join(out, 'product.json'), JSON.stringify(product, null, 2));
for (const m of product.engine.filter((id) => id !== 'core')) {
  const src = join(root, 'engine/artifacts/modules', m);
  if (!existsSync(src)) { console.error(`Module ${m} was not built (${src}).`); process.exit(1); }
  mkdirSync(join(out, 'modules', m), { recursive: true });
  for (const f of readdirSync(src).filter((f) => !f.endsWith('.pdb') && f !== 'runtimes')) cpSync(join(src, f), join(out, 'modules', m, f), { recursive: true });
  const runtimes = join(src, 'runtimes', rid, 'native');
  if (existsSync(runtimes)) cpSync(runtimes, join(out, 'modules', m, 'runtimes', rid, 'native'), { recursive: true });
}
console.log(`\n✓ ${product.name}'s engine for a server (${rid}, ${product.engine.join(' + ')}): ${out}`);
console.log(`  Run a job: dotnet ${join(out, 'engine/Lapin.Host.dll')} --product ${join(out, 'product.json')} --modules-dir ${join(out, 'modules')} --run job.json --out answer.json`);
