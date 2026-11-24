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

Uneltele mari ale panglicii își arată literele de la tastatură cât ții apăsat <kbd>Alt</kbd> singur, ca indicațiile de taste din Office; indicațiile lor (tooltip) le au mereu. În Confortabil, o bandă mai lată decât fereastra arată uneltele mici doar ca pictograme, cu numele în indicații, până când fereastra e din nou destul de lată. Panoul **Grup**, după **Modifică** pe taburile care îl au, ține **Creează grup**, **Fă componentă**, **Fă tip de apartament**, **Adaugă în setul de detalii**, **Degrupează**, **Editează grupul** și **Scoate din grup**.

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

## Uneltele mele

**Uneltele mele** e o filă de ribbon a ta, prima din rândul filelor: pui în ea doar uneltele pe care le folosești, luate din orice filă, în grupuri pe care le numești tu.

- **Pune unelte în ea.** Clic dreapta pe orice unealtă din ribbon → **Adaugă la Uneltele mele**, **Adaugă la Uneltele mele în…** un grup, sau **Adaugă într-un grup nou din Uneltele mele**. Sau trage o unealtă dintr-o altă filă pe fila **Uneltele mele**: ținută acolo o clipă, fila se deschide, iar tu lași unealta acolo unde o linie arată că va ajunge.
- **Ordonează-le chiar în ribbon.** Trage o unealtă înaintea sau după alta, în grupul ei sau într-un alt grup; trage un grup de numele lui ca să-l muți printre grupuri. O linie arată unde va ajunge, celelalte se dau la o parte, iar dacă lași în altă parte nu se schimbă nimic. Dublu clic pe numele unui grup ca să-l redenumești. Clic dreapta pe o unealtă pentru **Fă-o mică** sau **Fă-o mare**, **Mută mai devreme**, **Mută mai târziu** și **Scoate din Uneltele mele**; clic dreapta pe numele unui grup pentru **Redenumește grupul**, **Șterge grupul**, **Grup nou** și **Anulează ultima schimbare**. De la tastatură, <kbd>Alt</kbd> <kbd>←</kbd> sau <kbd>→</kbd> mută unealta aflată în focus, iar <kbd>Delete</kbd> o scoate.
- **Personalizează Uneltele mele…** (rotița de la capătul filei, View → Interface, sau Commands…) arată în stânga toate uneltele aplicației, căutate și grupate pe file, iar în dreapta fila ta. Trage uneltele dintr-o parte în alta sau apasă **+** (ori <kbd>Enter</kbd>) ca să adaugi una; trage în partea dreaptă ca să le ordonezi; trage o unealtă înapoi în stânga ca s-o scoți. Fiecare rând are și butoane pentru a-l muta în sus sau în jos, pentru a-l arăta ca unealtă mare sau mică, o listă ca să-l muți în alt grup și **×** ca să-l scoți. De la tastatură, <kbd>Alt</kbd> <kbd>↑</kbd> <kbd>↓</kbd> mută unealta aflată în focus (și peste capetele grupului), <kbd>Alt</kbd> <kbd>←</kbd> <kbd>→</kbd> o mută în grupul dinainte sau de după, <kbd>Delete</kbd> o scoate, iar <kbd>Ctrl</kbd> <kbd>Z</kbd> anulează ultima schimbare. **Anulează** și **Resetează** sunt jos, așa că o mutare greșită nu pierde nimic.
- **Exportă…** și **Importă…** salvează fila ca un fișier mic pe care îl poți duce pe alt calculator sau da unui coleg.

Lapin păstrează Uneltele mele pe acest calculator, pentru tine (aplicația desktop și browserul separat). O unealtă pe care ediția ta nu o are e ascunsă, nu pierdută: revine odată cu modulul ei.

![Uneltele mele](shots/my-tools-empty.webp)

![Uneltele mele](shots/my-tools-menu.webp)

![Uneltele mele: două grupuri, Pereți și Goluri](shots/my-tools-tab.webp)

![View → Interface → Personalizează Uneltele mele…](shots/my-tools-dialog.webp)

**Arhitectură → Gol** e orice gol: prima alegere e ce pui, după gazdă (o ușă, o fereastră, un bovindou sau un gol de o formă într-un perete; un gol în placă; o fereastră de acoperiș sau o lucarnă), iar vechile unelte (Ușă, Fereastră, Bovindou, Gol în perete, Gol în placă, Fereastră de acoperiș, Lucarnă) o pornesc aleasă pe felul lor, cu literele lor. **Îmbină pereții** (<kbd>JW</kbd>) îmbină doi pereți; **Îmbină**, **Taie** și **Prelungește** de pe Desen lucrează pe liniile desenate, iar **Îmbină liniile**, **Taie liniile** și **Prelungește liniile** din grupul Linii pe liniile pe care o vedere le desenează din model.

**Gestionare → Toate tipurile** arată într-un singur loc dialogurile de tipuri ale tuturor taburilor (Tipuri de pereți, Tipuri de plăci, Uși și ferestre, Tipuri de structură, Tipuri de mobilier…); fiecare tab își păstrează grupul Tipuri.

### Enter și Esc

O singură regulă în orice vedere, pentru orice unealtă și comandă:

- <kbd>Enter</kbd> **termină**: închide un contur (o placă, o încăpere, un gol în placă: trei puncte sau mai multe), termină o polilinie sau un traseu, termină un lanț (pereți, grinzi: bucățile desenate rămân), termină la alegerea elementului o unealtă care alege un element și apoi un punct și termină o comandă de desen.
- <kbd>Esc</kbd> **anulează pasul în curs**: punctele a ce nu e încă făcut pleacă, ce a făcut deja un lanț sau o comandă rămâne. Fără nimic în curs, <kbd>Esc</kbd> iese din unealtă; fără unealtă, lasă selecția.

## Catalogul de pus

**Gestionare → Pune din catalog…** e o singură fereastră pentru tot ce se poate pune într-o vedere, din toate modulele ediției: **Tipurile** proiectului (ca în catalogul de mai jos), **Simbolurile** (ale Lapin, ale proiectului, cu blocurile aduse de un fișier DXF, ale acestui calculator), **Componentele** și tipurile de apartament, **Piesele** (cele din Studio, fostele familii), **Mobilierul** (tipurile proiectului, Mobilierul meu, felurile Lapin) și **Echipamentele de instalații**. O singură căutare, sus, le cercetează pe toate (fiecare cuvânt scris trebuie să fie în nume, în grup sau în descriere); butoanele de dedesubt arată o singură sursă. O carte pornește punerea, cu unealta ei. **Simboluri…** (Gestionare și Adnotări), **Componente** (Gestionare), **Piese…** (Studio) și **Mobilierul meu…** (Interior) deschid catalogul pe sursa lor; butonul din dreapta sus (**Simboluri…**, **Componente…**, **Piese…**, **Mobilierul meu…**) deschide dialogul care le face și le modifică.

![Catalogul de pus: o singură căutare în simboluri, mobilier și echipamente de instalații](shots/place-catalogue.webp)

## Catalogul

Fila **Catalog** din browserul proiectului arată tipurile proiectului pentru fiecare unealtă care pune elemente cu tip: pereți, uși, ferestre, plăci, pereți cortină, scări, rampe, tavane și plinte. Fiecare tip e desenat mic (straturile unui perete ca în secțiunea din plan, ale unei plăci în secțiune, vederea unei uși sau ferestre, profilul unei plinte), cu ce este în câteva cuvinte.

