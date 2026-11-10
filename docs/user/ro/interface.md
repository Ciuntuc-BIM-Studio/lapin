---
title: Interfața și scurtăturile
order: 15
summary: Modurile Confortabil, Compact și Concentrare, plierea panourilor laterale și scurtăturile de tastatură pe care le poți schimba.
---

<!-- video interface -->

## Spații de lucru

Un spațiu de lucru arată doar taburile panglicii de care ai nevoie, în ordinea în care lucrezi cu ele. Îl alegi din butonul de lângă **Fișier**, care arată spațiul de lucru în care ești, ori pe ecranul de start, sus în **View → Interface…** sau din **Commands…** (Workspace: …): **Toate** (toate taburile ediției tale), **Arhitectură**, **Structură și armare**, **Instalații**, **Teren și organizare de șantier** sau **Desen 2D**. Proiectul rămâne unul singur: uneltele unui tab ascuns sunt tot în Commands… și în scurtăturile de la tastatură, iar un model făcut într-un spațiu de lucru se deschide întreg în altul.

![Spațiile de lucru în View → Interface…](shots/workspaces.webp)

Când uneltele panglicii sunt mai late decât fereastra, rotița mouse-ului deasupra lor le derulează în lateral.

Taburile stau pe un singur rând. Când fereastra e prea îngustă pentru toate, ultimele trec sub **Mai multe**, la capătul rândului, iar tabul pe care ești rămâne mereu la vedere.

Taburile sunt grupate după lucrul pe care îl servesc. **Interior** cuprinde finisajele și mobilierul. **Teren** cuprinde terenul, amenajarea peisagistică și organizarea de șantier (schele și cofraje). **Execuție** cuprinde programul lucrărilor și devizul (4D și 5D).

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

Sub părți, **Taburi**: debifează taburile din ribbon de care nu ai nevoie (un arhitect poate ascunde Structure, Armare simplă și MEP, un inginer Architecture și Site). View rămâne mereu, pentru că acolo e Interfață…. Uneltele unui tab ascuns rămân în Comenzi… și în scurtăturile de la tastatură.

Lapin îți păstrează alegerea pe acest calculator, pentru tine. Aplicația desktop și browserul o țin separat. În browser, Lapin pornește în Compact când fereastra are sub 800 de pixeli înălțime, până alegi tu.

## Uneltele rapide

Uneltele rapide sunt locurile numerotate de pe marginea din stânga a vederii, lipite de browserul proiectului, așa că se mută odată cu el; sunt mici și semi-transparente până când vine cursorul peste ele (View → Interface… le poate pune și plutind jos în vedere); tastele <kbd>1</kbd>–<kbd>9</kbd> le pornesc. Fiecare spațiu de lucru are uneltele lui: pornesc așa cum le dă ediția ta, iar tu le alegi singur:

- trage o unealtă din panglică pe un loc. O unealtă stă într-un singur loc, așa că îl lasă pe cel în care era;
- trage un loc peste altul ca să le schimbi între ele;
- clic dreapta pe un loc: **Alege o unealtă…**, **Golește acest loc** sau **Resetează uneltele rapide**;
- **View → Quick tools…** stabilește cele nouă locuri din liste, iar **Înapoi la cele ale ediției** le aduce înapoi pe cele ale ediției tale.
- o unealtă care pune elemente de un tip (perete, ușă, placă, scară…) poate ține și unul dintre tipurile proiectului: îl alegi din meniul de clic dreapta al locului sau lângă unealtă în **View → Quick tools…**. Locul arată atunci numele tipului, iar apăsat pornește unealta cu tipul acela, așa că un loc poate ține pereții de cărămidă și altul compartimentările. Tipul e păstrat după nume: locul merge în orice proiect cu un tip cu acel nume, iar într-unul fără el unealta ia tipul ei curent.

Plutind jos, fiecare loc arată numele uneltei, iar un loc gol după ultimul așteaptă o unealtă. Lapin îți păstrează uneltele rapide pe acest calculator, pentru tine.

Săgeata de la capătul barei ascunde uneltele rapide; în locul lor rămâne un mâner mic, **Unelte rapide**, care le readuce. Se ascund separat în vederile modelului (planuri, 3D, secțiuni) și în planșe și extrase, așa că le poți ține ascunse pe documente și vizibile pe desene.

![Uneltele rapide: un tip de perete pe locul 3, scara pe locul 7, locul 2 gol](shots/quick-tools.webp)

![View → Quick tools…](shots/quick-tools-dialog.webp)

## Catalogul

Fila **Catalog** din browserul proiectului arată tipurile proiectului pentru fiecare unealtă care pune elemente cu tip: pereți, uși, ferestre, plăci, pereți cortină, scări, rampe, tavane și plinte. Fiecare tip e desenat mic (straturile unui perete ca în secțiunea din plan, ale unei plăci în secțiune, vederea unei uși sau ferestre, profilul unei plinte), cu ce este în câteva cuvinte.

- un clic pe o carte pune elemente de acel tip: unealta pornește cu el;
- trage o carte pe un loc rapid ca să păstrezi acolo unealta cu acel tip;
- **Editează tipurile…** deschide dialogul în care se fac și se schimbă tipurile.

![Catalogul: tipurile de pereți](shots/catalog.webp)

![Tipurile de ferestre, desenate după vederea lor](shots/catalog-windows.webp)

