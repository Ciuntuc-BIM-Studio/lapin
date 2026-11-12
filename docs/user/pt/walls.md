---
title: Paredes
order: 20
summary: Desenhe paredes canto a canto, altere-as no painel Propriedades e configure tipos de parede com as suas camadas.
---

<!-- video walls -->

## Desenhar paredes

Abra a guia **Arquitetura** e escolha **Parede**, ou digite <kbd>WA</kbd>.

Enquanto a ferramenta está ativa, o painel Propriedades mostra **Novo elemento**: o tipo com que as paredes são desenhadas. Escolha outro ali antes do primeiro clique e veja as camadas dele, a espessura e quantas paredes já o usam; **Editar tipos…** abre Tipos de parede. Porta, Janela, Piso, Forro e Parede-cortina mostram os seus tipos da mesma forma.

![A guia Arquitetura](shots/ribbon-architecture.webp)

Clique onde a parede começa e depois em cada canto. A próxima parede segue o mouse. Enquanto desenha, você pode digitar um comprimento e um ângulo nas caixas acima da vista em vez de clicar. Pressione <kbd>Enter</kbd> ou <kbd>Esc</kbd> para parar.

![Desenhando paredes: a próxima parede segue o mouse, com o comprimento e o ângulo mostrados](shots/wall-drawing.webp)

Onde duas paredes se encontram, o Lapin faz a junção sozinho. As camadas delas entram umas nas outras como seriam construídas.

- **Parede curva** (<kbd>WC</kbd>) desenha uma parede ao longo de um arco.
- **Parede-cortina** (<kbd>CW</kbd>) desenha uma parede envidraçada com montantes e painéis; o seu tipo pode tomar a sua forma real de uma [biblioteca de perfis](profiles.md).
- As paredes se unem sozinhas onde se encontram: um canto em meia-esquadria, uma parede que termina em outra em T, uma que cruza outra em X. Em um T ou em um X as camadas entram umas nas outras como na obra: o núcleo da parede que termina atravessa os acabamentos da outra até o núcleo dela, e os acabamentos dela param na face da outra, encontrando os seus iguais, assim a planta mostra uma única composição sem linha atravessando. **Unir** (<kbd>JW</kbd>), **Meia-esquadria** e **Topo** mudam como duas paredes se encontram quando não é o que você quer; **Novos cantos em meia-esquadria** torna a junção de topo o padrão neste computador. Em qualquer caso, cada camada vira o canto por conta própria: os acabamentos encontram os seus iguais e contornam sem interrupção, e os núcleos se encontram na diagonal (meia-esquadria) ou um núcleo segue até a face externa do outro (de topo), assim uma planta de paredes em camadas mostra cantos limpos. Cada camada entra na outra parede através das camadas dela de **Prioridade** menor e para na primeira de prioridade igual ou maior (veja **Tipos de parede** abaixo), de modo que, por padrão, núcleos encontram núcleos e acabamentos param nas faces; num canto, um núcleo de prioridade maior atravessa o da outra, em meia-esquadria ou de topo. A **Prioridade de corte** de uma parede nunca a faz entrar numa parede unida a ela: entre paredes unidas, decide apenas qual núcleo passa quando os dois núcleos têm a mesma prioridade (num canto de topo ou num cruzamento).
- **Inverter** muda qual das duas paredes passa direto.

**A linha em que você desenha.** Enquanto a ferramenta Parede está ativa, a lista na barra da linha de comando escolhe a linha em que você clica: **Eixo do núcleo** (o eixo, como antes), **Núcleo, face externa**, **Núcleo, face interna**, **Face externa** ou **Face interna**. O exterior é a direita da parede no sentido em que você desenha (desenhe em volta de um edifício no sentido anti-horário para tê-lo do lado de fora). O eixo da parede fica no núcleo, afastado da linha, e paredes desenhadas uma depois da outra na mesma face se encontram no canto dessa face.

## Alterar uma parede

Clique em uma parede para selecioná-la. As alças dela aparecem. Arraste as pontas para alongá-la ou encurtá-la, ou arraste a cruz no meio para movê-la.

![Uma parede selecionada com as suas alças](shots/wall-selected.webp)

