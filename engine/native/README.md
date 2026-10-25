# Native libraries

`osx-arm64/libmanifoldc.dylib` — the C API of [manifold](https://github.com/elalish/manifold)
**v3.2.1** (Apache License 2.0), with Clipper2 (Boost Software License 1.0) linked in
statically; no other dependency than the system's. It computes the engine's booleans
(`Lapin.Booleans`).

Rebuild it from source with `node scripts/build-manifold.mjs` (needs git, cmake and a C++
compiler). macOS and Linux are wired (Linux builds `linux-x64/libmanifoldc.so`, not committed: CI builds it); Windows needs the same static link.
