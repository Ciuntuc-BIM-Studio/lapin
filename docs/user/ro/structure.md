---
title: Structură
order: 50
summary: Stâlpi, grinzi și fundații, tipurile lor și cum se îmbină.
---

Tabul **Structură** are cadrele și fundațiile.

![Tabul Structură](shots/ribbon-structure.webp)

## Cadre

- **Stâlp** (<kbd>CL</kbd>): un clic unde stă. Un stâlp merge de la nivelul lui la nivelul de deasupra.
- **Grindă** (<kbd>BM</kbd>): un clic la fiecare capăt. **Grindă curbă** (<kbd>BC</kbd>) urmează un arc.
- **Stâlpi pe axe** pune câte un stâlp la fiecare intersecție a axelor. Axele le desenezi cu **Adnotări → Axă** (<kbd>GR</kbd>).

## Fundații

- **Fundație continuă** (<kbd>SF</kbd>) merge sub pereți. Straturile ei exterioare se îmbină la 45° în colțuri.
- **Fundație continuă curbă** (<kbd>CF</kbd>) urmează un arc.
- **Fundație izolată** (<kbd>PF</kbd>): dă clic sub un stâlp. Cât timp unealta e pornită, se agață de centrele stâlpilor nivelului planului și ai nivelului de deasupra.
- **Fundații izolate sub stâlpi** pune o fundație izolată sub fiecare stâlp selectat care nu are una, în centrul lui și rotită ca el. Fără stâlpi selectați, ia stâlpii nivelului planului (într-un plan de fundații, pe cei ai nivelului de deasupra).

Desenează fundațiile pe nivelul **Foundations** sau dă-le un plan propriu.

**O fundație continuă e doar betonul**, dacă nu ceri peretele. Cât timp **Fundație continuă** sau **Fundație continuă curbă** e pornită, bara liniei de comandă oferă **Doar fundația** (implicit: o talpă de beton de 600 × 400 mm, un tip cu profil făcut prima dată) sau **Cu perete** (soclul unui tip de fundație continuă deasupra, cu straturile lui) și tipul cu perete de folosit. Alegerea se ține minte. O fundație deja desenată se schimbă cu tipul ei în **Tipuri structurale**.

![Opțiunea uneltei fundație continuă în linia de comandă: doar fundația sau cu perete](shots/structure-strip-option.webp)

**În planuri.** O fundație stă sub planșeu, deci planul o desenează întrerupt în planul nivelului pe care stă, cum arată planul unei clădiri ce e dedesubt. Ca s-o vezi tăiată și văzută, folosește un **plan de fundații**: în browserul proiectului, **+** de lângă **Planuri** face unul pentru nivelul activ (tăiat la 0,50 m sub el, privind 2,50 m în jos).

**Desenând într-un plan de fundații.** Arată și nivelul de dedesubt, iar ce desenezi acolo cu o unealtă de placă, perete, stâlp sau grindă stă pe acel nivel (Fundații la un proiect nou, la −1,50); uneltele de fundații păstrează nivelul planului. O fundație izolată desenată acolo sub un stâlp urcă la baza stâlpului; fața de jos a unei fundații continue e cea a tipului ei sub nivel.

**O fundație izolată urcă la stâlpul ei.** O fundație izolată al cărei punct cade în secțiunea unui stâlp (la un milimetru) are fața de sus la baza stâlpului: nivelul stâlpului plus **Decalaj la bază**, treptele sub ea. Muți stâlpul, îi schimbi nivelul sau decalajul bazei, și fundația îl urmează. Sub stâlpi așezați unul pe altul îl urmează pe cel mai de jos; din doi la fel de jos, pe cel cu centrul mai aproape. O fundație fără stâlp deasupra stă pe nivelul ei, cu **Baza** tipului ei sub el. **Fundații izolate sub stâlpi** pune fiecare fundație în cel mai înalt nivel aflat la sau sub fața ei de jos (Fundații la un proiect nou), altfel în cel mai de jos. O fundație izolată importată dintr-un fișier IFC rămâne unde o pune fișierul.

