# Apartamente-tip

Cerut de un tester la 09.11.2026 („work efficiently with instances”) și dus mai departe de utilizator („să putem configura întregi apartamente și să le refolosim, să le putem da mirror”). Plan, de aprobat înainte de cod.

## Granița

- **Gratuit, în acest depozit:** etapele 1 și 2, cu dimensiuni fixe. Un apartament-tip e un bloc exact: nu se întinde după slot.
- **Mai târziu, posibil premium:** apartamente care se adaptează la slot și configurarea de etaj (sloturi de-a lungul coridorului, mix, indicatori). Hotărârea rămâne de luat atunci.
- Etapa 2 e cea mai dorită, deci are prioritate după oglindirea hotlink-ului.

## Etapa 1: hotlink cu oglindire (făcută)

`HotLinks.Placing` are `Mirrored`: stânga și dreapta schimbate față de originea sursei, apoi rotirea, apoi mutarea. Se alege la legare și din Manage → Links. Un apartament desenat într-un `.lapin` se așază astfel de oricâte ori, oglindit, doar pentru citire, și nu se unește cu pereții vecini. Vezi `docs/links.md`.

## Etapa 2: apartamentul-tip în proiect

### Ce refolosim

Componentele (`docs/components.md`), nu generatorul halei metalice. Hala își reface piesele dintr-o specificație, dar un apartament e desenat liber cu uneltele obișnuite și nu are o specificație din care să fie refăcut. Componentele țin membri reali și duc schimbările de la o instanță la celelalte, cu aceleași id-uri (`Components.Settle`, `componentKey`), iar instanța știe deja să fie oglindită (`componentXf`, al cincilea număr).

Un apartament-tip e un `LapinComponent` cu `kind: "unit"`, care primește și pereți, uși, ferestre, finisaje de cameră și mobilier. Componentele obișnuite rămân cum sunt: fără pereți nativi, cum a cerut utilizatorul.

### Regula de împărțire

- **Apartamentul** are pereții interiori, ușile dintre camere, camerele, finisajele și mobilierul.
- **Clădirea** are fațada, pereții dintre apartamente și coridorul.
- **Ușile de intrare și ferestrele** le poartă camera, nu peretele (răspunsul utilizatorului, 09.11.2026): configuratorul încăperii le alege după tip și latură, iar la așezarea unei instanțe ele se pun în peretele de pe acea latură (`RoomOpenings`, `docs/rooms.md`). O cameră ține un punct pe latură, deci urmează oglindirea și rotirea.

Conturul apartamentului e limita lui: un perete interior poate să se oprească în ea, pe peretele clădirii.

### Problemele de rezolvat

1. **Îmbinări în interior (rezolvat la pasul A).** Se păstrează între copii și între o copie și un membru rămas.
2. **Îmbinări la limită (rezolvat la pasul B).** Îmbinarea unui perete interior cu peretele clădirii ține de instanță, nu de tip: se caută după fiecare așezare și nu se propagă. Vezi „Limita” mai jos.
3. **Oglindire.** Pereții, ușile și camerele o urmează (`UnitTests`), iar `WallPlacement` întoarce straturile. De testat cu ochii pereții cu straturi asimetrice, mobilierul și finisajele.
4. **Câmpuri proprii instanței.** Numărul camerei, marca ușii și numele apartamentului diferă de la o instanță la alta și nu se propagă. Azi instanțele au aceleași numere de cameră (101, 102 în amândouă); se rezolvă la pasul C.
5. **Excepții.** Un membru schimbat sau șters într-o singură instanță rămâne așa (marcat `own`, ca la generatorul de grafic), până când îl readuci la tip.
6. **Viteză (rezolvat la pasul A).** `Settle` duce acum doar membrii schimbați. Măsurat: un perete editat, dus la 100 de apartamente, circa 0,5 s în Debug. Copierea unei instanțe e mai lentă când sunt multe (zeci de secunde pentru 100 de copii, pe mașina încărcată); de văzut dacă deranjează.
7. **Etaje (verificat la pasul B).** Copy to storeys lasă instanțe cu pereți și uși (`UnitBoundaryTests`): se opresc în pereții acelui etaj, ușa camerei stă în fațada de acolo, o a doua copiere pe același etaj e sărită, iar o copie „keep: world” își păstrează înălțimea după o editare (`componentXf` ține acum diferența de nivel).
8. **IFC (făcut la pasul B).** Rămân elemente obișnuite într-un `IfcGroup`, cu `Lapin_Unit` (`Type`, `TypeId`, `X`, `Y`, `Z`, `Degrees`, `Mirrored`). La reimport fără „As one group” redevin instanțe, cu aceleași GlobalId-uri. Cu „As one group” (implicit în dialog) sau în fișierul altcuiva rămân elemente simple, pentru că grupurile nu se imbrică. Oprirea se scrie în `Description` al `IfcRelConnectsPathElements` (`stopsOn`), ca să nu se piardă la întoarcere.

