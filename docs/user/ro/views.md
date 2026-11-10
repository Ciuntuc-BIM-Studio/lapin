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

**Dimensiunea marcatorilor.** **Gestionare → Standarde de desen → Proiect** stabilește cât de mari se desenează marcatorii, în milimetri pe hârtie, separat pentru secțiuni și fațade: **Dimensiune marcator secțiune** (săgețile unei secțiuni au 0,7 din ea) și **Text marcator secțiune**, **Dimensiune marcator fațadă** (diametrul cercului fațadei) și **Text marcator fațadă**; 10 mm și înălțimea stilului de text standard până nu le stabilești. Un proiect care avea o singură dimensiune a marcatorilor o păstrează pentru amândouă. O secțiune sau o fațadă cu mărime proprie are **Dimensiune marcator (mm)** și **Text marcator (mm)** în proprietăți; goale, ia valorile proiectului. Motorul desenează marcatorii, așa că planurile, secțiunile pe care le taie, planșele, PDF-ul și DXF-ul îi urmează.

**Mutarea unei secțiuni.** Selectează secțiunea într-un plan și mut-o cu **Mută** (sau trage-o de linia ei): ca în Revit, taie unde o pui, iar rotită sau oglindită privește în sensul rotit sau oglindit. Fiecare cap (săgeata și numele ei) are propriul grip: trage-l ca să muți capul de la capătul liniei, de exemplu ca să nu stea peste o cotă, iar o linie de legătură subțire îl unește la loc; secțiunea taie la fel. <kbd>Ctrl</kbd>/<kbd>⌘</kbd>-clic pe grip pune capul înapoi. O secțiune de-a curmezișul sau de-a lungul unui element urmează elementul și se mută odată cu el.

Fiecare secțiune stă pe un **strat de marcatori** (**Sections** la început; **Strat de marcator** din proprietățile secțiunii îl schimbă), iar fiecare vedere alege ce marchează. **Vedere → Secțiuni…** listează secțiunile proiectului pe straturi, pentru vederea din față, cu o bifă pe strat și pe secțiune doar pentru această vedere, stratul fiecărei secțiuni (unul existent sau **Strat nou…**) și în câte vederi e ascunsă fiecare. O planșă marchează ce marchează vederile ei.

O secțiune tăiată transversal printr-o grindă sau un stâlp (**Secțiune aici**, **Secțiune transversală** din tabul Armare simplă) are și ea grip-uri în plan: trage oricare capăt de-a lungul liniei ca să dai părții aceleia mai multă sau mai puțină margine, trage mijlocul de-a lungul elementului ca să muți stația la care taie. Fereastra secțiunii urmează.

![Vedere → Secțiuni… pentru fațada de sud](shots/sections-dialog.webp)

## Fațade

**+** lângă **Fațade** face dintr-o dată cele patru fațade ale clădirii.

![Fațada de sud](shots/elevation-view.webp)

Fiecare plan marchează fiecare fațadă din fața clădirii, ferit de capetele secțiunilor de acolo: o linie subțire punct-linie de-a curmezișul direcției în care privește și, în mijlocul ei, un cerc a cărui săgeată neagră arată spre clădire, cu numele fațadei în spatele ei. Clic pe marcator selectează fațada, dublu-clic o deschide. **Marcaje de fațadă** din **Vizibilitate / Grafică** (<kbd>VV</kbd>) le ascunde într-o vedere.

**Cât de departe ajunge o fațadă.** Selectează fațada într-un plan: linia ei are câte un grip la fiecare capăt. Trage unul de-a lungul liniei ca să lungești sau să scurtezi fațada pe partea aceea; fațada desenează atunci doar ce se află între cele două capete, iar planșele ei și PDF-ul urmează. Fiecare tragere este un pas de anulare. Până nu le tragi, linia cuprinde toată clădirea și crește odată cu ea.

