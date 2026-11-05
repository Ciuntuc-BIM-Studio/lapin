---
title: Secțiuni, fațade și 3D
order: 70
summary: Faci secțiuni și fațade, deschizi vederi 3D și setezi ce arată fiecare vedere și cum.
---

Toate vederile sunt în navigatorul proiectului, puse sub disciplina lor: Arhitectură, Structură sau Instalații. Dublu clic pe o vedere o deschide într-un tab.

![Navigatorul proiectului cu o secțiune și o fațadă](shots/browser.webp)

## Secțiuni

Alege **Vedere → Secțiune** (<kbd>SE</kbd>) sau apasă **+** lângă **Secțiuni** în navigator. Apoi desenează linia de secționare într-un plan cu două clicuri, și dă un al treilea clic pe partea spre care privește secțiunea. Secțiunea e desenată din model și rămâne la zi pe măsură ce îl schimbi.

![Secțiunea A-A prin casă](shots/section-view.webp)

O secțiune nu trebuie să fie o singură linie dreaptă. **Secțiune în trepte** (<kbd>SJ</kbd>) o desenează ca polilinie, clic după clic, iar <kbd>Enter</kbd> încheie: fiecare bucată dreaptă e o tăietură plană care vede doar între capetele ei, iar bucățile se desenează una lângă alta, la lungimile lor reale, cu o linie subțire întreruptă la fiecare treaptă. **Secțiune curbă** (<kbd>SV</kbd>) desenează un arc prin trei puncte (începutul, sfârșitul, un punct de pe el) și îl desfășoară la fel, ca o fațadă curbă să se citească la lungimea ei adevărată. **Secțiune pe lungul** (<kbd>SA</kbd>) alege un element cu o linie în plan, un drum sau o alee, un șanț, un zid de sprijin, un perete, o grindă, o fundație sau o polilinie desenată, și îl urmărește așa cum e acum; scrie pichetajul jos (0+020, din zecime în zecime de scară, sau pasul pe care îl pui la **Pichetaj** în setările vederii). În plan, punctele unei secțiuni sunt grip-uri: trage unul, trage mijlocul unei bucăți ca să adaugi o treaptă, Ctrl/⌘-clic pe un punct ca să-l scoți. Arătată în 3D, o secțiune desfășurată taie modelul pe prima ei bucată și își așază desenul bucată cu bucată.

![O secțiune în trepte prin casă: trei bucăți una lângă alta, treptele marcate](shots/section-jogged.webp)

Selectează secțiunea în navigator ca să-i setezi scara, adâncimea, disciplina și stilul în panoul de proprietăți.

### Unde se marchează o secțiune

Linia unei secțiuni se desenează în fiecare plan, iar secțiunea se marchează în fiecare altă secțiune și fațadă pe care o taie planul ei: o linie subțire întreruptă acolo unde planul taie vederea, cu numele secțiunii și o săgeată care privește încotro privește secțiunea, sus, la aceeași mărime pe hârtie ca în plan. O secțiune nu se marchează niciodată în ea însăși. Clic pe un marcator selectează secțiunea, dublu-clic o deschide.

Fiecare secțiune stă pe un **strat de marcatori** (**Sections** la început; **Strat de marcator** din proprietățile secțiunii îl schimbă), iar fiecare vedere alege ce marchează. **Vedere → Secțiuni…** listează secțiunile proiectului pe straturi, pentru vederea din față, cu o bifă pe strat și pe secțiune doar pentru această vedere, stratul fiecărei secțiuni (unul existent sau **Strat nou…**) și în câte vederi e ascunsă fiecare. O planșă marchează ce marchează vederile ei.

O secțiune tăiată transversal printr-o grindă sau un stâlp (**Secțiune aici**, **Secțiune transversală** din fila Armare 2D) are și ea grip-uri în plan: trage oricare capăt de-a lungul liniei ca să dai părții aceleia mai multă sau mai puțină margine, trage mijlocul de-a lungul elementului ca să muți stația la care taie. Fereastra secțiunii urmează.

![Vedere → Secțiuni… pentru fațada de sud](shots/sections-dialog.webp)

## Fațade

**+** lângă **Fațade** face dintr-o dată cele patru fațade ale clădirii.

