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

## Coordinamento: volumi di foro e verifica delle interferenze

Ogni apertura in un muro, un solaio o un tetto può essere un **volume di foro** a sé: un solido grande quanto l'apertura, attraverso tutto lo spessore del suo ospite, finiture comprese. I programmi di interferenze (Navisworks, Solibri, BIMcollab) verificano gli impianti contro tali volumi con «completamente dentro»: un tubo, un canale o una passerella portacavi interamente dentro un volume passa; uno che sporge, o che attraversa un muro senza apertura, è un'interferenza. Lapin esegue la stessa verifica.

**Gestisci → Aperture vs impianti…** stabilisce per quali aperture si fanno i volumi (**Aperture nei muri**, **Aperture delle porte**, **Aperture delle finestre**, **Fori nei solai**, **Aperture nel tetto**), di quanto sono più grandi o più piccoli delle aperture (**Scostamento del foro**) e il **Franco** che un elemento degli impianti deve tenere dai lati di un foro. **Esegui la verifica** passa ogni tubo, canale e passerella — le linee di Lapin come i segmenti di un modello impiantistico importato, e le sue richieste di foro — ed elenca ogni punto in cui uno attraversa un muro, un solaio o un tetto:

- **Nessun foro**: attraversa l'ospite dove non ce n'è;
- **Foro troppo piccolo**: passa per un'apertura ma ne sporge, franco compreso; **Manca** dice quanti millimetri sul lato peggiore;
- **Ci sta** (con **mostra ciò che ci sta**): interamente nel volume, con il franco;
- **Foro vuoto** (con **Fori vuoti** spuntato): un'apertura in muro o solaio da cui non passa nulla (un vano scala non lo è mai).

Un clic su una riga mostra la coppia in 3D, entrambi selezionati. **Crea osservazioni** fa di ogni interferenza un'osservazione di tipo **Clash** con il suo punto di vista (**Gestisci → Osservazioni sul modello…**), pronta per il BCF; rieseguita, aggiorna le stesse osservazioni. Le interferenze compaiono anche tra gli avvisi di **Gestisci → Verifica**.

![Aperture vs impianti: per cosa si fanno i volumi, il franco e la verifica di tre tubi](shots/mep-voids-check.webp)

La scheda **Volumi** elenca ogni volume con la sua marca (V1, V2… piano per piano), l'ospite, la porta o finestra che contiene, dimensione e profondità; un clic ne seleziona l'apertura.

![I volumi con marche e dimensioni](shots/mep-voids-list.webp)

**Vista → Volumi di foro** li disegna in 3D, viola traslucido con le loro marche; un clic su uno ne seleziona l'apertura. Sono per lo schermo: tavole e disegni non li portano mai.

![I volumi nel muro interno, con i tubi che li attraversano](shots/mep-voids-3d.webp)

Per dare i volumi a un programma di interferenze, spunta **Volumi di foro** in **File → Esporta IFC o Fragments…** (o **Esporta 3D…**). In IFC ogni volume è un **IfcBuildingElementProxy** di tipo **PROVISIONFORVOID** con **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter se tondo, Depth) e il set di Lapin **Lapin_OpeningVoid** (marca, tipo, ospite, apertura e scostamento), legato al suo ospite; le aperture restano IfcOpeningElement. Ogni volume conserva il suo GlobalId da un'esportazione all'altra. In un'esportazione mesh formano un gruppo a sé, **Opening voids**. Senza spunta le esportazioni restano come prima.

## MEP Pro

La parte ingegneristica sulla rete (la portata che ogni tratto trasporta e la dimensione che la regge, le verifiche: estremità aperte, curve strette, pendenze di scarico, sorgenti sovraccariche; le regole di posa per materiale: rame a passi di 45°, PVC con curve di 45° al massimo; radiatori proposti sotto le finestre, percorsi suggeriti lungo i muri, il profilo di un tratto e l’assonometria di un sistema) si trova nell’edizione **Lapin MEP**, che aggiunge a questa scheda i gruppi **Ingegneria** e **Assistenti**.
