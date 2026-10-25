# Scări și rampe: configuratorul

Lapin are azi un buton „Stair” care nu face nimic și o clasă Studio `IfcStair` pentru solide libere. Acest document propune sistemul de scări și rampe: cum se configurează, ce tipuri vin gata făcute, ce primește arhitectul și ce primește inginerul din același element, și ce întrebări trebuie lămurite înainte de implementare (marcate **[Î1]** … **[Î10]**, răspunsurile intră în `decisions-pending.md`, #120).

Modelul e cel al tâmplăriei, care merge deja: un **tip** (`IfcStairType`, `IfcRampType`) ține regulile și construcția, editate într-un configurator cu previzualizare; o **instanță** (`IfcStair`, `IfcRamp`) ține plasarea, cotele și traseul, editate în plan cu grips. Tot ce iese (corpul 3D, piesele structurale, simbolul din plan, cotele, extrasul, IFC-ul) se deduce din cele două.

## Principii

1. **Se pleacă de la ce știe utilizatorul:** de unde pleacă scara, până unde urcă și cât loc are. Motorul propune contratreapta, treapta și numărul de trepte după normă; utilizatorul corectează doar ce vrea.
2. **Norma e vizibilă, nu ascunsă.** Fiecare valoare care are o limită (contratreaptă, treaptă, 2h+b, lățime, înălțime liberă, trepte pe rampă, panta rampei) se arată cu limita ei și se colorează când o depășește. Nimic nu e refuzat: se semnalează.
3. **Un element, două citiri.** Același `IfcStair` dă arhitectului finisajele, balustrada și simbolul din plan, iar inginerului placa (vangul), grinzile de reazem, podestele și cantitățile de beton, cofraj, oțel sau lemn. Diferența e în ce părți vede fiecare și în ce tabele intră.
4. **Tipuri gata făcute, puține și bune.** Câteva forme, câteva construcții, cu valori normale pentru o locuință din România. Un tip nou e o copie corectată.
5. **Piese, nu un bloc.** Scara e un părinte fără corp propriu, cu piese: rampele (`IfcStairFlight`), podestele (`IfcSlab` LANDING), balustradele (`IfcRailing`), vangurile de oțel sau lemn (`IfcMember` STRINGER). Așa se exportă în IFC (ca grupurile din `spec-ifc.md`), așa se selectează în 3D, așa se măsoară.

## Tipul: ce ține și ce calculează

Un tip de scară are trei straturi de parametri. Configuratorul le arată pe trei file, cu aceeași previzualizare (plan și secțiune) peste tot.

### 1. Forma **[Î2]**

| Formă | Descriere | IFC `PredefinedType` |
|---|---|---|
| dreaptă | o rampă | STRAIGHT_RUN_STAIR |
| dreaptă cu podest | două rampe în linie, podest intermediar | TWO_STRAIGHT_RUN_STAIR |
| în L | două rampe la 90°, podest de colț | QUARTER_TURN_STAIR |
| în U | două rampe paralele, podest de întoarcere (scara de bloc) | HALF_TURN_STAIR |
| în U cu trei rampe | trei rampe în jurul unui gol, două podeste | THREE_QUARTER_TURN_STAIR |
| balansată în L / în U | fără podest, trepte balansate în colț | QUARTER_WINDING_STAIR / HALF_WINDING_STAIR (mai târziu) |
| elicoidală | în jurul unui ax | SPIRAL_STAIR (mai târziu) |

Pentru rampe (`IfcRamp`): dreaptă, cu podest, în L, în U (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP, HALF_TURN_RAMP).

Forma e un **traseu de segmente**: rampă (n contratrepte, sau o lungime la o pantă pentru rampe), podest (lungime), întoarcere (±90°, 180°, spre stânga sau spre dreapta). Formele gata făcute sunt doar trasee predefinite; un traseu desenat de mână e aceeași structură (**[Î6]**).

### 2. Regulile: dimensiunile treptelor **[Î1]**

| Parametru | Implicit (locuință) | Limite propuse | De unde |
|---|---|---|---|
| contratreapta h | 17 cm | 15 … 18 cm (public: ≤ 16) | STAS 2965, NP 063 |
| treapta b | 28 cm | 26 … 32 cm (public: ≥ 30) | idem |
| 2h + b | 62 cm | 60 … 64 cm | formula pasului |
| lățimea rampei | 1,00 m | ≥ 0,90 m locuință; ≥ 1,20 m comun | P118, NP 063 |
| trepte pe o rampă | — | 3 … 18 (public: ≤ 16) | |
| lungimea podestului | = lățimea | ≥ lățimea rampei | |
| înălțimea liberă | — | ≥ 2,00 m (verificată sub placa de deasupra) | |
| nasul treptei | 2 cm | 0 … 4 cm | |
| panta rampei (IfcRamp) | 6 % | ≤ 8 % accesibil; ≤ 15 % tehnic | NP 051 **[Î8]** |
| podest la rampă | la 9 m sau 0,5 m urcare | | NP 051 |

Calculul: din înălțimea de urcat H și din regulă, motorul alege numărul de contratrepte n = round(H / h) și recalculează h = H / n, apoi b din 2h + b, rotunjit la 0,5 cm, cu cea mai bună potrivire în limite. Utilizatorul poate fixa oricare dintre n, h, b; celelalte se recalculează. Cu forma și n, lungimea traseului rezultă; pe dos, la un traseu desenat cu lungime fixă, b rezultă din lungime și se verifică.

Înălțimea de urcat **[Î9]**: implicit, de la cota etajului propriu la cota etajului de deasupra, deci inclusiv placa; scara ajunge la cota finită a etajului de sus. Un decalaj la bază și unul sus (`baseOffset`, `topOffset`) acoperă pardoselile și scările care nu urcă un etaj întreg.

### 3. Construcția: ce vede inginerul **[Î3]**

| Construcție | Piese | Parametri | Cantități |
|---|---|---|---|
| beton armat monolit (implicit) | rampa = placa înclinată (vang) + trepte pe ea; podeste = plăci; grinzi de podest opțional | grosimea plăcii de rampă (15 cm), grosimea podestului (15 cm), grinda de podest (b×h), reazemele: pe placă sus și jos, pe perete lateral (da/nu) | beton (m³), cofraj (m²: intradosul, laturile, contratreptele), trepte (buc), oțel estimativ (kg/m³, un coeficient al tipului) |
| beton prefabricat | rampe prefabricate, podeste monolit sau prefabricate | la fel, plus lățimea de rezemare | buc rampe, beton podeste |
| oțel pe vanguri | două vanguri (profil din catalogul de secțiuni: U, I, platbandă), trepte (tablă, grătar, lemn, beton) pe corniere | profilul vangului, trepta (material, grosime), podest (cadru + tablă) | lungimi de profil (m), kg oțel, trepte (buc) |
| lemn | vanguri (închise sau în dinți de fierăstrău), trepte, contratrepte opționale | secțiunea vangului, grosimea treptei, cu/fără contratreaptă | m³ lemn, buc |

Arhitectul vede în plus **finisajele**: grosimea și materialul treptei finite, contratreapta finită, nasul, și **balustrada**: pe partea interioară, exterioară, ambele sau nicio parte, cu înălțimea și un tip de balustradă (deocamdată bară + montanți, ca în Lapin-ul vechi). Cotele structurale și cele finite se arată amândouă în configurator (treapta brută și cea finită).

## Instanța: plasarea și editarea

- **Atribute:** `storeyId`, `typeId`, `at` (piciorul primei contratrepte), `direction` (unghiul rampei întâi), `turn` (stânga/dreapta pentru L și U), `width`, `baseOffset`, `topOffset`, `topStorey` (implicit etajul de deasupra), și, fixate de utilizator, `risers`, `riserHeight`, `tread`, `landing`. Totul în intenția IFC.
- **Plasarea** **[Î6]**: unealta cere forma (sau un tip), apoi un click la piciorul scării și unul pentru direcție; L și U cer partea întoarcerii (clic în stânga sau în dreapta liniei). Motorul întinde scara după regulă; dacă nu are loc, o semnalează. Traseul desenat de mână (click din podest în podest) vine după.
- **Editarea directă:** grips la picior, la vârf și la fiecare podest; tras un capăt, se schimbă lungimea și se recalculează b (cu semnal dacă iese din limite); tras lateral un podest, se schimbă lungimea podestului. Lățimea are un grip pe margine. Inspectorul arată numerele rezultate (n, h, b, 2h+b, lungimea, înălțimea liberă) și permite fixarea lor.
- **Golul în placa de sus** **[Î5]**: scara știe ce placă străpunge și propune golul (conturul rampelor și al podestelor de sus, plus distanța de siguranță); golul rămâne legat de scară și se reface la editare, ca buiandrugii cu golurile.
- **Înălțimea liberă:** se calculează sub placa și grinzile de deasupra pe tot traseul și se arată în inspector; roșu sub 2,00 m.

## Desen

- **Plan:** treptele ca linii pe lățime, linia de întrerupere la cota de tăiere a planului (1,20 m): sub ea linii pline, deasupra nimic (sau punctat, după standardul de desen); săgeata de urcare cu „URCĂ” sau numerotarea contratreptelor (1 … n) la picior și la vârf; podestele cu conturul lor; balustrada ca linie. Pe planul etajului de sus, partea care coboară apare ca „COBOARĂ”, tot până la linia de întrerupere.
- **Secțiune și fațadă:** profilul în trepte, placa, podestele, balustrada, din corpurile 3D, ca restul.
- **Cote:** o cotă de scară: „15 × 17,5 / 28” lângă săgeată; cota lungimii rampei în plan.
- **Extras:** fișa scării, pe tipul de extras existent: forma, n, h, b, lățimea, construcția, cantitățile.

## Comenzi și module **[Î4]**

- `architecture.createStairType`, `updateStairType`, `architecture.addStair {storeyId, typeId, at, direction, turn?, width?, topStorey?}`, `updateStair`, `architecture.addRamp` cu aceleași argumente; interogarea `architecture.stairCheck {id}` dă valorile și verificările pentru inspector.
- Modulul `architecture` deține `IfcStair`, `IfcRamp` și tipurile lor (Studio le lasă, păstrează doar solidul liber cu `exportAs`). Piesele structurale (vang, grinzi de podest, vanguri de oțel) sunt `GeometryPart`-uri cu materialul lor, deci intră în cantități și în extrasele structurii fără un al doilea element; modulul `structure` poate adăuga mai târziu armarea și verificarea reazemelor.
- Cantitățile: `Qto_StairFlightBaseQuantities` (Length, GrossVolume, NetVolume, NumberOfRiser, NumberOfTreads, RiserHeight, TreadLength) pe fiecare rampă, `Qto_SlabBaseQuantities` pe podeste, `Qto_RampFlightBaseQuantities` pe rampe; setul `Lapin_StairQuantities` pe scară cu betonul, cofrajul și oțelul totale.
- IFC: `IfcStair` părinte fără corp, `IfcRelAggregates` peste `IfcStairFlight` (cu `NumberOfRiser`, `NumberOfTreads`, `RiserHeight`, `TreadLength` în IFC4), `IfcSlab` LANDING, `IfcRailing`, `IfcMember` STRINGER; tipul ca `IfcStairType` cu intenția; cum fac deja pereții cu zone.

## Etape

1. Tipul și configuratorul: forma (dreaptă, cu podest, L, U), regulile cu verificare, construcția beton armat monolit, finisajele, balustrada bară + montanți; instanța cu plasarea prin două clicuri; corpul 3D pe piese; planul cu linia de întrerupere și săgeata; cantitățile; IFC.
2. Rampele (aceleași forme, în pantă), golul propus în placă, înălțimea liberă, grips.
3. Oțel pe vanguri și lemn; extrasul scării; cotarea automată.
4. Trepte balansate, scara elicoidală, traseul desenat de mână, armarea.

## Întrebări deschise

- **[Î1] Norma și valorile implicite.** Propun valorile din tabelul regulilor (h 17, b 28, 2h+b 60–64, lățime 0,90/1,20, max 18 trepte, 2,00 m liber). Sunt bune, sau le vrei pe ale tale? Vrei două seturi, „locuință” și „public”, alese pe tip?
- **[Î2] Formele primei etape.** Dreaptă, dreaptă cu podest, L și U cu podeste, plus rampele. Balansate și elicoidale mai târziu. De acord?
- **[Î3] Construcția primei etape.** Beton armat monolit întâi, oțelul și lemnul în etapa 3. De acord, sau ai nevoie de oțel mai devreme?
- **[Î4] Modulul.** Scara în `architecture`, cu piesele structurale ca părți ale ei (inginerul le vede în cantități și tabele). Alternativa, un modul `circulation` separat, nu aduce nimic acum.
- **[Î5] Golul în placă și balustrada.** Propuse și legate automat de scară (se refac la editare), cu posibilitatea de a le scoate. Sau le pune utilizatorul singur?
- **[Î6] Plasarea.** Forma aleasă, apoi două clicuri (picior și direcție) și partea întoarcerii. Traseul desenat liber vine după. Sau preferi de la început desenarea traseului, podest cu podest?
- **[Î7] Studio.** Clasa `IfcStair` din Studio rămâne pentru scări libere (sculptate), exportate ca `IfcStair`. Sau o scoatem, ca să nu fie două unelte cu același nume?
- **[Î8] Rampele.** Implicit 6 %, maxim 8 % accesibil, podest la 9 m sau 0,5 m urcare, lățime 1,20 m. Confirmi?
- **[Î9] Înălțimea de urcat.** De la cota etajului la cota etajului de deasupra (inclusiv placa), cu decalaje la bază și sus. O scară pe mai multe etaje = o scară pe etaj, cu același tip. De acord?
- **[Î10] Finisajele.** Treapta finită (material, grosime) și nasul în tip; configuratorul arată și cotele brute și cele finite. Contratreapta se calculează pe cotele finite (ce calcă omul). De acord?