![Fațada de sud](shots/elevation-view.webp)

### Umbre de soare

O fațadă, o secțiune sau un plan de situație pot desena umbrele pe care soarele le aruncă pe ce văd, pline sub liniile lor, așa că se tipăresc pe planșe, în PDF și DXF. Le alegi în proprietățile vederii, la **Umbre de soare**:

- **soare propriu**: un soare al vederii. Implicit convenția de desen: soarele peste umărul stâng, cu razele pe diagonala unui cub (din 45°, la 35,26° înălțime), deci o umbră e cât de adânc iese ce o aruncă. **Soarele din** și **Înălțimea soarelui** îl rotesc; **Convenția de desen** îl readuce.
- **soarele proiectului**: soarele locului proiectului (Gestionare → Amplasament) într-o zi și la o oră ale vederii. **Ca în Soare și umbre** ia ziua și ora din Vedere → Soare și umbre.

O secțiune aruncă și primește umbre doar din ce e dincolo de tăietura ei. Un plan de nivel nu desenează umbre: pereții lui, tăiați la înălțimea planului, le-ar arunca prea scurte.

![Fațada sudică cu umbra acoperișului](shots/elevation-shadows.webp)

![Umbrele de soare ale unei vederi](shots/view-shadows.webp)

### Hașurile suprafețelor

O fațadă sau o secțiune hașurează fiecare față pe care o vede cu hașura **Elevation** a materialului ei (**Gestionare → Materiale**, fila **2D graphics**): modelul, scara, unghiul, penița și umplerea. Se hașurează doar partea feței pe care nu o ascunde un element mai apropiat, deci tencuiala sau cărămida unei fațade se vede în jurul ușilor și ferestrelor, iar o secțiune hașurează ce vede dincolo de tăietură, nu în spatele elementelor pe care le taie. Un perete stratificat arată materialul stratului lui exterior. Hașura se desenează sub linii, deci se tipărește pe planșe, în PDF și în DXF (layerele SURFACE-HATCH și SURFACE-FILL).

Cu **Alignment** *view* modelul continuă de la o față la alta; cu *layer* stă pe față, de-a lungul orizontalei ei, din colțul ei de jos, iar o față văzută oblic îl arată scurtat. Un material care continuă în același plan peste mai multe elemente se hașurează ca unul singur, dacă **Join materials** din stilul vederii e pornit.

Până alegi una, hașura de fațadă a unui material vine **după nume**, ca hașura lui de secțiune, iar **Gestionare → Materials** arată care (*după nume (BRICK)*): cărămida și zidăria BRICK, piatra STONE, lemnul BOARDS, placările metalice VERTICAL, țigla, șindrila și ardezia HORIZONTAL, plăcile ceramice TILES, pietrișul și pământul ca în secțiuni. Izolația, membranele, tencuiala, vopseaua și betonul nu primesc niciuna: BATT-ul izolației e o hașură de secțiune, nu se propune niciodată pentru o suprafață (aleasă de mână, se desenează). Alege **none** ca să nu se deseneze nimic. O secțiune desfășurată (în trepte, curbă sau după un element) hașurează fiecare bucată a ei între capetele bucății și îi desenează la fel umbrele de soare. **Surface hatches** din stilul vederii (**Vedere → View settings**, **Gestionare → View styles**) le oprește pentru vederi întregi: pornit în Architectural, oprit în Structural, MEP și Coordination. Planurile își păstrează hașurile lor de secțiune și de plan.

![Fațada de sud, cu cărămida aparentă hașurată după numele ei](shots/elevation-surface-hatch.webp)

![O secțiune în trepte care hașurează zidul de grădină văzut de ultima ei bucată](shots/section-developed-surface-hatch.webp)

![Hașurile suprafețelor în setările vederii](shots/view-surface-hatch.webp)

## Vederi 3D

<!-- video section-3d -->

Dublu clic pe **3D** deschide vederea 3D. Trage cu butonul stâng ca să te rotești în jurul modelului. Planul-cheie din dreapta sus arată de unde privești. **Plan**, **Persp** și **Încadrează** schimbă camera. Ca să păstrezi o cameră, duplică vederea 3D.

