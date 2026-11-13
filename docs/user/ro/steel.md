---
title: Oțel
order: 56
summary: Bare metalice cu capete configurabile — prelungite sau retrase, tăiate după un plan, la 45°, ajustate pe altă bară, crestate — făcute și dintr-un contur desenat; profile tubulare și formate la rece; hale cu cadre portal și vute.
---

## Tabul Oțel

![Tabul Oțel](shots/ribbon-steel.webp)

- **Hală cu cadre portal…**: o hală metalică întreagă din câteva cifre, în configuratorul de hală (mai jos).
- **Stâlp**: un stâlp al structurii, pus cu un clic pe etaj, dintr-un profil metalic: implicit un **HEB 200** din catalogul EN integrat (tipul lui de stâlp se face prima dată și se refolosește). Profilul se schimbă în Proprietăți sau în **Structură → Tipuri de structură**; unealta Stâlp din fila Structură pune ca înainte tipul curent al proiectului.
- **Bară** (<kbd>SM</kbd>): o bară metalică între două puncte, la nivelul de sus al etajului, cu fața de sus a secțiunii pe axă, ca o grindă. Tipul îl alegi în Proprietăți înainte de primul clic, sau din tabul **Catalog**, unde fiecare tip metalic e desenat după secțiunea lui.
- **Convertește în bară**: contururile desenate selectate devin bare (mai jos).
- **La 45°** și **Încadrează**: două bare îmbinate la capete (mai jos).
- **Tipuri metalice**: tipurile de bare ale proiectului și altele noi.

O bară e un IfcMember: axa ei merge oriunde în spațiu (o diagonală, un căprior, un montant), secțiunea vine din tipul ei, se poate roti în jurul axei (**Rotire**), iar axa poate trece prin centrul, fața de sus sau fața de jos a secțiunii (**Punct de inserție**). Grinzile și stâlpii structurii dintr-un profil metalic primesc aceleași capete.

## Capetele

Selectezi o bară, sau o grindă ori un stâlp drept dintr-un profil metalic: secțiunea **Oțel** din Proprietăți arată cele două capete, **Început** și **Sfârșit**. Fiecare capăt e un set de reguli; corpul le urmează.

![Capetele unei bare în Proprietăți](shots/steel-ends.webp)

- **Prelungire**: cât trece capătul de punctul lui; negativă, capătul e retras (un joc de montaj).
- **Tăietură**: **Perpendicular pe axă**; **După un plan**, rotit față de tăietura dreaptă **În plan** (în jurul verticalei secțiunii) și **În elevație** (în jurul laturii ei), în grade; **La 45°** cu altă bară, pe planul care împarte în două colțul dintre ele; **Ajustat pe o bară**, pe fața celeilalte bare la care ajunge (talpa unui stâlp, sau latura inimii lui când stâlpul e rotit).
- **Crestătură**: tălpile crestate **Sus** și/sau **Jos**, pe **Lungimea crestăturii** înapoi de la capăt și pe **Adâncimea crestăturii** de la marginea secțiunii, ca la o grindă prinsă de o grindă principală.

Îmbinarea la 45° și ajustarea numesc cealaltă bară: o muți, iar capătul o urmează; o ștergi, iar capătul redevine drept și spune de ce. **La 45°** din tab îmbină cele două bare selectate în colțul unde se întâlnesc; **Încadrează** ajustează prima bară selectată pe a doua.

## Din schiță în bară

Desenezi conturul barei ca o polilinie închisă sau un dreptunghi (**Desen**), apoi **Oțel → Convertește în bară** cu unul sau mai multe contururi selectate. Într-un singur pas fiecare contur e citit și înlocuit cu o bară:

![Două contururi schițate în plan](shots/steel-sketch.webp)

- cele **două laturi lungi paralele** (în limita unei jumătăți de grad) dau axa, linia lor de mijloc de la un capăt la altul, și **lățimea** dintre ele;
- **latura de la fiecare capăt** dă tăierea capătului: dreaptă, sau după un plan cu înclinarea laturii.