O painel Propriedades mostra a parede selecionada. O alto do painel traz o tipo e as camadas. Abaixo ficam o comprimento, a altura, o deslocamento da base, o pavimento e a fase. Mude qualquer valor e o modelo acompanha. Os valores de cada parede são dela. A sua **espessura** (Thickness) vem das camadas do seu tipo: digite uma nova e as paredes selecionadas recebem o tipo com a mesma composição e essa espessura, com o núcleo redimensionado e os acabamentos mantidos. O Lapin usa o tipo do projeto se algum tiver exatamente essa composição; senão, cria um com o nome do delas (*Partition wall 130mm* passa a *Partition wall 150mm*). As outras paredes do tipo antigo ficam como estão, e <kbd>Ctrl</kbd> <kbd>Z</kbd> desfaz tudo num só passo.

![As propriedades de uma parede](shots/wall-properties.webp)

Mais abaixo, **Dependências** mostra o que é construído a partir da parede e a partir de que a parede é construída. **Usado por** lista os elementos que mudam quando a parede muda: as paredes a que se une, suas portas e janelas, uma parede ligada a ela. Clique em um nome para selecionar o elemento. **Construído a partir de** lista os elementos de que a parede depende, depois seu tipo e seus materiais. Quando você exclui um elemento de que outros dependem, uma mensagem nomeia os que foram reconstruídos; <kbd>Ctrl</kbd> <kbd>Z</kbd> traz tudo de volta.

**Inverter** vira as camadas da parede para o outro lado. Você também pode clicar nas pequenas setas mostradas ao lado de uma parede selecionada na planta.

Uma parede reconhecida de um IFC importado cuja extremidade o ficheiro corta em ângulo mantém esse corte: **Ângulo de corte inicial** e **Ângulo de corte final** nas suas propriedades, em graus a partir do esquadro. Uma junção nessa extremidade toma o seu lugar.

**Alinhamento da hachura** define como as camadas desta parede são hachuradas em planta. **material** segue a configuração de cada material. **axis** assenta a hachura de cada camada ao longo do eixo da parede, a partir do início do eixo, de modo que uma parede de tijolos inclinada mostra suas fiadas ao longo dela, contínuas de uma camada à outra. **view** começa cada hachura na origem da vista, sem rotação.

### Uma parede pelo seu perfil

**Perfil…** nas propriedades de uma parede desenha o seu topo em elevação, visto do início ao fim: uma empena, uma inclinação, degraus, uma parede que acompanha uma escada. Arraste os pontos (**Shift** mantém a altura de um ponto), clique na linha de cima para acrescentar um, clique com o botão direito num ponto para tirá-lo, ou digite-os na tabela em milímetros. **Começar de** dá um **Empena**, uma **Inclinação** ou **Degraus** até a altura que você digitar. Um degrau são dois pontos um sobre o outro. **Aplicar** dá forma à parede; **Topo plano** a deixa plana de novo.

A parede continua uma parede: a espessura, as camadas, as junções e as portas e janelas ficam como estavam, e ela é exportada para IFC com a sua forma.

![Uma empena desenhada no editor de perfil](shots/wall-profile-editor.webp)

![A parede oeste da casa como empena, com a janela mantida](shots/wall-profile-3d.webp)

### O contorno e as barras de uma parede-cortina

**Perfil…** nas propriedades de uma parede-cortina desenha o seu contorno em alçado, como numa parede: um frontão, uma inclinação, degraus ou qualquer contorno, com pontos para arrastar, acrescentar ou retirar e uma tabela em milímetros. A parede sobe até ao seu ponto mais alto. A sua malha ganha uma linha onde o contorno quebra (uma quebra a menos de um montante e meio de uma linha da malha é levada para ela); cada vão mantém as travessas que cabem sob o seu ponto mais baixo; o painel acima da última enche até ao contorno e uma barra de bordo segue-o, inclinada; os montantes são cortados por ele. As definições do tipo mantêm-se, e **Perfil…** volta a abrir o contorno para o mudar, sem reconstruir; **Topo plano** devolve o retângulo. Vê-se em 3D, alçados e cortes, os mapas medem painéis e barras como desenhados, e o IFC leva-os como peças.

![Um frontão desenhado como contorno de uma parede-cortina](shots/curtain-boundary-editor.webp)

![A parede-cortina sob o seu frontão, em 3D](shots/curtain-boundary-3d.webp)

