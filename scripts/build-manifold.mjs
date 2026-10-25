#!/usr/bin/env node
// Builds manifold's C API (github.com/elalish/manifold, Apache-2.0) into one
// self-contained native library for this machine: engine/native/<rid>/.
// Everything is linked statically (manifold, Clipper2, no TBB), so the library
// depends on the system only and travels next to the engine.
//
//   node scripts/build-manifold.mjs        (needs git, cmake and a C++ compiler)
//   node scripts/build-manifold.mjs --browser-wasm
// The engine in the browser (docs/plan-browser-engine.md): static libraries in
// engine/native/browser-wasm/, linked into the engine's WebAssembly, built by
// the Emscripten of the .NET wasm-tools workload (DOTNET_ROOT, else
// ~/.dotnet-wasm) with the runtime's exception model and SIMD. The C API's
// library is named manifoldc.a: the P/Invoke table names a static library by
// its file name, and Lapin.Booleans imports "manifoldc".
// macOS: libmanifoldc.dylib (clang++). Linux: libmanifoldc.so (g++), the C++
// runtime linked in statically too, so only glibc is needed on the target.
// Windows: manifoldc.dll (MSVC, run from a Visual Studio developer prompt, or
// after ilammy/msvc-dev-cmd in CI), the C runtime linked in statically (/MT),
// exporting the functions Lapin.Booleans imports (read from ManifoldBooleans.cs).
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir, platform, arch } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const TAG = 'v3.2.1';
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const rid = `${platform() === 'darwin' ? 'osx' : platform() === 'win32' ? 'win' : 'linux'}-${arch() === 'arm64' ? 'arm64' : 'x64'}`;
const mac = platform() === 'darwin';
const win = platform() === 'win32';
const lib = mac ? 'libmanifoldc.dylib' : win ? 'manifoldc.dll' : 'libmanifoldc.so';
const work = join(tmpdir(), `lapin-manifold-${TAG}`);
const sh = (cmd, args, cwd) => execFileSync(cmd, args, { cwd, stdio: 'inherit' });

if (process.argv.includes('--browser-wasm')) {
  const home = process.env.HOME ?? process.env.USERPROFILE ?? '';
  const dotnet = [process.env.DOTNET_ROOT, join(home, '.dotnet-wasm')].find((d) => d && existsSync(join(d, 'packs')) && readdirSync(join(d, 'packs')).some((p) => p.startsWith('Microsoft.NET.Runtime.Emscripten.')));
  if (!dotnet) throw new Error('build-manifold: no .NET with the wasm-tools workload (set DOTNET_ROOT, or install it in ~/.dotnet-wasm: see engine/src/Host/Lapin.Browser/README.md).');
  const packs = join(dotnet, 'packs');
  const pack = (kind, optional = false) => { const d = readdirSync(packs).find((p) => p.startsWith(`Microsoft.NET.Runtime.Emscripten.`) && p.includes(`.${kind}.`)); if (!d) { if (optional) return null; throw new Error(`build-manifold: no Emscripten ${kind} pack in ${packs}`); } const v = readdirSync(join(packs, d))[0]; return join(packs, d, v, 'tools'); };
  // Linux has no Python pack: the workload uses the system's python3 there.
  const python = pack('Python', true);
  const sdk = pack('Sdk'), node = join(pack('Node'), 'bin', 'node'), py = python ? join(python, 'bin') : null, cached = pack('Cache', true);
  const cache = cached ? join(cached, 'emscripten', 'cache') : join(tmpdir(), 'lapin-emscripten-cache');
  const env = { ...process.env, EMSDK_PATH: sdk, DOTNET_EMSCRIPTEN_LLVM_ROOT: join(sdk, 'bin'), DOTNET_EMSCRIPTEN_BINARYEN_ROOT: sdk, DOTNET_EMSCRIPTEN_NODE_JS: node, EM_CACHE: cache,
    PATH: [join(sdk, 'emscripten'), join(sdk, 'bin'), py, process.env.PATH].filter(Boolean).join(process.platform === 'win32' ? ';' : ':') };
  const wasmWork = `${work}-wasm`;
  const run = (cmd, args, cwd) => execFileSync(cmd, args, { cwd, stdio: 'inherit', env });
  if (!existsSync(join(wasmWork, 'src'))) {
    mkdirSync(wasmWork, { recursive: true });
    run('git', ['clone', '--depth', '1', '--branch', TAG, 'https://github.com/elalish/manifold.git', 'src'], wasmWork);
  }
  rmSync(join(wasmWork, 'build'), { recursive: true, force: true });
  const flags = '-fwasm-exceptions -msimd128 -O3';
  run(join(sdk, 'emscripten', process.platform === 'win32' ? 'emcmake.bat' : 'emcmake'), ['cmake', '-S', 'src', '-B', 'build', '-DCMAKE_BUILD_TYPE=Release', '-DBUILD_SHARED_LIBS=OFF', '-DMANIFOLD_TEST=OFF',
    '-DMANIFOLD_PAR=OFF', '-DMANIFOLD_CBIND=ON', '-DMANIFOLD_CROSS_SECTION=ON', '-DMANIFOLD_USE_BUILTIN_CLIPPER2=ON', '-DMANIFOLD_PYBIND=OFF', '-DMANIFOLD_JSBIND=OFF',
    `-DCMAKE_C_FLAGS=${flags}`, `-DCMAKE_CXX_FLAGS=${flags}`], wasmWork);
  run('cmake', ['--build', 'build', '--target', 'manifoldc', '-j', '8'], wasmWork);
  const wasmOut = join(root, 'engine', 'native', 'browser-wasm');
  mkdirSync(wasmOut, { recursive: true });
  const b = (p) => join(wasmWork, 'build', p);
  copyFileSync(b('bindings/c/libmanifoldc.a'), join(wasmOut, 'manifoldc.a'));
  copyFileSync(b('src/libmanifold.a'), join(wasmOut, 'libmanifold.a'));
  copyFileSync(b('_deps/clipper2-build/libClipper2.a'), join(wasmOut, 'libClipper2.a'));
  console.log(`manifold ${TAG} → engine/native/browser-wasm/{manifoldc,libmanifold,libClipper2}.a`);
  process.exit(0);
}

