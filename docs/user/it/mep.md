---
title: Impianti MEP
order: 57
summary: Disegna impianti di riscaldamento, idrici, di scarico, elettrici e di ventilazione come schemi sulla pianta, con sorgenti, terminali e i tratti tra di essi, ed esportali come rete IFC.
---

La scheda **Impianti** disegna gli impianti dell’edificio come li abbozza un progettista: un sistema per ogni impianto, la sua **sorgente** (una caldaia, un contatore dell’acqua, una colonna di scarico, un quadro elettrico, un ventilatore), i suoi **terminali** (radiatori, lavabi, WC, prese, punti luce, diffusori) e i **tratti** tra di essi. Ogni nodo è un vero elemento IFC con i suoi attacchi; ogni tratto è un segmento di tubo, di canale o di cavo; i collegamenti restano quando sposti le cose.

![La scheda Impianti](shots/ribbon-mep.webp)

## Un sistema con un clic

Sotto **Nuovo sistema**, scegli l’impianto (**Riscaldamento**, **Acqua**, **Acqua calda**, **Scarico**, **Elettrico**, **Ventilazione**) e fai clic sulla pianta. Lapin posiziona l’intero sistema: la sorgente dove hai fatto clic, un primo terminale a qualche metro di distanza e il tratto tra di essi. Il sistema si apre per la modifica.

![Un impianto di riscaldamento sulla pianta del piano terra: la caldaia, due radiatori sotto le finestre e i tratti tra di essi](shots/mep-plan.webp)

In modalità di modifica tutto ciò che non fa parte del sistema è sbiadito, e si possono selezionare solo i nodi e i tratti del sistema. Trascina la sorgente e il terminale dove vanno: il loro tratto li segue. Aggiungi terminali con **+ Radiatore**, **+ Lavabo**… nel pannello Proprietà, oppure con gli strumenti sotto **Riscaldamento**, **Idraulico**, **Elettrico** e **Aria**: ogni nuovo terminale si unisce al sistema con un tratto proprio, dall’attacco libero più vicino della sorgente, oppure dal tratto più vicino (una derivazione a T). **Fatto** (o <kbd>Esc</kbd>) esce dalla modalità di modifica e ti dice quante curve e derivazioni a T forma il sistema e cosa resta ancora aperto. **Modifica sistema** (<kbd>ES</kbd>) lo riapre.

## Tratti

**Tratto** (<kbd>RN</kbd>) disegna un tratto punto per punto; premi <kbd>Enter</kbd> per terminare. Un’estremità che cade su un attacco libero vi si collega; un’estremità che cade su un tratto dello stesso sistema si dirama da esso.

**Collega** unisce l’estremità di un tratto a un nodo o a un altro tratto; **Scollega** la libera. **Attacchi liberi** seleziona tutto ciò che non è ancora collegato.

## Rimodellare un tratto

Seleziona un tratto: le sue maniglie stanno sulla linea così come è disegnata, anche dopo che hai spostato i nodi che collega.

- **Trascina una maniglia** per spostare liberamente quell’angolo.
- **Trascina il centro di un segmento** per farlo scorrere lateralmente; i segmenti accanto si allungano, così un percorso ortogonale resta ortogonale. Fai scorrere il primo o l’ultimo segmento e Lapin vi aggiunge una sfalsatura, così l’estremità resta sul suo attacco.
- **Ctrl-clic** su una maniglia per toglierla, o sul centro di un segmento per aggiungerne una; **Aggiungi maniglia** ne mette una dove fai clic.
- **Deviazione** (<kbd>DU</kbd>) fa passare un tratto attorno a un ostacolo: fai clic sul tratto dove inizia la deviazione, fai clic dove finisce sullo stesso segmento, poi spostati dal lato verso cui va e fai clic (o digita la distanza). Il tratto esce, prosegue e rientra, con quattro nuovi angoli; mantiene i suoi collegamenti.
- **Ridisegna percorso** (<kbd>RP</kbd>) dà a un tratto un nuovo percorso: selezionalo (oppure parti da esso) e disegna il percorso punto per punto, <kbd>Enter</kbd> per terminare. Il tratto mantiene il suo sistema, la sua dimensione e i suoi collegamenti, e le sue estremità tornano sui loro attacchi.

