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

Dublu clic pe **{3D}** deschide vederea 3D. Trage cu butonul stâng ca să te rotești în jurul modelului. Planul-cheie din dreapta sus arată de unde privești. **Plan**, **Persp** și **Încadrează** schimbă camera. Ca să păstrezi o cameră, duplică vederea 3D.

### O secțiune în 3D

Clic dreapta pe o secțiune în navigator și alege **Arată în 3D**. Vederea 3D taie modelul după linia secțiunii. Păstrează partea spre care privește secțiunea, până la adâncimea ei, dacă are una. Fiecare element tăiat e închis acolo unde e tăiat, strat cu strat, fiecare strat în materialul lui: un perete își arată miezul de cărămidă și tencuielile, plin, cum e peretele. **Hașuri**, pe eticheta din josul vederii 3D, pune în schimb pe tăietură desenul secțiunii, cu umpluturile și hașurile de secțiune pe care le tipărește o planșă. Cotele, semnele de nivel, etichetele și textele ei stau în același plan, la fel de mari și în același loc ca în secțiune. Liniile își păstrează grosimea peniței și modelul complet de linie întreruptă. Cele desenate lângă clădire stau în aer, lângă tăietură. **Adnotări**, pe eticheta din josul vederii 3D, le ascunde sau le arată din nou. La o fațadă, **Arată în 3D** întoarce camera să privească în direcția fațadei.

![Meniul de clic dreapta al secțiunii în navigator](shots/section-3d-menu.webp)

![Secțiunea B-B văzută în 3D](shots/section-3d.webp)

Ca să vezi din nou tot modelul, apasă **×** pe eticheta **Secțiune** din josul vederii 3D sau alege din nou **Arată în 3D**.

![Secțiunea B-B cu Hașuri: tăietura cum o tipărește planșa](shots/section-3d-hatches.webp)

### Cutia de secțiune

**Vedere → Cutie de secțiune** (scurtătura BX) taie vederea 3D după o cutie. Cutia pornește în jurul întregului model. Fiecare față are o săgeată la mijloc: trage de ea ca să muți fața. Fiecare element tăiat de cutie e închis pe fața aceea, strat cu strat, în materialele lui, iar fețele se întâlnesc curat la muchiile cutiei. **Taie la plan** pune capacul cutiei la tăietura planului nivelului activ (din domeniul lui de vedere), ca nivelul să se citească în 3D cum îl taie planul. **Încadrează** pune cutia din nou în jurul întregului model. **×** pe eticheta **Cutie de secțiune** oprește cutia.

![Cutia de secțiune tăiată la planul parterului, cu fața de est trasă înăuntru](shots/section-box.webp)

### Plimbare printr-o încăpere

Într-un plan, clic dreapta într-o încăpere și alege **Plimbă-te în 3D**. Vederea 3D te pune în încăpere, la 1,60 m deasupra pardoselii, privind de-a lungul laturii ei celei mai lungi.

![Meniul de clic dreapta al unei încăperi în plan](shots/room-walk-menu.webp)

![În camera de zi](shots/room-walk.webp)

- Trage cu butonul stâng ca să privești în jur.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> sau săgețile te poartă înainte, înapoi și în lateral. Ține <kbd>Shift</kbd> ca să alergi. <kbd>Spațiu</kbd> sare.
- Rotița mouse-ului face un pas înainte sau înapoi.

Mergi cum ar merge un om. Pereții, stâlpii, mobilierul și ferestrele închise te opresc, iar tu aluneci de-a lungul lor. Stai pe pardoseli, urci și cobori scări și rampe și cazi dacă pășești peste o margine. Ușile îți sunt deschise: treci prin golurile lor.

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