- un clic pe o carte pune elemente de acel tip: unealta pornește cu el;
- trage o carte pe un loc rapid ca să păstrezi acolo unealta cu acel tip;
- **Editează tipurile…**, la capătul rândului de feluri, deschide dialogul în care se fac și se schimbă tipurile; clic dreapta pe o carte dă **Editează tipul…** (dialogul deschis pe acel tip) și **Duplică**.

![Catalogul: tipurile de pereți](shots/catalog.webp)

![Tipurile de ferestre, desenate după vederea lor](shots/catalog-windows.webp)

**Din biblioteci**, în capul filei, arată ce se poate adăuga în proiect din orice sursă: bibliotecile Lapin, **Acest calculator**, **folderele firmei** și **extensiile** instalate. Sunt intrări pentru tipuri, simboluri, tipuri de linie, standarde de desen, pachete de reguli, forme de bare, parametri comuni, hașuri și familii, cu căutare, filtru de fel și de sursă, simbolurile desenate mic și sursa și licența fiecărei intrări. **Adaugă în proiect** o aduce prin importul propriei biblioteci, într-un pas de anulat; un tip aduce și din ce e făcut (materiale, profile).

![Bibliotecile din Catalog: tipurile de linie și simbolul din folderul firmei](shots/catalog-libraries.webp)

### Folderele firmei în dialogurile bibliotecilor

Dialogul fiecărei biblioteci (**Simboluri…**, **Pachete de tipuri…**, **Tipuri de linie** din Standarde de desen, **Standarde de desen**, **Reguli…**, **Forme de bare…**, **Parametri comuni**, **Materiale** pentru fișierele `.pat`, **Liste de prețuri…**, **Studio → Piese…**, **Gestionare → Cartușe…** și **Interior → Mobilierul meu…**) are un bloc **Folderele firmei** cu fișierele de felul lui din folderele firmei. Un clic citește fișierul prin previzualizarea importului, cu aceleași alegeri ca la **Importă…** (vezi mai jos). **Salvează în folderul firmei…** scrie exportul propriu al bibliotecii într-un folder de scris, înlocuind un fișier cu același nume. Butonul mic cu disc păstrează o copie a fișierului pe acest calculator.

![Simboluri: fișierul din folderul firmei lângă Acest proiect și Acest calculator](shots/symbols-firm-folder.webp)

### Importul unui fișier de bibliotecă: o singură previzualizare, aceleași alegeri

Orice fișier de bibliotecă intră la fel, oricare ar fi dialogul, folderul firmei sau intrarea din Catalog care îl aduce: simboluri, pachete de tipuri, JSON-ul tipurilor de pereți și plăci, standarde de desen, tipuri de linie `.lin`, hașuri `.pat`, pachete de reguli, parametri comuni, nume de câmpuri, cartușe, forme de bare, piese (și familiile mai vechi), și `.lapinlib` din Mobilierul meu. Când totul e nou pentru proiect, sau există deja la fel, intră direct. Când ceva există deja, dar făcut altfel, **Previzualizarea importului** arată fișierul element cu element: **Nou**, **La fel**, **Același nume** (alt element cu acel nume) sau **Același element, schimbat** (același stil, tip de linie, parametru, formă de bară sau intrare de mobilier, modificat între timp), cu ce diferă. Pentru fiecare care diferă alegi:

- **Păstrează-le pe amândouă**: cel din fișier intră lângă cel existent, cu (2) după nume (*Bubble (2)*). Implicit pentru un element cu același nume.
- **Înlocuiește**: cel existent ia pe loc forma celui din fișier; ce îl folosește îl urmează (simbolurile plasate, pereții unui tip, planșele unui cartuș). Implicit pentru același element schimbat.
- **Sari peste**: cel existent rămâne cum e.

**Aplică la toate…** pune aceeași alegere la fiecare element care o are (o formă de bară e numită după cod, deci e înlocuită sau sărită, niciodată păstrată de două ori; un material dintr-un pachet de tipuri îl păstrează implicit pe al proiectului). **Importă** aduce fișierul, într-un singur pas de anulat, iar o notă numără ce s-a adăugat, actualizat, păstrat și sărit, și ce a fost păstrat alături cu un nume nou.

![Previzualizarea importului: un pachet de reguli cu același nume în proiect, implicit păstrate amândouă](shots/library-import-preview.webp)

Un fișier scris de această versiune spune sus cine l-a scris: *Scris de Lapin 0.1.1300 pe 2026-11-16, din Bloc P+4* (proiectul din care vine). Un fișier salvat înainte de această versiune nu are rândul acesta (*Scris de o versiune mai veche de Lapin*) și se citește ca întotdeauna. Un fișier dintr-un Lapin mai nou e refuzat cu o propoziție: actualizează Lapin ca să-l citești.

### Salvează în bibliotecă la configuratorul unui tip

Dialogurile care fac tipuri (pereți, plăci, uși și ferestre, scări, pereți cortină, stâlpi și grinzi, oțel și mobilier) au **Salvează în bibliotecă**: tipul ales se scrie ca `.lapintypes`, cu straturile, materialele, profilele și parametrii lui, pe **Acest calculator** sau într-un folder al firmei care poate fi scris. Apoi apare în Catalog, gata de adăugat într-un alt proiect cu un clic.

![Salvează în bibliotecă la tipurile de pereți](shots/save-type-library.webp)

## Plierea panourilor laterale

În orice mod, browserul proiectului și panoul de proprietăți se pliază pe o bară subțire, pe partea lor:

- butonul din dreapta sus al panoului îl pliază, iar săgeata de pe bară îl depliază;
- dublu-clic pe marginea dintre panou și vedere;
- <kbd>Ctrl</kbd> <kbd>B</kbd> pliază sau depliază browserul, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> proprietățile, iar <kbd>Tab</kbd> pe amândouă.

Apasă o pictogramă de pe bara unui panou pliat ca să-l deschizi peste vedere fără să-l depliezi. Dispare cu <kbd>Esc</kbd> sau cu un clic în altă parte; pioneza îl ține deschis. Trage de marginea unui panou ca să-l lărgești; Lapin ține minte lățimea.

![Un browser pliat, deschis peste vedere de pe bara lui](shots/browser-overlay.webp)

## Panourile pe oricare parte

**View → Panels** pune browserul de proiect și panoul de proprietăți unde vrei, ca în Revit: **Browser în stânga, Proprietăți în dreapta** (implicit), **Browser în dreapta, Proprietăți în stânga**, **Ambele panouri în stânga** sau **Ambele panouri în dreapta**. Când sunt amândouă pe o parte, stau unul sub altul; trage separatorul dintre ele ca să împarți înălțimea (dublu clic îl pune la jumătate) și trage marginea părții ca să o lărgești. Fiecare panou se pliază în continuare singur: unul pliat e o bară subțire în coloană, sau o bandă laterală când sunt pliate ambele. Fila asistentului (desktop) rămâne cu proprietățile. Lapin ține minte aranjarea pe acest calculator. Aceleași patru alegeri sunt în **View → Interface…**, la **Părțile panourilor**, lângă celelalte părți ale interfeței.

