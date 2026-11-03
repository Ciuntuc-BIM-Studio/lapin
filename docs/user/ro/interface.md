---
title: Interfața și scurtăturile
order: 15
summary: Modurile Confortabil, Compact și Concentrare, plierea panourilor laterale și scurtăturile de tastatură pe care le poți schimba.
---

<!-- video interface -->

## Spații de lucru

Un spațiu de lucru arată doar taburile panglicii de care ai nevoie, în ordinea în care lucrezi cu ele. Îl alegi pe ecranul de start, sus în **View → Interface…** sau din **Commands…** (Workspace: …): **Toate** (toate taburile ediției tale), **Arhitectură**, **Structură și armare**, **Instalații**, **Teren și organizare de șantier** sau **Desen 2D**. Proiectul rămâne unul singur: uneltele unui tab ascuns sunt tot în Commands… și în scurtăturile de la tastatură, iar un model făcut într-un spațiu de lucru se deschide întreg în altul.

![Spațiile de lucru în View → Interface…](shots/workspaces.webp)

Arată, ascunde sau mută un tab la **Tabs**, în același dialog, și alegerea devine **Spațiul meu de lucru**, păstrat pe acest calculator pentru data viitoare; celelalte spații de lucru rămân și poți trece oricând la ele. **View** rămâne mereu, pentru că Interface… e în el.

## Trei moduri

**Vedere → Interfață…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) alege cât de multe stau în jurul vederii. Alege unul dintre cele trei moduri din capul dialogului; schimbarea se vede imediat.

![Dialogul Interfață: cele trei moduri și fiecare parte în parte](shots/interface-dialog.webp)

- **Confortabil** e fereastra așa cum o arată celelalte pagini: ribbonul cu unelte mari și numele lor, o linie de comandă sub fiecare desen, iar HUD-ul, indicația, câmpurile de lungime și unghi, planul-cheie și uneltele rapide peste vedere.
- **Compact** dă vederii cea mai mare parte din fereastră:

![Compact: mai mult loc pentru desen](shots/interface-compact.webp)

1. **Ribbonul** e o singură bandă de pictograme. Linii subțiri despart grupurile. Ține mouse-ul pe o unealtă ca să-i vezi numele, tastele și grupul.
2. **Browserul proiectului** se pliază pe o bară. Pictogramele ei deschid pentru o clipă, peste vedere, proiectul, nivelurile sau arborele IFC.
3. **Uneltele rapide** <kbd>1</kbd>–<kbd>9</kbd> stau pe marginea din stânga a vederii.
4. **Planul-cheie** e un buton. Apasă-l ca să deschizi harta, iar × o pliază la loc.
5. **O singură linie de stare** ține numele uneltei și ce cere, ce tastezi, câmpurile de lungime și unghi cât desenezi, SNAP, ORTHO și POLAR, nivelul și vederea, apoi proiectul.

- **Concentrare** păstrează doar filele și o linie de comandă subțire. Apasă o filă și uneltele ei coboară peste vedere; banda se pliază la loc după ce alegi o unealtă, cu <kbd>Esc</kbd> sau cu un clic în altă parte. Panourile se ascund. Când e ceva selectat, o fișă mică arată ce e și primele lui proprietăți; **Toate proprietățile…** deschide panoul de proprietăți peste vedere. <kbd>Tab</kbd> aduce înapoi ambele panouri și le ascunde din nou.

![Concentrare: selecția într-o fișă](shots/interface-focus.webp)

În orice mod, bara de sus păstrează **Anulează** și **Refă** la capătul din dreapta: cu numele lor în Confortabil, ca două săgeți în Compact și Concentrare (<kbd>Ctrl</kbd> <kbd>Z</kbd> și <kbd>Ctrl</kbd> <kbd>Y</kbd>; indicația lor spune ce anulează).

Sub moduri, fiecare parte se poate alege separat: ribbonul, uneltele rapide (sau deloc: tastele cu cifre merg în continuare), linia de comandă, HUD-ul, mesajul și câmpurile uneltei, planul-cheie, panourile și înălțimea rândurilor de proprietăți. Când schimbi o parte, alegerea devine **Personalizat**.

Sub părți, **Taburi**: debifează taburile din ribbon de care nu ai nevoie (un arhitect poate ascunde Structure, Rebar 2D și MEP, un inginer Architecture și Site). View rămâne mereu, pentru că acolo e Interfață…. Uneltele unui tab ascuns rămân în Comenzi… și în scurtăturile de la tastatură.

Lapin îți păstrează alegerea pe acest calculator, pentru tine. Aplicația desktop și browserul o țin separat. În browser, Lapin pornește în Compact când fereastra are sub 800 de pixeli înălțime, până alegi tu.