Pe un **plan**, conturul e bara văzută de sus: lățimea e a tălpilor, iar un profil cu acea lățime e căutat printre tipurile de bare ale proiectului și în catalog (altfel tipul ales pentru Bară); bara stă la nivelul de sus al etajului. Pe o **secțiune** sau o **fațadă**, conturul e bara văzută din lateral: lățimea e înălțimea secțiunii, un capăt înclinat e o tăiere în elevație, iar o **treaptă** în unghi drept la un capăt e o **crestătură** sus sau jos; bara stă unde e desenată. O treaptă desenată în plan e refuzată (crestătura se vede din lateral). Un contur care nu e o bandă (un triunghi, un L, laturi neparalele) rămâne cum e, iar Lapin spune de ce.

## Tipuri metalice

**Oțel → Tipuri metalice** listează tipurile de bare ale proiectului cu greutatea pe metru și face altele noi: un profil din catalog (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** și profilele generice formate la rece **C**, **Z** și **Σ**) sau o secțiune tubulară ori formată la rece **cu dimensiunile ei** (înălțime, lățime, grosime, rebord). Tipul nou poate fi un tip de bară, sau un **tip de grindă** ori **tip de stâlp** al structurii, ca uneltele Grindă și Stâlp să pună și ele oțel. Un clic pe un tip din listă îl face pe cel pe care îl pune Bară.

![Dialogul Tipuri metalice](shots/steel-types.webp)

Orice alt profil vine din biblioteci: **Dintr-o bibliotecă de profile…** caută în toate (vezi [Biblioteci de profile](profiles.md)).

Profilele tubulare sunt goale în interior, în vederea 3D, în secțiuni și în cantități. Profilele formate la rece sunt forme generice cu dimensiuni, cu îndoiturile în unghi drept, fără catalog de producător.

![Bare în 3D](shots/steel-3d.webp)

## Hale cu cadre portal

**Oțel → Hală cu cadre portal…** deschide configuratorul de hală. Ca al acoperișului, are stilurile în stânga, câmpurile halei în file la mijloc și, în dreapta, hala așa cum ar construi-o Lapin: un cadru curent văzut de-a lungul halei, cu vutele lui, și un rezumat al greutăților.

![Configuratorul de hală](shots/steel-hall.webp)

- **Stilurile**: halele tipice din catalog (**Hală 20 m**, **Hală 30 m**, un **Șopron 10 m** într-o apă) și cele ale proiectului. Un stil ales completează toate câmpurile; oricare se poate schimba apoi. **Salvează ca stil…** păstrează hala așa cum e configurată ca stil al proiectului; **Actualizează stilul** o salvează din nou în stilul ei, iar toate halele stilului îl urmează.
- **Cadru**: **Deschiderea** (de la axa unui stâlp la a celuilalt), **Traveile**, de la cadru la cadru, în unitatea proiectului (egale ca `5 × 6000`, sau fiecare pe rând: `6000, 6000, 7500`; o lungime își poate purta unitatea, `6m`, iar un număr simplu sub un metru e citit în metri, deci `5 × 6` înseamnă tot cinci travei de 6 m), **Lungimea** halei (scrisă, păstrează numărul de travei și le face egale), **Înălțimea la streașină** (fața de sus a riglelor deasupra feței exterioare a stâlpului), **Acoperișul** (în două ape sau într-o apă) și **Panta** lui, profilele **Stâlpilor** și ale **Riglelor**.
- **Vute**: **Vuta la streașină** și **Vuta la coamă**, fiecare cu **Lungimea** de-a lungul riglei de la fața stâlpului (sau de la coamă) și **Adâncimea** sub riglă acolo, și cum e **Executată**: **tăiată din profilul riglei** (un I tăiat oblic de-a lungul inimii, o jumătate sudată sub riglă: inima și talpa riglei) sau **din plăci sudate** (o **Placă a inimii** și o **Placă a tălpii** cu grosimile lor, și lățimea tălpii).

![Vutele din plăci sudate](shots/steel-hall-haunches.webp)

- **Frontoane**: **stâlpi de fronton sub o riglă simplă** (stâlpi de colț și stâlpi la **Distanța** aleasă, cu axa puternică perpendiculară pe peretele de fronton, sub o riglă mai ușoară care merge până la fața exterioară a pereților, stâlpii ajustați pe fața ei de jos), sau **un cadru curent** la fiecare capăt.

![Un fronton cu stâlpi](shots/steel-hall-gable.webp)

- **Elemente secundare**: panele, riglele de perete, contravântuirile, grinda de streașină și contrafișele (mai jos).
- **Amplasare**: unde se întâlnesc axa A și axa 1, și direcția deschiderii în plan (**Rotire**); previzualizarea arată axele halei de sus.