### Limita (pasul B)

- **Oprirea** (`Joins.Stop`, `JoinOptions.Through`, atributul `stopsOn` pe îmbinare) e un T impus: capătul liber al membrului ajunge pe axa peretelui clădirii și e tăiat la fața lui, iar peretele clădirii nu se schimbă, nici lângă capătul lui, unde o îmbinare obișnuită ar face un colț și l-ar scurta.
- **Când se caută** (`Components.StopInnerWalls`): instanță nouă, mutată, rotită, oglindită sau pe alt etaj, membru refăcut de la altă instanță, perete al clădirii desenat, mutat sau șters la cel mult 1,5 m de instanță. Doar pereți din afara oricărei instanțe, deci apartamentele nu se îmbină între ele.
- **Ce nu se propagă** (`Components.FollowsLimit`): o îmbinare care iese din instanță și un capăt mutat doar pentru că urmează peretele clădirii. Dacă peretele alunecă pe lungimea lui, capătul membrului revine pe linia lui, ca să nu se strâmbe. Rămâne editare a tipului orice altceva, inclusiv un capăt liber tras de mână până la perete.
- **La mutare** (`Components.Leaving`): înainte să plece, instanța își scoate îmbinările cu ce rămâne pe loc, ca să nu tragă după ea un perete al clădirii.
- **Ușile și ferestrele camerei** (`IInstanceMembers`, `RoomOpenings.Settle`): lista camerei se aplică din nou unde stă instanța, fără refuz. O intrare fără perete sau fără loc rămâne pe listă, fără ușă, iar configuratorul o spune (`noWall`). Ușa se regăsește după cheie (`roomOpeningKey`). Intrarea ține și direcțiile `hand` și `face`, duse de mutare și oglindire, deci o instanță oglindită are mâna inversă.
- **Editările de mână pe o astfel de ușă** (mutată, întoarsă, alt tip, parapet, ștearsă) se scriu pe intrare (`RoomOpenings.Edited`), deci ajung la toate instanțele. Altfel, la următoarea așezare ar fi revenit cum erau. Excepțiile pe o singură instanță vin la pasul C.
- **Măsurat** (`A_block_of_many_flats_follows_an_edit_in_reasonable_time`, 60 de apartamente, Debug, pe mașina încărcată): o copie în medie 0,5 s, un perete dus la toate 1,3 s, un perete al clădirii editat 0,5 s.

Rămase: pereții curbi nu se opresc și nu primesc uși din cameră; un perete al clădirii care se termină pe un perete interior pierde îmbinarea când instanța se mută sau membrul e refăcut; pereții cu straturi asimetrice, mobilierul și finisajele oglindite rămân de văzut cu ochii (problema 3).

### Pașii

- **A (făcut, 09.11.2026). Modelul:** `kind: "unit"`, Make apartment type din selecție, instanțe mutate, rotite, oglindite, editarea dusă la celelalte, îmbinările interioare, propagarea doar a ce s-a schimbat.
- **B (făcut, 09.11.2026). Limita:** peretele interior oprit în peretele clădirii, pentru fiecare instanță; ușile și ferestrele camerei în peretele clădirii, la fiecare instanță; verificate Copy to storeys și IFC.
- **C. Instanța:** câmpurile proprii și excepțiile, numerotarea camerelor pe apartament.
- **D. Folosirea:** lista de apartamente (tip, număr, suprafețe), biblioteca din alt `.lapin`, un hotlink transformat în apartament-tip, ghidul și scena.

Fiecare pas pornește de la teste, cu un apartament de probă cu două camere, baie, uși și mobilier.

## Răspunsurile utilizatorului (09.11.2026)

1. Ușile și ferestrele le poartă camera, din configuratorul ei, după tip și latură, ca opțiune oprită implicit, și la camerele obișnuite (făcut, `docs/rooms.md`).
2. Apartamentul se desenează direct în proiect și apoi se face tip; fișierul separat vine la pasul D.