![Un tratto di radiatore fatto passare attorno a un ostacolo con Deviazione: esce, prosegue e rientra](shots/mep-detour.webp)


## Nodi

Ogni nodo ha un **ruolo**: una caldaia, un radiatore, un collettore, un contatore dell’acqua, un lavabo, un lavello, un WC, una doccia, una vasca, una colonna di scarico, un tappo d’ispezione, un quadro elettrico, una presa, un punto luce, un interruttore, un ventilatore, un diffusore, una griglia di ripresa, una valvola, una salita / discesa, un tappo. Il ruolo gli dà la sua classe IFC, i suoi attacchi, la sua dimensione e il suo simbolo sulla pianta. Una valvola, un contatore o un interruttore posato su un tratto lo divide e si mette nel mezzo. Una **salita / discesa** porta un tratto a un’altra quota o al piano superiore.

![Le proprietà di un radiatore: il suo sistema, il suo ruolo e i suoi attacchi, la sua dimensione e la sua potenza](shots/mep-radiator-properties.webp)

Nel pannello Proprietà un nodo mostra il suo sistema, il suo ruolo e i suoi attacchi (liberi o collegati); un tratto mostra le sue due estremità. Entrambi prendono i valori predefiniti del sistema, a meno che tu non imposti un valore su di essi: dimensione, quota, il piano su cui corrono (pavimento, soffitto, parete), doppio o singolo, materiale, pendenza.

## Sistemi

**Sistemi…** elenca i sistemi del progetto e i valori predefiniti di quello corrente: la dimensione, il piano di posa e le quote di ogni piano, la pendenza (scarico), se i tratti sono disegnati in coppia (riscaldamento: mandata e ritorno) e a quale distanza, il materiale e l’isolamento. Cambia un valore predefinito e ogni nodo e tratto senza un valore proprio si adegua.

![La finestra di dialogo Sistemi](shots/mep-systems.webp)

## Piante, elenchi e IFC

**Pianta impianti** crea una pianta della disciplina impiantistica per il livello, archiviata sotto il gruppo Impianti nel browser. L’**Elenco apparecchiature** e l’**Elenco tratti** sotto **Sistema** sono abachi come gli altri: vanno sulle tavole e si esportano con esse.

In un’esportazione IFC ogni sistema è un IfcDistributionSystem, ogni nodo e tratto ha i suoi IfcDistributionPorts, e i collegamenti vengono scritti tra gli attacchi, così la rete arriva in un altro programma collegata dalla sorgente all’ultimo terminale.

## Gas e gasolio

**Gas** e **Gasolio** sono servizi a sé sotto **Nuovo sistema**. Un sistema del gas dice il suo gas (**Gas naturale** o **GPL**) e la sua **Classe di pressione** (bassa in casa, media in strada) in **Sistemi…**; i suoi tubi sono di default in acciaio nero sulle pareti a 2,1 m, e possono essere in rame, multistrato o **PE 100** per ciò che è interrato. Il gasolio corre in rame.

L’impianto del gas di una casa parte dall’**armadietto contatore** sulla facciata. Fai clic su **Nuovo sistema → Gas**: l’armadietto va dove fai clic e una **caldaia a gas** dentro, collegati da un tratto. Lungo il percorso, gli strumenti di **Gas** mettono sul tratto il **regolatore del gas**, il **contatore del gas**, la **valvola generale del gas**, i **rubinetti degli apparecchi** e i **raccordi flessibili**, che lo dividono come ogni raccordo; la **cucina a gas** e lo **scaldabagno a gas** si collegano all’impianto con un tratto loro. Dove un tubo del gas o del gasolio attraversa un muro passa in una **guaina**, disegnata e contata con il tratto. Una caldaia a gas disegnata nel sistema di riscaldamento come sua sorgente si collega all’impianto del gas dalle sue proprietà (il pulsante **+** con il nome del sistema del gas).