**Barras…** dá a montantes e travessas escolhidos um perfil próprio. O alçado da parede mostra cada barra: clique numa (<kbd>Shift</kbd> ou <kbd>Ctrl</kbd>/<kbd>⌘</kbd> acrescenta mais, ou tome todos os montantes ou todas as travessas), escolha um perfil das [bibliotecas de perfis](profiles.md) ou uma caixa simples, a sua **Rotação** e o seu **Espelhamento**, o seu alinhamento na transversal da barra (face da frente ou de trás no seu plano) e ao longo (aresta inicial ou final na linha da malha ou no nível) e os seus deslocamentos na transversal e para o exterior do edifício; a imagem ao lado mostra a secção como assenta. **Aplicar aos escolhidos** dá-lhes o estilo, **Voltar ao do tipo** tira-o. O perfil próprio de um tipo tem os mesmos campos de posição em **Paredes-cortina…**, para cada parede do tipo. O perfil continua uma cópia no projeto: **Atualizar da biblioteca** muda cada barra feita com ele, nesta parede e noutras.

![Barras: um perfil colocado nos montantes escolhidos](shots/curtain-members-dialog.webp)

![Dois montantes com perfil próprio, em 3D](shots/curtain-members-3d.webp)

**Painéis e portas…** dá às células escolhidas um painel próprio ou uma porta. O alçado da parede mostra cada célula tal como o motor a constrói: clique numa (<kbd>Shift</kbd> ou <kbd>Ctrl</kbd>/<kbd>⌘</kbd> acrescenta outras). Um **tipo de painel** guarda-se uma vez no projeto e dá-se às células de qualquer parede-cortina: vidro, cheio, um **enchimento com quadro** (um quadro da sua largura e profundidade à volta da célula, o enchimento de vidro ou cheio recuado da sua face exterior pelo recuo) ou **aberta** (sem painel); a sua espessura, o deslocamento fora do eixo e os materiais (os do tipo de parede-cortina quando nenhum é escolhido). **Dar às escolhidas** aplica-o, **O painel do tipo** retira-o; um tipo de painel alterado muda todas as suas células. **Porta nas escolhidas** substitui cada célula escolhida por uma porta: uma nova porta de parede-cortina (simples ou dupla, de batente ou de correr, a sua largura e altura, folhas envidraçadas ou cheias, o perfil do aro das [bibliotecas de perfis](profiles.md)) ou qualquer tipo de porta do projeto. **Preencher a célula** dá-lhe o tamanho da célula; caso contrário mantém o do seu tipo, fica à esquerda, ao centro ou à direita na célula com um deslocamento, e recebe uma travessa por cima e um montante ao lado onde fica vidro, envidraçados com o painel da célula. Na fila de baixo a porta assenta no pavimento e a travessa inferior é cortada por baixo dela. As definições ficam por célula, por isso seguem a grelha quando muda: uma porta que preenche a sua célula cresce ou diminui com ela, e uma cuja célula desapareceu di-lo e volta com ela. A porta é uma porta: selecione-a para mudar a mão ou o lado, o tipo, ou movê-la para outra célula (Mover ao longo da parede); as etiquetas, os mapas de portas (no tamanho em que está) e a exportação IFC (uma IfcDoor que faz parte da sua IfcCurtainWall) contam-na.

![Painéis e portas: um parapeito com quadro e uma porta nas células escolhidas](shots/curtain-cells-dialog.webp)

![Uma parede-cortina com uma porta e parapeitos com quadro, em 3D](shots/curtain-cells-3d.webp)

Sob um contorno desenhado com **Perfil…** uma porta entra numa célula recortada onde quer que o seu retângulo caiba sob o contorno: os montantes ao lado sobem até ao contorno, os vidros ao lado deles enchem-se até ele, uma travessa atravessa a sua cabeça e o vidro por cima segue a inclinação. Ao preencher uma célula assim, a sua cabeça para uma travessa abaixo do lado mais baixo do contorno. Onde não cabe, o Lapin diz quanto falta e o que fazer (baixá-la, movê-la para o lado mais alto ou escolher uma célula mais alta). Um painel com quadro numa célula recortada tem o quadro ao longo de todo o contorno, também no lado inclinado. Um alçado virado para o exterior de uma porta desenha os seus símbolos de abertura, tracejados, como em qualquer porta ou janela.

![Uma porta sob o contorno inclinado de uma parede-cortina, com painéis com quadro ao longo da inclinação, em 3D](shots/curtain-cells-gable.webp)