**Creează hala** o face într-un singur pas, pe care o anulare îl ia înapoi: **axele** ei proprii (axele 1, 2… la cadre, A și B la stâlpi), iar la fiecare cadru doi **stâlpi** (stâlpi ai structurii dintr-un profil metalic, cu inima în planul cadrului și capătul de sus tăiat după panta acoperișului) și două **rigle** îmbinate la 45° la coamă (una singură, de la stâlpul scund la cel înalt, la acoperișul într-o apă): bare metalice ajustate pe fețele stâlpilor, cu vutele lor. Hala stă pe etajul curent; streașina și coama sunt înălțimi ale cadrelor ei, nu etaje noi. Rezumatul dă cadrele, înălțimile și oțelul pe feluri: stâlpi, rigle, vute, stâlpi de fronton, în total și pe m² de pardoseală.

![Hala în 3D](shots/steel-hall-3d.webp)

Hala rămâne editabilă. Selectează una din barele ei: secțiunea **Oțel** arată hala și **Editează hala…**, care deschide configuratorul pe ea; **Aplică** reface hala peste aceleași bare, care își păstrează identitatea (o bară care nu mai trebuie dispare, una nouă se adaugă). Tot acolo sunt vutele riglei, fiecare schimbată separat (felul, lungimea, adâncimea, plăcile): o vută dată așa rămâne cum e când hala se reface; **×** o scoate.

![Vutele unei rigle în Proprietăți](shots/steel-hall-props.webp)

## Noduri

Cadrele unei hale vin cu **nodurile** lor (Steel → Portal frame hall…, fila **Noduri**): un **picior de stâlp** sub fiecare stâlp și stâlp de fronton, un **nod de streașină** unde fiecare riglă ajunge la stâlpul ei, un **nod de coamă** unde se întâlnesc două rigle și o placă de prindere sub rigla de deasupra fiecărui stâlp de fronton. Fiecare e un element al lui, care știe ce bare leagă; plăcile, șuruburile și ancorele lui se fac din parametrii lui și din bare așa cum sunt, deci le urmează când hala se schimbă.

![Fila Noduri](shots/steel-nodes-hall.webp)

- **Noduri**: pornite sau oprite pentru toată hala.
- **Picioarele stâlpilor**: **articulate** (o placă de bază, două ancore de o parte și de alta a inimii sau patru între tălpi) sau **încastrate** (o placă mai lungă și mai groasă, patru până la opt ancore în afara tălpilor, nervuri în planul inimii). **Ancorele** sunt **drepte**, **cu cârlig** sau **cu placă de ancoraj** la capăt; **Numărul de ancore** și **Lungimea ancorelor** (sub fața betonului) se aleg după profil când rămân goale. Când sub stâlp e o fundație din structură, ancorele intră în ea (și se opresc la 50 mm deasupra fundului ei); altfel merg lungimea lor sub subturnare.
- **Nodul de streașină**: o placă de capăt extinsă pe riglă, prinsă cu șuruburi de talpa stâlpului: **Grosimea plăcii**, **Diametrul șuruburilor** și **Grupa șuruburilor**, cât iese **Peste riglă** (cu un rând de șuruburi acolo) și **Rigidizările în stâlp** în dreptul tălpii de sus a riglei și al tălpii vutei.
- **Nodul de coamă**: plăcile de capăt ale celor două rigle prinse cu șuruburi una de alta, pe planul de îmbinare, cu vutele lor.

Ce rămâne gol se alege după profile: șuruburi de la M16 la M27 după înălțimea barei prinse, o placă groasă cât diametrul șuruburilor, distanțele la margini și între rânduri după găuri. Un nod retrage capetele barelor cu ce pune între ele (rigla cu placa de capăt, stâlpul cu placa de bază, stâlpul de fronton cu un rost de 20 mm), prin regulile capetelor barelor.

![Un nod de streașină](shots/steel-nodes-eaves.webp)

![Un picior de stâlp încastrat pe ancore cu cârlig](shots/steel-nodes-base.webp)

Selectează un nod ca să-l vezi în Proprietăți: barele pe care le leagă, fiecare parametru așa cum e acum (o mărime aleasă după profil se arată ca atare) și piesele lui: plăcile, șuruburile și ancorele după mărime și grupă, sudurile după grosime cu lungimea lor, greutatea, fundația în care intră ancorele. Un parametru schimbat acolo se dă doar acelui nod, iar hala refăcută îl păstrează. **Editează hala…** îi deschide hala.