Rotița mouse-ului face zoom spre punctul modelului de sub cursor, care rămâne pe loc pe ecran; zoomul se oprește chiar înainte de punct și nu trece niciodată prin el. O tragere cu butonul stâng rotește vederea în jurul mijlocului elementelor selectate, care rămâne pe loc pe ecran, ca să le vezi din toate părțile; fără nimic selectat se rotește în jurul punctului modelului de sub cursor de unde începe tragerea, iar în jurul mijlocului vederii dacă sub cursor nu e nimic. **Vedere → Zoom la cursor** și **Vedere → Rotire în jurul selecției** le opresc pe fiecare (rotița face atunci zoom spre mijlocul vederii, iar vederea se rotește în jurul mijlocului ei), la fel și casetele din proprietățile unei vederi 3D. Alegerea se păstrează pe acest calculator.

### O secțiune în 3D

Clic dreapta pe o secțiune în navigator și alege **Arată în 3D**. Vederea 3D taie modelul după linia secțiunii. Păstrează partea spre care privește secțiunea, până la adâncimea ei, dacă are una. Fiecare element tăiat e închis acolo unde e tăiat, strat cu strat, fiecare strat în materialul lui: un perete își arată miezul de cărămidă și tencuielile, plin, cum e peretele. **Hașuri**, pe eticheta din josul vederii 3D, pune în schimb pe tăietură desenul secțiunii, cu umpluturile și hașurile de secțiune pe care le tipărește o planșă. Cotele, semnele de nivel, etichetele și textele ei stau în același plan, la fel de mari și în același loc ca în secțiune. Liniile își păstrează grosimea peniței și modelul complet de linie întreruptă. Cele desenate lângă clădire stau în aer, lângă tăietură. **Adnotări**, pe eticheta din josul vederii 3D, le ascunde sau le arată din nou. La o fațadă, **Arată în 3D** întoarce camera să privească în direcția fațadei.

![Meniul de clic dreapta al secțiunii în navigator](shots/section-3d-menu.webp)

![Secțiunea B-B văzută în 3D](shots/section-3d.webp)

Ca să vezi din nou tot modelul, apasă **×** pe eticheta **Secțiune** din josul vederii 3D sau alege din nou **Arată în 3D**.

![Secțiunea B-B cu Hașuri: tăietura cum o tipărește planșa](shots/section-3d-hatches.webp)

### Cutia de secțiune

**Vedere → Cutie de secțiune** (scurtătura BX) taie vederea 3D după o cutie. Cutia pornește în jurul întregului model. Fiecare față are o săgeată la mijloc: trage de ea ca să muți fața. Fiecare element tăiat de cutie e închis pe fața aceea, strat cu strat, în materialele lui, iar fețele se întâlnesc curat la muchiile cutiei. **Taie la plan** pune capacul cutiei la tăietura planului nivelului activ (din domeniul lui de vedere), ca nivelul să se citească în 3D cum îl taie planul. **Încadrează** pune cutia din nou în jurul întregului model. Ca să rotești cutia, trage de mânerul rotund de lângă colțul ei de sus (din grad în grad; cu Shift, din 15° în 15°), sau selectează un perete și apasă **Aliniază**: cutia se rotește pe direcția peretelui. **Hașuri** desenează tăieturile cutiei ca pe planșă, fiecare material cu umplutura și hașura lui de secțiune, în locul materialelor. O vedere 3D salvată își păstrează cutia de secțiune: salvează vederea cu cutia pornită, sau deschide o vedere salvată și pune-i cutia, iar cutia e acolo de fiecare dată când deschizi vederea. **×** pe eticheta **Cutie de secțiune** oprește cutia.

![Cutia de secțiune tăiată la planul parterului, cu fața de est trasă înăuntru](shots/section-box.webp)

![Aceeași cutie cu Hașuri: tăieturile cum le desenează planșa](shots/section-box-hatches.webp)

### Câte un etaj

Ca să te uiți în interiorul clădirii, arată doar unele dintre etajele ei. Clic dreapta pe un nivel în navigator și alege **Arată doar acest nivel în 3D** sau **Arată acest nivel și cele de dedesubt în 3D**, ca să vezi clădirea tăiată la acel nivel. Nivelul devine cel activ, iar vederea 3D urmează nivelul activ: dă clic pe alt nivel în navigator ca să urci sau să cobori un etaj.