## Plierea panourilor laterale

În orice mod, browserul proiectului și panoul de proprietăți se pliază pe o bară subțire, pe partea lor:

- butonul din dreapta sus al panoului îl pliază, iar săgeata de pe bară îl depliază;
- dublu-clic pe marginea dintre panou și vedere;
- <kbd>Ctrl</kbd> <kbd>B</kbd> pliază sau depliază browserul, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> proprietățile, iar <kbd>Tab</kbd> pe amândouă.

Apasă o pictogramă de pe bara unui panou pliat ca să-l deschizi peste vedere fără să-l depliezi. Dispare cu <kbd>Esc</kbd> sau cu un clic în altă parte; pioneza îl ține deschis. Trage de marginea unui panou ca să-l lărgești; Lapin ține minte lățimea.

![Un browser pliat, deschis peste vedere de pe bara lui](shots/browser-overlay.webp)

## Panourile pe oricare parte

**View → Panels** pune browserul de proiect și panoul de proprietăți unde vrei, ca în Revit: **Browser în stânga, Proprietăți în dreapta** (implicit), **Browser în dreapta, Proprietăți în stânga**, **Ambele panouri în stânga** sau **Ambele panouri în dreapta**. Când sunt amândouă pe o parte, stau unul sub altul; trage separatorul dintre ele ca să împarți înălțimea (dublu clic îl pune la jumătate) și trage marginea părții ca să o lărgești. Fiecare panou se pliază în continuare singur: unul pliat e o bară subțire în coloană, sau o bandă laterală când sunt pliate ambele. Fila asistentului (desktop) rămâne cu proprietățile. Lapin ține minte aranjarea pe acest calculator.

![Browserul și proprietățile, unul sub altul în stânga](shots/panel-layouts.webp)

## Luminos și întunecat

Luna din capătul din dreapta al panglicii face interfața întunecată (grafit); soarele o face din nou luminoasă (hârtie). În tema întunecată planurile, secțiunile și fațadele stau pe fundalul întunecat, cu penițele negre în cerneală deschisă; planșele rămân hârtie. Desenele mici ale configuratoarelor (planul, secțiunea și fațada unei uși sau ferestre, ale unui acoperiș, balcon, lift, hale, scări…) și previzualizările etichetelor, cartușelor și materialelor se desenează pe un card de hârtie deschis în ambele teme, ca liniile, textele și cotele să se citească la fel ca pe un desen tipărit.

Tema se păstrează: Lapin pornește din nou în cea în care l-ai lăsat. **Fișier → Preferințe…** oferă și **Ca sistemul**, care urmează setarea de luminos sau întunecat a calculatorului.

## Preferințe

**Fișier → Preferințe…** (<kbd>Ctrl</kbd>+<kbd>,</kbd>, și în **Vedere → Interfață**) ține setările tale, păstrate pe acest calculator pentru toate proiectele și aplicate pe loc:

- **General**: tema (luminoasă, întunecată sau ca sistemul), limba și legături spre **Interfață…** și **Scurtături de tastatură…**.
- **Grilă și snap**: grila planului, adaptivă sau fixă, snap la ea, ce snap-uri la obiecte ia cursorul și unghiul urmăririi polare (vezi [Snap și ajutoare](drafting.md#snap-și-ajutoare)).
- **Coordonate**: ce citește bara de stare la cursor, dacă punctele proiectului se văd și UCS-ul (vezi [Coordonate reale](site.md#coordonate-reale)).
- **Afișare**: comutatoarele din Vedere → Grafică (grila, liniile subțiri, desenele și DXF-ul în 3D) și felul în care navighezi în vederea 3D, într-un singur loc.
- **Profile**: standardele de profile oferite de un proiect nou și cele arătate de alegătoare (vezi [Biblioteci de profile](profiles.md#standardele)).

![Fișier → Preferințe: grila și snap-ul](shots/preferences.webp)

## Taburile vederilor și ferestrele

Trei butoane rapide stau înaintea tab-urilor: **3D** deschide vederea 3D, **Secțiune** (<kbd>SE</kbd>) pornește unealta de secțiune, iar **Măsoară** (<kbd>ME</kbd>) unealta de măsurare, deci cele mai folosite vederi și unelte sunt la un clic distanță din orice vedere.

Fiecare vedere deschisă are un tab deasupra ei. Trage un tab ca să muți vederea în altă parte. Tras afară din fereastră, sau pe marginea ei, se deschide într-o **fereastră separată** acolo unde îl lași, pentru un al doilea ecran (aplicația desktop). Lăsat pe sfertul din dreapta sau de jos al unei vederi, o împarte și se deschide alături sau dedesubt. Lăsat pe celălalt panou, se deschide acolo. Cât timp tragi, locul în care ar ajunge e luminat și numit. Un clic pe tab doar îl deschide, ca înainte.

Un clic dreapta pe tab oferă același lucru: **Deschide într-o fereastră nouă**, **Împarte: deschide alături**, **Împarte: deschide dedesubt** (sau **Deschide în celălalt panou** când vederea e împărțită), **Închide** și **Închide-le pe celelalte**. Butonul de fereastră din dreapta taburilor și **Vedere → Fereastră nouă** deschid vederea din față într-o fereastră separată. Ferestrele împart modelul, selecția, nivelul și unealta; fiecare își păstrează zoomul.

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
