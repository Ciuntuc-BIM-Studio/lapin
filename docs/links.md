# Legături (Manage → Links)

Un fișier legat (IFC sau DXF) intră în model ca orice import, dar aparține legăturii: fiecare entitate adusă poartă `linkId`, se desenează în vederi și în 3D, dar nu se editează și nu se șterge singură. Ca în Revit: contextul vine din fișierul altcuiva, se vede, nu se atinge.

## Cum se leagă

- IFC: File → Link IFC… (`project.importIfc` cu `mode: "link"`). Fișierul e importat ca la „auto” (elementele străine sunt recunoscute ca native unde se poate, restul referințe), apoi tot ce a adus e marcat al legăturii, într-un singur pas de undo. Pe desktop legătura ține minte calea; din browser doar octeții citiți o dată (nu se poate reciti).
- DXF: Import DXF… cu bifa „As a link” (`drafting.importDxf` cu `link: true`). Legătura ține minte vederea și opțiunile importului, ca să poată fi recitită identic.

## Ce se poate face cu o legătură

`LapinLink` (nume, `kind`, `file`, `path`, `visible`, `halftone`, `viewId`, `options`). Dialogul Manage → Links, pe fiecare rând:

- Shown: ascunsă, elementele ei ies din toate vederile 2D și din 3D (filtrul `IViewElementFilter` al platformei; desenele CAD în `Drafts.Marks`; 3D prin `LinksLook`).
- Halftone: liniile și umplerile ei se desenează gri (`#9ca3af`), în 3D fețele la 35% opacitate.
- Reload (desktop, cu cale): `project.reloadLink` scoate ce ținea și citește fișierul din nou; setările legăturii rămân.
- Convert to native: `link.convert` scoate `linkId` de pe elemente și șterge legătura; elementele rămân, editabile.
- Open separately (desktop, cu cale): comanda Tauri `open_separately` pornește altă instanță a aplicației cu `LAPIN_OPEN=<cale>`; instanța nouă face un proiect nou și importă fișierul nativ (DXF: deschide dialogul de import), de editat acolo și salvat ca proiect al lui.
- Unlink: `link.unlink` șterge legătura și tot ce a adus (etajele făcute de ea doar dacă nu mai stă nimic pe ele). Undo aduce totul înapoi.

## Blocarea editării

`CommandContext.EnsureEditable` refuză `Add`, `Replace` și `Remove` pe orice entitate cu `linkId`, cu mesaj („belongs to the link … unlink it, convert it to native, or open it on its own”), în afara comenzilor `link.*` și a importurilor care fac legături. Comenzile care ar atinge indirect un element legat (ștergere, mutare pe etaj, schimbarea tipului) cad pe același mesaj.

## Reguli de afișare în 3D

- Fog (View → Fog): scena se estompează cu distanța (`THREE.FogExp2`, culoarea fundalului temei), partajat între ferestrele aplicației.
- Halftone pe legătură: fețele la 35% opacitate, prin mecanismul `looks` (sursa `links`).
- 2D-ul din IFC (amprente, simboluri, umpleri, texte; docs/spec-ifc.md, Reprezentările 2D) vine și cu o legătură: planurile îl desenează în locul tăieturii, în halftone când legătura e în halftone.

## Rămase

- Poziționarea unei legături (translație/rotație a unui IFC legat) și legături imbricate.
- Recitirea automată când fișierul se schimbă pe disc.
- Mapări de etaje între fișier și proiect (acum etajele noi vin ca ale legăturii, cele care se potrivesc rămân ale proiectului).