if (!existsSync(join(work, 'src'))) {
  mkdirSync(work, { recursive: true });
  sh('git', ['clone', '--depth', '1', '--branch', TAG, 'https://github.com/elalish/manifold.git', 'src'], work);
}
rmSync(join(work, 'build'), { recursive: true, force: true });
sh('cmake', ['-S', 'src', '-B', 'build', '-DCMAKE_BUILD_TYPE=Release', '-DBUILD_SHARED_LIBS=OFF', '-DMANIFOLD_TEST=OFF',
  '-DMANIFOLD_PAR=OFF', '-DMANIFOLD_CBIND=ON', '-DMANIFOLD_CROSS_SECTION=ON', '-DMANIFOLD_USE_BUILTIN_CLIPPER2=ON',
  '-DMANIFOLD_PYBIND=OFF', '-DCMAKE_POSITION_INDEPENDENT_CODE=ON', ...(mac ? ['-DCMAKE_OSX_DEPLOYMENT_TARGET=12.0'] : []),
  ...(win ? ['-A', 'x64', '-DCMAKE_POLICY_DEFAULT_CMP0091=NEW', '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded'] : [])], work);
sh('cmake', ['--build', 'build', '--target', 'manifoldc', '-j', '8', '--config', 'Release'], work);

const out = join(root, 'engine', 'native', rid);
mkdirSync(out, { recursive: true });
const a = (p) => join(work, 'build', p);
if (win) {
  // The static libraries wherever the Visual Studio generator put them (build/<dir>/Release/<name>.lib).
  const find = (name, dir = join(work, 'build')) => {
    for (const f of readdirSync(dir)) {
      const p = join(dir, f);
      if (statSync(p).isDirectory()) { const r = find(name, p); if (r) return r; } else if (f.toLowerCase() === name.toLowerCase() && /[\\/]Release[\\/]/.test(p)) return p;
    }
    return null;
  };
  const libs = ['manifoldc.lib', 'manifold.lib', 'Clipper2.lib'].map((n) => find(n) ?? (() => { throw new Error(`build-manifold: ${n} was not built`); })());
  // The exports: every function Lapin.Booleans imports.
  const cs = readFileSync(join(root, 'engine/src/Geometry/Lapin.Booleans/ManifoldBooleans.cs'), 'utf8');
  const names = [...cs.matchAll(/static extern[^(]*?\b(manifold_\w+)\s*\(/g)].map((m) => m[1]);
  if (names.length === 0) throw new Error('build-manifold: no manifold_* imports found in ManifoldBooleans.cs');
  const def = join(work, 'build', 'manifoldc.def');
  writeFileSync(def, `LIBRARY manifoldc\nEXPORTS\n${names.map((n) => `  ${n}`).join('\n')}\n`);
  // MSVC's link.exe by its full path: Git for Windows puts a different `link` on the PATH.
  const vc = process.env.VCToolsInstallDir;
  const linker = vc ? join(vc, 'bin', 'Hostx64', 'x64', 'link.exe') : 'link.exe';
  if (vc && !existsSync(linker)) throw new Error(`build-manifold: no ${linker}`);
  if (!vc) console.warn('build-manifold: VCToolsInstallDir is not set; run this from a Visual Studio x64 developer prompt.');
  sh(linker, ['/nologo', '/DLL', '/MACHINE:X64', `/DEF:${def}`, `/OUT:${join(out, lib)}`, ...libs]);
} else if (mac)
  sh('clang++', ['-shared', '-o', join(out, lib), '-mmacosx-version-min=12.0', `-Wl,-force_load,${a('bindings/c/libmanifoldc.a')}`,
    a('src/libmanifold.a'), a('_deps/clipper2-build/libClipper2.a'), '-install_name', `@rpath/${lib}`]);
else
  sh('g++', ['-shared', '-o', join(out, lib), '-Wl,--whole-archive', a('bindings/c/libmanifoldc.a'), '-Wl,--no-whole-archive',
    a('src/libmanifold.a'), a('_deps/clipper2-build/libClipper2.a'), '-static-libstdc++', '-static-libgcc', `-Wl,-soname,${lib}`]);
console.log(`manifold ${TAG} → engine/native/${rid}/${lib}`);