**Mutarea marcajului unei fațade.** Trage gripul cercului sau selectează fațada și folosește **Mută**: marcajul merge unde îl pui, mai departe de clădire sau de-a lungul liniei lui, într-un singur pas de anulare. O fațadă Lapin privește mereu toată clădirea din exterior, așa că mutarea marcajului ei nu schimbă niciodată ce arată (în Revit linia marcajului este și locul de unde fațada începe să vadă; aici întinderea ei are gripurile ei, iar o tăietură în interiorul clădirii este o secțiune). O fațadă privește spre nord, sud, est sau vest, deci nu se rotește și nu se oglindește. O fațadă sau o secțiune fixată (**Modifică → Fixează**) nu se mută până nu e eliberată.

![Marcajul unei fațade în plan, mutat mai departe de casă, cu linia scurtată și capul unei secțiuni mutat de la capătul ei](shots/elevation-marker.webp)

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

Până alegi una, hașura de fațadă a unui material vine **după nume**, ca hașura lui de secțiune, iar **Gestionare → Materials** arată care (*după nume (BRICK)*): cărămida și zidăria BRICK, piatra STONE, lemnul BOARDS, placările metalice VERTICAL, țigla, șindrila și ardezia HORIZONTAL, plăcile ceramice TILES, pietrișul și pământul ca în secțiuni. Izolația, membranele, tencuiala, vopseaua și betonul nu primesc niciuna: BATT-ul izolației e o hașură de secțiune, nu se propune niciodată pentru o suprafață (aleasă de mână, se desenează). Alege **none** ca să nu se deseneze nimic. O secțiune desfășurată (în trepte, curbă sau după un element) hașurează fiecare bucată a ei între capetele bucății și îi desenează la fel umbrele de soare. **Surface hatches** din stilul vederii (**Vedere → View settings**, **Gestionare → View styles**) le oprește pentru vederi întregi: pornit în Architectural, oprit în Structural, MEP și Coordination. Planurile își păstrează hașurile lor de secțiune și de plan. Învelitoarea unui acoperiș se hașurează după materialul ei, deci un acoperiș de țiglă își arată asizele pe pantele pe care vederea le vede și niciuna pe cele întoarse.

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

## Modelare în 3D

Vederea 3D e un loc în care modelezi, nu doar privești: elementele se mută și se copiază acolo ca în plan, prinse de colțurile, muchiile și fețele celorlalte elemente.

### Mutare și copiere în 3D

Selectează elementele, apoi **Move** (M) sau **Copy** (CO) cu vederea 3D în față. Dă clic pe punctul de bază: un colț, o muchie sau o față a ceea ce muți. Cursorul spune de ce se prinde: **Endpoint** (capăt), **Midpoint** (mijloc), **On edge** (pe muchie), **On face** (pe față), sau **On level** (pe nivel) în gol; ce stă în spatele feței de sub cursor nu e luat. Liniile unui DXF importat, arătate în 3D, prind și ele. Apoi dă clic unde ajunge, de exemplu pe colțul unei plăci: selecția urmează cursorul în 3D, și în sus și în jos, și e pusă acolo. Din punctul de bază se deduc axele roșie, verde și albastră când cursorul merge de-a lungul uneia; → ← ↑ blochează axa roșie, verde sau albastră (aceeași tastă eliberează), iar Shift ține axa dedusă. Cu o axă blocată, un colț peste care treci e adus pe axă, ca mutarea să se alinieze cu el fără să ajungă acolo.

Scrie o valoare oricând, fără să dai clic într-o căsuță: o lungime merge pe direcția cursorului (1200), iar dx,dy sau dx,dy,dz e chiar deplasarea (0,0,450 ridică cu 450 mm). Enter o aplică, Esc anulează. Copy copiază mai departe din același punct de bază până la Enter sau Esc.

Ce are o cotă proprie urcă sau coboară cu mutarea: baza unui perete, a unui stâlp sau a unui obiect, baza și vârful unei scări (urcarea ei rămâne), vârful unei grinzi, decalajul de sus al unei plăci, cosoroaba unui acoperiș; un solid Studio primește un pas de mutare. O ușă sau o fereastră alunecă de-a lungul peretelui ei.

