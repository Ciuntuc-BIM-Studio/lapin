---
title: Escadas e rampas
order: 42
summary: Coloque uma escada ou uma rampa entre pavimentos ou até qualquer altura, configure-a no seu tipo e faça os poucos degraus até uma varanda ou uma porta.
---

## Escadas

Escolha **Arquitetura → Escada** (<kbd>ST</kbd>). Clique no pé da escada (o meio do seu primeiro espelho) e depois num ponto na direção em que ela sobe. Para uma escada em L ou em U, um terceiro clique à esquerda ou à direita do lance define para que lado ela vira. A escada sobe do seu pavimento até o pavimento de cima. O Lapin calcula o número de espelhos, o espelho e o piso a partir da altura e das regras do tipo.

Selecione a escada para alterá-la no painel de propriedades:

- **Tipo de escada.** A forma, as regras, a construção e o guarda-corpo vêm do tipo, configurado em **Arquitetura → Tipos de escada**.
- **Largura**, **Mudança de direção**, **Deslocamento da base** e **Deslocamento do topo.**
- **Altura.** Deixe em branco e a escada sobe até o pavimento de cima. Um valor dá à escada uma altura própria, independentemente dos pavimentos. Use-o para alguns degraus entre dois níveis no mesmo pavimento.
- **Fixos.** Espelhos, espelho, piso e comprimento do patamar, quando você quer fixá-los. Deixe-os em branco para que as regras decidam.
- **Verificações.** Cada número comparado com as regras do tipo, em verde ou vermelho.

Uma escada pede à laje de cima uma abertura sobre ela. Desmarque **Abertura no piso de cima** se a laje deve ficar inteira.

## Formas de escada e tipos prontos

Em **Arquitetura → Tipos de escada**, a guia **Forma** oferece: **Reta**, **Reta com patamar**, **Em L** e **Em U** (cada uma virando num patamar ou com **degraus em leque**; uma U sem vão entre os lances é uma escada de lances paralelos), **Caracol** (em volta de um pilar central, com o seu **Raio do pilar central**), **Curva** (com o seu **Raio interno**) e **Bifurcada**: um lance principal subindo pelo meio até um patamar, depois dois lances de retorno, um de cada lado. Para uma escada helicoidal ou curva, **Giro (°)** define quanto ela dá a volta; em branco, os espelhos e o piso decidem, e informado, o piso decorre dele. Cada escada também pode alterá-lo nas suas propriedades.

A biblioteca acrescenta tipos prontos ao projeto: uma escada de lances paralelos, uma escada com degraus em leque, uma escada helicoidal, uma escada curva, uma escada em balanço e uma escada bifurcada.

Na guia **Construção**, **Construída como** escolhe uma laje inclinada, degraus maciços sobre o terreno, **Degraus em balanço (flutuantes)** (cada degrau uma placa engastada na parede) ou **Degraus sobre longarinas**.

O **Guarda-corpo** tem o seu **Desenho**: **Montantes e corrimão**, **Balaústres** (afastados no máximo pelo **Espaçamento dos balaústres**), **Painéis de vidro**, **Cabos** ou um **Guarda-corpo cheio**; um **Corrimão** quadrado ou redondo, a sua altura, **Recuo da borda**, quanto ele **Prolonga-se além das extremidades**, e a sua cor. No painel de propriedades, **Guarda-corpo** o coloca em outros lados que não os do tipo, para uma escada.

![O tipo da escada helicoidal, os seus degraus numerados](shots/stair-spiral.webp)

### Degraus numerados

Em **Em planta**, na guia **Forma**: **Numerar os degraus** escreve o número de cada degrau num círculo, do primeiro espelho para cima; **Rótulo** escreve os espelhos, o espelho e o piso, e a largura junto ao pé; **“SOBE” no pé da seta** marca onde ela começa. As plantas e as pranchas os mostram.

![Degraus numerados na planta, uma escada bifurcada e uma escada em balanço](shots/stair-numbers-plan.webp)

![Uma escada helicoidal, uma bifurcada e uma em balanço em 3D](shots/stair-shapes-3d.webp)