![Un nod în Proprietăți](shots/steel-nodes-props.webp)

Barele desenate de mână își primesc nodurile din **Steel → Găsește nodurile**: un picior sub fiecare bară verticală, un nod de streașină unde o bară e ajustată pe un stâlp, prinderea unui stâlp de fronton unde un stâlp e ajustat sub o bară, un nod de coamă unde două bare sunt tăiate în unghi una cu alta, o înnădire unde două bare merg în prelungire, cap la cap; pentru barele selectate, sau pentru tot modelul când nu e nimic selectat. **Steel → Piese metalice** face extrasul plăcilor, șuruburilor, ancorelor și sudurilor din noduri. Un nod șters dă barelor capetele înapoi; o bară ștearsă își ia nodurile cu ea; **Șterge hala** din configurator ia hala cu barele, nodurile și axele ei, într-un singur pas.

## Elemente secundare

O hală vine cu **elementele ei secundare** (Steel → Portal frame hall…, fila **Elemente secundare**), făcute odată cu ea și din nou când e schimbată, fiecare bară păstrându-și identitatea:

![Fila Elemente secundare](shots/steel-secondary-hall.webp)

- **Elemente secundare**: pornite sau oprite pentru toată hala.
- **Pane**: **Profilul** lor (Z, C și Σ formate la rece, un U sau un I ușor), **Distanța dintre pane** pe pantă, cât trec **Peste frontoane** și **Lungimile**: **câte una pe travee** (de la cadru la cadru) sau **continuă** (pe toată lungimea halei). Stau pe fața de sus a riglelor, cu inima perpendiculară pe acoperiș, de la 150 mm în interiorul feței stâlpilor până la 200 mm de coamă, în atâtea șiruri câte cere distanța.
- **Rigle de perete**: **Profilul** și **Distanța dintre riglele de perete**, de la **Prima riglă la** până sub streașină, câte una pe travee pe fața exterioară a stâlpilor, cu inima orizontală și buzele în jos; pe frontoane, de la stâlp la stâlp (sau între stâlpii unui cadru de fronton), cât de sus le lasă rigla de deasupra. **Grinda de streașină** ia partea de sus a pereților lungi (cu **Profilul** ei), riglele stând la o distanță sub ea.
- **Contravântuiri**: **Traveile contravântuite** (numerele lor, de la prima), **Profilul** barelor (bare rotunde de la Ø16 la Ø30, corniere sau țevi) și unde: **În acoperiș** (un X în planul acoperișului, sub talpa de sus a riglelor, în panouri cam cât travea) și **În pereții lungi** (un X în planul pereților, prin axele stâlpilor, de la înălțimea primei rigle până sub vută).
- **Contrafișe**: de la fiecare a doua pană și riglă de perete până la talpa de jos a riglei și talpa interioară a stâlpului, de o parte și de alta, în afara vutelor.

![Elementele secundare în 3D](shots/steel-secondary-3d.webp)

Fiecare pană și riglă stă pe câte o **eclisă** pe fiecare riglă, stâlp sau stâlp de fronton pe care îl traversează: o placă sudată pe fața barei, lângă inima panei (în afara inimii unui C, pe partea unui Z opusă tălpii lui de jos), prinsă cu șuruburi prin inimă, două șuruburi la fiecare capăt de pană (M12 la o pană de până la 250 mm înălțime, M16 peste). Un stâlp de colț mai îngust decât stâlpii are o eclisă mai înaltă, până la riglă. Fiecare X stă pe **gușee** sudate pe inima riglelor sau a stâlpilor, câte o diagonală pe fiecare față; o cornieră sau o țeavă e prinsă cu două șuruburi, capătul filetat al unei bare rotunde își ia piulița. O contrafișă e prinsă cu șurub la ambele capete. Sunt noduri ca ale cadrelor: selectează unul ca să-i vezi placa și șuruburile în Proprietăți și să le schimbi acolo. Rezumatul configuratorului numără și cântărește panele, riglele, grinda de streașină, contravântuirile și contrafișele; extrasul **Steel parts** are eclisele, gușeele și șuruburile lor.

![O pană pe eclisa ei, o contrafișă sub ea](shots/steel-secondary-cleat.webp)

