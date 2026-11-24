---
title: Calcul structural
order: 52
summary: Rulează un calcul static liniar al cadrului sub încărcările tale, citește diagramele eforturilor și deformata în 3D, stabilește articulațiile capetelor barelor, calculează modurile proprii și răspunsul la cutremur și verifică barele, plăcile și pereții după norme.
---

**Rulează calculul** din grupul **Încărcări** al tabului **Structură** (și din dialogul Încărcări) calculează barele, plăcile și pereții modelului sub încărcările și combinațiile din [Încărcări](loads.md) și arată eforturile din ele.

![Grupul Încărcări, cu Rulează calculul](shots/ribbon-analysis.webp)

> Rezultatele sunt **orientative**. Încărcările, combinațiile și calculul trebuie verificate de tine înainte să te bazezi pe ele.

## Ce se calculează

- **Barele** (stâlpi, grinzi, elemente de oțel și de lemn) intră cu rigiditatea lor, din material și secțiune. Modulul de elasticitate E al unei bare vine din materialul ei: din clasa de rezistență din nume (C25/30, S355, C24, GL24h, după Eurocoduri), din E = 210 000 MPa pentru orice oțel (200 000 MPa pentru oțelul-beton) sau din datele materialului: în **Gestionare → Materiale → Date**, alege **Modul de elasticitate E** din *Adaugă unul obișnuit…* (parametrul se numește Young's modulus) și scrie-l în MPa. Se citește și un parametru adăugat de tine, numit E, Young's modulus sau Modul de elasticitate, cu unitatea scrisă după valoare (`31 GPa`, `30000 MPa`, `4350 ksi`) sau după nume (`E (GPa)`), altfel dedusă din mărime (de la 500 în MPa, sub 500 în GPa, de la un milion în Pa); YoungModulus din Pset_MaterialMechanical e în pascali. Rezistențele se citesc la fel: parametri proprii numiți Yield strength, fy sau fyk (Limită de curgere) și Compressive strength, fck sau fc (Rezistență la compresiune), cu unitatea după valoare sau după nume, altfel în MPa (în Pa de la un milion); Pset_MaterialSteel.YieldStress și Pset_MaterialConcrete.CompressiveStrength din IFC, în pascali, au întâietate. O bară al cărei material nu are E este lăsată deoparte: calculul nu pornește și numește materialul și locul unde se dă E.
- **Planșeele și plăcile de beton** sunt calculate ca **elemente de placă**: fiecare este împărțită în elemente mici (o plasă, implicit 0,5 m) care se încovoaie și se întind, cu nodurile comune cu grinzile, stâlpii și pereții pe care îi întâlnesc. Placa își duce singură încărcările la ce o susține, prin rigiditatea ei și a lor. O grindă sub o placă își păstrează axa dedesubt și lucrează cu placa ca o grindă în T.
- **Pereții portanți de beton** sunt tot elemente de placă: stau pe fundațiile lor (un perete cu nimic modelat sub el este ținut pe toată talpa) și duc planșeele de pe ei, precum și vântul sau alte încărcări laterale în planul lor.
- **Pereții de zidărie** sunt și ei elemente de placă, liniar elastici și izotropi, cu valori după EN 1996-1-1, cum le ia CR 6-2013 (E = KE·fk, G = 0.4 E; vezi [Calculul seismic](#calculul-seismic)). Pereții din alte materiale sunt reazeme: țin plăcile și barele de pe ei, dar nu sunt calculați ca pereți.
- **Care pereți poartă**: hotărăște **LoadBearing** al peretelui (Pset_WallCommon, pe perete sau pe tipul lui). Bifat, peretele poartă; debifat, sau un perete al cărui tip IFC e perete despărțitor (PARTITIONING), nu poartă, iar greutatea lui trece pe planșeul de sub el ca încărcare din pereți despărțitori (vezi [Încărcări](loads.md)). Un perete care **nu spune nimic** poartă dacă e din zidărie sau beton, cu miezul de 10 cm sau mai mult: o notă îi enumeră, ca să poți debifa **LoadBearing** pe pereții despărțitori dintre ei. Cei mai subțiri și pereții din alte materiale rămân despărțitori, iar o notă îi enumeră pe cei subțiri.
- **Planșeele din alte materiale** (un planșeu de lemn) își trec încărcările barelor și pereților de sub ele după **regula de 45°**: bisectoarele colțurilor împart placa în zonele pe care le duce fiecare latură, iar fiecare devine o încărcare trapezoidală sau triunghiulară pe bara de dedesubt.
- **Acoperișurile cu șarpantă** trec încărcarea fiecărui căprior după **lățimea lui aferentă**, până la jumătatea distanței față de vecini; zăpada se ia pe proiecția orizontală.
- **Tijele și tiranții** (o bară plină rotundă care contravântuiește, un tirant sau un cablu) lucrează axial: sunt articulați la capete, iar greutatea lor merge la capete, deci nu se încovoaie ca grinzile. Câmpul **Bară cu efort axial** din proprietățile **Analiză** ale unei bare le schimbă: **Din secțiune și utilizare**, **Da (tijă sau tirant)** sau **Nu (grindă)**.
- **Grinzile de înălțimi diferite** care se întâlnesc la un stâlp își păstrează fiecare cota; stâlpul are un singur nod, iar grinzile sunt legate de el printr-un braț rigid.
- **Vântul pe fațade** merge la grinzi, rigle și marginile planșeelor de-a lungul lor; fâșia de la baza lor merge direct în teren și e listată separat de sumele barelor.
- **Greutatea proprie** a barelor se ia de-a lungul lor, iar încărcările desenate de tine și cele derivate merg acolo unde găsesc ceva dedesubt. O încărcare care nu găsește nimic este lăsată deoparte și raportată.

## Rularea

Alege **Rulează calculul**. Se deschide dialogul **Rezultatele calculului**:

![Dialogul Rezultatele calculului](shots/analysis-results.webp)

- Rezumatul: barele, elementele de placă, nodurile și necunoscutele calculului și cât a durat.
- Pentru fiecare caz, **suma încărcărilor** față de **suma reacțiunilor** și dacă se **echilibrează** (bifă) sau nu (cruce: nu te baza pe acele rezultate). Încărcările nelegate sunt date în kN.
- Ce a fost **lăsat în afara** calculului și de ce.
- Valorile **cele mai mari și cele mai mici** ale eforturilor și momentelor, deplasarea și săgeata maximă (cu raportul față de deschidere, L/300) ale cazului, combinației sau înfășurătorii alese; sub ele, valorile cele mai mari și cele mai mici pe **plăci și pereți**, și unde sunt.
- **Elemente de placă**: ce plăci și pereți sunt calculate ca plăci (**Plăci și pereți**, **Doar plăcile**, **Doar pereții** sau **Niciunul**: plăcile după regula de 45° și pereții ca reazeme) și dimensiunea **Plasei** în metri. O plasă mai mică este mai exactă și mai lentă; o piesă îngustă primește mereu cel puțin patru elemente pe lățime. O schimbare este un singur pas de anulare; **Rulează din nou** o preia.

**Rulează din nou**, **Arată diagramele** și **Închide fără diagrame** stau la baza dialogului, oricât ai derula. Notele calculului (cum s-a făcut și ce e de verificat) sunt strânse sub **Note (n)**; cele de verificat vin primele, cu roșu.

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

## Plăci și pereți

Când plăcile sau pereții sunt calculați ca elemente de placă, cardul are un al doilea rând de butoane, **Plăci și pereți**, iar elementele se colorează în benzi după mărimea aleasă (un al doilea clic pe ea scoate culorile):

![Momentul my al planșeului, în benzi](shots/analysis-slab.webp)

- **w**: deplasarea perpendiculară pe placă sau perete (în sus pozitivă pe o placă).
- **mx**, **my**: momentele încovoietoare pe direcțiile x și y ale plăcii (X și Y globale pe o placă; de-a lungul și în sus pe un perete), în kN·m pe metru. Pe o placă, **pozitiv înseamnă fața de jos întinsă** (în câmp), negativ fața de sus (peste reazem); pe un perete, fața din dreapta direcției lui.
- **mxy**: momentul de torsiune; **nx**, **ny**, **nxy**: eforturile în planul plăcii (întinderea pozitivă), în kN pe metru; **vx**, **vy**: forțele tăietoare transversale.

Legenda de pe card arată valorile fiecărei benzi și unitatea:

![Legenda curbelor de nivel](shots/analysis-legend.webp)

Aceleași benzi sunt desenate pe planul etajului plăcii, pentru ecran; planșele nu le tipăresc încă. Pentru o înfășurătoare, fiecare punct arată valoarea cu modulul mai mare dintre cea mai mică și cea mai mare.

## Calculul seismic

Secțiunea **Seismic** din dialogul **Rezultatele calculului** calculează modurile proprii de vibrație ale structurii, răspunsul la cutremur prin spectrul de răspuns, forțele tăietoare și deplasările relative de nivel, cu verificările normei. Are nevoie de valorile amplasamentului; până le dai, doar spune asta.

![Secțiunea Seismic](shots/analysis-seismic.webp)

**Parametrii.** Fiecare schimbare e un pas de anulare; **Dezactivează** îi șterge pe toți.

- **Normă**: **P100-1/2013** (implicit) sau **EN 1998-1**, cu valorile ei recomandate.
- **ag (g)**: accelerația terenului pentru proiectare a amplasamentului, ca fracțiune din g, și **TC (s)** (0,7, 1,0 sau 1,6 s) cu P100, sau **Tipul de teren** de la A la E și **Tipul spectrului** 1 sau 2 cu EN. **Acestea sunt ale tale**: ia-le din hărțile de zonare seismică ale normei; Lapin nu vine cu hartă.
- **Clasa de importanță** I–IV (factorul ei γ se vede) și **q**, factorul de comportare (1,5 implicit). Sub ele, un tabel dă factorii din P100-1/2013 Tabelul 8.10 pentru zidărie, de înmulțit cu αu/α1 al clădirii tale.
- **Combinarea modurilor** (CQC implicit, sau SRSS), **Direcțiile** (regula de 30 % implicit, sau SRSS) și **Diafragma**: o diafragmă rigidă se aplică doar nivelurilor fără planșeu-placă; un planșeu calculat ca placă o dă el singur. **Limita modurilor** e cel mai mare număr de moduri calculate.

**Modurile.** Tabelul dă perioada T și frecvența f ale fiecărui mod, masa lui efectivă pe X și pe Y și suma până la el. Se calculează moduri până se atinge 90 % din masă pe ambele direcții sau până la limită; dialogul te avertizează când limita oprește mai devreme.

**Spectrul.** Graficul desenează spectrul de proiectare Sd și spectrul elastic Se în funcție de perioadă, cu perioadele modurilor marcate pe spectrul de proiectare și perioadele de colț TB, TC, TD.

Notele analizei modale și seismice sunt strânse sub **Note (n)** al secțiunii.

**Nivelurile.** Pentru fiecare nivel: masa, forța tăietoare pe X și pe Y, deplasarea relativă de nivel elastică și verificările deplasării relative la starea limită de serviciu (SLS) și la cea ultimă (ULS, doar P100) față de limitele lor, bifate sau tăiate cu cruce (cea care nu trece e roșie). Forța tăietoare de bază apare împreună cu combinația seismică și e comparată cu metoda forțelor laterale, ca verificare. O coloană **θ** dă coeficientul de sensibilitate la deplasarea relativă de nivel θ = Ptot·dr/(Vtot·h) pe X și pe Y (P100-1/2013 §4.6.2.2, dr = q·de): până la 0,1 efectele de ordinul doi se neglijează (verde), până la 0,2 efectele seismice se înmulțesc cu 1/(1 − θ) (galben, cu factorul), până la 0,3 e nevoie de o analiză de ordinul doi, iar peste 0,3 structura este refuzată: rigidizați-o (roșu).

**Un mod în 3D.** **Animă** pe rândul unui mod (sau modul ales lângă **Animă** la baza dialogului) închide dialogul și arată forma lui oscilând în vederea 3D: plăcile și pereții ca suprafețe umbrite, barele ca tuburi, modelul în repaus transparent în spatele lor. Forma e dimensionată ca să se vadă: deplasarea ei cea mai mare e cam 7 % din mărimea modelului, iar **Scalează** pe card o micșorează sau o mărește (×0,25 până la ×4). Arată cum se mișcă structura, nu cu cât. Debifează **Arată nedeformat** ca să ascunzi modelul în repaus. **Stop** pe card o oprește și aduce înapoi cardul diagramelor cum era. Urmărește modelul când îl schimbi.

![Forma unui mod în 3D](shots/analysis-mode.webp)

**Combinația seismică.** Cu parametrii dați, selectorul oferă **Combinația seismică** printre înfășurători (G + ψ2 Q ± AEd, cea mai mică și cea mai mare valoare în fiecare punct), iar înfășurătoarea ultimă o cuprinde. Schimbarea unui parametru schimbă modelul, deci rezultatele se învechesc: **Rulează din nou**.

**Pereții de zidărie** sunt calculați ca plăci, cu valori luate după numele materialului (cărămidă, bloc ceramic, BCA, bloc de beton), orientative. Ca să dai valorile tale, pune pe material aceste parametri ai setului `Lapin_Masonry`: `UnitGroup` (clay1, clay2, aac, concrete1, concrete2), `fb`, `fm`, `fk` (MPa) și `KE`; sau `YoungModulus` din `Pset_MaterialMechanical`. Zidăria e liniară în această versiune: fără limitare la întindere, fără fisurare.

> Toate valorile de aici sunt orientative: ag și TC ale amplasamentului, q și numerele normei sunt ale tale de verificat.

## Verificări de calcul

După o rulare, secțiunea **Verificări** a dialogului **Rezultatele analizei** verifică barele, plăcile și pereții după norme, cu eforturile combinațiilor. Fiecare verificare este **orientativă**: valorile sunt cele recomandate de Eurocoduri (și **CR 6-2013** la zidărie), nu cele ale anexelor naționale, iar calculul semnat rămâne al inginerului.

![Secțiunea Verificări](shots/analysis-checks.webp)

**Ce se verifică, pe materiale**

- Barele de **oțel** (EN 1993-1-1; fy din marca din numele materialului, ca S355, sau din **Limită de curgere fy** în MPa în Gestionare → Materiale → Date): clasa secțiunii, rezistența la întindere, compresiune, încovoiere și forfecare, flambajul și flambajul lateral cu răsucire, și interacțiunea efortului axial cu încovoierea (Anexa A, metoda 1, cum alege anexa națională română; Anexa B, metoda 2, dacă proiectul o cere). Lungimile de flambaj presupun un cadru contravântuit: când modelul nu are pereți sau contravântuiri pe o direcție, notele o spun, iar lungimile stâlpilor le dai tu. Secțiunile de clasa 4 sunt spuse ca neverificate.
- Grinzile și stâlpii de **beton** (EN 1992-1-1; fck din clasa din numele materialului, ca C25/30, sau din **Rezistență la compresiune** în MPa în Gestionare → Materiale → Date, din care se ia și Ecm după EN 1992-1-1 Tabelul 3.1): încovoierea (armătura necesară), forfecarea și etrierii, armătura minimă și maximă, raportul deschidere/înălțime, iar la stâlpi efectele de ordinul 2 cu curbura nominală. **Plăcile și pereții** calculați ca plăci primesc armătura necesară pe fiecare strat, prin metoda Wood–Armer.
- Barele de **lemn** (EN 1995-1-1, clasa de rezistență din numele materialului, ca C24 sau GL24h): încovoiere, întindere, compresiune cu flambaj, forfecare și flambaj lateral, cu kmod după durata încărcării și clasa de serviciu (zăpada e implicit de scurtă durată; un amplasament montan o poate lua de durată medie).
- Pereții de **zidărie** (EN 1996-1-1 așa cum îl ia CR 6-2013): rezistența la efort axial la bază, la mijloc și la cap, forfecarea și încovoierea în plan, cu γM 2,2 (1,65 unde norma permite). Combinația seismică folosește aceleași rezistențe, cu eforturile ei.

**Tabelul elementelor.** Un rând pentru fiecare element (o grindă făcută din mai multe piese în calcul e un rând, cu piesa cea mai solicitată): **gradul de utilizare** (verificarea cea mai solicitată împărțită la rezistența ei, ca bară și număr; peste 1 nu se verifică și e roșu), **verificarea determinantă** cu clauza ei și **combinația determinantă**. Filtrele aleg un **Material** sau un **Etaj**. Un clic pe rând selectează elementul în model. **Plăcile și pereții** de beton urmează, cu armătura necesară pe fiecare strat, în cm²/m.

**Tabelul pereților de zidărie.** Pe fiecare etaj, fiecare perete cu direcția (X sau Y), lungimea, grosimea, eforturile **N**, **V** și **M** (ține cursorul pe o valoare pentru combinația din care vine), rezistențele **NRd**, **VRd** și **MRd**, gradul de utilizare și verificarea determinantă. Dedesubt, pe fiecare direcție, **ΣV** față de **ΣVRd**: sistemul de pereți al etajului luat în ansamblu.

![Pereții de zidărie ai unui etaj](shots/analysis-walls.webp)

**Valorile de calcul.** Valorile proiectului (acoperirea cu beton, diametrele barelor și ale etrierilor, fyk, fvk0 și γM ale zidăriei, clasa de serviciu a lemnului, metoda de **interacțiune la oțel** și durata **zăpezii la lemn**) sunt sub **Valorile de calcul ale proiectului**, în secțiunea Verificări; cele proprii ale unui element sunt în proprietățile lui (mai jos). Un câmp gol ia valoarea implicită; fiecare schimbare este un pas de anulare, iar **Rulează din nou** verifică cu ea.

**Culori după gradul de utilizare.** **Colorează modelul după gradul de utilizare** (aici, sau **Grad de utilizare** pe cardul de peste vederea 3D) colorează fiecare bară, placă și perete verificat de la verde (0) prin galben și portocaliu (1) la roșu (peste 1), și în gri ce nu a fost verificat; planurile unui etaj arată aceleași culori pe plăcile și barele lui. Cât timp e pornit, diagramele îi cedează locul.

![Modelul colorat după gradul de utilizare](shots/analysis-utilisation.webp)

![Pereți de zidărie colorați după gradul de utilizare](shots/analysis-utilisation-walls.webp)

**Armătura ca izolinii.** Butoanele **Armare** ale cardului (**asxBottom**, **asyBottom**, **asxTop**, **asyTop**) desenează armătura de care are nevoie o placă pe fiecare strat, în cm²/m, pe înfășurătoarea SLU.

**În proprietăți.** Selectează o bară, o placă sau un perete: secțiunea lui **Verificări de calcul** arată fiecare verificare cu clauza, gradul de utilizare și combinația determinantă (deschide una pentru valorile intermediare), notele și armătura necesară. La o bară are și valorile de calcul ale elementului: **factorii lungimii de flambaj** ky și kz (sau lungimile Lcr,y și Lcr,z în metri), **lungimea nerezemată lateral** LLT (o grindă sub o placă pe care o poartă e luată ca susținută), **clasa de serviciu** a lemnului și **schema deschiderii** grinzii de beton.

![Verificările și valorile de calcul ale unei grinzi](shots/analysis-design.webp)

> Verificările sunt orientative. Lapin ia valorile recomandate ale Eurocodurilor; valorile anexelor naționale, încărcările și valorile de calcul îți revine să le verifici înainte să te bazezi pe ele.

**Lungimi de flambaj din αcr.** Implicit, verificările la flambaj prin încovoiere și la încovoiere cu compresiune ale barelor din oțel și lemn folosesc lungimile date de tine: k·L, cu factorii ky și kz (sau Lcr,y și Lcr,z) ai barei. **Lungimi de flambaj**, la **Valorile de calcul ale proiectului**, le poate lua în schimb **Din αcr**; o bară își poate alege propria opțiune în proprietăți (gol urmează proiectul). Lungimea pe o axă este atunci Lcr = π√(EI/(αcr·NEd)), cu αcr al celui mai de jos mod de flambaj care încovoaie bara în jurul acelei axe (axa tare y-y, axa slabă z-z), aflat pentru combinația determinantă. Nu e niciodată mai scurtă decât k·L, iar cu **Ordinul doi (P-Δ)** pornit fiecare bară își păstrează k·L: deplasarea laterală e atunci în eforturi (EN 1993-1-1 5.2.2(3)b). Barele din beton doar o arată (EN 1992-1-1 5.8.3.2): verificarea își păstrează l0. Tabelul de verificări are o coloană **Lcr,y / Lcr,z** cu lungimile folosite și sursa lor (k, αcr sau dată); treci cu mouse-ul peste ea pentru ambele valori, αcr și mod. Proprietățile unei bare arată, pentru fiecare axă, k·L, lungimea din αcr și pe cea folosită. Rulează din nou după ce schimbi opțiunea.

![Tabelul cu k·L](shots/analysis-lcr-k.webp)

![Același tabel cu lungimile din αcr: utilizarea stâlpilor crește](shots/analysis-lcr-alpha.webp)

![Lungimile de flambaj ale unui stâlp în proprietăți](shots/analysis-lcr-props.webp)

> Lungimile din αcr pot fi mult mai mari decât k·L (aici lungimea stâlpilor pe axa slabă crește de la 3,27 m la 4,09 m, iar pe axa tare la 7,71 m, utilizarea de la 0,23 la 0,27; la hala din oțel a acestui ghid lungimea riglelor în planul cadrului ajunge la circa 38 m și utilizarea lor se dublează). Pentru halele din oțel calea mai bună este ordinul doi (P-Δ) cu imperfecțiunile, care nu cere lungime de flambaj pentru deplasarea laterală.

## Ordinul doi și flambajul

Două întrebări hotărăsc dacă un cadru poate fi calculat în ordinul întâi: cât de departe sunt încărcările lui de cele critice și cât adaugă deplasarea lui laterală. Secțiunea **Stabilitate** din dialogul **Rezultatele calculului** răspunde la prima, iar caseta **Ordinul doi (P-Δ)**, de lângă opțiunile plăcilor (opțiunile calculului), la a doua.

![Secțiunea Stabilitate](shots/analysis-stability.webp)

**Ordinul doi (P-Δ).** Cu caseta bifată, fiecare combinație statică se rezolvă separat, cu rigiditatea geometrică a barelor din eforturile lor axiale, iterat până când eforturile nu se mai schimbă. Tabelul arată, pentru fiecare combinație, iterațiile, dacă a convergit și **amplificarea**: cea mai mare deplasare orizontală față de valoarea ei de ordinul întâi. Cazurile de încărcare rămân de ordinul întâi. Pe lungimea unei bare momentul își păstrează forma de ordinul întâi între capete: flambajul propriu al barei e al verificării ei (EN 1993-1-1 6.3). O combinație instabilă sau un mecanism rămâne în afara înfășurătoarelor și a verificărilor, iar notele spun asta cu roșu.

**Imperfecțiuni globale.** Caseta **Imperfecțiuni globale (±X, ±Y)**, lângă Ordinul doi, adaugă fiecărei combinații la starea limită ultimă (și accidentale) imperfecțiunile de deplasare laterală din EN 1993-1-1 5.3.2. Funcționează cu sau fără ordinul doi, iar bifarea Ordinului doi o bifează și pe ea într-un proiect care nu a spus nimic despre imperfecțiuni (debifarea ei se ține minte). Înclinarea este φ = φ0·αh·αm, cu φ0 = 1/200, αh = 2/√h (h înălțimea, între 2/3 și 1) și αm = √(0,5(1 + 1/m)), m fiind numărul stâlpilor unui rând care poartă cel puțin jumătate din încărcarea lui medie. O bară la mai puțin de 10° de verticală și comprimată este un stâlp. Fiecare primește forțele orizontale echivalente: φ·NEd în cap și −φ·NEd în picior, NEd fiind efortul lui din rezultatul de ordinul întâi. Fiecare combinație se rezolvă de patru ori, **+X, −X, +Y și −Y**, iar rezultatele ei (diagramele, înfășurătoarele, verificările) sunt înfășurătoarea celor patru, arătate ca o singură combinație. Tabelul adaugă o coloană **Determinantă** cu varianta amplificării celei mai mari (cu cursorul deasupra le vezi pe toate patru), rândul de deasupra dă φ ca 1/N pe X și pe Y, cu m, h și αh, iar verificările numesc varianta determinantă în combinația lor. Betonul ia aceeași formă (EN 1992-1-1 5.2), iar lemnul urmează EN 1995-1-1 5.4.4. Acolo unde încărcările orizontale sunt cel puțin 0,15 din cele verticale, 5.3.2(4) ar permite omiterea imperfecțiunilor: notele spun asta, iar Lapin le păstrează, de partea siguranței. Încărcarea verticală a pereților-placă nu este înclinată: dați-o pe a lor ca încărcări acolo unde contează.

**Contravântuită de tabla cutată.** O pană de acoperiș sau o riglă de perete care poartă tablă cutată este ținută dreaptă de aceasta pe lățime, în planul tablei, așa că primele ei moduri de flambaj sunt adesea ale ei proprii, cu un α mult sub cel al cadrului. Când primele moduri din tabelul de flambaj sunt ale unei pane de acoperiș, sub el apare butonul **Tabla cutată contravântuiește pânele** (EN 1993-1-3 10.1); face același lucru ca bifarea **Contravântuită de tabla cutată** în proprietățile fiecărei pane sau rigle, pe care o poți face și pentru o bară sau o selecție. Calculul ține apoi fiecare element contravântuit drept între elementele pe care reazemă, în planul tablei lui (pe lățimea secțiunii), iar secțiunea Stabilitate spune câte elemente sunt contravântuite. Tabla nu leagă un element de altul (nu e diafragmă), iar verificările de flambaj ale elementelor își păstrează lungimile de flambaj. Găsiți din nou factorii critici după calcul, ca să vedeți cum se mută modurile.

![Modurile proprii ale pânelor, înainte ca tabla să le contravântuiască](shots/analysis-sheeting-before.webp)

![După: tabla ține pânele drepte](shots/analysis-sheeting-after.webp)

**Încărcările critice.** **Află încărcările critice** calculează, pentru fiecare combinație ultimă, factorul αcr care aduce încărcările ei la cele sub care cadrul flambează. Poate dura câteva secunde pe un model mare, de aceea rulează la cerere. Tabelul arată primii trei (α1, α2, α3), **αcr global** (primul mod pe care nicio bară singură nu îl ține în mai mult de jumătate: al cadrului; un α marcat * e flambajul propriu al unei bare, iar o valoare scrisă ≥ e o limită inferioară), **clasa** și **amplificatorul** 1/(1 − 1/αcr). Clasa urmează EN 1993-1-1 5.2.1(3):

- **αcr ≥ 10** (verde): ajunge calculul de ordinul întâi.
- **3 ≤ αcr < 10** (galben): efectele de ordinul doi trebuie luate, prin calcul de ordinul doi sau înmulțind efectele deplasării laterale cu amplificatorul.
- **1 ≤ αcr < 3** (roșu): e cerut calculul de ordinul doi.
- **αcr < 1** (roșu): încărcările depășesc pe cele critice, structura e instabilă.

Cel mai mic αcr e evidențiat. Alegeți o combinație și un mod, apoi **Arată modul de flambaj** ca să vedeți forma în 3D, oscilând ca modurile proprii (cardul are aceeași scară și Stop). Verificările oțelului spun când αcr e sub 10 și ordinul doi e oprit.

> Valorile sunt orientative: αcr e al barelor singure (plăcile nu au rigiditate geometrică), betonul e la rigiditatea nefisurată, iar judecata rămâne a inginerului.

## Reazeme elastice și bare care lucrează într-un singur sens

Selectați o fundație, o placă pe sol sau baza unui stâlp de oțel: proprietățile lor au o secțiune **Reazem de analiză**.

- **Arcuri**: o rigiditate pentru fiecare din cele șase grade de libertate (kx, ky, kz în kN/m; krx, kry, krz în kN·m/rad). Gol înseamnă cum îl ține reazemul, 0 înseamnă liber.
- **Mediu Winkler** (fundații și plăci): coeficientul de pat **ks** (kN/m³) al terenului. O fundație izolată ia kz = ks·B·L și rigiditățile de rotire corespunzătoare, o fundație continuă fiecare nod pe lungimea lui, o placă pe sol fiecare nod pe cota lui de arie. Lista de dedesubt dă valori tipice ale lui ks pe tipuri de teren, **doar orientative: ks e al geotehnicianului**.
- **Numai compresiune (se dezlipește)**: un reazem care ar trage se ridică de pe teren, iar combinația se rezolvă din nou fără el. Secțiunea arată rigiditatea primită de fiecare nod în ultima analiză.

Forțele din arcuri sunt reacțiuni. O placă ia doar ks, baza unui stâlp doar arcuri (nu are talpă).

![Reazemul de analiză al unei fundații](shots/analysis-support.webp)

Secțiunea **Calcul** a unei bare are **Lucrează în**: **Ambele sensuri**, **Numai întindere** (o contravântuire dintr-un X, un tirant) sau **Numai compresiune** (o șprais). O bară cu un singur sens care ar lucra invers e scoasă din combinația respectivă, iar tabelul Stabilitate numește **barele destinse** și **reazemele dezlipite** pe fiecare. Cazurile rămân liniare, cu toate barele și toate reazemele; la fel analiza modală. Fiecare schimbare e un pas de anulare.

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
