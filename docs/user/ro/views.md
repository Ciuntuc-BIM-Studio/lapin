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

Selectează secțiunea în navigator ca să-i setezi scara, adâncimea, disciplina și stilul în panoul de proprietăți.

## Fațade

**+** lângă **Fațade** face dintr-o dată cele patru fațade ale clădirii.

![Fațada de sud](shots/elevation-view.webp)

## Vederi 3D

<!-- video section-3d -->

Dublu clic pe **3D** deschide vederea 3D. Trage cu butonul stâng ca să te rotești în jurul modelului. Planul-cheie din dreapta sus arată de unde privești. **Plan**, **Persp** și **Încadrează** schimbă camera. Ca să păstrezi o cameră, duplică vederea 3D.

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

## Ce arată o vedere

Tabul **Vedere** setează ce arată vederea din față și cum arată.

![Tabul Vedere](shots/ribbon-view.webp)

- **Setările vederii.** Disciplina și stilul vederii, intervalul ei (cota de secționare și adâncimea) și grafica.
- **Stiluri de vedere.** Arhitectural, Structural (doar miezurile), Instalații sau al tău. Fiecare stil alege straturile desenate (toate, fără finisaje sau doar miezul) și cum se unesc materialele.
- **Vizibilitate** (<kbd>VV</kbd>) și **Grafică** (<kbd>VG</kbd>). Arată sau ascund categorii și le setează penițele, tipurile de linie și umpluturile.
- **Împarte** și **Fereastră.** Arată două vederi alăturate sau deschid o vedere într-o fereastră separată.