![Contravântuirea acoperișului pe gușeele ei](shots/steel-secondary-bracing.webp)

## Extrase și desene

**Steel → Steel members** face extrasul oțelului din proiect: barele, grinzile și stâlpii dintr-un profil metalic, pe hale (cele fără hală la sfârșit), câte o **Marcă** pentru barele de același fel cu același profil, aceeași lungime și aceleași tăieturi la capete (C stâlpi, R rigle, GP stâlpi de fronton, P pane, SR rigle de perete, EB grinzi de streașină, B contravântuiri, FB contrafișe, M alte bare, BM grinzi), numerotate de la profilul cel mai greu și cea mai lungă. Un rând dă **Profilul**, **Lungimea**, **Tăierea la început** și **Tăierea la sfârșit** (cât se rotește planul capătului față de drept, și crestătura: unghiurile listei de debitare), **Numărul**, greutatea pe metru, a **Bucății** și totală, și suprafața de **Vopsire**; un total sub fiecare hală și unul general. **Steel parts** are plăcile, șuruburile, ancorele și sudurile nodurilor, cu eclisele și gușeele lor.

![Extrasul barelor metalice](shots/steel-members.webp)

**Steel → Anchor plan** desenează picioarele stâlpilor halei pentru fundații: un plan al etajului halei la 1:50, tăiat chiar peste plăcile de bază, cu fiecare ancoră încercuită, distanța ei până la axă (± când ancorele stau simetric față de ea), fiecare picior marcat după tipul lui (**B1**, **B2**…: aceeași placă, aceleași ancore, grupă și subturnare), traveile și deschiderea axelor, și tabelul tipurilor lângă hală: numărul, felul, placa, ancorele (câte, mărimea, forma), grupa și subturnarea. **Steel → Frame elevation** desenează un cadru (cel curent, sau cadrul barei selectate) văzut de-a lungul halei la 1:50, cu deschiderea, înălțimea la streașină și la coamă. Amândouă sunt vederi ale halei (cerute din nou, se deschide aceeași) și se pun pe planșe ca oricare alta; urmează hala când e schimbată.

![Un plan al ancorelor](shots/steel-anchor-plan.webp)

![Elevația unui cadru](shots/steel-frame-view.webp)

## IFC și cantități

O bară se exportă ca **IfcMember** cu **IfcMemberType**-ul ei. Corpul se scrie cum scriu oțelul celelalte programe (Tekla, Advance Steel, Revit): secțiunea **extrudată** (IfcExtrudedAreaSolid), **tăiată** de planele capetelor (IfcBooleanClippingResult cu IfcHalfSpaceSolid), iar fiecare crestătură un **IfcOpeningElement** care o golește. O grindă sau un stâlp metalic cu capete se scrie la fel. Cantitățile fiecărei bare dau lungimea dintre tăieturi, aria secțiunii, volumul și greutatea netă (7850 kg/m³).

O hală se exportă ca **IfcBuildingSystem** (LOADBEARING) căruia îi sunt atribuite barele ei, cu datele ei în setul de proprietăți `Lapin_SteelHall`, și cu propriul **IfcGrid**. O riglă cu vute e un **IfcElementAssembly** al riglei și al vutelor ei: o vută tăiată din profil e un **IfcMember**, una din plăci sudate două **IfcPlate**, fiecare extrudată (IfcExtrudedAreaSolid). Vutele nu intră în cantitățile riglei; rezumatul configuratorului le cântărește. Panele se exportă ca **IfcMember** PURLIN, contravântuirile și contrafișele ca BRACE, riglele de perete și grinda de streașină ca USERDEFINED (**Side rail**, **Eaves beam**), toate atribuite sistemului halei, cu nodurile lor.

Un nod se exportă ca **IfcElementAssembly** al plăcilor (**IfcPlate**), șuruburilor (**IfcMechanicalFastener** BOLT), ancorelor (ANCHORBOLT) și plăcilor de ancoraj (**IfcDiscreteAccessory** ANCHORPLATE), fiecare cu mărimea și grupa în `Lapin_SteelPart`; el realizează legătura dintre barele pe care le leagă (**IfcRelConnectsWithRealizingElements**; un picior, între fundație și stâlpul lui). Sudurile rămân date, nu corpuri.

Clasele de șuruburi pe care le poate primi o îmbinare sunt regulile **Șuruburi** ale proiectului: vezi [Reguli](rules.md).
