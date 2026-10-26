# Comenzi utile (lapin-pro)

Toate se rulează din `~/Documents/GitHub/lapin-pro`, dacă nu scrie altfel.

## Zi de zi

```sh
git pull --ff-only             # aduce ce s-a pus pe main
pnpm setup                     # pachete + motorul (prima dată, sau după schimbări mari)
pnpm dev                       # motor + UI în browser: http://localhost:5180
pnpm dev:desktop               # același lucru în fereastra desktop (Tauri)
LAPIN_PRODUCT=lapin-mep pnpm dev   # altă ediție (lista: pnpm products)
```

Dacă `pnpm install` se plânge de @tauri-apps: `CI=true pnpm install`.

## Verificări

```sh
pnpm typecheck                 # tipurile din UI
pnpm test                      # testele UI (vitest) + motor (xUnit)
pnpm test:engine               # doar motorul
pnpm check:licenses            # licențele pachetelor npm și NuGet
pnpm products                  # ce module conține fiecare ediție
```

## Rapoartele din aplicație (Report an issue)

Stau pe serverul Hetzner. Prima dată copiază `deploy/feedback/.env.example` în `deploy/feedback/.env` și pune serverul (`DEPLOY_SERVER=user@host`).

```sh
deploy/feedback/reports.sh                 # cele deschise
deploy/feedback/reports.sh list all        # toate (sau: list closed)
deploy/feedback/reports.sh show 12         # un raport întreg; captura merge în feedback-screenshots/
deploy/feedback/reports.sh close 12        # îl închizi
deploy/feedback/reports.sh reopen 12       # îl redeschizi
```

## Publicarea pe web

Publică ultimul commit de pe GitHub la https://lapin.ciuntucbimstudio.ro/web/.

```sh
git pull --ff-only
deploy/web/deploy.sh lapin                 # construiește și urcă
SKIP_BUILD=1 deploy/web/deploy.sh lapin    # urcă build-ul existent, fără să mai construiască
```

Dacă spune „HEAD is not pushed”, ești pe un commit care nu e pe GitHub: fă `git pull --ff-only` sau împinge commit-ul. `ALLOW_DIRTY=1` doar în urgențe.

## Build-uri desktop

```sh
pnpm build:product lapin       # aplicația desktop → build/lapin/bundle
pnpm build:web lapin           # varianta de browser → build/lapin/web
```

- **macOS:** se face local, semnat și notarizat.
- **Linux (.deb, .AppImage):** pe GitHub, Actions → Build Linux → Run workflow (sau un tag `v*`). Pachetele sunt la artefactele rulării.
- **Windows:** pe un PC cu Windows, dintr-un „x64 Native Tools Command Prompt for VS”:

  ```sh
  node scripts/build-manifold.mjs
  pnpm build:product lapin
  ```

## Urcarea build-urilor pe site

Din `~/Documents/GitHub/ciuntucbimstudio-site`. Numele publicate rămân fixe, ca linkurile de pe site să nu se schimbe.

```sh
deploy/upload-downloads.sh lapin ~/Downloads/Lapin_0.1.0_x64-setup.exe=Lapin-Windows-x64-setup.exe
deploy/upload-downloads.sh lapin <fișier local>=<nume publicat> …
```

Linkurile ajung la https://ciuntucbimstudio.ro/downloads/lapin/.

## Git

```sh
git status                     # ce e schimbat
git log --oneline -10          # ultimele commituri
git pull --ff-only             # aduce main fără merge-uri
```

Eu (Claude) lucrez într-un worktree separat, `~/Documents/GitHub/lapin-pro-ifc`, și împing direct pe main. Ca să fii la zi în folderul tău e de ajuns `git pull --ff-only`.