**Mutarea unui panou cu mouse-ul.** Trage un panou de antet (partea goală a barei de titlu sau a rândului de file): se luminează zonele în care poate ateriza. Peste celălalt panou, jumătatea de sus pune panoul tras deasupra lui, cea de jos dedesubt; la capătul din stânga sau din dreapta al vederii, panoul se fixează pe acea parte (deasupra sau dedesubtul celui care e deja acolo); oriunde altundeva peste vedere, plutește. Eliberează ca să-l lași; <kbd>Esc</kbd> anulează. Un **panou plutitor** e o fereastră peste vedere: trage-l de antet ca să-l muți, de colțul din dreapta jos ca să-l redimensionezi, și apasă butonul lui de fixare (sau trage-l pe o zonă) ca să-l pui la loc pe o parte. Nu ia loc din vedere, iar Lapin îl ține în fereastră când o redimensionezi. Lapin ține minte unde stă fiecare panou, iar cele patru aranjări de mai sus sunt prestabilite care pun totul la loc. Fără mouse, **View → Panels → Float or dock the project browser** (și **…the properties**) face plutirea, iar cele patru aranjări restul.

![Browserul și proprietățile, unul sub altul în stânga](shots/panel-layouts.webp)

![Tragi proprietățile de antet: jumătatea de jos a browserului se luminează](shots/panel-drag.webp)

![Proprietățile plutind peste vedere](shots/panel-float.webp)

## Luminos, întunecat, Clasic și Contrast ridicat

Luna din capătul din dreapta al panglicii face interfața întunecată (grafit); soarele o aduce la ultima temă luminoasă folosită (hârtie, Clasică sau Contrast ridicat). În tema întunecată planurile, secțiunile și fațadele stau pe fundalul întunecat, cu penițele negre în cerneală deschisă; planșele rămân hârtie. Desenele mici ale configuratoarelor (planul, secțiunea și fațada unei uși sau ferestre, ale unui acoperiș, balcon, lift, hale, scări…) și previzualizările etichetelor, cartușelor și materialelor se desenează pe un card de hârtie deschis în ambele teme, ca liniile, textele și cotele să se citească la fel ca pe un desen tipărit.

Tema se păstrează: Lapin pornește din nou în cea în care l-ai lăsat. **Fișier → Preferințe…** listează **Temă deschisă**, **Temă întunecată**, **Temă clasică**, **Temă cu contrast ridicat** și **Ca sistemul**, care urmează setarea de luminos sau întunecat a calculatorului. Dedesubt, **Zonă de desen** stabilește separat aspectul zonelor de desen (3D, planuri, secțiuni, fațade, planșe și butoanele, mânerele și marcajele de pe ele): **La fel ca panourile** (implicit), **Hârtie (deschis)** sau **Grafit (închis)**. Poți ține astfel panouri închise în jurul unei zone de desen albe sau invers; se schimbă pe loc. Tipărirea, planșele, PDF-ul și exporturile rămân pe hârtie, orice ai alege.

**Clasică** este un aspect de program desktop, gri neutru, potrivit celor care vin din alte programe BIM de birou: panglică, file și dialoguri gri, panouri albe, pânză albă de plan și secțiune cu linii negre, accent și selecție albastre, fontul sistemului puțin mai mic, colțuri drepte și etichete simple.

![Un plan cu un perete selectat în tema Clasică](shots/theme-classic.webp)

**Contrast ridicat** este alb și negru cu un accent albastru închis. Tot textul și toate chenarele ating un contrast de 7:1 sau mai mult, focusul de tastatură este un contur de 3 px, un element selectat se desenează mai gros în plan și în 3D, iar grila rămâne discretă, dar vizibilă. Fundalurile (underlay) și semitonurile rămân lizibile.

![Același plan în tema Contrast ridicat](shots/theme-contrast.webp)

![Preferințe → General: lista temelor](shots/theme-list.webp)

## Preferințe

**Fișier → Preferințe…** (<kbd>Ctrl</kbd>+<kbd>,</kbd>, și în **Vedere → Interfață**) ține setările tale, păstrate pe acest calculator pentru toate proiectele și aplicate pe loc:

- **General**: tema (luminoasă, întunecată, Clasică, Contrast ridicat sau ca sistemul), limba și legături spre **Interfață…** și **Scurtături de tastatură…**.
  La **Tastatură**, pe lângă scurtăturile cu litere, **Un clic în afara casetei de introducere o acceptă** (oprit implicit): cu o valoare tastată în caseta de lângă cursor (o lungime, un unghi, o lățime) sau în linia de comandă, un clic stânga în altă parte ia valoarea ca <kbd>Enter</kbd>, iar clicul este consumat, deci nu pune un punct al lui; <kbd>Esc</kbd> sau clic dreapta aruncă în schimb valoarea tastată. Într-un plan sau un desen, clicul pe vedere ia deja o lungime tastată, clicul dând direcția, oricum ar fi setat. Câmpurile din panoul Proprietăți își iau mereu valoarea când dai clic în altă parte.
