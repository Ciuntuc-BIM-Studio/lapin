# Build-urile Linux și oglinda pe GitHub Releases

## Linux

Două pachete, din același build: `.deb` (Ubuntu, Debian, Mint…) și `.AppImage` (oricare distribuție cu glibc 2.35 sau mai nou), x64, nesemnate. Se fac:

- în GitHub Actions: Actions → Build Linux → Run workflow (sau un tag `v*`); pachetele sunt artefactele rulării;
- fără Actions: `scripts/build-linux-docker.sh [product] [ref]`, într-un container Ubuntu 22.04 x64 (OrbStack pe Mac), din checkout-ul principal, nu dintr-un worktree → `build/<product>-linux/`.

Amândouă rulează `pnpm build:product <product>`, care pe Linux dă lui Tauri (`scripts/build-product.mjs`, `linuxBundle`):

- **intrarea de meniu** (issue #23): un fișier `.desktop` făcut din `desktop/src-tauri/linux/app.desktop.hbs`, în `/usr/share/applications/<Nume>.desktop` în `.deb` și în rădăcina AppImage-ului (Tauri face AppDir-ul din datele `.deb`-ului, deci același șablon). Câmpurile: `Name` (numele ediției), `GenericName`, `Comment` (`product.description`, altfel „BIM modeller on the IFC schema”), `Exec=lapin-desktop %f`, `Icon=lapin-desktop` (numele după care temele de iconițe o găsesc; Tauri pune PNG-urile în `/usr/share/icons/hicolor/<mărime>/apps/`), `Categories=Graphics;Engineering;`, `Keywords`, `MimeType=application/x-lapin-project;`, `StartupWMClass=lapin-desktop` (clasa ferestrei GTK: Tauri nu setează un app id GTK, deci e numele binarului);
- **tipul MIME `.lapin`**: `desktop/src-tauri/linux/lapin-project.xml` (`application/x-lapin-project`, subclasă de `application/zip`, glob `*.lapin`) în `/usr/share/mime/packages/<product>.xml`; trigger-ul dpkg al `shared-mime-info` îl adaugă în baza MIME la instalare. În AppImage e pus în același loc, pentru uneltele care îl integrează (appimaged, Gear Lever); AppImage-ul rulat direct nu instalează nimic;
- **un fișier deschis din file manager**: `Exec=… %f` dă calea ca prim argument; `startup_file` (desktop/src-tauri/src/lib.rs) îl ia când `LAPIN_OPEN` lipsește, și interfața îl deschide ca pe un proiect pornit din Manage → Links.

Asocierea `.lapin` se face doar pe Linux: pe macOS și Windows ar înregistra tipul înainte ca aplicația să primească fișierul deschis așa (macOS îl trimite ca eveniment, nu ca argument).

Verificat fără un build Linux complet: șablonul randat ca Tauri (handlebars, fără escape) trece de `desktop-file-validate` 0.26 (Ubuntu 22.04) și 0.27 (Ubuntu 24.04); cu XML-ul în `/usr/share/mime/packages` și `update-mime-database`, `xdg-mime query filetype a.lapin` dă `application/x-lapin-project`, iar `xdg-mime query default application/x-lapin-project` dă `Lapin.desktop`. La primul build Linux de după: `dpkg -c Lapin_*.deb | grep -E 'applications|mime'`, `desktop-file-validate` pe fișierul din pachet, și meniul cu o temă de iconițe (Papirus) după instalare.

## GitHub Releases (oglinda)

Fiecare build publicat al ediției `lapin` e oglindit ca GitHub Release (decizia 663), cu numele fixe ale site-ului și `SHA256SUMS.txt`. Release-ul îl creează utilizatorul: lui Claude, în modul auto, `gh release create` îi e refuzat.

După `publish-update.mjs`, cu pachetele tuturor platformelor la îndemână (fiecare build gol `build/<product>/bundle`, deci pachetele se țin deoparte):

```sh
node scripts/github-release.mjs lapin build/lapin/bundle build/lapin-linux <folderul cu pachetele Windows> --target <commit-ul build-ului>
```

Scriptul nu rulează nimic: tipărește comenzile, care se citesc și se dau în terminal:

1. copiază pachetele în `build/github-release/<versiune>/` sub numele de pe site (`Lapin-macOS-AppleSilicon.dmg`, `Lapin-Windows-x64-setup.exe`, `Lapin-Windows-x64-portable.zip`, `Lapin-Linux-x64.AppImage`, `Lapin-Linux-x64.deb`);
2. scrie `SHA256SUMS.txt` (`shasum -a 256`);
3. scrie `NOTES.md` ca la 0.1.1355: linkurile lapinbim.com, ce fișier e pentru ce platformă, subiectele commit-urilor de la release-ul anterior (`v*`);
4. `gh release create v<versiune> --target <commit> --title "Lapin <versiune>" --notes-file …/NOTES.md <fișierele>`.

Scriptul se oprește dacă pachetele nu sunt de aceeași versiune sau dacă un tip apare de două ori, și avertizează (linii `# !`) când commit-ul nu e al versiunii (`0.1.<numărul de commit-uri>`; `BUILD.txt` din arhiva serverului îl numește), când tag-ul există deja sau când lipsește o platformă (adăugată apoi cu `gh release upload v<versiune> <fișier>`).
