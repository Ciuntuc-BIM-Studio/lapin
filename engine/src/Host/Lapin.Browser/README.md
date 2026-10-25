# Lapin.Browser — the engine in the browser

The desktop's engine, unchanged, run in a Web Worker (docs/plan-browser-engine.md). Built by `pnpm build:web <product>`, never by the desktop's build (it is not in `Lapin.slnx`).

It needs the .NET `wasm-tools` workload (for AOT and for linking manifold in). The SDK installed for the whole machine takes workloads only as root; a user-level SDK beside it works as well:

```sh
curl -sSL -o /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 10.0.300 --install-dir ~/.dotnet-wasm --no-path
~/.dotnet-wasm/dotnet workload install wasm-tools
```

`scripts/build-web.mjs` finds it in `DOTNET_ROOT`, else in `~/.dotnet-wasm`, then:

1. builds manifold for the browser if it is missing (`node scripts/build-manifold.mjs --browser-wasm` → `engine/native/browser-wasm/`);
2. writes `generated/` (the product's modules, from the same resolution the desktop uses);
3. publishes this project (AOT, trimmed);
4. builds the interface with `VITE_ENGINE=worker` and puts both in `build/<product>/web/`.