![O símbolo de abertura da porta no alçado](shots/curtain-cells-elevation.webp)

## Tipos de parede

Um tipo de parede é um conjunto de camadas, da face à direita do sentido em que a parede é desenhada (a externa, quando você desenha ao redor de um edifício no sentido anti-horário) à face à sua esquerda. Cada camada tem um material e uma espessura, e uma camada é o **núcleo**. A linha da parede corre ao longo do núcleo. Para editar os tipos, escolha **Arquitetura → Tipos de parede**. Uma camada que não é o núcleo pode ter uma altura própria: **De** e **Até** definem até onde ela sobe acima da base da parede (um barrado de azulejos até 1500, um revestimento de 300 para cima); vazios, ela ocupa a altura toda.

A coluna **Prioridade** (de 0 a 100) diz como a camada encontra as camadas de outra parede onde as paredes se unem: atravessa as camadas da outra de prioridade menor e para na primeira de prioridade igual ou maior. Vazia, a camada assume a prioridade do seu material em **Gerenciar → Ordem de corte**, senão 50 para o núcleo e 0 para as demais camadas (o número mostrado em cinza). Assim, um núcleo de concreto de 100 não é interrompido por um núcleo de montantes de 99 e atravessa a divisória, e um reboco duro de 60 segura o núcleo de uma parede que termina nele. A exportação IFC a grava como a Priority da camada, e a importação a lê de volta.

A coluna **Nota** diz como a camada é aplicada neste tipo (*em camada contínua, Baumit SupraFix*): pertence ao tipo, não ao material, e uma etiqueta de composição escreve-a depois da camada; o IFC leva-a como Description da camada. Quando o material da camada tem espessuras de catálogo (**Gerenciar → Materiais → Dados**), a pequena seta ao lado da espessura oferece-as e um aviso marca uma camada de outra espessura.

Uma camada que não é o núcleo também pode mudar de material na altura: o botão **camadas** no fim da sua linha adiciona uma peça (azulejos, 2100 por padrão), da base da camada para cima; adicione mais para uma faixa (pintura até 900, azulejos até 1500), e acima da última peça a camada continua no seu próprio material até o topo. Cada peça é desenhada, cortada em planta e em cortes e medida no seu próprio material.

Uma camada que não é o núcleo pode ter espessura 0, para um material fino demais para modelar mas que precisa ser medido: uma impermeabilização, uma barreira de vapor, a pintura. Ela não forma nenhum sólido; as plantas e os cortes a desenham como uma linha grossa tracejada onde ela fica entre duas camadas (numa face, a linha da face é a dela), o desenho de Composições dos tipos a nomeia como as outras, e ela é medida por área: a face da camada sobre a qual fica, descontadas as aberturas, nas quantidades da parede e no levantamento de quantidades por material.

![Uma camada de reboco com azulejos até 2100 e o reboco acima](shots/wall-layer-parts.webp)

**As hachuras à mão.** Ao lado do material de cada camada, uma amostra mostra como o material é hachurado onde é cortado. Clique nela para ajustar ali mesmo as hachuras do material, sem ir a **Gerenciar → Materiais**: a hachura dele onde um corte o corta, onde uma planta o corta e onde uma elevação o vê, cada uma com sua escala, ângulo, alinhamento, preenchimento e pena, como na aba **Gráficos 2D** de Materiais. São do material, então mudam também todos os tipos e elementos feitos dele, e cada mudança é um passo de desfazer. Os **Tipos de piso** têm a mesma amostra. Um clique fora dela ou <kbd>Esc</kbd> a fecha.

![A amostra de hachura de uma camada aberta em Tipos de parede: as hachuras de corte e de elevação do material](shots/layer-hatch.webp)

![Tipos de parede: as camadas de uma parede de tijolos](shots/wall-types.webp)

A legenda acima das camadas mostra a parede em planta como uma parede selecionada a mostra: o eixo tracejado com uma ponta de seta no sentido em que foi desenhada, **D** na face direita e **E** na face esquerda. As camadas são numeradas de 1, na face D, até a última, na face E, com os mesmos números das linhas da tabela; o núcleo, por onde passa a linha da parede, fica destacado. Uma parede invertida com **Inverter** tem as camadas ao contrário, de E para D. A mensagem de um salvamento recusado some assim que você muda o tipo de novo.

