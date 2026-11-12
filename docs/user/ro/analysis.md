---
title: Calcul structural
order: 52
summary: Rulează un calcul static liniar al cadrului sub încărcările tale, citește diagramele eforturilor și deformata în 3D și stabilește articulațiile capetelor barelor.
---

**Rulează calculul** din grupul **Încărcări** al tabului **Structură** (și din dialogul Încărcări) calculează barele modelului sub încărcările și combinațiile din [Încărcări](loads.md) și arată eforturile din ele.

![Grupul Încărcări, cu Rulează calculul](shots/ribbon-analysis.webp)

> Rezultatele sunt **orientative**. Încărcările, combinațiile și calculul trebuie verificate de tine înainte să te bazezi pe ele.

## Ce se calculează

- **Barele** (stâlpi, grinzi, elemente de oțel și de lemn) intră cu rigiditatea lor, din material și secțiune. O bară al cărei material nu are modul de elasticitate este lăsată deoparte și listată.
- **Pereții portanți** sunt reazeme: țin barele de pe ei, dar nu sunt calculați ca pereți.
- **Planșeele și plăcile** nu duc încărcarea ca plăci; își trec încărcările barelor și pereților de sub ele după **regula de 45°**: bisectoarele colțurilor împart placa în zonele pe care le duce fiecare latură, iar fiecare devine o încărcare trapezoidală sau triunghiulară pe bara de dedesubt.
- **Acoperișurile cu șarpantă** trec încărcarea fiecărui căprior după **lățimea lui aferentă**, până la jumătatea distanței față de vecini; zăpada se ia pe proiecția orizontală.
- **Greutatea proprie** a barelor se ia de-a lungul lor, iar încărcările desenate de tine și cele derivate merg acolo unde găsesc ceva dedesubt. O încărcare care nu găsește nimic este lăsată deoparte și raportată.

## Rularea

Alege **Rulează calculul**. Se deschide dialogul **Rezultatele calculului**:

![Dialogul Rezultatele calculului](shots/analysis-results.webp)

- Rezumatul: barele, nodurile și necunoscutele calculului și cât a durat.
- Pentru fiecare caz, **suma încărcărilor** față de **suma reacțiunilor** și dacă se **echilibrează** (bifă) sau nu (cruce: nu te baza pe acele rezultate). Încărcările nelegate sunt date în kN.
- Ce a fost **lăsat în afara** calculului și de ce.
- Valorile **cele mai mari și cele mai mici** ale eforturilor și momentelor, deplasarea și săgeata maximă (cu raportul față de deschidere, L/300) ale cazului, combinației sau înfășurătorii alese.

**Arată diagramele** închide dialogul și arată rezultatele în vederea 3D.

## Diagramele în 3D

Un card peste vederea 3D alege ce se desenează:

![Momentul încovoietor al înfășurătorii la starea limită ultimă](shots/analysis-moments.webp)

- **Ce**: un caz de încărcare, o combinație sau **înfășurătoarea** combinațiilor la starea limită ultimă (SLU) sau de serviciu (SLS): valoarea cea mai mare și cea mai mică în fiecare punct.
- **Mărime**: **N** efortul axial (întinderea pozitivă), **Vy** și **Vz** forțele tăietoare, **T** torsiunea, **My** și **Mz** momentele încovoietoare (**Mz** este axa tare a unei grinzi orizontale, pozitiv cu fibra întinsă jos) sau forma **Deformată**.
- **Scară**: mărimea diagramei pe ecran; modelul în sine nu se schimbă.
- **Reacțiuni**: săgeți la reazeme cu valorile lor în kN.

Fiecare bară are linia de bază, curba, hașuri între ele și valorile la extreme; roșu înseamnă pozitiv, albastru negativ. Momentele se desenează pe partea în care bara este întinsă.

![Forma deformată](shots/analysis-deformed.webp)

## Articulațiile capetelor

Selectează un stâlp, o grindă sau un element: în proprietățile lui este o secțiune **Calcul** cu articulațiile **Început (i)** și **Sfârșit (j)**.

![Secțiunea Calcul a unei grinzi](shots/analysis-bar.webp)

- **Implicit**: ce spune îmbinarea (o eclisă, un guseu sau o contravântuire articulează elementul), un element de lemn al acoperișului articulat pe axa tare, orice altă bară încastrată.
- **Încastrat**: capătul preia momente. **Articulat**: se rotește liber în jurul ambelor axe de încovoiere.
- **Personalizat**: șase căsuțe, câte una pentru fiecare libertate (deplasarea pe bară, pe fiecare axă de forfecare, torsiunea, rotirea în jurul fiecărei axe de încovoiere); o libertate bifată este eliberată.

O schimbare este un singur pas de anulare.

## Când se schimbă modelul

Rezultatele se calculează din modelul așa cum este și nu se salvează. Când schimbi modelul, diagramele dispar din vedere și cardul spune asta:

![Cardul după o schimbare](shots/analysis-pick.webp)

**Rulează din nou** calculează noul model.

## Un mecanism

Dacă structura se poate mișca fără rezistență (prea multe articulații sau o parte pe care nimic n-o ține), calculul se oprește și spune unde:

![Un mecanism](shots/analysis-mechanism.webp)

Numește nodul, libertatea și barele în cauză; **Selectează elementele implicate** le selectează ca să le poți corecta, de obicei încastrând un capăt sau adăugând un reazem.