## Uneltele rapide

Uneltele rapide sunt locurile numerotate de jos din vedere (pe marginea ei din stânga în Compact și Concentrare); tastele <kbd>1</kbd>–<kbd>9</kbd> le pornesc. Fiecare spațiu de lucru are uneltele lui: pornesc așa cum le dă ediția ta, iar tu le alegi singur:

- trage o unealtă din panglică pe un loc. O unealtă stă într-un singur loc, așa că îl lasă pe cel în care era;
- trage un loc peste altul ca să le schimbi între ele;
- clic dreapta pe un loc: **Alege o unealtă…**, **Golește acest loc** sau **Resetează uneltele rapide**;
- **View → Quick tools…** stabilește cele nouă locuri din liste, iar **Înapoi la cele ale ediției** le aduce înapoi pe cele ale ediției tale.

Plutind jos, fiecare loc arată numele uneltei, iar un loc gol după ultimul așteaptă o unealtă. Lapin îți păstrează uneltele rapide pe acest calculator, pentru tine.

![Uneltele rapide, cu scara pe locul 7 și locul 2 gol](shots/quick-tools.webp)

![View → Quick tools…](shots/quick-tools-dialog.webp)

## Plierea panourilor laterale

În orice mod, browserul proiectului și panoul de proprietăți se pliază pe o bară subțire, pe partea lor:

- butonul din dreapta sus al panoului îl pliază, iar săgeata de pe bară îl depliază;
- dublu-clic pe marginea dintre panou și vedere;
- <kbd>Ctrl</kbd> <kbd>B</kbd> pliază sau depliază browserul, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> proprietățile, iar <kbd>Tab</kbd> pe amândouă.

Apasă o pictogramă de pe bara unui panou pliat ca să-l deschizi peste vedere fără să-l depliezi. Dispare cu <kbd>Esc</kbd> sau cu un clic în altă parte; pioneza îl ține deschis. Trage de marginea unui panou ca să-l lărgești; Lapin ține minte lățimea.

![Un browser pliat, deschis peste vedere de pe bara lui](shots/browser-overlay.webp)

## Scurtăturile de tastatură

**Vedere → Scurtături de tastatură…** (<kbd>KS</kbd>) arată fiecare comandă la care ajunge tastatura, pe file. Fiecare rând arată scurtăturile comenzii.

![Scurtăturile de tastatură, căutate după „wall”](shots/shortcuts-dialog.webp)

- **Caută** după numele comenzii sau după o tastă.
- **+** adaugă o scurtătură: apasă o combinație (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>) sau tastează până la trei litere și apasă <kbd>Enter</kbd>. <kbd>Esc</kbd> renunță.
- Dacă scurtătura e deja a altei comenzi, Lapin îți spune a cui. **Mut-o** o ia de la acea comandă, **Păstrează-le pe amândouă** o lasă amândurora (câștigă prima din listă), **Renunță** nu schimbă nimic.
- **×** scoate o scurtătură. Săgeata de la capătul rândului readuce comanda cum o are setul, iar **Resetează tot** face asta pentru toate comenzile.
- **Set** alege de la ce pornești: **Stil Revit** (implicit: literele obișnuite din Revit, unde Lapin are comanda) sau **Lapin clasic** (literele de dinainte ale lui Lapin).
- **Exportă…** îți scrie scurtăturile într-un fișier, iar **Importă…** citește unul, pentru alt calculator sau pentru un coleg.

Indicațiile, meniurile și paleta de comenzi (<kbd>Ctrl</kbd> <kbd>K</kbd>) arată mereu scurtăturile așa cum sunt acum.

### Literele și linia de comandă

Literele ca <kbd>WA</kbd> merg când niciun câmp nu e activ și linia de comandă e goală: tastează-le peste vedere. Comanda pornește imediat. Când o comandă din linia de comandă începe cu aceleași litere (<kbd>DI</kbd> și <kbd>DIMADD</kbd>), Lapin așteaptă o clipă: tastează mai departe ca să scrii comanda lungă, sau apasă <kbd>Enter</kbd> ori <kbd>Space</kbd> ca s-o pornești acum pe cea scurtă. Cât o comandă cere un punct sau o valoare, literele și cifrele merg la ea. Comenzile din linia de comandă ca <kbd>L</kbd>, <kbd>M</kbd> sau <kbd>TRIM</kbd> urmate de <kbd>Enter</kbd> merg în orice set. <kbd>Esc</kbd> anulează mereu.

### Scurtăturile implicite

Acestea sunt scurtăturile în stil Revit, așa cum le are Lapin acum:

<!-- shortcuts -->

Tastele cu cifre <kbd>1</kbd>–<kbd>9</kbd> aleg uneltele rapide.