![L’impianto del gas di una casa: armadietto, regolatore e contatore fuori, caldaia e cucina in cucina](shots/mep-gas-plan.webp)

Ogni apparecchio tiene il suo **consumo** (m³/h) e il suo **tipo di apparecchio** secondo lo scarico (A senza scarico, B a tiraggio naturale, C stagno), per gli elenchi e il dimensionamento.

**Gasolio** mette un **serbatoio del gasolio** (interno, esterno o interrato) e una **caldaia a gasolio**; l’**attacco di carico** e il **cappello di sfiato** si collegano al carico e allo sfiato del serbatoio, e il **filtro del gasolio** sta sulla mandata.

### L’alimentazione in strada

Sotto **Strada**, **Condotta del gas lungo…** (<kbd>GM</kbd>) posa una condotta del gas lungo ciò su cui fai clic — una strada, un tracciato, una linea disegnata, un muro — alla **distanza dalla linea** e alla **profondità (ricoprimento)** della barra della riga di comando; sotto un terreno la profondità si misura dal suolo, altrimenti dal livello. **Condotta del gas** la disegna punto per punto. La condotta va in un sistema del gas a media pressione, in tubo di PE, con il suo **nastro di segnalazione**.

**Allacciamento** (<kbd>GS</kbd>): fai clic sulla condotta, poi sull’armadietto. Lapin mette una **presa in carico** sulla condotta, posa il tubo di allacciamento in squadra alla sua profondità fino all’armadietto, e lì risale nell’armadietto.

![L’allacciamento dalla condotta della strada all’armadietto contatore, selezionato](shots/mep-gas-street.webp)

![La condotta e l’allacciamento sottoterra, l’impianto sulla facciata](shots/mep-gas-3d.webp)

![Le proprietà di un allacciamento: dalla presa in carico all’armadietto, ricoprimento e diametro](shots/mep-gas-service-properties.webp)

![I valori di default di un sistema del gas: il suo gas e la sua classe di pressione](shots/mep-gas-systems.webp)

**Computo gas e gasolio** sotto **Sistema** elenca, sistema per sistema, i tubi per materiale e diametro con la loro lunghezza (e di essa le colonne montanti), curve, raccordi a T e guaine, il nastro di segnalazione, valvole, contatori e armadietti, e gli apparecchi con consumo e potenza installati. In IFC un sistema del gas è un IfcDistributionSystem di tipo GAS e uno del gasolio FUEL; l’armadietto è un IfcDistributionChamberElement, il contatore un IfcFlowMeter GASMETER, le valvole IfcValve, la cucina un IfcBurner, il serbatoio un IfcTank.

![Il computo gas e gasolio](shots/mep-gas-list.webp)

Dimensionare i tubi dal consumo degli apparecchi e verificare la perdita di carico è ingegneria, nell’edizione **Lapin MEP**.

## Aspiratori

Il gruppo **Ventilatori** nella scheda Impianti mette gli aspiratori di bagni e cucine come elementi propri, ciascuno di un tipo che configuri:

- **Aspiratore a parete** (<kbd>VF</kbd>): un ventilatore assiale su una parete, che soffia dritto attraverso di essa;
- **Aspiratore a soffitto**: un ventilatore sotto il soffitto con un condotto che esce dal lato;
- **Ventilatore in linea**: un ventilatore tubolare nel controsoffitto, sul condotto;
- **Aspiratore da finestra**: un ventilatore montato nel vetro di una finestra;
- **Aspiratore a soffitto con luce**: un aspiratore a soffitto con una luce nella griglia;
- **Aspiratore con pannello a lamelle**: un pannello a parete con lamelle accanto al ventilatore e una copertura sotto.

Scegli uno strumento e fai clic. Un aspiratore a parete, da finestra o con pannello a lamelle va sulla faccia della parete più vicina al clic (entro 600 mm), con il fronte verso il locale e il centro a 2,1 m dal pavimento (uno da finestra a 1,9 m). Un aspiratore a soffitto va dove fai clic, sotto il soffitto del locale; uno in linea 250 mm sopra. La barra della riga di comando sceglie il tipo che lo strumento mette, tra i tipi del progetto o quelli predefiniti di Lapin (un aspiratore a parete Ø100 con sensore di umidità, uno a soffitto con luce e sensore di movimento, uno in linea per due bagni…).

