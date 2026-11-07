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

Uma **em U** que vira com **degraus em leque** escolhe cada canto: **Degraus em leque no primeiro canto**, **Degraus em leque no segundo canto** e **Degraus retos entre os cantos**, os degraus do lado curto entre eles. Com degraus entre eles, ou um número diferente em cada canto, cada canto vira por si e a bomba entre os lanços é tão larga como esses degraus: leque, depois um ou dois degraus, depois leque. A **Escada em U em leque** da biblioteca tem dois em cada canto e um degrau reto entre eles. Selecionada, uma escada em leque muda os mesmos números nas suas propriedades (**Leque, canto 1**, **Leque, canto 2**, **Degraus entre**; vazio: os do tipo). A planta desenha cada espelho, os degraus em leque e a linha de piso pelos dois cantos; o 3D, as quantidades e o IFC acompanham, um IfcStairFlight por cada lanço reto e os degraus em leque como um lanço WINDER.

![Uma escada em U com dois degraus em leque em cada canto e um degrau reto entre eles](shots/stair-winder-u.webp)

![A U em leque em planta: leque, o degrau entre eles, leque, a linha de piso pelos dois cantos](shots/stair-winder-u-plan.webp)

![As propriedades de uma escada em leque: os degraus em leque de cada canto e os degraus entre eles](shots/stair-winder-u-properties.webp)

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

## O espaço sob a escada

O espaço sob um lance pode ser mobiliado: um armário de arrumação, um escritório no armário, um cantinho de leitura, uma entrada com banco e cabides, uma casinha de brincar para os pequenos ou uma caminha de cachorro. Selecione uma escada de lances retos e clique em **Espaço sob a escada…** no fim das propriedades dela (ou **Arquitetura → Espaço sob a escada**).

![O configurador do espaço sob a escada: o cantinho de leitura sob o lance](shots/under-stair-dialog.webp)

À esquerda estão os estilos prontos: **Armário de arrumação**, **Escritório no armário**, **Cantinho de leitura**, **Entrada e vestíbulo**, **Casinha de brincar infantil** e **Casinha de cachorro**, e acima deles os estilos do projeto. Escolha um e altere-o no meio:

- **Posição.** **Sob o lance** (numa escada em L ou U), o **Lado aberto** (por padrão o oposto à volta), onde **Começa em** e **Termina em** ao longo do lance (vazio: de onde o intradorso fica 300 mm acima do piso até o último espelho) e a **Profundidade** (vazia: tão larga quanto a escada).
- **A frente.** A **Parte baixa** fechada com **Portas de armário** (cada uma com cerca da **Largura da porta**, com **Prateleiras atrás das portas**), um **Painel fechado** ou deixada aberta; uma **Estante no extremo alto**.
- **Elemento principal.** Uma **Escrivaninha com prateleira** (e uma luz), um **Banco com almofadas** (gavetas na frente), um **Banco de entrada e cabides** (nichos para sapatos embaixo), uma **Porta de casinha de brincar** (e uma **Janela ao lado da porta**) ou uma **Caminha de cachorro** atrás de uma abertura emoldurada, cada um com suas medidas.
- **Materiais.** A **Espessura da chapa** e os materiais do **Corpo**, das **Frentes** e do **Tecido**, entre os do projeto.

À direita, a pré-visualização mostra o espaço visto do lado aberto sob o intradorso do lance e de cima, seu comprimento, profundidade e alturas, e suas peças. Cada peça é cortada pela inclinação; a que não cabe (uma escrivaninha onde a escada é baixa demais) fica de fora e a pré-visualização avisa. **Criar** ou **Aplicar** é um único passo de desfazer. **Salvar como estilo…** guarda o que preenche o espaço como estilo do projeto; **Atualizar o estilo** salva de novo e todos os espaços do estilo acompanham.

![O espaço sob a escada nas propriedades](shots/under-stair-properties.webp)

O espaço é um elemento próprio, hospedado pela sua escada. Quando a escada muda (largura, rotação, degraus, tipo), o espaço é refeito embaixo dela, e é excluído junto com ela. Os campos de posição ficam nas propriedades, e **Configurar…** reabre o configurador.

![Os seis estilos sob seis escadas](shots/under-stair-3d.webp)

Plantas, cortes, elevações e folhas o desenham como qualquer outro elemento: cortado onde a planta corta, visto abaixo.

![O cantinho de leitura em planta, os outros ao lado](shots/under-stair-plan.webp)

![Um corte ao longo do lance pelo cantinho de leitura](shots/under-stair-section.webp)

**Arquitetura → Tabela de espaços sob escada** lista as peças de cada espaço, com quantidade, material e volume, e os totais por material. Em IFC, o espaço é um IfcElementAssembly ("Under-stair space") de IfcFurniture (prateleiras, escrivaninha, banco, almofadas, caminha), IfcDoor, IfcPlate, IfcDiscreteAccessory (puxadores, cabides) e IfcLightFixture, ligado à sua IfcStair.

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

## Varandas

**Architecture → Balcony** (<kbd>BY</kbd>) prende uma varanda a uma parede: escolha o estilo na barra da linha de comandos e clique fora da face da parede, onde ela vai; fica desse lado da parede, à cota do piso do pavimento. Uma **varanda francesa** (Juliet) clicada junto a uma porta ou janela centra-se nela, 200 mm mais larga. Onde não há parede perto, a varanda fica onde clicar. **Balcony by outline** desenha qualquer forma: clique nos cantos, o primeiro lado fica encostado ao edifício. **Gallery edge** desenha a borda de um mezanino sobre um espaço de pé-direito duplo com dois pontos (o vazio à esquerda, a galeria à direita).

Os estilos predefinidos cobrem os tipos pedidos:

- **Juliet balcony**: uma guarda de balaústres em frente a uma porta-janela sobre uma pequena laje de pedra com duas consolas.
- **Cantilevered slab, glass**: uma laje de betão em consola a partir do pavimento com corte térmico, revestida com pendente, pingadeira sob a borda, vidro em três lados.
- **Stacked, on posts**: uma laje sobre pilares de aço à frente, apoiados na varanda de baixo (ou no terreno); repetida piso sobre piso.
- **On steel brackets**, **Hung from tie rods** (tirantes dos cantos da frente até à parede), **Rounded corners**, **Curved front** com parapeito maciço.
- **Loggia**: recuada na fachada. Corta o seu recuo na parede (tão largo como a loggia e tão alto como a sua **Recess height**), constrói as paredes laterais e uma laje de teto atrás da face e levanta a sua própria **Back wall** (do tipo da parede, da mesma altura) à profundidade da loggia, onde vai a porta. A guarda fica na face.
- **Corner balcony**: ao longo da parede e à volta do canto do edifício, de volta pela parede lateral; clicada junto ao extremo de uma parede, envolve-o.
- **Mezzanine gallery edge**: a laje da galeria atrás da borda, um friso sobre a borda e uma guarda de barras.

![Varandas no primeiro piso: uma francesa na porta, uma sobre pilares, uma de canto, uma loggia, uma curva, uma arredondada e uma suspensa; a varanda do piso acima, a tracejado](shots/balconies-plan.webp)

A planta desenha a laje, o acabamento e a guarda de uma varanda como os corta ou vê, e as varandas do piso acima a tracejado. Cortes e alçados desenham a laje, a guarda, os pilares e as consolas.

### O configurador

**Architecture → Balcony configurator** (ou a roda dentada junto a **Style** nas propriedades da varanda) funciona como o das coberturas: os estilos à esquerda (os do projeto, depois os predefinidos), os campos da varanda em separadores à direita, sobre a sua planta ou um corte pelo meio, tal como o rascunho os construiria:

- **Shape**: **Kind**, **Outline** (retângulo, cantos arredondados com **Corner radius**, frente curva com **Front bulge** ou o polígono desenhado), **Width** e **Depth**, o retorno da varanda de canto, a laje e **Fascia height** da galeria.
- **Slab and finish**: **Slab thickness** e material, **Edge** (direito, com **drip** — pingadeira —, com **nosing** do acabamento ou **tapered** até **Edge thickness**), espessura e material do acabamento e a sua **Fall (%)** para a frente, o teto e **Thermal break** (80 mm entre a laje e o pavimento interior; a mapa e o IFC dizem-no).
- **Railing**: os modelos de guarda (prumos e corrimão, balaústres, vidro, cabos, painel cheio) ou um **parapet** com a sua espessura e material, ou nenhum; altura, só à frente ou também nos lados, recuo da borda, corrimão, prumos e cor.
- **Supports**: **Carried by** nada (consola), **posts** (redondos ou quadrados, tamanho e espaçamento), **brackets** (profundidade, altura e espaçamento) ou **hangers** (altura a que os tirantes se fixam acima da laje) e o seu material.
- **Loggia**: altura do recuo, espessura das paredes laterais, laje além da face, laje de teto e parede de fundo.

![O configurador de varandas: os estilos, a forma e a planta](shots/balcony-configurator.webp)

![Os seus apoios, sobre um corte da varanda](shots/balcony-configurator-section.webp)

**Apply** altera a varanda num só passo. **Save as style…** guarda-a como estilo do projeto (um tipo de varanda), oferecido na barra da ferramenta e nas propriedades; **Update style** guarda-a de novo no seu estilo e todas as varandas do estilo a seguem; junto ao nome de um estilo, muda-se-lhe o nome ou elimina-se (as suas varandas ficam como estão). Um estilo é um tipo: os seus artigos de custo (Execução → Map articles) medem as suas varandas.

![As propriedades de uma varanda: o seu estilo e os campos do seu tipo](shots/balcony-properties.webp)

Nas propriedades uma varanda mostra só os campos que o seu tipo usa. As suas pegas deslizam-na ao longo da parede, o meio da frente define a profundidade (numa loggia, a do recuo), os meios dos lados a largura; uma desenhada arrasta-se pelos cantos. O recuo e a parede de fundo de uma loggia seguem-na e vão-se com ela.

**Stack on storeys above** (no configurador, nas propriedades ou **Architecture → Stack on storeys above**) copia as varandas selecionadas para cada piso acima do seu num só passo, com Copy to storeys: a cópia prende-se à parede do seu piso que está onde está a sua parede, uma loggia empilhada corta a parede desse piso, os pilares apoiam na varanda de baixo.

![A fachada sul em 3D: a varanda francesa, as varandas empilhadas sobre pilares, a de canto e a suspensa por tirantes](shots/balconies-3d.webp)

![De norte: a loggia cortada na parede, a varanda curva com o parapeito e a arredondada](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** lista cada varanda com o piso, estilo, tipo, largura, profundidade, área, comprimento e modelo da guarda, apoio e corte térmico, com os totais. Em IFC uma varanda é um IfcElementAssembly (USERDEFINED, "Balcony") de um IfcElementAssemblyType, feito das suas partes: a laje um IfcSlab (FLOOR), acabamento e teto IfcCovering (FLOORING, CEILING), a guarda um IfcRailing (GUARDRAIL) ou o parapeito um IfcWall (PARAPET), pilares IfcColumn, consolas IfcDiscreteAccessory (BRACKET), tirantes IfcMember, o corte térmico um IfcBuildingElementPart; o recuo de uma loggia é um IfcOpeningElement na sua parede. O Lapin volta a ler as suas varandas como varandas.