- **+ Camada** adiciona uma camada. As setas movem uma camada para cima ou para baixo.
- **Zonas na altura** divide o núcleo de baixo para cima, por exemplo uma primeira fiada, a alvenaria e uma cinta.
- **Vergas** escolhe as vergas colocadas sobre portas e janelas: pré-moldadas ou moldadas no local, com o apoio e a altura.
- **Desenhar com ele** fecha a caixa de diálogo e começa a desenhar paredes deste tipo.

Uma mudança em um tipo muda todas as paredes desse tipo.

**Importar…**, **Exportar CSV** e **Exportar JSON**, sob a lista de tipos, levam os tipos de parede e de piso de um projeto para outro, ou para uma planilha e de volta: o CSV tem uma linha por camada (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), as linhas de um tipo uma depois da outra, e os pontos e vírgulas e as vírgulas decimais do Excel também são lidos. Ao entrar, um tipo de mesmo nome passa a ter as camadas do arquivo e qualquer outro é criado, em um único passo para desfazer; um material que o projeto não tem é criado pelo nome. A tabela **Composições dos tipos** (Tabelas → + → Composições de tipos) lista cada tipo de parede e de piso com a marca, a espessura total, quantos elementos o usam e as camadas em ordem.

## Molduras e frisos de parede

**Arquitetura → Moldura de parede** (<kbd>WB</kbd>) faz correr um perfil ao longo da face de uma parede: uma cornija, uma faixa, um rodapé exterior, um peitoril. **Friso de parede** (<kbd>RV</kbd>) recorta-o na parede: um friso, uma junta de sombra. Escolha o perfil e a altura na barra da linha de comandos e clique na face da parede. Em 3D a moldura fica à altura clicada; em planta, à altura escrita, do lado clicado. **a partir do topo** mede a altura para baixo a partir do topo da parede, para uma cornija sob o beiral. **Moldura nas paredes selecionadas** e **Friso nas paredes selecionadas** põem o mesmo em cada parede selecionada, na sua face exterior.

Uma moldura percorre toda a face e interrompe-se nas portas e janelas que atravessaria. Onde duas paredes se encontram num canto e ambas têm uma moldura do mesmo perfil à mesma altura nas faces que se encontram, as duas unem-se em meia-esquadria, uma só cornija que dá a volta ao canto. Selecione uma moldura para a alterar nas Propriedades: o seu perfil, o seu material, a sua face (**Trocar de lado**), a sua altura, o afastamento da face, onde começa e acaba ao longo da parede e se se interrompe nos vãos. As propriedades de uma parede mostram as molduras e os frisos que tem.

Molduras e frisos acompanham a sua parede quando é copiada, movida para outro nível ou apagada. São medidos em comprimento e volume (num friso, o volume retirado). Em IFC uma moldura é um IfcCovering MOLDING e um friso um IfcOpeningElement RECESS na sua parede; ambos voltam como estavam.

![Cornijas, faixas e um friso nas paredes exteriores de uma casa](shots/wall-sweeps-3d.webp)

### Molduras no tipo de parede

Uma moldura que todas as paredes de um tipo devem ter (uma faixa de soco, uma cornija, uma junta de sombra) pertence ao tipo. Em **Arquitetura → Tipos de parede**, por baixo das camadas, **Molduras e frisos** lista-as, uma linha cada: o perfil, **Moldura de parede** ou **Friso de parede**, a face (D, a face da primeira camada do tipo, ou E, como a legenda as designa), a altura acima da base da parede (ou abaixo do seu topo com **Do topo**), o afastamento para fora da face, o material de uma moldura e os seus retornos. **Moldura de parede** e **Friso de parede** por baixo da tabela acrescentam uma linha. Guarde o tipo: todas as suas paredes recebem-nas, e também uma parede desenhada com o tipo ou mudada para ele. São molduras como as outras: medidas, na tabela de molduras de parede, cortadas em meia-esquadria nos cantos onde paredes do tipo se encontram, escritas em IFC. As molduras de uma parede invertida mudam de face com as suas camadas. **Exportar JSON**, por baixo da lista de tipos, leva-as consigo, com o contorno dos perfis e os materiais pelo nome (o CSV só tem as camadas).