Un aspiratore a parete taglia un foro tondo nella sua parete, 20 mm più largo del suo condotto: il foro predisposto della parete, come quello di una tubazione (appare come volume di foro nelle piante e va in IFC come apertura della parete). Viene trovato da dove sta l’aspiratore, così un aspiratore spostato lungo la parete porta con sé il suo foro. Un aspiratore da finestra sta nel vetro e non taglia nulla.

![Un bagno con un aspiratore a parete, un aspiratore a soffitto con luce condotto fuori attraverso la parete e un aspiratore con pannello a lamelle](shots/fans-plan.webp)

Le piante disegnano un aspiratore con il suo simbolo impiantistico: un cerchio con le pale, il fronte sulla parete, il manicotto tratteggiato attraverso la parete e una freccia nel verso dell’aria; la griglia di un aspiratore a soffitto tratteggiata, perché è sopra il taglio. Piante del soffitto, sezioni, prospetti e 3D disegnano i corpi.

**Condotto allo sbocco** porta fuori l’aria di un aspiratore a soffitto o in linea: fai clic sull’aspiratore, poi dove esce il condotto. Il condotto è una tubazione del sistema **Estrazione bagni** del progetto (creato con il primo condotto; taglia le pareti che attraversa), largo quanto il condotto dell’aspiratore, all’altezza del suo raccordo. Finisce in una griglia sul lato esterno della parete o, con **Attraverso il tetto** attivo nella barra, in un tubo che sale a un comignolo. Spostato o alzato, l’aspiratore porta con sé l’inizio del condotto.

![Gli aspiratori in 3D: il condotto fino alla griglia esterna, la griglia dell’aspiratore a parete sulla facciata](shots/fans-3d.webp)

### Tipi di aspiratore

**Tipi di ventilatore** apre i tipi: quelli del progetto e quelli predefiniti da aggiungere con un clic. Un tipo imposta:

- il suo **montaggio** (quelli ammessi dal suo genere) e il **diametro del condotto** (Ø100, 125, 150 o 200 mm);
- la **forma** della griglia (tonda o quadrata), la **dimensione**, il **disegno** (lamelle, fessure, anelli o liscia) e la **finitura** (bianco, argento, nero, inox o cromo);
- la **portata d’aria** (m³/h) e la **potenza** (W);
- il **comando**: un interruttore, un sensore di umidità, un temporizzatore, entrambi o un sensore di movimento;
- una **serranda antiritorno**, e la **luce** di un aspiratore a soffitto, la lunghezza di uno in linea, la larghezza e l’altezza di un pannello a lamelle, mostrate solo dove servono.

L’anteprima accanto è quella del motore: il simbolo in pianta sopra, il fronte sotto. Ogni aspiratore di un tipo segue una modifica.

![I tipi di aspiratore con l’anteprima di un pannello a lamelle](shots/fans-types.webp)

Seleziona un aspiratore per cambiarne il tipo, l’**altezza dal pavimento**, la rotazione e, quando ha un condotto, lo **sbocco** (parete o tetto) e quanto lo sbocco sale sopra il condotto. Trascina la maniglia al centro per spostarlo, quella davanti per ruotarlo.

![Le proprietà di un aspiratore a soffitto](shots/fans-properties.webp)

### Abachi, costi e IFC

L’**Abaco ventilatori** elenca ogni aspiratore con locale, genere, dimensione, portata, montaggio, comando, altezza e sbocco, e la portata totale. La **Verifica dell’estrazione per locale** elenca ogni bagno, WC e cucina (e ogni locale con un aspiratore) con il volume, l’estrazione necessaria e quella data dai suoi aspiratori: un bagno 8 ricambi d’aria all’ora e almeno 54 m³/h, un WC 6 e 25 m³/h, una cucina 6 e 108 m³/h. Sono valori indicativi; verificali con la norma che segui.