## Degraus de entrada (uma varanda)

Os degraus até uma porta de entrada têm uma altura própria: do terreno, alguns degraus abaixo do térreo, até ±0.00. O Lapin tem um tipo de escada pronto para eles, **Degraus de entrada**: 2 m de largura, três espelhos de 150 mm, concreto maciço moldado sobre o terreno, sem guarda-corpo.

![Degraus de entrada diante de uma varanda, na planta](shots/entrance-steps-plan.webp)

1. **A varanda.** Desenhe o patamar como uma laje: escolha **Arquitetura → Piso** (<kbd>SB</kbd>) e clique no seu contorno fora da parede, diante da porta. O topo de uma laje fica no nível do seu pavimento (±0.00 no térreo) e o seu tipo dá a sua espessura. Para mudar a espessura, crie um tipo de laje para varandas em **Arquitetura → Tipos de piso**, por exemplo 150 mm de concreto.
2. **Os degraus.** Escolha **Arquitetura → Degraus de entrada**. Clique no meio da borda da varanda e depois no lado para o qual os degraus saem. Os degraus sobem até a borda da varanda e terminam em ±0.00. Eles começam 450 mm abaixo: o seu **Deslocamento da base** é −450 mm.
3. **A altura.** Selecione os degraus. Em **Desnível**, digite a diferença real entre o terreno e a varanda, por exemplo 300. O topo fica na varanda e o pé sobe: o deslocamento da base passa a −300 mm e os degraus ficam com dois espelhos. **Largura** os deixa tão largos quanto a varanda, por exemplo 2400.

![As propriedades dos degraus de entrada: altura, deslocamento da base, níveis](shots/entrance-steps-properties.webp)

**Níveis** mostra onde os degraus começam e terminam, por exemplo −0.45 → ±0.00. Com um espelho (uma altura de 150 mm), o degrau é a própria borda da varanda e não há piso a construir. Dê aos degraus dois espelhos ou mais para ter pisos diante da varanda.

O tipo é acrescentado ao projeto na primeira vez que você o usa. Para alterá-lo, abra **Arquitetura → Tipos de escada** e escolha **Degraus de entrada**:

- **Forma → Sobe → Uma altura própria.** A altura do tipo, subida **Até o nível do seu pavimento, vindo de baixo (uma entrada)**; cada escada ainda pode alterá-la com **Desnível**.
- **Regras → Conjunto de regras → degraus de entrada.** Espelhos de 120–170 mm e pisos de 300–400 mm, com um único espelho permitido.
- **Construção → Construída como.** **Maciça, moldada sobre o terreno** preenche os degraus até a sua base. **Laje inclinada sob os degraus** os constrói como uma escada interna.

![O tipo Degraus de entrada no configurador de escadas](shots/stair-types.webp)

Para fazer qualquer outro tipo de escada subir uma altura própria, escolha **Sobe → Até → Uma altura própria** na sua guia **Forma** (veja a próxima seção). Para transformar uma escada já colocada em degraus de entrada, escolha **Degraus de entrada** como o seu **Tipo de escada**: ela desce até que o seu topo fique no nível do seu pavimento.

![A casa com a sua varanda e os degraus de entrada em 3D](shots/entrance-steps-3d.webp)

No IFC, os degraus são um IfcStair do tipo STRAIGHT_RUN_STAIR. O seu lance é um IfcStairFlight com NumberOfRisers, NumberOfTreads, RiserHeight e TreadLength, e o Pset_StairCommon traz os mesmos números. A varanda é um IfcSlab.

## Uma escada até qualquer altura

Uma escada não precisa ir de um pavimento ao seguinte. Na guia **Forma** de um tipo, **Sobe → Até** escolhe **O pavimento de cima** ou **Uma altura própria**, com a sua **Desnível**, e **Começa** diz onde ela começa:

- **Até o nível do seu pavimento, de baixo (uma entrada).** Ela termina no nível do pavimento e começa a altura mais abaixo, como os degraus de entrada.
- **Do nível do seu pavimento para cima (um terraço, uma plataforma).** Ela começa no nível do pavimento e sobe a altura: 1,20 m até um terraço ou uma doca de carga, por exemplo.