Selecione uma numa parede: as Propriedades indicam **Do tipo**. Se mudar a sua altura, o seu afastamento ou outro campo, esse campo passa a ser próprio da parede (**Próprios:** altura): as alterações posteriores do tipo não lhe tocam. **Voltar ao tipo** devolve-lhe os valores do tipo. **Desligar nesta parede**, ou apagá-la, deixa a moldura do tipo desligada nessa parede; **Voltar a ligar as molduras do tipo**, em **Molduras e frisos** da parede, tra-las de volta.

![Uma moldura no tipo de parede, no diálogo Tipos de parede](shots/wall-type-sweeps.webp)

### Retornos

Uma moldura que chega a uma extremidade livre da sua parede (sem outra parede ligada aí) faz retorno: cortada em meia-esquadria na extremidade, o seu perfil contorna o canto sobre o topo da parede e para no plano da face, de modo que a moldura fica fechada em vez de mostrar o corte. **Retornos nas extremidades livres**, nas Propriedades, escolhe ambos (o predefinido numa moldura), no início, no fim ou nenhum, para extremidades retas. Um friso nunca faz retorno. Num canto ligado a outra parede a moldura não faz retorno: vai até ao canto, ou junta-se a uma moldura igual na outra parede. Nas portas e janelas para a direito; marque **Retornos nos vãos** para que também faça retorno à volta das ombreiras. **Recuo desde o início** e **Recuo desde o fim** param-na antes das extremidades da face (a direito aí), e **Perfil invertido** vira o seu perfil ao contrário.

### Mover e copiar uma moldura

Selecione uma moldura e use **MOVE** ou **COPY** como em qualquer elemento; a sua parede fica no lugar. O que o deslocamento percorre ao longo da parede desliza a moldura sobre ela (uma moldura em toda a face recebe então um início e um fim próprios); o que percorre através da parede é ignorado. Movida para cima ou para baixo num alçado ou num corte, a sua altura muda. Uma cópia fica na mesma parede, deslocada, ou noutra parede quando cai dentro dela, na face em que cai. **ROTATE** e **MIRROR** mantêm-na na sua parede: espelhada em relação ao eixo da parede, passa para a outra face. Uma moldura do tipo que mova guarda o novo lugar como próprio; movida para outra parede, passa a ser uma moldura própria dessa parede e a do tipo fica desligada na primeira.

### Perfis

**Gerenciar → Perfis** guarda os perfis do projeto, os contornos com que se varrem molduras, frisos e sapatas corridas. **Novo perfil** parte do catálogo com as suas medidas: um retângulo, uma faixa chanfrada, uma cornija, uma meia-cana, um nariz de peitoril, uma faixa com pingadeira; um friso em V, retangular ou redondo; para sapatas, retangular, escalonada em 2 ou 3 degraus, com a face superior inclinada, excêntrica, com murete. Ou escolha **Vértices escritos**: cada vértice u v em milímetros, com ponto e vírgula entre eles, e um terceiro número para fazer do lado que sai desse vértice um arco. **De um contorno desenhado** lê uma polilinha fechada ou um retângulo desenhado com Desenho e selecionado na sua vista. O círculo na pré-visualização é o ponto de inserção: numa moldura fica na face da parede, à altura da moldura, com u para fora da face (para dentro da parede num friso). Se alterar um perfil, tudo o que o usa acompanha-o.

![A caixa de diálogo Perfis](shots/profiles.webp)

### Revestimentos

Modele os revestimentos com as camadas da própria parede, não com molduras. Acrescente uma camada de acabamento no exterior do tipo de parede (pedra, réguas de madeira, fibrocimento, um ETICS), com a sua espessura e o seu material. **De** e **Até** dão-lhe uma altura própria (um soco de pedra até 600, reboco por cima), e o botão **camadas** da camada muda o seu material em faixas ao longo da altura. Para um revestimento afastado da parede, desenhe uma parede própria sobre a face (a opção de linha **Face externa** da ferramenta de parede) com um tipo feito das camadas do revestimento. O revestimento assente régua a régua (réguas verticais ou horizontais, ripas) ainda não é modelado.

## Paredes sob um telhado

As paredes no mesmo nível de um telhado param sob ele sozinhas e sobem até as empenas. Para uma parede em outro nível, selecione-a e escolha **Arquitetura → Anexar ao telhado** (<kbd>AR</kbd>), depois clique no telhado — ou em qualquer outro corpo até o qual a parede deve subir: um sólido do Studio, um elemento importado. Veja [Pisos e telhados](floors-roofs.md).
