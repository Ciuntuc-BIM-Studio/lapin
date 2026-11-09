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
- **Fundație izolată** (<kbd>PF</kbd>) stă sub un stâlp.

Desenează fundațiile pe nivelul **Foundations** sau dă-le un plan propriu.

**În planuri.** O fundație stă sub planșeu, deci planul o desenează întrerupt în planul nivelului pe care stă, cum arată planul unei clădiri ce e dedesubt. Ca s-o vezi tăiată și văzută, folosește un **plan de fundații**: în browserul proiectului, **+** de lângă **Planuri** face unul pentru nivelul activ (tăiat la 0,50 m sub el, privind 2,50 m în jos).

![Stâlpi, grinzi și fundații izolate în 3D](shots/structure-3d.webp)

## Tipuri și colțuri

**Tipuri structurale** setează secțiunile stâlpilor, grinzilor și fundațiilor: secțiuni de beton și profile metalice. **Secțiunea** unui stâlp e un **Dreptunghi**, un **Cerc** sau un **Poligon**, unul regulat, dat de **Laturi** și **Diametru** (prin colțuri) (un stâlpișor octogonal, cum îl poate avea un IFC importat). Dă clic pe un tip din listă ca să-i schimbi dimensiunea sau numele, apoi **Salvează tipul**: toate elementele de acel tip îl urmează; **Tip nou** te întoarce la crearea unui tip.

Grinzile și fundațiile se îmbină singure unde se întâlnesc, un colț la 45°. O grindă trasată din axul unui stâlp în axul altuia se oprește la fețele stâlpilor; lungimea, volumul și etrierii ei numără deschiderea liberă dintre ele. **La 45°**, **Cap la cap** și **Inversează** schimbă cum se întâlnesc două grinzi sau două fundații. **Preia tipul** (<kbd>MA</kbd>) copiază tipul unui element pe altele, iar **Creează similar** (<kbd>CS</kbd>) începe desenarea unui alt element de același tip.

Orice alt profil vine din biblioteci: **Dintr-o bibliotecă de profile…** caută în toate (vezi [Biblioteci de profile](profiles.md)).

## Fundații continue cu profil

Un tip de fundație continuă e fie cel standard, o elevație cu straturile ei pe o talpă, fie orice profil extrudat de-a lungul axului fundației, ca profilele de fundație din Revit. În **Tipuri structurale → Strip**, alegi **Formă: Profil** și un profil: unul din catalog (dreptunghiulară, în 2 sau 3 trepte, cu fața de sus înclinată, excentrică cu elevația la o margine, cu elevație), făcut cu dimensiunile lui, sau unul al proiectului (**Gestionare → Profile**, unde poți și să-i tastezi colțurile sau să citești un contur desenat). Punctul de inserție al profilului stă pe axul fundației, la cota **Jos** a tipului.

Fundațiile cu profil merg drept sau curb. Două care se întâlnesc într-un colț se taie pe bisectoare, iar una care se oprește în alta intră în ea și e tăiată de corpul ei, așa că fiecare treaptă se întâlnește cu treapta celeilalte. Planurile, secțiunile și fațadele taie corpul extrudat. Cantitățile dau lungimea, aria secțiunii și volumul, iar fețele ei verticale sunt cele pe care Organizarea de șantier le cofrează. În IFC una dreaptă e un IfcFooting STRIP_FOOTING al cărui corp e profilul extrudat (un IfcArbitraryClosedProfileDef), tăiat de planele colțurilor; una curbă merge ca fețele ei. Alegi **Standard** ca să te întorci la elevația pe talpă.

![Fundații continue în trepte sub o casă, tăiate pe bisectoare la colțuri](shots/footing-profile-3d.webp)

![Un tip de fundație continuă cu profil](shots/footing-profile-type.webp)

## O vedere de structură

Disciplina unei vederi decide ce arată. O vedere **Structură** arată doar miezurile portante, fără finisaje. Disciplina o alegi în proprietățile vederii. Vezi [Secțiuni, fațade și 3D](views.md).