![Meniul de clic dreapta al unui nivel în navigator](shots/storeys-3d-menu.webp)

![Doar parterul în 3D](shots/storeys-3d.webp)

**Vedere → Etaje afișate** sau proprietățile vederii 3D stabilesc restul:

- **Arată**: toate etajele, etajele bifate, doar etajul activ sau etajul activ și cele de dedesubt.
- **Fără etaj**: ce nu stă pe niciun etaj (amplasamentul, terenul, fișierele legate) se arată sau se ascunde.
- **Secționat deasupra nivelului**: modelul e tăiat la atât deasupra celui mai înalt etaj afișat, la început 1,20 m, cum îl taie un plan. Fiecare element tăiat acolo e închis pe tăietură, strat cu strat, în materialele lui, ca o secțiune arătată în 3D.

![Parterul tăiat la 1,20 m: pereții închiși, strat cu strat](shots/storeys-3d-cut.webp)

Etajele ascunse nu se desenează și nu le poți selecta, încadra sau călca pe ele. Ce taie o tăietură (cea de deasupra etajelor afișate, o secțiune arătată în 3D, cutia de secțiune) nu mai e nici în calea plimbării: treci pe lângă partea de sus a unui perete pe care tăietura a înlăturat-o, iar capacele de pe tăietură închid ce deschide ea. **Fișier → Exportă 3D…** ia vederea 3D așa cum o arată (vezi [Import și export](import-export.md)). O vedere 3D salvată își păstrează propria alegere. Eticheta **Etaje** din josul vederii 3D spune ce se vede; **×** de pe ea arată din nou toate etajele.

### Fără finisaje sau doar miezurile

O vedere 3D poate arăta doar structura clădirii, cum o arată un plan de structură. În proprietățile vederii 3D (fără nimic selectat) sau în **Vedere → Setările vederii** cu vederea 3D în față, câmpul **Straturi** arată:

- **toate straturile**: pereții, planșeele și fundațiile cum sunt construite;
- **fără finisaje**: fără straturile din materiale de finisaj (tencuială, vopsea, faianță, gresie, rigips, pardoseli sau un material cu categoria Finish în Manage → Materials); izolația rămâne;
- **doar miezul**: doar stratul marcat ca miez (zidăria, placa de beton).

![O casă în 3D doar cu miezul: pereții sunt zidăria lor, planșeele placa lor](shots/layers-3d-core.webp)

Fiecare strat păstrat rămâne un solid închis, în materialul lui, așa că o tăietură sau cutia de secțiune îl închide ca înainte. Ușile și ferestrele rămân în golurile lor; un șpalet finisat dispare odată cu finisajele peretelui. O vedere 3D își ia straturile din stilul ei, ca un plan: cu **Disciplina** Structure arată doar miezurile; din **Straturi** alegi altfel, iar butonul de lângă câmp o întoarce la stil. Eticheta **Straturi afișate** din josul vederii 3D spune ce lipsește; **×** de pe ea arată din nou toate straturile. Straturile ascunse nu se selectează și nu te opresc la plimbare, iar **Fișier → Exportă 3D…** cum o arată vederea exportă aceleași straturi.

### Plimbare printr-o încăpere

Într-un plan, clic dreapta într-o încăpere și alege **Plimbă-te în 3D**. Vederea 3D te pune în încăpere, la 1,60 m deasupra pardoselii, privind de-a lungul laturii ei celei mai lungi.

![Meniul de clic dreapta al unei încăperi în plan](shots/room-walk-menu.webp)

![În camera de zi](shots/room-walk.webp)

- Trage cu butonul stâng ca să privești în jur.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> sau săgețile te poartă înainte, înapoi și în lateral. Ține <kbd>Shift</kbd> ca să alergi. <kbd>Spațiu</kbd> sare.
- Rotița mouse-ului face un pas înainte sau înapoi.

Mergi cum ar merge un om. Pereții, stâlpii, mobilierul și ferestrele închise te opresc, iar tu aluneci de-a lungul lor. Stai pe pardoseli, urci și cobori scări și rampe și cazi dacă pășești peste o margine. Ușile îți sunt deschise: treci prin golurile lor. Te oprește doar ce desenează vederea 3D: elementele ascunse, etajele pe care nu le arată și ce taie tăieturile ei nu-ți stau în cale.

