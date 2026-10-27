# Native libraries

`osx-arm64/libmanifoldc.dylib` — the C API of [manifold](https://github.com/elalish/manifold)
**v3.2.1** (Apache License 2.0), with Clipper2 (Boost Software License 1.0) linked in
statically; no other dependency than the system's. It computes the engine's booleans
(`Lapin.Booleans`).

Rebuild it from source with `node scripts/build-manifold.mjs` (needs git, cmake and a C++
compiler). macOS, Linux and Windows are wired. Linux builds `linux-x64/libmanifoldc.so` (not committed: the Linux build makes it) and Windows `win-x64/manifoldc.dll` (from a Visual Studio x64 developer prompt). `win-x64/manifoldc.dll` is committed since 27.10.2026, so a Mac can build the Windows edition (`pnpm build:product lapin --target windows`, docs/release-windows.md); it came out of that day's Windows build, links the C runtime statically and needs only KERNEL32.