Ce stă pe nivelul lui (un perete, o scară, un stâlp, o placă, o grindă, o balustradă, un acoperiș, o piesă de mobilier, o ușă sau o fereastră) își păstrează nivelul când mutarea se prinde de la un punct la altul: ajunge acolo doar în plan, așa că o scară mutată de colțul ei de sus pe colțul unei plăci nu urcă cei câțiva centimetri dintre structura scării și finisajul plăcii (vârful scării ajunge deja la fața finisajului nivelului la care urcă). Ca s-o muți în sus sau în jos, urmează sau blochează axa albastră (↑), ori scrie dx,dy,dz; eticheta spune când nivelul e păstrat. Fără nimic selectat, Move și Copy cer elementele: dă clic pe ele în 3D, apoi Enter.

Poți și să apeși pe un element selectat și să-l tragi: se mută la fel, din punctul apăsat, prins, și rămâne unde îl eliberezi. **View → Drag to move in 3D** oprește asta, ca o tragere pe selecție să rotească vederea.

![O scară mutată în 3D de colțul treptei de sus pe colțul plăcii](shots/model-3d-move.webp)

### Elemente puse în 3D

Orice unealtă care ia puncte în plan merge și în vederea 3D: pereți, pereți cortină, plăci, acoperișuri, tavane, goluri în placă, camere, stâlpi, grinzi, fundații, balustrade, plinte, mobilier, lifturi, drumuri, plante și celelalte. Punctele merg pe nivelul activ: cursorul se prinde de model ca la mutare (colțul unei plăci, capătul unei grinzi, mijlocul unei muchii, liniile unui DXF), iar punctul e luat drept dedesubt, pe nivel; o linie punctată arată unde, când prinderea e mai sus sau mai jos. Punctele și conturul lor se desenează pe nivel pe măsură ce lucrezi: linia unui perete, conturul unei plăci care se închide înapoi pe primul punct, un arc prin al treilea punct. Din ultimul punct se deduc axele roșie și verde; → și ← le blochează.

Scrie o valoare oricând: o lungime pleacă din ultimul punct pe direcția cursorului (3000, sau 3000<30 cu un unghi), lățime,înălțime a unui dreptunghi din primul colț. Enter o aplică, închide o placă sau termină o balustradă; Backspace ia ultimul punct înapoi; Esc renunță la puncte, iar a doua oară oprește unealta. O tragere rotește în continuare vederea, deci poți să te învârți în jurul modelului între două clicuri. Cipul de jos spune pe ce nivel merg punctele.

Ușile și ferestrele se pun unde dai clic pe fața unui perete în 3D, în locul de-a lungul peretelui pe care l-ai ales.

![Un perete început pe colțul plăcii în 3D, cu axa verde dedusă din început](shots/model-3d-wall.webp)

### Mânere și componente în 3D

Mânerele unui element selectat (capetele unui perete, colțurile unei plăci) se trag în 3D ca în plan și se prind ca la Move: întâi un colț, apoi un mijloc, apoi o muchie la îndemână, apoi axele roșie și verde prin punctul de unde pleacă mânerul, apoi o față. Punctul e marcat și numit lângă cursor și luat la înălțimea mânerului. Scrie o lungime sau un unghi în timp ce tragi, ca în plan; Enter pune mânerul acolo.

Trage o carte din **Catalog** sau o unealtă din ribbon pe vederea 3D: unealta pornește cu acel tip. O unealtă pusă printr-un clic (un stâlp, o fundație izolată, mobilier, un lift, o plantă) e pusă imediat unde o lași, prinsă de model. Lăsată, sau pusă cu un clic, pe placa altui nivel, elementul merge pe acel nivel: o masă pusă pe placa etajului ține de etaj, oricare ar fi nivelul activ.

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

### Vizibilitate / Grafică

**Vizibilitate** (<kbd>VV</kbd>) sau **Grafică** (<kbd>VG</kbd>) deschide Vizibilitate / Grafică pentru vederea din față: un plan, o secțiune, o fațadă sau o vedere 3D. Ce ascunde se ascunde și pe planșele vederii, în PDF și DXF, și la exportul 3D.