![Spre ușa dormitorului](shots/room-walk-door.webp)

![Pe scară în sus](shots/room-walk-stair.webp)

**Zbor** din eticheta plimbării, sau <kbd>F</kbd>, te lasă să zbori: fără gravitație, treci prin pereți și planșee. <kbd>E</kbd> și <kbd>Q</kbd> urcă și coboară. Apasă din nou <kbd>F</kbd> ca să mergi; cobori pe pardoseala de sub tine.

<kbd>Esc</kbd> sau **Ieși din plimbare** aduce înapoi camera pe care o aveai.

### Realist, cu soarele

Vedere → **Calitate afișare** stabilește cum se desenează vederea 3D, pe acest calculator:

- **Desen**: paleta planșei și o lumină egală, cum a desenat Lapin dintotdeauna. Rapid pe orice calculator.
- **Mediu**: umbrele soarelui, umbră în colțurile unde se întâlnesc fețele și lumină din cer.
- **Realist**: cerul în spatele modelului și un teren sub el (unde proiectul nu are teren), și sticlă prin care trece lumina. Pentru prezentări, pe o placă grafică bună.

![O casă în vederea realistă](shots/view-realistic.webp)

Soarele e al proiectului: unde stă pentru locul proiectului (Gestionare → **Amplasament**) într-o zi, la o oră. Vedere → **Soare și umbre** stabilește ziua (echinocțiul și solstițiile sunt la un clic) și ora, rulează ziua și aprinde soarele și în vederea de desen. Fără sistem de coordonate și punct de reper, locul e Bucureștiul. Fusul orar urmează longitudinea locului, cu ora de vară europeană în Europa.

![Soare și umbre](shots/sun-dialog.webp)

Vederea realistă își ia câteva cifre (umbra din colțuri, calitatea umbrelor, culoarea soarelui) din [BimGo](https://github.com/aussieBIMguru/BimGo), de Aussie BIM Guru (MIT-0).

## Ce arată o vedere

Tabul **Vedere** setează ce arată vederea din față și cum arată.

**Grid** (<kbd>F7</kbd>) ascunde sau arată grila de fundal a planului, iar **Linii subțiri** (<kbd>TL</kbd>) desenează toate liniile de un pixel, ca să citești un desen încărcat. Amândouă sunt doar pentru ecran: planșele, PDF-ul și DXF-ul își păstrează peniţele, iar prinderea pe grilă rămâne. Grila de la sol din vederea 3D urmează același comutator.

![Tabul Vedere](shots/ribbon-view.webp)

- **Setările vederii.** Disciplina și stilul vederii, intervalul ei (cota de secționare și adâncimea) și grafica.
- **Stiluri de vedere.** Arhitectural, Structural (doar miezurile), Instalații sau al tău. Fiecare stil alege straturile desenate (toate, fără finisaje sau doar miezul) și cum se unesc materialele.
- **Vizibilitate** (<kbd>VV</kbd>) și **Grafică** (<kbd>VG</kbd>). Arată sau ascund categorii și le setează penițele, tipurile de linie și umpluturile.
- **Împarte** și **Fereastră.** Arată două vederi alăturate sau deschid o vedere într-o fereastră separată.
- **Calitate afișare.** Cum se desenează vederea 3D (**Desen**, **Mediu** sau **Realist**, păstrat pe acest calculator: vezi *Realist, cu soarele*) și cum desenează vederea 3D piesele cu multe triunghiuri (o instalație importată, mobilier, țevi) când sunt departe. **Echilibrat** desenează mai grosier o piesă îndepărtată doar unde diferența e sub un pixel. **Rapid** o lasă să se abată până la 3 pixeli, pentru modele mari și calculatoare mai lente. **Precis** desenează fiecare piesă așa cum e. Precizia ține de proiect. Modelul, desenele, exporturile și cantitățile nu devin niciodată mai grosiere: o curbă e făcută din bucăți drepte care se abat de la ea cel mult un milimetru.
- **Soare și umbre.** Soarele proiectului într-o zi, la o oră, luminând vederea 3D cu umbrele lui (vezi *Realist, cu soarele*).

![Calitate afișare](shots/display-quality.webp)