Un nuovo tipo ha la voce di costo `LAP-FAN`, contata per aspiratore (**Costi → Assegna voci…** la cambia).

IFC scrive un aspiratore come **IfcFan** di un **IfcFanType** con il suo tipo predefinito (PROPELLORAXIAL per uno a parete o da finestra, CENTRIFUGALRADIAL per uno a soffitto, TUBEAXIAL per uno in linea) e **Pset_FanTypeCommon** (NominalAirFlowRate, NominalPowerRate); il foro della parete come IfcOpeningElement. Lapin li rilegge come propri aspiratori.

## Coordinamento: volumi di foro e verifica delle interferenze

Ogni apertura in un muro, un solaio o un tetto può essere un **volume di foro** a sé: un solido grande quanto l'apertura, attraverso tutto lo spessore del suo ospite, finiture comprese. I programmi di interferenze (Navisworks, Solibri, BIMcollab) verificano gli impianti contro tali volumi con «completamente dentro»: un tubo, un canale o una passerella portacavi interamente dentro un volume passa; uno che sporge, o che attraversa un muro senza apertura, è un'interferenza. Lapin esegue la stessa verifica.

**Gestisci → Verifica interferenze…** esegue la verifica solo su richiesta: niente la avvia da sé, né una modifica né **Gestisci → Verifica**. La sua scheda **Impostazioni** stabilisce per quali aperture si fanno i volumi (aperture in muri, porte, finestre, solai e coperture), di quanto sono più grandi o più piccoli delle loro aperture (**Scostamento del volume**) e il **Gioco** che un elemento impiantistico deve tenere dai lati di un volume. **Esegui verifica interferenze** passa una volta per ogni tubo, canale e passerella portacavi — le tratte di Lapin e i segmenti di un modello impiantistico importato allo stesso modo, e i fori previsti propri di un modello impiantistico — e conserva quanto trovato come **rapporto delle interferenze** del progetto, con la data dell'esecuzione, finché non viene eseguita di nuovo. Quando il modello cambia dopo, il rapporto dice che è **non aggiornato**. I suoi problemi sono raggruppati per tipo e numerati, e ogni numero resta lo stesso da un'esecuzione all'altra:

- **Nessun foro**: attraversa l'ospite dove non ne ha;
- **Non tagliati: portante o ordine di taglio**: nessun foro, perché i fori propri delle tratte (sotto) non tagliano quell'ospite;
- **Foro troppo piccolo**: passa per un foro ma ne sporge, gioco compreso; **Mancano** dice di quanti millimetri sul lato peggiore;
- **Volume vuoto** (con **Fori vuoti** spuntato in **Impostazioni**): un foro di muro o solaio da cui non passa nulla (un vano scala non lo è mai).

![Il rapporto delle interferenze: nessun foro, un foro troppo piccolo e un volume vuoto, ciascuno numerato](shots/mep-voids-check.webp)

Ogni problema è **Da correggere**, **Risolta** o **Ignorata**, con un commento. Un clic su una riga mostra la coppia in 3D, entrambi selezionati (**isolati in 3D** nasconde il resto per ora); le frecce vanno alla precedente e alla successiva. **Segna nella vista** (anche **Vista → Segni interferenze**) disegna ogni problema aperto nella pianta, sezione o prospetto in primo piano come una nuvola rossa con il suo numero, solo a schermo.

![La pianta con le interferenze segnate: nuvole rosse con i loro numeri](shots/mep-voids-clouds.webp)

**Crea foro** taglia un foro semplice attorno all'elemento nel muro (tondo per un tubo o un cavo, rettangolare per un canale) o nel solaio, con i lati alla distanza del gioco e la misura arrotondata per eccesso come dice la regola impiantistica dell'ordine di taglio; **Allarga foro** allarga il foro troppo piccolo. **Correggi selezione** lo fa per ogni riga spuntata, **Ignora** e **Riapri** ne cambiano lo stato; ciascuno è un solo passo da annullare. Il rapporto dice allora che non è aggiornato: eseguilo di nuovo, e ciò che è corretto risulta **Risolta** (**mostra risolte**). La sua riga dice allora a quale esecuzione è stata trovata risolta e com'è ora: il foro che attraversa e la dimensione della tratta al suo interno (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), o che la tratta non lo attraversa più; ogni esecuzione successiva aggiorna le dimensioni.