- **Grilă și snap**: grila planului, adaptivă sau fixă, snap la ea, ce snap-uri la obiecte ia cursorul și unghiul urmăririi polare (vezi [Snap și ajutoare](drafting.md#snap-și-ajutoare)). La **Cote temporare**, aceleași alegeri ca la **Adnotări → Cote temporare…**: de unde și până unde măsoară cotele temporare ale pereților, stâlpilor și grinzilor (**Axe**, **Fețe**, **Centrul miezului**, **Fețele miezului** sau **Linia de referință**) și cele ale ușilor și ferestrelor (**Axe** sau **Goluri**); vezi [Referința cotelor](walls.md#referinta-cotelor-de-la-fata-miezului-la-fata-miezului).
- **Coordonate**: ce citește bara de stare la cursor, dacă punctele proiectului se văd și UCS-ul (vezi [Coordonate reale](site.md#coordonate-reale)).
- **Afișare**: comutatoarele din Vedere → Grafică (grila, liniile subțiri, desenele și DXF-ul în 3D) și felul în care navighezi în vederea 3D, într-un singur loc. La **Selecție și preselecție**: **Evidențierea preselecției** luminează ce se află sub cursor înainte de clic, în planuri, secțiuni, planșe și 3D (și formele desenate, cotele, etichetele, simbolurile, vederile inserate și vederile din planșe; e oprită cât timp o unealtă așteaptă un punct), iar **Culoarea preselecției** și **Culoarea selecției** le alegi tu; **Culori implicite** le readuce pe ale temei.
- **Culori**: mostrele pe care le oferă câmpurile de culoare, mai jos.
- **Profile**: standardele de profile oferite de un proiect nou și cele arătate de alegătoare (vezi [Biblioteci de profile](profiles.md#standardele)).
- **Biblioteci** (aplicația desktop): **folderele firmei**, oricâte foldere locale, din rețea sau sincronizate cu fișiere de bibliotecă (simboluri, tipuri, tipuri de linie, standarde, pachete de reguli, forme de bare, parametri, hașuri, liste de prețuri, profile, piese). Bifează **de scris** ca Lapin să poată salva într-un folder. Dialogurile bibliotecilor și Catalogul le citesc (mai jos).

### Mostre de culoare

Fiecare câmp de culoare din Lapin (materiale, straturi, culorile camerelor, hașuri, umbrele vederilor, tabele…) e căsuța de culoare a sistemului cu o săgeată mică alături. Săgeata deschide mostrele: scrii o culoare hex sau alegi una din

- **Recente**: culorile alese de curând, păstrate automat pentru tine.
- **Paleta proiectului**: mostre păstrate în proiect, deci le vede oricine îl deschide. Fiecare schimbare e un singur pas de anulare.
- **Paletele tale**: oricâte, păstrate pe acest calculator pentru toate proiectele.

**+** de lângă o paletă adaugă în ea culoarea din câmp. **Editează paletele…** (și **Fișier → Preferințe → Culori**) creează, redenumește, mută și șterge paletele tale, adaugă sau scoate o culoare dintr-o paletă și îi reordonează culorile (selectezi o mostră, apoi săgețile), iar **Adaugă culorile materialelor folosite** pune într-o paletă culorile tuturor materialelor folosite de proiect. Paletele tale nu fac parte din fișierul de standarde (`.lapinstandards`); ca să împarți un set de culori cu o echipă, pune-le în paleta proiectului.

![Mostrele de culoare: culori recente, paleta proiectului și o paletă a ta](shots/colour-swatches.webp)

![Fișier → Preferințe → Afișare: culorile selecției și preselecției](shots/preferences-highlight.webp)

![Fișier → Preferințe → Biblioteci: un folder al firmei](shots/preferences-libraries.webp)

![Fișier → Preferințe: grila și snap-ul](shots/preferences.webp)

## Taburile vederilor și ferestrele

Trei butoane rapide stau înaintea tab-urilor: **3D** deschide vederea 3D, **Secțiune** (<kbd>SE</kbd>) pornește unealta de secțiune, iar **Măsoară** (<kbd>ME</kbd>) unealta de măsurare, deci cele mai folosite vederi și unelte sunt la un clic distanță din orice vedere.

Fiecare vedere deschisă are un tab deasupra ei. Trage un tab ca să muți vederea în altă parte. Tras afară din fereastră, sau pe marginea ei, se deschide într-o **fereastră separată** acolo unde îl lași, pentru un al doilea ecran (aplicația desktop). Lăsat pe sfertul din dreapta sau de jos al unei vederi, o împarte și se deschide alături sau dedesubt. Lăsat pe celălalt panou, se deschide acolo. Cât timp tragi, locul în care ar ajunge e luminat și numit. Un clic pe tab doar îl deschide, ca înainte.

<kbd>Ctrl</kbd> <kbd>Tab</kbd> aduce în față tab-ul următor, iar <kbd>Ctrl</kbd> <kbd>Shift</kbd> <kbd>Tab</kbd> pe cel dinainte (și <kbd>Ctrl</kbd> <kbd>Page Down</kbd> și <kbd>Page Up</kbd>), trecând de la ultimul la primul; pe Mac e tasta <kbd>Control</kbd>, fiindcă <kbd>⌘</kbd> <kbd>Tab</kbd> e a sistemului. Nu fac nimic cât scrii într-un câmp. Sunt comenzi (**Next view tab**, **Previous view tab**) pe care le poți realoca în scurtături.

**Tab peste elemente suprapuse.** Într-un plan, o secțiune sau o fațadă, când sub cursor se află mai mult de un element (un perete peste o placă peste o încăpere), <kbd>Tab</kbd> luminează următorul, iar <kbd>Shift</kbd> <kbd>Tab</kbd> pe cel dinainte, în cerc; linia de stare spune care ("2 din 3: Placă Etaj 1"), iar un clic selectează elementul luminat. Mută cursorul și revine la alegerea a ceea ce e sub el. Cu niciun element sau unul singur sub cursor, <kbd>Tab</kbd> funcționează și cât o unealtă sau o comandă alege elemente (Mută, Copiază, Împarte elementul, o unealtă care alege un perete sau o placă); cât așteaptă un punct sau o valoare tastată, <kbd>Tab</kbd> își păstrează celelalte sensuri: pliază panourile, iar peste un mâner comun mai multor elemente trece prin ele. Cât tragi, este tasta de menținere automată (mai jos). **Light the previous element under the pointer** este comanda de realocat dacă vrei altă tastă.

![Tab luminând al doilea din două elemente aflate sub cursor într-un plan](shots/tab-cycle.webp)

**Ține Tab apăsat pentru listă.** O atingere de <kbd>Tab</kbd> luminează următorul element, ca mai sus. **Ține-l apăsat** un sfert de secundă și se deschide lângă cursor o listă mică cu toate elementele de sub el: elementul curent e umplut cu albastru și marcat cu o săgeată, celelalte sunt doar conturate. Rotește rotița mausului ca să mergi prin listă (rotița în jos: următorul), mută cursorul și lista îl urmează, iar când dai drumul la <kbd>Tab</kbd> elementul curent e luat ca printr-un clic: se selectează sau e dat uneltei care alege. Cu un singur element sub cursor când îl apeși nu există listă, iar <kbd>Tab</kbd> funcționează ca înainte. Odată deschisă, lista rămâne deschisă cât ții tasta, chiar dacă cursorul alunecă de pe elemente (arată atunci ce e sub cursor acum, sau *Nimic sub cursor*, și se umple iar când revii); dacă dai drumul deasupra a nimic, nu se ia nimic. Lista ține de selectare și de alegere: cât tragi, <kbd>Tab</kbd> înseamnă menținere automată.

**Tragere fără a ține butonul mausului (menținere automată).** După ce ai început o tragere (corpul unei selecții sau un mâner), apasă <kbd>Tab</kbd>: tragerea continuă ca și cum butonul ar fi încă apăsat și urmează cursorul; caseta de la cursor spune **Menținere automată**. Dă drumul butonului și mută-te mai departe.
- Un clic stânga lasă tragerea unde e cursorul (cu agățarea lui) sau la valorile tastate.
- <kbd>Tab</kbd> din nou trece prin câmpurile casetei de intrare directă (lungime, unghi, unghiul față de referință), ca la desenarea unui segment; tastează valorile exacte cu cursorul lăsat jos, iar previzualizarea urmează cifrele.
- <kbd>Enter</kbd> acceptă: la valorile tastate, sau la cursor când nu s-a tastat nimic. Și un clic în afara casetelor acceptă. <kbd>Esc</kbd> anulează tragerea.

Așadar <kbd>Tab</kbd> în caseta de intrare directă este câmpul următor oriunde, în afară de chiar începutul unei trageri, unde primul îl pornește pe cel de menținere automată. Tragerile făcute prin comandă (Mută, Copiază, cu cele două clicuri ale lor) n-au avut niciodată nevoie de buton; asta e pentru tragerea selecției sau a unui mâner.


Un tab 3D își păstrează starea cât lucrezi în altă vedere: camera (de unde privește, perspectivă sau ortogonală) și **caseta de secțiune**, pornită sau oprită și unde ai lăsat-o, sunt ca atunci când te întorci dintr-un plan sau dintr-o planșă. Fiecare vedere 3D (3D-ul simplu și fiecare vedere salvată) o ține pe a ei cât timp proiectul e deschis; camera și caseta proprii ale unei vederi salvate o deschid când nu s-a păstrat încă nimic.

Un clic dreapta pe tab oferă același lucru: **Deschide într-o fereastră nouă**, **Împarte: deschide alături**, **Împarte: deschide dedesubt** (sau **Deschide în celălalt panou** când vederea e împărțită), **Închide** și **Închide-le pe celelalte**. Butonul de fereastră din dreapta taburilor și **Vedere → Fereastră nouă** deschid vederea din față într-o fereastră separată. Ferestrele împart modelul, selecția, nivelul și unealta; fiecare își păstrează zoomul.

## Ce ia un clic

**Ctrl+A** (**Selectează tot**) selectează tot ce arată vederea din față și ar putea lua un clic: într-un plan, o secțiune, o fațadă sau o vedere desenată, tot ce ar lua o fereastră de selecție peste toată vederea; în 3D, elementele arătate (nu cele ascunse de vedere sau puse deoparte cu Ascunde / Izolează). Într-un câmp de text selectează, ca de obicei, textul câmpului.

Cu o unealtă pornită care pune elemente dintr-o clasă, **Ctrl+A** le selectează doar pe acelea pe care le arată vederea: pornești unealta **Perete**, apeși **Ctrl+A** și toți pereții din vedere sunt selectați; unealta se oprește, ca să poți lucra imediat cu ei. La fel pentru uși, ferestre, planșee, stâlpi și celelalte unelte care pun un tip.

Un clic, o fereastră de selecție sau un lasou iau doar ce arată vederea, în planuri, secțiuni, fațade, vederile de pe planșe și 3D. Un element pe care Vizibilitate / Grafică al vederii îl ascunde (categoria lui, o subcategorie, un filtru), unul pus deoparte cu Ascunde / Izolează sau de faza vederii, unul în afara domeniului sau a decupării vederii, pe un nivel, o legătură sau un grup care nu se arată, ori tăiat de o cutie de secțiune, e sărit: clicul ia ce e desenat sub el, iar elementul care se luminează sub cursor e unul pe care îl vezi. Cu **Selecție pe niveluri de ansamblu** pornit, un clic într-un întreg intră în continuare în părțile lui. Cu liniile de modelare arătate în locul desenului (butonul cu peniță din capul planului), tot ce e pe ecran se poate lua. Navigatorul proiectului, un tabel, o legătură din panoul de proprietăți și dependențele selectează în continuare un element după nume, ascuns sau nu; panoul de proprietăți spune atunci *Nu se vede în această vedere*.

## Clic dreapta pe o selecție

Un clic dreapta pe un element selectat (într-un plan, un desen sau în 3D) oferă ce se potrivește:

- **Selectează similare în această vedere** și **Selectează similare în tot proiectul** selectează elementele din aceeași clasă și de același tip (din aceeași clasă, când elementul nu are tip). În vedere contează doar ce arată vederea, iar într-un plan doar nivelul pe care ești. **Selectează similare în această vedere** are și o tastă: <kbd>Ctrl</kbd> <kbd>Shift</kbd> <kbd>A</kbd> (se schimbă în **Scurtături de tastatură**).
- **Selectează tot ce are același…** deschide o listă cu ce deosebește elementul. Prima intrare, **Aceeași clasă, în această vedere**, selectează toate elementele clasei pe care le arată vederea, oricare le-ar fi tipul (toți pereții, toți stâlpii); celelalte sunt tipul, lățimea, materialul miezului, materialul, nivelul. Fiecare intrare arată valoarea elementului; alegând-o se selectează toate elementele clasei care o au la fel, oricum ar fi restul.
- **Editează tipul…** deschide dialogul de tipuri al tipului elementului (cel pe care îl deschide **Editează tipurile…**) direct pe acel tip, făcut cel curent: o ușă sau o fereastră deschide **Uși și ferestre** pe tipul ei, nu pe primul din listă, și la fel planșeele, scările și rampele, tavanele, plintele, pereții cortină, stâlpii, grinzile și fundațiile (**Tipuri de structură**), mobilierul, parasolarele, porțile, ventilatoarele, schelele și cofrajele. Butonul cu rotiță de lângă **Tip** din proprietățile unui element face la fel.
- **Selecții anterioare** arată ultimele zece selecții, cea mai nouă prima. O selecție intră în listă când o golești sau alegi altceva; una reluată trece în față, iar elementele șterse dispar din ea. Dai clic pe una ca s-o selectezi din nou. Comanda **Selectează selecția anterioară** (<kbd>Ctrl</kbd> <kbd>Alt</kbd> <kbd>Z</kbd>, se schimbă în **Scurtături de tastatură**) o ia pe cea mai nouă, fără meniu.
- În 3D, **Ascunde**, **Izolează** și **Arată tot** (HH, HI, HR) merg ca în Revit, pe moment.

## Mai multe elemente deodată

Selectează mai multe elemente (Shift-clic sau trage un dreptunghi), iar panoul de proprietăți îl arată pe ultimul ales, cu **N selectate** lângă clasa lui. O valoare pe care o au toate e afișată; una care diferă apare ca *diferă*. Schimbă un câmp și fiecare element selectat care îl are primește noua valoare, într-un singur pas pe care **Anulează** îl desface: înălțimea și decalajul bazei pereților, tipul fiecărui perete, ușă sau grindă selectată, parapetul ferestrelor, fazele fiecărui element selectat oricare i-ar fi clasa, stratul și culoarea liniilor desenate, stilul textelor. Ce e al fiecărui element rămâne al lui: numărul unei încăperi, lungimea unui perete, locul unei uși de-a lungul peretelui.

## Ascunderea într-un plan sau o secțiune

Un clic dreapta într-un plan, o secțiune sau o fațadă, cu ceva selectat, oferă să-l pui deoparte:

- **Ascunde în vedere** ascunde selecția în această vedere de tot, ca *Hide in view* din Revit: se păstrează cu proiectul, iar planșele, PDF-ul și DXF-ul vederii o lasă și ele deoparte. Fiecare ascundere e un singur pas de anulare. Planul implicit al unui nivel devine vederea lui de plan când se ascunde ceva în el. La fel și adnotările: un text, o cotă, o etichetă, cota unui nivel, marcajul de secțiune sau de fațadă al altei vederi, o săgeată a nordului și o singură axă (doar acea axă, doar în această vedere).
- **Ascunde temporar** (<kbd>HH</kbd>) și **Izolează temporar** (<kbd>HI</kbd>) ascund selecția, sau tot restul, doar în această vedere, până la **Resetează ascunderea/izolarea temporară** (<kbd>HR</kbd>). Nu se salvează și nu se anulează, și nu schimbă nimic la tipărire. Aceleași taste în 3D ascund și izolează în vederea 3D, ca până acum.
- **Arată ascunsele** desenează cu magenta ce ascunde vederea, ca să poată fi luat: selectează-l și alege **Reafișează în vedere** ca să-l arăți din nou. Alege iar **Arată ascunsele** ca s-o oprești.

## Selecția rapidă

**Selecție rapidă…** (în meniul de clic dreapta al unei vederi, sau **Vedere → Selectează → Selecție rapidă**, <kbd>QS</kbd>) selectează elemente după reguli, cum fac QSELECT din AutoCAD și filtrele din Revit. Regulile ei se scriu în același editor ca filtrele vederilor, seturile de căutare ale coliziunilor, filtrul unui extras de câmpuri și regula unei sarcini: fiecare regulă alege un câmp (al elementului: **Clasa IFC**, **Tipul**, **Nivelul**, **Materialul**, **Numele**, **Categoria**, **Grupul**, **Fișierul legat**, **Faza creării** sau **Faza demolării**, **Tipul predefinit**, un **Parametru (ca text)** după nume, cum ar fi rezistența la foc, sau orice câmp al proiectului, cantitățile printre ele) și îl compară: este, nu este, conține, nu conține, începe cu, este mai mic decât, este cel mult, este mai mare decât, este cel puțin, este între (oricare capăt poate rămâne gol), este unul din, nu este niciunul din, are o valoare, nu are valoare. O listă își ia valorile despărțite prin virgule. Căsuța valorii sugerează valorile pe care le are modelul. Elementele trebuie să respecte **toate regulile** sau **oricare dintre reguli**. **Caută în** alege ce arată vederea din față (contează decuparea ei, ce ascunde și ce e ascuns temporar) sau tot proiectul. Numărul de elemente găsite se schimbă pe măsură ce scrii; **Selectează** le selectează, sau le adaugă la selecție cu **Adaugă la selecție** bifat.

![Selecția rapidă: pereții unui tip, în tot proiectul](shots/quick-select.webp)

## Stiluri de navigare cu mouse-ul

Bara de stare, jos, arată stilul de navigare cu mouse-ul în uz (la început *Lapin*). Dă clic pe el ca să alegi altul: **Revit-like**, **AutoCAD-like**, **ArchiCAD-like**, **SketchUp-like**, **Blender-like** sau **Rhino-like**, fiecare cu gesturile lui principale. Îl setează și **Fișier → Preferințe → Afișare → Navigare cu mouse-ul**. Se păstrează pentru tine, pe acest calculator, pentru toate proiectele, și se aplică deopotrivă vederii 3D și planurilor, secțiunilor, fațadelor și planșelor.

![Meniul stilurilor de navigare din bara de stare](shots/nav-styles-menu.webp)

| Stil | Rotire (3D) | Deplasare | Zoom | Selecție | Regiune |
|---|---|---|---|---|---|
| Lapin | tragere cu stânga | tragere cu dreapta (și Shift sau Ctrl + stânga) | rotița; tragere cu mijlocul | clic; <kbd>Shift</kbd> comută; <kbd>Ctrl</kbd> ia partea | <kbd>Alt</kbd> + tragere cu stânga |
| Revit-like | <kbd>Shift</kbd> + tragere cu mijlocul | tragere cu mijlocul | rotița | <kbd>Ctrl</kbd> adaugă, <kbd>Shift</kbd> scoate | tragere cu stânga |
| AutoCAD-like | <kbd>Shift</kbd> + tragere cu mijlocul | tragere cu mijlocul | rotița | <kbd>Shift</kbd> scoate, <kbd>Ctrl</kbd> adaugă | tragere cu stânga |
| ArchiCAD-like | <kbd>Shift</kbd> + tragere cu mijlocul | tragere cu mijlocul | rotița | <kbd>Shift</kbd> adaugă sau scoate | tragere cu stânga |
| SketchUp-like | tragere cu mijlocul | <kbd>Shift</kbd> + tragere cu mijlocul | rotița | <kbd>Ctrl</kbd> adaugă, <kbd>Shift</kbd> comută, <kbd>Ctrl</kbd> + <kbd>Shift</kbd> scoate | tragere cu stânga |
| Blender-like | tragere cu mijlocul | <kbd>Shift</kbd> + tragere cu mijlocul | rotița (spre mijlocul vederii); <kbd>Ctrl</kbd> + tragere cu mijlocul | <kbd>Shift</kbd> comută | tragere cu stânga (un chenar) |
| Rhino-like | tragere cu dreapta | <kbd>Shift</kbd> + tragere cu dreapta | rotița | <kbd>Shift</kbd> adaugă, <kbd>Ctrl</kbd> scoate | tragere cu stânga |

**Lapin** e felul în care a lucrat mereu Lapin și rămâne cel implicit: nimic nu se schimbă până nu alegi altul. În celelalte, o tragere cu stânga care începe pe loc gol (sau pe un element neselectat) desenează în vederea 3D o regiune de selecție, ca în planuri: trasă spre dreapta, o fereastră (contur plin) ia elementele a căror geometrie vizibilă e toată înăuntru; trasă spre stânga, un crossing (punctat) ia orice element atinge. Blender-like ia mereu ce atinge chenarul, oricum e tras. <kbd>Space</kbd> în timp ce tragi face din chenar un lasou. Tastele din coloana *Selecție* spun cum se alătură regiunea selecției: cu <kbd>Ctrl</kbd> în Revit-like adaugă, cu <kbd>Shift</kbd> scoate; fără nicio tastă înlocuiește. Apăsarea pe un element selectat, o clipă de așteptare și tragerea îl mută în continuare, în orice stil. Contează doar ce arată vederea: un element ascuns, izolat în afară, tăiat de o cutie de secțiune sau dintr-o altă fază nu e luat niciodată, iar un grup sau un ansamblu e luat așa cum l-ar lua un clic.

**Selectarea în planuri, desene și 3D.** În stilul propriu al Lapin, <kbd>Shift</kbd> apăsat pe loc gol lasă selecția cum e (doar un clic simplu pe nimic o dă drumul), iar o regiune (un dreptunghi sau un laso) trasă cu <kbd>Ctrl</kbd> (<kbd>⌘</kbd> pe Mac) scoate din selecție ce atinge, așa cum <kbd>Shift</kbd> adaugă la ea; un clic cu <kbd>Ctrl</kbd> alege tot partea. O apăsare care se mișcă doar câțiva pixeli e un clic în locul unde ai apăsat, nu un dreptunghi, iar un dreptunghi care nu ia nimic nu dă niciodată drumul selecției. În planuri și desene, elementul pe care l-ar alege un clic se luminează sub cursor, iar cursorul e o mână cu degetul întins; marcajul de agățare apare doar unde o comandă cere un punct. <kbd>Esc</kbd> oprește mai întâi ce rulează (o comandă, o unealtă); când nu rulează nimic, urcă un nivel dintr-o selecție în care ai intrat, apoi golește selecția.

![Un crossing tras peste casă în 3D cu stilul Revit-like](shots/nav-3d-crossing.webp)

În planuri, secțiuni, fațade și planșe, stilul alege ce buton deplasează și ce taste adaugă la selecție sau scot din ea; tragerea cu stânga desenează în continuare o fereastră sau un crossing, iar butonul din mijloc apăsat de două ori potrivește în continuare vederea (și în 3D, în Revit-like și AutoCAD-like). Celelalte stiluri presupun un mouse cu trei butoane: pe un trackpad, fără buton din mijloc, rămâi la Lapin. Se împrumută doar comenzile: numele sunt simple etichete și nimic altceva din acele programe nu e imitat.

## Modifică și Îmbinări pe fiecare tab

Grupul **Modifică** (Mută, Copiază, Rotește, Oglindește, Repetare, Șterge, Fixează, **Creează similar**, Preia proprietățile, uneltele de grupuri) e același pe Desen, Arhitectură, Interior, Structură, Oțel, Lemn, Studio, Amplasament și Instalații. Pe Desen și Studio se unește cu grupul acelor taburi care se numea deja Modifică, așa că fiecare buton apare o singură dată (Desen păstrează Scalează, Repetare și Șterge; Studio păstrează Mută 3D, Rotește 3D, Oglindește 3D și propria Repetare, care face repetarea parametrică a solidului). **Creează similar** merge pe orice pune o unealtă: pornește acea unealtă pregătită ca elementul selectat (tipul unui perete sau al unui stâlp, arcul unei grinzi, felul și tipul unei piese de mobilier, rolul unui nod de instalații, sistemul unui traseu, șablonul unui drum, forma unei bare, piesa și valorile unui obiect, valoarea unei încărcări). E gri până când selecția e ceva de la care poate porni.

Grupul **Îmbinări** (**La 45°**, **Cap la cap**, **Inversează**) e același pe Arhitectură și Structură și lucrează pe ce e selectat: colțurile pereților, grinzilor și fundațiilor continue selectate. **La 45°** și **Încadrează** ale Oțelului îmbină două elemente și rămân pe tabul Oțel.

O unealtă care erau mai multe butoane e acum una cu o alegere în bara liniei de comandă: **Perete**, **Grindă**, **Fundație continuă** și **linia de separare a încăperilor** au **Linie** / **Arc**; **Traseu** are **Traseu** / **Drum**; **Plasează mobilier** (Interior) și **Plasează nod** (Instalații) aleg felul piesei sau al nodului. Numele vechi (Perete curb, Drum, Sofa, Radiator…) merg în continuare ca locuri rapide, intrări în Uneltele mele, scurtături și în interfețele salvate: pornesc aceeași unealtă deja setată pe acea alegere.

## Preia proprietățile

**Modifică → Preia proprietățile** (<kbd>MA</kbd>, sau `MATCHPROP` în linia de comandă, ca în AutoCAD; în Revit se numește Match Type) dă altor elemente ce are un element. Selectează elementul de la care copiezi, sau pornește comanda și dă clic întâi pe el; apoi dă clic pe fiecare element care trebuie să-i primească proprietățile. Elementul de sub cursor se luminează înainte de clic, în planuri, secțiuni și 3D. Fiecare clic e un pas pe care **Anulează** îl desface. **Aplică selecției** le dă deodată tuturor celorlalte elemente selectate. Apasă <kbd>Enter</kbd> sau <kbd>Esc</kbd> când ai terminat.

Rotița din bara liniei de comandă arată ce se preia, iar Lapin ține minte alegerea: **Tip** (unui element din aceeași clasă), **Dimensiuni și setări** (înălțime, decalaje, materiale, prioritatea la tăiere, stilul unui text, poziția tocului și parapetul unei uși sau ferestre; altei clase doar câmpurile pe care le are și ea), **Parametri** (ai tăi; ai unui set standard doar aceleiași clase), **Faze**, **Strat, culoare și hașură** (forme desenate) și **Nume** (oprit la început). Locul unui element, forma și GlobalId-ul lui nu se preiau niciodată. Un element fixat le primește totuși: fixarea ține elementul pe loc, nu setările lui.

![Preia proprietățile în plan: peretele de la care se copiază, peretele de sub cursor luminat și ce se preia](shots/match-properties.webp)

Ca să scoți un set de proprietăți întreg de pe un element sau de pe un tip, folosește butonul cu numele setului, sub parametri, în **Proprietăți → Parametri** (numele setului lângă un coș): toți parametrii din set pleacă într-un singur pas de anulare.

## Parametri calculați prin formule

Un parametru al tău care e număr, lungime, arie, unghi, da/nu sau text (nu alegere) poate fi calculat în loc să fie scris. În **Proprietăți → Parametri**, apasă butonul **Σ** dinaintea valorii lui: valoarea devine un câmp de **formulă**. Scrie formula și apasă <kbd>Enter</kbd> (sau părăsește câmpul) ca s-o păstrezi. Sub câmp, Lapin arată ce dă formula, *= 21*, sau de ce nu se citește; o formulă care nu se citește nu se păstrează. Apasă **Σ** din nou ca să te întorci la o valoare: ultimul rezultat rămâne valoare.

![Un parametru Studs calculat din lungimea peretelui și dintr-un parametru Spacing](shots/param-formula.webp)

O formulă se păstrează cum e scrisă și se calculează la fiecare cerere, deci urmează elementul: schimbă lungimea peretelui sau *Spacing* și *Studs* se schimbă odată cu ele. Etichetele, extrasele și exportul IFC citesc rezultatul. Ce poate folosi o formulă:

- **Nume**: ceilalți parametri ai elementului și ai tipului lui, și numerele proprii ale elementului (*Height*, *Width*). Un nume cu spații poate fi scris fără ele: *Fire rating* sau *FireRating*. Un parametru poate folosi altul care e formulă.
- **Cantități**, când elementul le are: *Length*, *Width*, *Height*, *GrossArea*, *NetArea*, *GrossVolume*, *NetVolume*.
- **Funcții**: *min*, *max*, *round*, *floor*, *ceil*, *sqrt*, *sin*, *cos*, *tan*, *atan2*, *if* și *clamp*, cu + − × ÷ și paranteze. Zecimalele se scriu cu punct; argumentele se despart prin virgulă sau punct și virgulă.

**Nume și funcții**, sub câmp, enumeră numele pe care le oferă elementul. O formulă da/nu e da când rezultatul nu e zero (*if(Height > 3, 1, 0)*).

![O formulă care nu se citește spune de ce](shots/param-formula-error.webp)

Același limbaj merge în textul viu al [simbolurilor și etichetelor](drafting.md), scris `{= attr.w * 2 | 0.00}`.

### Texte calculate

Formula unui parametru text dă un text. Scrie-o într-unul din două feluri:

- **Un text cu câmpuri între acolade**, cum le scrie o etichetă: `{storey.code}.{Hall}.{Flat}.{number}` dă *K1.A.3.1* pentru o încăpere cu numărul 1, cu parametrii *Hall* = A și *Flat* = 3, pe un nivel al cărui *code* e K1. Ce e în afara acoladelor se scrie așa cum e.
- **O expresie cu texte**: texte între ghilimele (`"K"` sau `'K'`), unite cu `&` (`storey.code & "." & number`) sau cu *concat* (`concat(storey.code; "."; number)`).

Funcțiile pentru texte: *text(x; "00")* scrie un număr cum spune formatul (*7* → *07*; `"0.00"` cu două zecimale), *pad(x; 3)* îl completează la trei caractere cu zerouri în față, *upper*, *lower*, *trim*, *left(x; n)*, *right(x; n)* și *len*. Două texte se compară ca texte, fără să conteze literele mari: `if(storey.code == "K1"; "subsol"; "etaj")`. O formulă dată unui parametru nou fără fel îl face text când dă un text.

### Citirea altor elemente: nivelul, proiectul, gazda, încăperile

Un nume poate citi parametrul sau câmpul altui element, scris înainte de un punct:

- `storey.…` nivelul pe care stă elementul (`storey.code`, `storey.name`, `storey.elevation`); `building.…` și `site.…` deasupra lui;
- `project.…` datele proiectului (`project.number`, `project.client`) și parametrii lui proprii;
- `type.…` tipul elementului; `group.…` grupul din care face parte; `assembly.…` întregul al cărui parte este;
- `host.…` peretele unei uși sau ferestre (`host.FireRating`);
- `room.…` încăperea în care stă elementul (a unei uși sau ferestre: cea din partea interioară); `fromRoom.…` și `toRoom.…` încăperile din partea exterioară și interioară a unei uși (își schimbă locul când ușa e întoarsă).

**Parametrii proprii ai proiectului sunt globali.** Adaugă-i în **Gestionare → Datele proiectului…**, la **Parametrii proprii ai proiectului**, jos (o cheie, o etichetă, un fel și o valoare): formula sau eticheta oricărui element citește unul doar prin cheia lui (`zone`) sau ca `project.zone`.

Nimic din toate acestea nu se păstrează pe element: se citește la fiecare cerere. Schimbă *code* al nivelului din K1 în K2 și formula fiecărei încăperi, eticheta ei, celelalte etichete, extrasul și exportul IFC spun K2 imediat. O formulă care se citește pe ea însăși, direct sau prin altele, nu dă nicio valoare și spune de ce.

![Indexul unei încăperi calculat din codul nivelului ei și din parametrii ei](shots/param-scopes.webp)

### Inspectorul de parametri

Ce nume poate citi un element? **Proprietăți → Parametri → Inspector de parametri** le arată pe toate, cu valoarea lor de acum: câmpurile pe care le are orice element (`{name}`, `{mark}`, `{type}`, `{class}`), atributele lui (`{number}` al unei încăperi, `{height}` al unui perete), ce calculează modulul lui (`{finish.floor}` al unei încăperi), cantitățile lui, parametrii lui proprii, proprietățile standard IFC ale clasei lui, parametrii proiectului după cheie și apoi, grup cu grup, ce citește prin nivelul, clădirea, amplasamentul lui, proiect, tipul, grupul, ansamblul, gazda și încăperile lui (`{storey.code}`, `{project.number}`, `{room.name}`, `{host.FireRating}`…). Scrie în **Caută câmpurile** ca să restrângi lista; ține mouse-ul pe un câmp ca să vezi ce este. Dă clic pe unul ca să-l copiezi, apoi lipește-l într-o etichetă, un cartuș, textul unui simbol sau o formulă.

![Inspectorul de parametri al unei încăperi: câmpurile ei, apoi ale nivelului și ale proiectului](shots/fields-inspector.webp)

Cât timp o formulă e deschisă, butonul **{ }** de lângă ea (**Inserează câmp…**) deschide aceeași listă sub ea: un clic pune câmpul unde e cursorul, `{storey.code}` într-un text cu câmpuri, numele simplu (`storey.code`, *FireRating*) într-o expresie. O cantitate e citită doar de formule: într-un text intră ca `{= NetArea}`.

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

Sunt două feluri de a tasta o comandă. **Literele** ca <kbd>WA</kbd> merg când niciun câmp nu e activ și linia de comandă e goală: tastează-le peste vedere. Comanda pornește imediat. Când o comandă din linia de comandă începe cu aceleași litere (<kbd>DI</kbd> și <kbd>DIMADD</kbd>), Lapin așteaptă o clipă: tastează mai departe ca să scrii comanda lungă, sau apasă <kbd>Enter</kbd> ori <kbd>Space</kbd> ca s-o pornești acum pe cea scurtă. Cât o comandă cere un punct sau o valoare, literele și cifrele merg la ea. **Comenzile din linia de comandă** ca <kbd>L</kbd>, <kbd>M</kbd> sau <kbd>TRIM</kbd> urmate de <kbd>Enter</kbd> merg în orice set. <kbd>Esc</kbd> anulează mereu.

Când cele două se ciocnesc, alegi cum își împart tastatura:

- **Scurtături cu litere (ca în Revit)**, în acest dialog și în **Preferințe → General → Tastatură**, e pornit implicit. Oprește-l ca să rămână doar linia de comandă: fiecare literă tastată merge la ea, iar <kbd>Enter</kbd> sau <kbd>Space</kbd> pornește o comandă, un alias al tău sau, când textul nu e niciuna, comanda ale cărei litere sunt (<kbd>WA</kbd> <kbd>Enter</kbd> pornește tot peretele). Acordurile (<kbd>Ctrl</kbd> + o tastă, tastele de funcții, <kbd>Delete</kbd>) și cifrele uneltelor rapide merg în continuare.
- Tasta <kbd>`</kbd> (cea din stânga lui <kbd>1</kbd>, găsită după locul ei pe orice tastatură, și o scurtătură ca celelalte: **Linia de comandă: preia toată tastarea**) dă liniei de comandă toată tastarea, cu literele pornite sau oprite. Linia de comandă e încadrată și spune asta; nicio literă nu pornește o unealtă, nicio cifră nu alege o unealtă rapidă. <kbd>Enter</kbd> pornește ce ai tastat și dă tastarea înapoi, afară de cazul în care o comandă cere apoi un punct sau o valoare: modul rămâne până se termină comanda. <kbd>Esc</kbd> sau aceeași tastă îl închide.

### Aliasurile comenzilor

Pagina **Aliasuri de comenzi** a dialogului ține nume proprii pentru comenzi. Tastează 1 până la 12 litere sau cifre (prima literă), găsește comanda în listă (o comandă din linia de comandă, ca <kbd>LINE</kbd>, sau orice comandă din panglică ori din aplicație) și apasă **Adaugă alias**. Tastează aliasul în linia de comandă și apasă <kbd>Enter</kbd> sau <kbd>Space</kbd>. Ale tale au prioritate față de numele încorporate. Pagina spune ce înlocuiește un alias: un nume încorporat din linia de comandă, o scurtătură cu aceleași litere sau o scurtătură pe care o face să aștepte (cu literele pornite, o scurtătură care e alias, sau începutul unuia, așteaptă o clipă ca să poți tasta mai departe și apăsa <kbd>Enter</kbd>). Aliasurile se păstrează odată cu scurtăturile, și în **Export…** și **Import…**.

![Aliasurile comenzilor, cu ce umbrește un alias](shots/shortcuts-aliases.webp)

### Scurtăturile implicite

Acestea sunt scurtăturile în stil Revit, așa cum le are Lapin acum:

<!-- shortcuts -->

Patru combinații în plus sunt la uneltele Modifică, în ambele seturi: <kbd>Ctrl</kbd> <kbd>D</kbd> trage (Mută), <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>D</kbd> trage o copie (Copiază), <kbd>Ctrl</kbd> <kbd>E</kbd> rotește (Rotește) și <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>E</kbd> rotește o copie (**Rotește o copie**, la fel cu Copiază deja pornit). Pe Mac, <kbd>⌘</kbd> ia locul lui <kbd>Ctrl</kbd>. Le poți schimba ca pe oricare altă scurtătură.

Tastele cu cifre <kbd>1</kbd>–<kbd>9</kbd> aleg uneltele rapide.

## Mutarea unui dialog

Orice dialog se mută de bara lui de titlu, ca o fereastră: apasă pe titlu și trage, cu mausul sau cu degetul. Rămâne în fereastră, se deschide din nou unde l-ai lăsat cât timp programul e deschis, iar dublu clic pe titlu îl aduce înapoi la mijloc. Așa poți muta un dialog de pe partea de model la care lucrezi sau poți vedea modelul din spatele lui.
