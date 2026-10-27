# Build-urile Windows

Două drumuri, cu același rezultat: installer-ul NSIS (`Lapin_<versiune>_x64-setup.exe`, pentru utilizatorul curent, fără drepturi de administrator, aduce WebView2 dacă lipsește) și un zip portabil (`Lapin_<versiune>_x64-portable.zip`: `Lapin/Lapin.exe`, `lapin-host.exe`, `resources/`). Amândouă nesemnate (Authenticode); pagina de descărcări explică SmartScreen. Updater-ul le semnează separat, la publicare, pe Mac (`scripts/publish-update.mjs`).

## De pe Mac (compilare încrucișată)

```sh
pnpm build:product lapin --target windows
```

Rezultatul e în `build/lapin/bundle/nsis/`. Urcarea pe site, ca până acum:

```sh
W=build/lapin/bundle/nsis
~/Documents/GitHub/ciuntucbimstudio-site/deploy/upload-downloads.sh lapin \
  $W/Lapin_*_x64-setup.exe=Lapin-Windows-x64-setup.exe $W/Lapin_*_x64-portable.zip=Lapin-Windows-x64-portable.zip
```

Ce face:
- motorul: `dotnet publish -r win-x64 --self-contained`, un singur `lapin-host.exe`, cu `engine/native/win-x64/manifoldc.dll` în el (`Lapin.Booleans` alege biblioteca nativă după ținta build-ului, nu după sistemul pe care rulează);
- aplicația Tauri: compilată pentru `x86_64-pc-windows-msvc` cu `cargo-xwin` (descarcă headerele și bibliotecile MSVC de la Microsoft, sub licența lor; scriptul o acceptă cu `XWIN_ACCEPT_LICENSE=1`), legată cu `lld-link`;
- installer-ul: NSIS (`makensis`), cu plugin-urile Tauri descărcate de Tauri;
- zip-ul portabil: făcut de script din aplicație, motor și resurse.

Uneltele, o singură dată:

```sh
brew install llvm lld nsis
rustup target add x86_64-pc-windows-msvc
cargo install --locked cargo-xwin
```

`clang-cl`, `llvm-rc` și `llvm-lib` vin cu `llvm` (scriptul îl găsește în `/opt/homebrew/opt/llvm/bin` dacă e pe PATH). Tauri numește acest drum experimental: build-ul se pornește pe un Windows înainte de publicare.

## Pe Windows

Din „x64 Native Tools Command Prompt for VS”: `node scripts/build-manifold.mjs` (doar când se schimbă manifold), apoi `pnpm build:product lapin`. Zip-ul portabil îl face scriptul și acolo.

## manifoldc.dll

`engine/native/win-x64/manifoldc.dll` (manifold v3.2.1, Apache-2.0, cu Clipper2 și runtime-ul C legate static; depinde doar de KERNEL32) e scos din build-ul Windows de pe 27.10.2026 (`lapin-host.exe`, pachetul .NET single-file) și ținut în repo, ca biblioteca macOS. Se reface pe Windows cu `node scripts/build-manifold.mjs`.