- **Categorii de model.** Fiecare categorie se arată sau se ascunde, cu culoarea și grosimea liniei, umplutura și hașura de secțiune. Săgeata de lângă o categorie îi deschide subcategoriile: **Miez**, **Finisaje**, **Izolație**, **Membrane** și **Alte straturi** ale unui perete, planșeu sau acoperiș (după materialul fiecărui strat și categoria lui din Gestionare → Materiale), **Șpaleții** unei uși sau ferestre. Debifează **Finisaje** sub **Pereți** și tencuiala dispare, cărămida rămâne. Armăturile, finisajele de încăpere, cofrajele, schelele, instalațiile, îmbinările metalice, lifturile și parasolarele sunt categorii proprii.
- **Categorii de adnotări.** Cotele, textele, etichetele, etichetele de încăpere, axele, nivelurile, marcajele de secțiune și de fațadă, detaliile, planurile de referință, limitele vederii și legendele, fiecare arătată sau ascunsă în această vedere.
- **Categorii importate.** Fiecare DXF importat în vedere, după fișierul lui: vizibil, halftone, și straturile lui unul câte unul (săgeata de lângă el). Un PDF sau o imagine de calchiat peste vedere se arată sau se ascunde tot aici.
- **Filtre.** Pe reguli, ca în Revit: **Filtru nou**, apoi regulile lui (clasa IFC, categoria, tipul, numele, materialul, nivelul, grupul, legătura, faza creării, tipul predefinit sau un parametru, ca *Rezistență la foc* sau *Pset_WallCommon.IsExternal*; este egal cu, conține, începe cu, mai mare, mai mic, are valoare…), toate sau oricare. Vederea folosește filtrele din lista ei, în ordine; primul care ia un element decide dacă e arătat. **Gestionare → Filtre de vedere** deschide această filă.
- **Legături.** Fiecare fișier legat, cu **Vizibil** și **Halftone** doar pentru această vedere. **Afișare** e **Ca vederea gazdă** (legătura ia categoriile vederii) sau **Personalizat**: atunci **Categorii…** îi dă legăturii categoriile, subcategoriile și clasele IFC ale ei în această vedere. Planul arhitectului ascunde armăturile legăturii de structură; al inginerului, finisajele legăturii de arhitectură.
- **Clase IFC.** Clasele elementelor proiectului, arătate sau ascunse.
- **Suprascrieri grafice.** O categorie ia și un **Model** de linie pentru liniile ei secționate și văzute, **Halftone** și o **Transparență** (în 3D, de la 0 opac la 95); o categorie de adnotări o **Culoare** și **Halftone**; un filtru o **Culoare**, un **Model**, **Halftone** și o **Transparență** pentru ce ia, primul filtru din listă dând fiecare câmp. **Nivel de detaliu** (grosier, mediu, fin) spune cât desenează simbolurile unui plan, altfel după scară.
- **Nivelul de dezvoltare.** Un fel de a privi modelul, niciodată luat din scară: o vedere desenează la nivelul pe care i-l dă stilul ei (Gestionare → Stiluri de vedere: 350, un stil de structură 400) sau la al ei (Vedere → Setările vederii, ori capul dialogului Vizibilitate / Grafică, și pentru o vedere 3D). **LOD 100** (volume) desenează pereții, planșeele și acoperișurile ca un singur contur hașurat ca miezul, elementele metalice ca o singură linie pe axa lor, iar o ușă sau o fereastră ca golul și deschiderea ei; **LOD 200** (straturi) adaugă straturile, iar tocurile și cercevelele ușilor și ferestrelor simplificate (fără baghete, fără feronerie); **LOD 300** (detalii) profilele lor întregi și detaliile (glafuri, solbancuri, ancadramente, praguri, pervazuri, feronerie, obloane, grilaje) și căptușelile golurilor; **LOD 350** (îmbinări) plăcile, șuruburile și ancorele îmbinărilor metalice; **LOD 400** (fabricație) barele de armătură ale unui model de structură legat și secțiunea reală a barelor metalice: racordări, raze la vârfuri, tălpi înclinate. Un proiect făcut înainte își păstrează desenele: fiecare vedere primește nivelul pe care i-l dădea scara (1:50 LOD 400, 1:100 LOD 200, 1:200 LOD 100), o vedere 3D LOD 400. Pereții și planșeele unui IFC legat sau importat se împart după seturile de straturi pe care i le dă fișierul, așa că **Finisajele**, **Izolația** și **Miezul** lor se ascund ca la un perete nativ, iar desenul doar cu miezuri al stilului Structural ajunge și la ele; tencuiala unui perete adusă ca piesă separată sau un finisaj dintr-un singur material merge cu **Finisajele**. Orice vedere 3D lasă deoparte **Armăturile** până le arăți.