În **Proprietăți** o fundație izolată își arată cotele **Sus** și **Jos** și stâlpul pe care îl urmează. Scrie o cotă **Sus** ca să-i dai una proprie: nu mai urmează stâlpul. **La baza stâlpului** o leagă din nou.

![Cota unei fundații izolate în Proprietăți: la baza stâlpului ei](shots/structure-pad-level.webp)

![Stâlpi, grinzi și fundații izolate în 3D](shots/structure-3d.webp)

## Tipuri și colțuri

**Tipuri structurale** setează secțiunile stâlpilor, grinzilor și fundațiilor: secțiuni de beton și profile metalice. **Secțiunea** unui stâlp e un **Dreptunghi**, un **Cerc** sau un **Poligon**, unul regulat, dat de **Laturi** și **Diametru** (prin colțuri) (un stâlpișor octogonal, cum îl poate avea un IFC importat). Dă clic pe un tip din listă ca să-i schimbi dimensiunea sau numele, apoi **Salvează tipul**: toate elementele de acel tip îl urmează; **Tip nou** te întoarce la crearea unui tip.

**Duplică** de lângă nume copiază tipul ca „Nume (2)”, iar **↑** **↓** îl mută în sus și în jos în listele tipurilor lui, ca la [Tipurile de pereți](walls.md#tipurile-de-pereți).

Grinzile și fundațiile se îmbină singure unde se întâlnesc, un colț la 45°. O grindă trasată din axul unui stâlp în axul altuia se oprește la fețele stâlpilor; lungimea, volumul și etrierii ei numără deschiderea liberă dintre ele. **La 45°**, **Cap la cap** și **Inversează** schimbă cum se întâlnesc două grinzi sau două fundații. **Preia proprietățile** (<kbd>MA</kbd>) copiază tipul și setările unui element pe altele, iar **Creează similar** (<kbd>CS</kbd>) începe desenarea unui alt element de același tip.

Orice alt profil vine din biblioteci: **Dintr-o bibliotecă de profile…** caută în toate (vezi [Biblioteci de profile](profiles.md)).

## Tipuri dintr-un tabel

**Importă…** sub lista tipurilor de stâlpi și grinzi citește un CSV sau un registru Excel, un rând un tip: numele, felul (grindă, stâlp sau buiandrug), profilul, ori înălțimea h și lățimea b pentru un dreptunghi, materialul și marcajul, iar pentru un buiandrug rezemarea, producătorul, codul și lungimile în care se face. Lapin ghicește fiecare coloană după antet, în engleză, română, germană, franceză sau cehă (*Název*, *Výška*, *Šířka*, *Uložení*…), și citește exportul unui producător cu coloane ca *height.value* și *height.unit*; dialogul arată ce a luat, iar tu poți da unui câmp altă coloană sau altă unitate. Profilul e căutat în proiect, în bibliotecile tale de profile și în catalogul Lapin (IPE 300, HEA 240…); un rând al cărui profil nu e în nicio bibliotecă apare ca necitit. Rândurile care nu spun felul sunt ce alegi tu (grinzi, stâlpi sau buiandrugi prefabricați). **Un tip pentru lungimile unei secțiuni** face din rândurile unei secțiuni, unui material și unui producător, ale căror nume diferă doar prin lungime, un singur tip cu toate lungimile de stoc și codul fiecăreia (*KP 7 - 100*, *KP 7 - 125*… devin *KP 7* cu 1000 și 1250); Lapin o bifează când fișierul are astfel de rânduri.

Previzualizarea arată fiecare tip ca **Nou**, **Există deja în proiect** (lăsat cum e) sau **Diferă**, cu ce diferă (*secțiune*, *lungimi*, *material*…); pentru acestea alegi, ca la tipurile de pereți, **Actualizează-l pe al proiectului** (elementele lui și buiandrugii care îl folosesc îl urmează), **Păstrează-le pe amândouă** (*Nume (2)*) sau **Sari peste**. Importul e un singur pas de anulat. **Exportă CSV** scrie tipurile în aceleași coloane, ca o listă să poată merge într-un tabel și să se întoarcă.

![Tipuri structurale: un tip de buiandrug prefabricat importat din tabelul unui producător, cu marca, rezemarea și lungimile de stoc](shots/structure-types-lintel.webp)

## Tipuri de grinzi ca buiandrugi

Un tip de grindă poate fi produsul unui buiandrug prefabricat. În **Tipuri de pereți** sau în configuratorul unei uși ori ferestre, **Buiandrugi → Prefabricat**, apoi săgeata: **Produs** arată întâi **Tipurile de grinzi**: buiandrugul ia secțiunea tipului (lățimea pe grosimea peretelui, înălțimea), cea mai scurtă dintre lungimile lui care acoperă golul și ambele rezemări (un tip fără lungimi se taie la lungime) și materialul lui, dacă regula nu numește altul. Rezemarea e a tipului: câmpul de rezemare o arată estompat, iar o valoare scrisă acolo e a regulii, până o ștergi. Ales în **Tipuri structurale**, un tip de grindă își arată **Marca**, **Rezemarea** și **Lungimile de stoc** (milimetri, separați prin spații sau |) ca să le schimbi; buiandrugii urmează tipul. Importul catalogului de buiandrugi al unui producător (butonul de lângă **Produs**) face din fiecare produs și un tip de grindă, așa că buiandrugii și tipurile de grinzi sunt o singură listă; produsele importate înainte merg mai departe.

![Buiandrugii unui tip de perete: prefabricați, tipul de grindă KP 7 ca produs](shots/lintel-beam-type.webp)

Buiandrugii rămân părți ale peretelui lor, ca până acum; în IFC fiecare e un *IfcBeam* LINTEL cu tipul său *IfcBeamType*, iar tabelul buiandrugilor îi listează după marcajul și numele tipului, cu codul lungimii pe care o ia fiecare.

## Fundații continue cu profil

Un tip de fundație continuă e fie cel standard, o elevație cu straturile ei pe o talpă, fie orice profil extrudat de-a lungul axului fundației, ca profilele de fundație din Revit. În **Tipuri structurale → Strip**, alegi **Formă: Profil** și un profil: unul din catalog (dreptunghiulară, în 2 sau 3 trepte, cu fața de sus înclinată, excentrică cu elevația la o margine, cu elevație), făcut cu dimensiunile lui, sau unul al proiectului (**Gestionare → Profile**, unde poți și să-i tastezi colțurile sau să citești un contur desenat). Punctul de inserție al profilului stă pe axul fundației, la cota **Jos** a tipului.

Fundațiile cu profil merg drept sau curb. Două care se întâlnesc într-un colț se taie pe bisectoare, iar una care se oprește în alta intră în ea și e tăiată de corpul ei, așa că fiecare treaptă se întâlnește cu treapta celeilalte. Planurile, secțiunile și fațadele taie corpul extrudat. Cantitățile dau lungimea, aria secțiunii și volumul, iar fețele ei verticale sunt cele pe care Organizarea de șantier le cofrează. În IFC una dreaptă e un IfcFooting STRIP_FOOTING al cărui corp e profilul extrudat (un IfcArbitraryClosedProfileDef), tăiat de planele colțurilor; una curbă merge ca fețele ei. Alegi **Standard** ca să te întorci la elevația pe talpă.

![Fundații continue în trepte sub o casă, tăiate pe bisectoare la colțuri](shots/footing-profile-3d.webp)

![Un tip de fundație continuă cu profil](shots/footing-profile-type.webp)

## O vedere de structură

Disciplina unei vederi decide ce arată. O vedere **Structură** arată doar miezurile portante, fără finisaje. Disciplina o alegi în proprietățile vederii. Vezi [Secțiuni, fațade și 3D](views.md).

## Încărcări

Grupul **Încărcări** are ce îi trebuie unui calcul structural: cazuri de încărcare, încărcări pe suprafață, liniare și punctuale, și combinațiile. Vezi [Încărcări](loads.md).