Cada escada colocada pode subir outra altura: digite-a em **Desnível** nas suas propriedades (em branco: a do tipo). Subindo a partir do nível, uma nova altura mantém o pé onde está; vindo de baixo, mantém o topo.

## Rampas

Escolha **Arquitetura → Rampa** (<kbd>RU</kbd>). Clique no meio da extremidade baixa da rampa e depois num ponto na direção em que ela sobe; para uma rampa em L ou em zigue-zague, um terceiro clique à esquerda ou à direita do lance define para que lado ela vira. Na primeira vez, o projeto recebe a **Rampa 6 %** da biblioteca: 1,20 m de largura a 6 %, em lances de no máximo 9 m com patamares de 1,50 m entre eles, uma laje de concreto armado sob um revestimento, corrimãos dos dois lados. Ela sobe até o pavimento de cima, a menos que o seu tipo ou a sua **Desnível** digam outra coisa.

**Arquitetura → Rampa de entrada** é colocada como os degraus de entrada: clique no meio da borda da varanda e depois no lado para o qual a rampa sai. Ela sobe 450 mm do terreno até ±0.00, maciça sobre o terreno, e **Desnível** a altera como faz com os degraus.

Os tipos de rampa são criados no mesmo configurador, **Arquitetura → Tipos de escada**, com **Novo tipo de rampa**, ou a partir da **Rampa em zigue-zague** e da **Rampa de entrada** da biblioteca:

- **Forma.** **Reta**, **Reta, com patamares**, **Em L** ou **Em zigue-zague (ida e volta)** com o seu **Distância entre lances**; a **Largura**, a **Inclinação** em porcentagem, o **Lance mais longo** (se for maior, a rampa é dividida em lances de comprimento igual com patamares entre eles) e o **Comprimento do patamar**. A projeção em planta é a altura dividida pela inclinação: 3 m a 6 % são 50 m. **Sobe** funciona como nas escadas.
- **Regras.** **Inclinação máxima** (8 % por padrão), **Largura mínima** e **Altura livre**. As verificações mostram em vermelho uma inclinação maior do que as regras permitem, um lance mais longo que o máximo e um patamar mais curto que 1,50 m ou que a largura da rampa.
- **Construção.** **Uma laje inclinada de concreto armado** com a sua **Espessura da laje**, ou **Maciça, moldada sobre o terreno**; o revestimento e o guarda-corpo como nas escadas.

![A rampa em zigue-zague no configurador](shots/ramp-types.webp)

Selecione uma rampa para ver o seu tipo, **Largura**, **Mudança de direção**, **Deslocamento da base** e **Desnível**, uma **Inclinação** e um **Comprimento do patamar** próprios (em branco: os do tipo), os comprimentos dos seus lances e as suas verificações. As plantas desenham os seus lances e patamares, a seta subindo e a inclinação em porcentagem ao longo de cada lance. Uma rampa que sobe até o pavimento de cima pede àquela laje uma abertura, como faz uma escada.

![A rampa em zigue-zague e a rampa de entrada na planta, a inclinação ao longo de cada lance](shots/ramps-plan.webp)

![As rampas em 3D](shots/ramps-3d.webp)

No IFC uma rampa é um IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP ou HALF_TURN_RAMP) tipificado por um IfcRampType, feito de IfcRampFlights, patamares (IfcSlab LANDING) e IfcRailings.

## Guarda-corpos

**Arquitetura → Guarda-corpo** (<kbd>RL</kbd>) desenha um guarda-corpo próprio ao longo da borda de uma sacada, de um terraço ou de uma galeria: clique nos seus pontos, Enter termina. Ele usa os desenhos dos guarda-corpos de escada (montantes e corrimão, balaústres, vidro, cabos ou um painel maciço), a sua altura (1 m por padrão), o deslocamento da base, o perfil e as dimensões do corrimão e o espaçamento dos montantes nas suas propriedades; os seus pontos são arrastados pelas suas alças (Ctrl/⌘-clique no meio de um trecho acrescenta um). No IFC ele é um IfcRailing (GUARDRAIL).