![Eseguita di nuovo dopo le correzioni: le due interferenze risolte](shots/mep-voids-fixed.webp)

**BCF** trasforma le interferenze aperte in osservazioni di tipo **Clash** con i loro punti di vista (**Gestisci → Osservazioni sul modello…**) e le salva come file BCF; eseguito di nuovo, aggiorna le stesse osservazioni. **CSV** e **PDF** salvano il rapporto come elenco. **Gestisci → Verifica** legge soltanto il rapporto: le sue interferenze aperte sono avvisi, e lo è anche un rapporto non aggiornato.

### Tratte che tagliano i propri fori

L'ordine di taglio (**Gestisci → Ordine di taglio**) comprende anche le tratte impiantistiche, sopra muri, solai e coperture. Con **Le tratte tagliano muri, solai e coperture** attivo, una tratta che attraversa un muro, un solaio o una copertura vi taglia il proprio foro: tondo per un tubo o un cavo che passa in squadra, rettangolare per un canale o una tratta obliqua, grande quanto l'esterno della tratta (il suo isolamento, la guaina di un tubo del gas) più il **Gioco** attorno, arrotondato per eccesso al passo, mai sotto il **Foro minimo**. Il foro non è tenuto a parte: si sposta e cresce con la tratta e sparisce quando la tratta sparisce o non attraversa più. È un volume come ogni foro (disegnato, elencato, verificato), e l'esportazione IFC lo scrive come **IfcOpeningElement** con **Lapin_ProvisionOpening** (e come foro previsto con **Volumi di foro** spuntato), così una verifica eseguita di nuovo non trova nulla da segnalare. Un muro, un solaio o una copertura posto sopra le tratte nell'ordine di taglio (per classe o per materiale) non viene tagliato, né uno portante (LoadBearing nel suo Pset_…Common) a meno che **tagliali anche loro** sia spuntato: la verifica interferenze li segnala come **Non tagliati**. Una tratta demolita non taglia nulla, e dove una tratta passa già per un foro non se ne taglia un altro. Un sistema può decidere diversamente in **Impianti → Sistemi…** (**Le tratte tagliano muri, solai e coperture**: come l'ordine di taglio, sì, no). È attivo in un progetto nuovo; un progetto fatto prima lo tiene spento finché non viene acceso.

In un muro curvo il foro si trova dove la tratta incontra l'arco, con la larghezza misurata lungo di esso; uno rettangolare ha i lati radiali, come quello di una porta o di una finestra. In una copertura viene tagliato nella falda che la tratta attraversa: verticale per un tubo in piedi (uno sfiato), allungato lungo il percorso per una tratta obliqua, come in un solaio; gli arcarecci e la trave di colmo sotto la falda non vengono tagliati, e la verifica interferenze segnala una tratta che li attraversa. Spostare o ridimensionare una tratta ricostruisce solo i muri, i solai e le coperture che attraversava o attraversa ora, per quanto grande sia il modello. L'IFC di Lapin riletto non riprende questi fori quando arrivano anche le loro tratte (le tratte li tagliano di nuovo, mai due volte); senza la loro tratta (lasciata fuori dall'importazione, o un progetto le cui tratte non tagliano nulla) ciascuno entra come foro del suo elemento ospite, così nulla va perso.

![Le tratte impiantistiche nell'ordine di taglio](shots/mep-voids-cut-order.webp)

La scheda **Volumi** elenca ogni volume con la sua marca (V1, V2… piano per piano), l'ospite, la porta o finestra che contiene, dimensione e profondità; un clic ne seleziona l'apertura.

![I volumi con marche e dimensioni](shots/mep-voids-list.webp)

**Vista → Volumi di foro**, con la vista 3D davanti, li disegna in 3D, viola traslucido con le loro marche; un clic su uno ne seleziona l'apertura.

![I volumi nel muro interno, con i tubi che li attraversano](shots/mep-voids-3d.webp)

Con una pianta, una sezione o un prospetto davanti, **Vista → Volumi di foro** li disegna in quella vista, come una tavola di coordinamento mostra i fori previsti: tagliati dove la vista li taglia (l'apertura di un muro per tutto il suo spessore, quella di un solaio attraverso la soletta), visti dove li vede (un foro nel solaio sotto il piano di taglio, l'apertura di un muro sulla faccia che un prospetto guarda), tratteggiati quando l'apertura di un muro sta sotto il taglio o quella di un solaio sopra; ciascuno con la croce, con la sua sigla e, sotto, la dimensione e la quota inferiore di un'apertura nel muro (**400×200 +0.90**, **Ø200**). Li disegna il motore con il resto del disegno, quindi le tavole, il PDF e il DXF (layer **VOIDS**) li stampano come li mostra lo schermo.

![La pianta con i volumi: tagliati nei muri, il foro del solaio visto, ciascuno con la croce e la sigla](shots/mep-voids-plan.webp)

L’etichetta di un vuoto sta accanto a esso, attraverso il suo muro, dove non c’è già altro disegnato: l’apertura di una porta, il contrassegno della porta o della finestra, altre etichette. Se entrambi i lati del muro sono occupati, si sposta lungo il muro oltre l’apertura, altrimenti più in fuori.

![Il vuoto di una porta: la sua etichetta lontana dall’apertura e dal contrassegno della porta](shots/void-label.webp)

Per spostare un’etichetta, fai clic su di essa (o sul vuoto): così si seleziona l’apertura, la cui etichetta mostra un grip quadrato per spostarla e uno rotondo per ruotarla. Ctrl/⌘-clic su un grip la rimette dove sta di default, o di nuovo orizzontale. Un’etichetta spostata è tenuta da quella vista, lungo e attraverso il muro dal centro del vuoto, quindi segue l’apertura quando il muro si sposta; tavole e PDF la stampano dove l’hai messa.

![L’etichetta del vuoto spostata con il suo grip](shots/void-label-moved.webp)

È un'impostazione della vista, anche nelle sue proprietà (**Vista → Impostazioni di vista**): **Volumi di foro**, e **Dimensioni dei fori** per la dimensione sotto la sigla. Di base nessuna vista li disegna; lo stile di vista **Coordinamento** (**Gestisci → Stili di vista**, scelto come **Stile** della vista) li disegna in ogni vista che lo usa. Le loro linee sono la categoria **Volumi**: penne e colori in **Gestisci → Stili oggetto** (linee di taglio, in proiezione, nascoste, sopra), mostrate o nascoste per vista in **Vista → Visibilità / Grafica…**.

Per dare i volumi a un programma di interferenze, spunta **Volumi di foro** in **File → Esporta IFC o Fragments…** (o **Esporta 3D…**). In IFC ogni volume è un **IfcBuildingElementProxy** di tipo **PROVISIONFORVOID** con **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter se tondo, Depth) e il set di Lapin **Lapin_OpeningVoid** (marca, tipo, ospite, apertura e scostamento), legato al suo ospite; le aperture restano IfcOpeningElement. Ogni volume conserva il suo GlobalId da un'esportazione all'altra. In un'esportazione mesh formano un gruppo a sé, **Opening voids**. Senza spunta le esportazioni restano come prima.

## MEP Pro

La parte ingegneristica sulla rete (la portata che ogni tratto trasporta e la dimensione che la regge, le verifiche: estremità aperte, curve strette, pendenze di scarico, sorgenti sovraccariche; le regole di posa per materiale: rame a passi di 45°, PVC con curve di 45° al massimo; radiatori proposti sotto le finestre, percorsi suggeriti lungo i muri, il profilo di un tratto e l’assonometria di un sistema) si trova nell’edizione **Lapin MEP**, che aggiunge a questa scheda i gruppi **Ingegneria** e **Assistenti**.
