#!/usr/bin/env node
// Builds manifold's C API (github.com/elalish/manifold, Apache-2.0) into one
// self-contained native library for this machine: engine/native/<rid>/.
// Everything is linked statically (manifold, Clipper2, no TBB), so the library
// depends on the system only and travels next to the engine.
//
//   node scripts/build-manifold.mjs        (needs git, cmake and a C++ compiler)
// macOS: libmanifoldc.dylib (clang++). Linux: libmanifoldc.so (g++), the C++
// runtime linked in statically too, so only glibc is needed on the target.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir, platform, arch } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const TAG = 'v3.2.1';
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const rid = `${platform() === 'darwin' ? 'osx' : platform() === 'win32' ? 'win' : 'linux'}-${arch() === 'arm64' ? 'arm64' : 'x64'}`;
const mac = platform() === 'darwin';
const lib = mac ? 'libmanifoldc.dylib' : 'libmanifoldc.so';
const work = join(tmpdir(), `lapin-manifold-${TAG}`);
// Windows needs the same static link with its own linker: not wired yet.
if (platform() === 'win32') throw new Error(`build-manifold: macOS and Linux are wired so far (this is ${rid})`);
const sh = (cmd, args, cwd) => execFileSync(cmd, args, { cwd, stdio: 'inherit' });

if (!existsSync(join(work, 'src'))) {
  mkdirSync(work, { recursive: true });
  sh('git', ['clone', '--depth', '1', '--branch', TAG, 'https://github.com/elalish/manifold.git', 'src'], work);
}
rmSync(join(work, 'build'), { recursive: true, force: true });
sh('cmake', ['-S', 'src', '-B', 'build', '-DCMAKE_BUILD_TYPE=Release', '-DBUILD_SHARED_LIBS=OFF', '-DMANIFOLD_TEST=OFF',
  '-DMANIFOLD_PAR=OFF', '-DMANIFOLD_CBIND=ON', '-DMANIFOLD_CROSS_SECTION=ON', '-DMANIFOLD_USE_BUILTIN_CLIPPER2=ON',
  '-DMANIFOLD_PYBIND=OFF', '-DCMAKE_POSITION_INDEPENDENT_CODE=ON', ...(mac ? ['-DCMAKE_OSX_DEPLOYMENT_TARGET=12.0'] : [])], work);
sh('cmake', ['--build', 'build', '--target', 'manifoldc', '-j', '8', '--config', 'Release'], work);

const out = join(root, 'engine', 'native', rid);
mkdirSync(out, { recursive: true });
const a = (p) => join(work, 'build', p);
if (mac)
  sh('clang++', ['-shared', '-o', join(out, lib), '-mmacosx-version-min=12.0', `-Wl,-force_load,${a('bindings/c/libmanifoldc.a')}`,
    a('src/libmanifold.a'), a('_deps/clipper2-build/libClipper2.a'), '-install_name', `@rpath/${lib}`]);
else
  sh('g++', ['-shared', '-o', join(out, lib), '-Wl,--whole-archive', a('bindings/c/libmanifoldc.a'), '-Wl,--no-whole-archive',
    a('src/libmanifold.a'), a('_deps/clipper2-build/libClipper2.a'), '-static-libstdc++', '-static-libgcc', `-Wl,-soname,${lib}`]);
console.log(`manifold ${TAG} → engine/native/${rid}/${lib}`);
