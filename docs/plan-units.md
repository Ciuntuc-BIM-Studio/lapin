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

1. **Îmbinări în interior.** La copiere, îmbinările dintre membri trebuie să rămână între copiile lor. De verificat ce face azi `ElementTransform.Run` cu ele.
2. **Îmbinări la limită.** Îmbinarea unui perete interior cu peretele clădirii ține de instanță, nu de tip: se caută după fiecare așezare și nu se propagă. Aici e partea cea mai grea.
3. **Oglindire.** Ușile își schimbă mâna (verificat prin hotlink, `A_mirrored_door_opens_the_other_way`). De testat pereții cu straturi asimetrice, camerele, mobilierul și finisajele.
4. **Câmpuri proprii instanței.** Numărul camerei, marca ușii și numele apartamentului diferă de la o instanță la alta și nu se propagă.
5. **Excepții.** Un membru schimbat sau șters într-o singură instanță rămâne așa (marcat `own`, ca la generatorul de grafic), până când îl readuci la tip.
6. **Viteză.** `Settle` reface azi toate instanțele în întregime. Măsurat: o editare la 200 de instanțe cu 2 membri, circa 0,5 s în Debug. Un apartament are zeci de membri, deci prin extrapolare ar ajunge la zeci de secunde. Trebuie dus la cei schimbați, nu la toți, și măsurat din nou.
7. **Etaje.** Copy to storeys există; de verificat că apartamentele repetate pe etaje rămân instanțe cu pereți și uși.
8. **IFC.** Rămân elemente obișnuite într-un `IfcGroup`, cu `Lapin_Unit` pentru tip. La import vin ca grup, fără legătura de tip, dacă nu există intenția Lapin.

### Pașii

- **A. Modelul:** `kind: "unit"`, Make apartment type din selecție, instanțe mutate, rotite, oglindite, editarea dusă la celelalte, îmbinările interioare, propagarea doar a ce s-a schimbat.
- **B. Limita:** peretele interior oprit în peretele clădirii, pentru fiecare instanță.
- **C. Instanța:** câmpurile proprii și excepțiile, numerotarea camerelor pe apartament.
- **D. Folosirea:** lista de apartamente (tip, număr, suprafețe), biblioteca din alt `.lapin`, un hotlink transformat în apartament-tip, ghidul și scena.

Fiecare pas pornește de la teste, cu un apartament de probă cu două camere, baie, uși și mobilier.

## Răspunsurile utilizatorului (09.11.2026)

1. Ușile și ferestrele le poartă camera, din configuratorul ei, după tip și latură, ca opțiune oprită implicit, și la camerele obișnuite (făcut, `docs/rooms.md`).
2. Apartamentul se desenează direct în proiect și apoi se face tip; fișierul separat vine la pasul D.