Setările sunt ale vederii sau ale unui set cu nume, comun mai multor vederi (**Salvează ca set…**). Un stil de vedere poate numi un set (Gestionare → Stiluri de vedere): o vedere fără setări proprii ia setul stilului ei, ca șabloanele de vedere din Revit; **Fă-le ale acestei vederi** pornește setările ei de la el.

![Vizibilitate / Grafică: finisajele unui perete ascunse](shots/vg-categories.webp)

![O structură legată, personalizată într-o vedere](shots/vg-links.webp)

![Un filtru care ascunde ce iau regulile lui](shots/vg-filters.webp)

![Categorii de adnotări](shots/vg-annotations.webp)

![Un import DXF pe straturi](shots/vg-imports.webp)

![Culoarea și modelul unui filtru în plan](shots/vg-overrides.webp)

![Planul cu filtrul lui](shots/vg-plan.webp)

![Un plan la LOD 350, al stilului: straturile pereților și detaliile ferestrei](shots/detail-fine.webp)

![Același plan la LOD 100: un singur contur hașurat ca miezul](shots/detail-coarse.webp)

### Norii de puncte în vederi

Un nor de puncte legat (*Import și export*, Nori de puncte) se vede în toate vederile. Un **plan** desenează felia pe care o ia tăietura lui, 10 cm în jurul planului de tăiere, ca puncte sub pereți; o **secțiune** felia de imediat din spatele planului ei; o **fațadă** toate punctele spre care privește, cele mai apropiate în față. Punctele au culorile scanării, întunecate ca să se citească pe hârtie, și se tipăresc pe planșe și în PDF cum le arată ecranul. Punctele lor sunt puncte de snap: pereții desenați peste felia unei scanări ajung pe fețele ei. În **vederea 3D** uneltele de așezare și Move fac snap pe punctele norului ca pe colțuri. Un plan nou se deschide încadrat pe model, sau pe felia norului când nu are altceva; se desenează doar partea feliei aflată pe ecran, așa că scanarea unui cartier întreg rămâne rapidă cu zoom pe o singură clădire.

**Visibility / Graphics** (<kbd>VV</kbd>) are o filă **Point clouds**: pentru fiecare nor, doar în această vedere, **Shown**, **Halftone**, **Colours** (ca legătura, RGB, intensitate, cotă sau clasificare), **Slice**, felia pe care o desenează un plan sau o secțiune (cm, până la 100 m: fațadele dintr-o scanare aeriană cer o felie groasă), și **Classes…**: clasele ASPRS pe care le au punctele (teren, vegetație, clădire, zgomot…), fiecare vizibilă sau ascunsă. Fila unei vederi 3D hotărăște ce desenează 3D-ul. **Export 3D…** ia un eșantion din norii vizibili (cel mult 5 milioane de puncte) ca puncte în GLB și glTF când e bifat **Point clouds**; niciun alt export nu îi duce.

![Felia unei scanări în plan, un perete desenat peste ea](shots/point-cloud-plan.webp)

![Visibility / Graphics: vegetația unui nor ascunsă într-o vedere](shots/point-cloud-vg.webp)
