---
title: Cortes, elevações e 3D
order: 70
summary: Crie cortes e elevações, abra vistas 3D e defina o que cada vista mostra e como.
---

Todas as vistas estão no navegador do projeto, organizadas pela sua disciplina: Arquitetura, Estrutura ou MEP. Clique duas vezes em uma vista para abri-la em uma guia.

![O navegador do projeto com um corte e uma elevação](shots/browser.webp)

## Cortes

Escolha **Vista → Corte** (<kbd>SE</kbd>) ou clique no **+** ao lado de **Cortes** no navegador. Depois desenhe a linha de corte em uma planta com dois cliques e clique uma terceira vez no lado para o qual o corte olha. O corte é desenhado a partir do modelo e fica atualizado enquanto você o altera.

![Corte A-A através da casa](shots/section-view.webp)

Um corte não precisa ser uma única linha reta. **Corte deslocado** (<kbd>SJ</kbd>) o desenha como uma polilinha, clique após clique, e <kbd>Enter</kbd> termina: cada trecho reto é um corte plano que vê apenas entre as suas próprias extremidades, e os trechos são desenhados lado a lado nos seus comprimentos reais, com uma linha tracejada fina marcando cada desvio. **Corte curvo** (<kbd>SV</kbd>) desenha um arco por três pontos (o início, o fim e um ponto sobre ele) e o desenvolve da mesma forma, para que uma fachada curva seja lida no seu comprimento real. **Corte ao longo de** (<kbd>SA</kbd>) escolhe um elemento com uma linha em planta, uma estrada ou um caminho, uma vala, um muro de arrimo, uma parede, uma viga, uma fundação ou uma polilinha desenhada, e o segue como ele está agora; ele escreve as estacas ao longo da base (0+020, a cada décimo da escala, ou o passo que você definir em **Estacas** nas configurações da vista). Na planta, os pontos de um corte são alças: arraste um, arraste o meio de um trecho para adicionar um desvio, clique com Ctrl/⌘ em um ponto para removê-lo. Mostrado em 3D, um corte desenvolvido corta o modelo ao longo do seu primeiro trecho e dispõe o seu desenho trecho por trecho.

![Um corte com desvios através da casa: três trechos lado a lado, os desvios marcados](shots/section-jogged.webp)

Selecione o corte no navegador para definir a sua escala, a sua profundidade, a sua disciplina e o seu estilo no painel de propriedades.

### Onde um corte é marcado

A linha de um corte é desenhada em todas as plantas, e o corte é marcado em todos os outros cortes e elevações que o seu plano atravessa: uma linha tracejada fina onde o plano corta a vista, com o nome do corte e uma seta no topo apontando para onde o corte olha, com o mesmo tamanho no papel que em uma planta. Um corte nunca é marcado em si mesmo. Clique em um marcador para selecionar o corte e clique duas vezes para abri-lo.

Cada corte fica em um **layer de marcadores** (**Cortes** no início; **Layer do marcador** nas propriedades do corte a altera), e cada vista escolhe o que marca. **Vista → Cortes…** lista os cortes do projeto por layer para a vista em primeiro plano, com uma caixa de seleção por layer e por corte só para esta vista, o layer de cada corte (um existente ou **Novo layer…**) e em quantas vistas cada um está oculto. Uma prancha marca o que as suas vistas marcam.

Um corte feito através de uma viga ou de um pilar (**Corte aqui**, **Corte transversal** na guia Armadura simples) também tem alças em planta: arraste qualquer uma das extremidades ao longo da linha para dar mais ou menos margem a esse lado, arraste o meio ao longo do elemento para mover a estaca em que ele corta. A janela do corte acompanha.

![Vista → Cortes… para a elevação sul](shots/sections-dialog.webp)

## Elevações

O **+** ao lado de **Elevações** cria as quatro elevações do edifício de uma vez.

![A elevação sul](shots/elevation-view.webp)

### Sombras do sol

Um alçado, um corte ou uma planta de implantação podem desenhar as sombras que o sol projeta sobre o que veem, preenchidas por baixo das suas linhas, para que se imprimam nas folhas, em PDF e DXF. Escolhem-se nas propriedades da vista, em **Sombras do sol**:

- **sol próprio**: um sol da vista. Por omissão a convenção de desenho: o sol sobre o ombro esquerdo, os raios na diagonal de um cubo (de 45°, a 35,26° de altura), assim uma sombra é tão funda quanto sobressai o que a projeta. **Sol de** e **Altura do sol** rodam-no; **Convenção de desenho** repõe-no.
- **sol do projeto**: o sol do local do projeto (Gerenciar → Localização) num dia e a uma hora da vista. **Como em Sol e sombras** toma o dia e a hora de Vista → Sol e sombras.

Um corte projeta e recebe sombras só do que está para lá do seu corte. Uma planta de piso não desenha sombras: as suas paredes, cortadas à altura da planta, projetá-las-iam curtas demais.

![O alçado sul com a sombra da cobertura](shots/elevation-shadows.webp)

![As sombras do sol de uma vista](shots/view-shadows.webp)

### Tracejados de superfície

Um alçado ou um corte traceja cada face que vê com o tracejado **Elevation** do seu material (**Gerir → Materiais**, separador **2D graphics**): padrão, escala, ângulo, pena e preenchimento. Só se traceja a parte de uma face que nenhum elemento mais próximo esconde, por isso o reboco ou o tijolo de uma fachada vê-se à volta das portas e janelas, e um corte traceja o que vê para lá do seu plano de corte, não atrás dos elementos que corta. Uma parede em camadas mostra o material da sua camada exterior. O tracejado é desenhado sob as linhas, por isso imprime-se nas folhas, em PDF e em DXF (camadas SURFACE-HATCH e SURFACE-FILL).

Com **Alignment** *view* o padrão continua de uma face para a seguinte; com *layer* assenta na própria face, ao longo da sua horizontal a partir do canto de baixo, e uma face vista de viés mostra-o encurtado. Um material que continua no mesmo plano por vários elementos é tracejado como um só, a não ser que **Join materials** do estilo de vista esteja desligado.

Até escolher um, o tracejado de alçado de um material vem **pelo nome**, como o seu tracejado de corte, e **Gerir → Materials** mostra qual (*pelo nome (BRICK)*): tijolo e alvenaria BRICK, pedra STONE, madeira BOARDS, revestimento metálico VERTICAL, telhas, telhas de madeira e ardósia HORIZONTAL, ladrilhos TILES, gravilha e terra como em corte. O isolamento, as membranas, o reboco, a pintura e o betão não recebem nenhum: o BATT do isolamento é um tracejado de corte, nunca proposto para uma superfície (escolhido à mão, é desenhado). Escolha **none** para não desenhar nada. Um corte desenvolvido (quebrado, curvo ou ao longo de um elemento) traceja cada um dos seus troços entre as suas pontas, e desenha neles da mesma forma as sombras do sol. **Surface hatches** do estilo de vista (**Vista → View settings**, **Gerir → View styles**) desliga-os para vistas inteiras: ligado em Architectural, desligado em Structural, MEP e Coordination. As plantas mantêm os seus próprios tracejados de corte e de planta. A cobertura de um telhado é tracejada pelo seu material: um telhado de telha mostra as fiadas nas águas que a vista vê, e nenhuma nas que lhe ficam de costas.

![O alçado sul, o tijolo à vista tracejado pelo seu nome](shots/elevation-surface-hatch.webp)

![Um corte quebrado traceja o muro do jardim visto pelo seu último troço](shots/section-developed-surface-hatch.webp)

![Os tracejados de superfície nas definições da vista](shots/view-surface-hatch.webp)

## Vistas 3D

<!-- video section-3d -->

Clique duas vezes em **3D** para abrir a vista 3D. Arraste com o botão esquerdo para orbitar em volta do modelo. A planta-chave no canto superior direito mostra de onde você está olhando. **Planta**, **Persp** e **Ajustar** mudam a câmera. Para guardar uma câmera, duplique a vista 3D.

A roda do rato faz zoom para o ponto do modelo sob o cursor, que fica onde está no ecrã; o zoom para logo antes desse ponto e nunca o atravessa. Arrastar com o botão esquerdo gira a vista em volta do centro dos elementos selecionados, que fica onde está no ecrã, para os ver de todos os lados; sem nada selecionado gira em volta do ponto do modelo sob o cursor onde o arrastar começa, e em volta do centro da vista se não houver nada sob o cursor. **Vista → Zoom para o cursor** e **Vista → Orbitar em torno da seleção** desligam cada um (a roda faz então zoom para o centro da vista, e a vista gira em volta do seu centro), tal como as caixas nas propriedades de uma vista 3D. A escolha fica guardada neste computador.

### Um corte em 3D

Clique com o botão direito em um corte no navegador e escolha **Mostrar em 3D**. A vista 3D corta o modelo ao longo da linha do corte. Ela mantém o lado para o qual o corte olha, até a profundidade do corte, se ele tiver uma. Cada elemento cortado é fechado onde é cortado, camada por camada, cada camada no seu próprio material: uma parede mostra o seu núcleo de tijolo e o seu reboco, tão sólida quanto a parede é. **Hachuras** no chip na parte de baixo da vista 3D aplica em vez disso o desenho do corte sobre a seção cortada, com os preenchimentos e as hachuras de corte que uma prancha imprime. As cotas, as marcas de nível, as etiquetas e os textos do corte ficam no mesmo plano, com o mesmo tamanho e no mesmo lugar que no corte. As linhas mantêm as espessuras de pena e os padrões de traço completos. As que foram desenhadas ao lado do edifício ficam no ar ao lado do corte. **Anotações** no chip na parte de baixo da vista 3D as oculta ou as mostra de novo. O **Mostrar em 3D** de uma elevação vira a câmera para olhar na mesma direção que a elevação.

![O menu de clique direito do corte no navegador](shots/section-3d-menu.webp)

![Corte B-B visto em 3D](shots/section-3d.webp)

Para ver o modelo inteiro de novo, clique no **×** do chip **Corte** na parte de baixo da vista 3D, ou escolha **Mostrar em 3D** de novo.

![Corte B-B com Hachuras ativado: a seção cortada como a prancha a imprime](shots/section-3d-hatches.webp)

### A caixa de corte

**Vista → Caixa de corte** (atalho BX) recorta a vista 3D em uma caixa. A caixa começa em volta do modelo inteiro. Cada face tem uma seta no meio: arraste-a para mover essa face. Cada elemento que a caixa corta é fechado nessa face, camada por camada, nos seus materiais, e as faces se encontram de forma limpa nas arestas da caixa. **Cortar na planta** coloca o topo da caixa no plano de corte da planta do nível ativo (a sua faixa de vista), para que o nível seja lido em 3D como a sua planta o corta. **Ajustar** coloca a caixa de novo em volta do modelo inteiro. Para girar a caixa, arraste a alça redonda além do seu canto superior (de grau em grau; segure Shift para passos de 15°), ou selecione uma parede e clique em **Alinhar**: a caixa gira na direção da parede. **Hachuras** desenha os cortes da caixa como uma prancha faz, cada material com o seu preenchimento e a sua hachura de corte, em vez dos materiais. Uma vista 3D salva mantém a sua caixa de corte: salve a vista com a caixa ativada, ou abra uma vista salva e defina a sua caixa, e a caixa estará lá cada vez que a vista abrir. O **×** do chip **Caixa de corte** desativa a caixa.

![A caixa de corte cortada na planta do térreo, com a face leste arrastada para dentro](shots/section-box.webp)

![A mesma caixa com Hachuras ativado: os cortes como uma prancha os desenha](shots/section-box-hatches.webp)

### Um pavimento por vez

Para olhar dentro do edifício, mostre só alguns dos seus pavimentos. Clique com o botão direito em um nível no navegador do projeto e escolha **Mostrar só este nível em 3D**, ou **Mostrar este nível e os de baixo em 3D** para ver o edifício cortado nesse nível. O nível passa a ser o ativo, e a vista 3D acompanha o nível ativo: clique em outro nível no navegador para subir ou descer um andar.

![O menu de clique direito do nível no navegador](shots/storeys-3d-menu.webp)

![O térreo sozinho em 3D](shots/storeys-3d.webp)

**Vista → Pavimentos exibidos**, ou as propriedades da vista 3D, definem o resto:

- **Mostrar**: todos os pavimentos, os pavimentos marcados, só o pavimento ativo, ou o pavimento ativo e os de baixo.
- **Em nenhum pavimento**: o que não está em nenhum pavimento (o terreno, a topografia, os arquivos vinculados) é mostrado ou ocultado.
- **Cortar acima do seu nível**: o modelo é cortado a essa distância acima do pavimento mais alto mostrado, 1,20 m no início, como uma planta o corta. Cada elemento cortado ali é fechado no corte, camada por camada, nos seus materiais, como acontece com um corte mostrado em 3D.

![O térreo cortado a 1,20 m: as paredes fechadas, camada por camada](shots/storeys-3d-cut.webp)

Os pavimentos ocultos não são desenhados, e você não pode selecioná-los, ajustar a vista a eles nem caminhar sobre eles. O que um corte retira (o corte acima dos pavimentos mostrados, um corte mostrado em 3D, a caixa de corte) também não está lá para a caminhada: você passa pela parte de cima de uma parede que o corte removeu, e as tampas no corte fecham o que ele abre. **Arquivo → Exportar 3D…** pega a vista 3D como ela a mostra (veja [Importar e exportar](import-export.md)). Uma vista 3D salva mantém a sua própria escolha. O chip **Pavimentos** na parte de baixo da vista 3D diz o que é mostrado; o seu **×** mostra todos os pavimentos de novo.

### Sem acabamentos, ou só os núcleos

Uma vista 3D pode mostrar só a estrutura do edifício, como faz uma planta estrutural. Nas propriedades da vista 3D (sem nada selecionado), ou em **Vista → Configurações da vista** com a vista 3D em primeiro plano, **Layers** mostra:

- **todas as camadas**: as paredes, as lajes e as fundações como são construídas;
- **sem acabamentos**: sem as camadas cujo material é um acabamento (gesso, reboco, pintura, cerâmica, placas, revestimentos de piso, ou um material cuja categoria é Acabamento em Gerenciar → Materiais); o isolamento fica;
- **só o núcleo**: só a camada marcada como núcleo (o tijolo, a laje de concreto).

![Uma casa em 3D só com o núcleo: as paredes são o seu tijolo, as lajes o seu concreto](shots/layers-3d-core.webp)

Cada camada mantida continua um sólido fechado no seu próprio material, então um corte ou a caixa de corte a fecha como antes. Portas e janelas ficam nas suas aberturas; um requadro revestido com um acabamento vai junto com os acabamentos da parede. Uma vista 3D pega as suas camadas do seu estilo, como uma planta: defina a sua **Disciplina** como Estrutura e ela mostra só os núcleos; defina **Layers** para escolher outra coisa, e o botão ao lado volta ao valor do estilo. O chip **Camadas exibidas** na parte de baixo da vista 3D diz o que fica de fora; o seu **×** mostra todas as camadas de novo. As camadas ocultas não são selecionadas nem bloqueiam a caminhada, e **Arquivo → Exportar 3D…** como a vista mostra exporta as mesmas camadas.

### Entrar caminhando em um ambiente

Em uma planta, clique com o botão direito dentro de um ambiente e escolha **Caminhar em 3D**. A vista 3D coloca você dentro do ambiente, 1,60 m acima do piso, olhando ao longo do seu lado mais comprido.

![O menu de clique direito de um ambiente na planta](shots/room-walk-menu.webp)

![Em pé na sala de estar](shots/room-walk.webp)

- Arraste com o botão esquerdo para olhar em volta.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> ou as setas fazem você caminhar. Segure <kbd>Shift</kbd> para correr. <kbd>Space</kbd> pula.
- A roda do mouse dá passos para frente e para trás.

Você caminha como uma pessoa caminharia. Paredes, pilares, móveis e janelas fechadas param você, e você desliza ao longo deles. Você fica em pé sobre as lajes, sobe e desce escadas e rampas, e cai se sair de uma borda. As portas estão abertas para você: passe pelas aberturas delas. Só o que a vista 3D desenha para você: elementos ocultos, os pavimentos que ela não mostra e o que os seus cortes retiram não ficam no seu caminho.

![Caminhando até a porta do quarto](shots/room-walk-door.webp)

![Subindo a escada](shots/room-walk-stair.webp)

**Voar** no chip da caminhada, ou <kbd>F</kbd>, deixa você voar em vez de caminhar: sem gravidade, e você atravessa paredes e lajes. <kbd>E</kbd> e <kbd>Q</kbd> sobem e descem. Pressione <kbd>F</kbd> de novo para caminhar; você cai sobre o piso que estiver embaixo.

<kbd>Esc</kbd> ou **Sair da caminhada** traz de volta a câmera que você tinha antes.

### Realista, com o sol

Vista → **Qualidade de exibição** define como a vista 3D é desenhada, neste computador:

- **Desenho**: a paleta da folha e uma luz uniforme, como o Lapin sempre desenhou. Rápido em qualquer computador.
- **Médio**: as sombras do sol, sombra nos cantos onde as faces se encontram, e luz do céu.
- **Realista**: o céu atrás do modelo e um terreno por baixo (onde o projeto não tem terreno), e vidro que a luz atravessa. Para apresentar, com uma boa placa gráfica.

![Uma casa na vista realista](shots/view-realistic.webp)

O sol é o do projeto: onde está para o local do projeto (Gerenciar → **Localização**) num dia a uma hora. Vista → **Sol e sombras** define o dia (o equinócio e os solstícios estão a um clique) e a hora, reproduz o dia e acende o sol também na vista de desenho. Sem sistema de coordenadas nem ponto de levantamento, o local é Bucareste. O fuso horário segue a longitude do local, com a hora de verão europeia na Europa.

![Sol e sombras](shots/sun-dialog.webp)

A vista realista toma alguns números (a sombra nos cantos, a qualidade das sombras, a cor do sol) do [BimGo](https://github.com/aussieBIMguru/BimGo), de Aussie BIM Guru (MIT-0).

## Modelar em 3D

A vista 3D é um lugar para modelar, não só para olhar: os elementos movem-se e copiam-se lá como em planta, ajustados aos cantos, arestas e faces dos outros elementos.

### Mover e copiar em 3D

Selecione os elementos e depois **Move** (M) ou **Copy** (CO) com a vista 3D à frente. Clique no ponto base: um canto, uma aresta ou uma face do que move. O cursor diz a que se ajusta: **Endpoint** (extremidade), **Midpoint** (ponto médio), **On edge** (na aresta), **On face** (na face) ou **On level** (no nível) no vazio; o que está atrás da face sob o cursor não é tomado. As linhas de um DXF importado, mostrado em 3D, também atraem o cursor. Depois clique onde vai, por exemplo o canto de uma laje: a seleção segue o cursor em 3D, também para cima e para baixo, e é colocada lá. A partir do ponto base inferem-se os eixos vermelho, verde e azul quando o cursor segue um; → ← ↑ travam o eixo vermelho, verde ou azul (a mesma tecla solta) e Shift mantém o eixo inferido. Com um eixo travado, um canto sobre o qual passa é trazido para o eixo, para alinhar o movimento com ele sem lá ir.

Digite um valor a qualquer momento, sem clicar numa caixa: um comprimento vai na direção do cursor (1200), e dx,dy ou dx,dy,dz é o próprio deslocamento (0,0,450 sobe 450 mm). Enter aplica, Esc cancela. Copy continua a copiar do mesmo ponto base até Enter ou Esc.

O que tem altura própria sobe ou desce com o movimento: a base de uma parede, de um pilar ou de um objeto, a base e o topo de uma escada (a sua altura mantém-se), o topo de uma viga, o desvio superior de uma laje, o frechal de um telhado; um sólido Studio recebe um passo de movimento. Uma porta ou janela desliza ao longo da sua parede.

O que assenta no seu nível (uma parede, uma escada, um pilar, uma laje, uma viga, uma guarda, uma cobertura, um móvel, uma porta ou uma janela) mantém o seu nível quando a deslocação se prende de um ponto a outro: vai para lá só em planta, pelo que uma escada movida pelo seu canto de cima para o canto de uma laje não sobe os poucos centímetros entre a estrutura da escada e o acabamento da laje (o topo de uma escada já fica à face do acabamento do nível a que sobe). Para a subir ou descer, siga ou bloqueie o eixo azul (↑), ou escreva dx,dy,dz; a etiqueta diz quando o nível é mantido. Sem nada selecionado, Move e Copy pedem os elementos: clique neles em 3D e prima Enter.

Também pode premir um elemento selecionado e arrastá-lo: move-se da mesma forma, a partir do ponto premido, ajustado, e fica onde o solta. **View → Drag to move in 3D** desliga isto, para que arrastar sobre a seleção faça orbitar a vista.

![Uma escada movida em 3D pelo canto do degrau de cima até ao canto da laje](shots/model-3d-move.webp)

### Colocar elementos em 3D

Todas as ferramentas que tomam pontos em planta funcionam também na vista 3D: paredes, fachadas-cortina, lajes, telhados, tetos, aberturas na laje, compartimentos, pilares, vigas, fundações, guardas, rodapés, mobiliário, elevadores, estradas, plantas e as restantes. Os pontos vão para o nível ativo: o cursor ajusta-se ao modelo como ao mover (o canto de uma laje, a ponta de uma viga, o meio de uma aresta, as linhas de um DXF) e o ponto é tomado mesmo por baixo, no nível; uma linha tracejada mostra onde, quando o ajuste fica acima ou abaixo. Os pontos e o contorno que formam desenham-se no nível à medida: a linha de uma parede, o contorno de uma laje que fecha no primeiro ponto, um arco pelo terceiro ponto. A partir do último ponto inferem-se os eixos vermelho e verde; → e ← travam-nos.

Digite um valor a qualquer momento: um comprimento parte do último ponto na direção do cursor (3000, ou 3000<30 com um ângulo), largura,altura de um retângulo a partir do primeiro canto. Enter aplica, fecha uma laje ou termina uma guarda; Backspace retira o último ponto; Esc descarta os pontos e, na segunda vez, a ferramenta. Arrastar continua a rodar a vista, para girar à volta do modelo entre dois cliques. A etiqueta em baixo diz em que nível vão os pontos.

Portas e janelas ficam onde clica na face de uma parede em 3D, no sítio da parede onde clicou.

![Uma parede começada no canto da laje em 3D, com o eixo verde inferido desde o início](shots/model-3d-wall.webp)

### Pegas e componentes em 3D

As pegas de um elemento selecionado (as pontas de uma parede, os cantos de uma laje) arrastam-se em 3D como em planta e prendem-se como com Move: primeiro um canto, depois um ponto médio, depois uma aresta ao alcance, depois os eixos vermelho e verde pelo ponto de partida da pega, depois uma face. O ponto é marcado e nomeado junto ao cursor e tomado à altura da pega. Escreva um comprimento ou um ângulo enquanto arrasta, como em planta; Enter põe a pega lá.

Arraste um cartão do **Catalog** ou uma ferramenta da faixa para a vista 3D: a ferramenta começa com esse tipo. Uma ferramenta que coloca com um clique (um pilar, uma sapata, mobiliário, um elevador, uma planta) é colocada logo onde a larga, ajustada ao modelo. Largada, ou clicada, sobre a laje de outro piso, o elemento vai para esse piso: uma mesa posta na laje do primeiro andar pertence ao primeiro andar, seja qual for o nível ativo.

## O que uma vista mostra

A guia **Vista** define o que a vista em primeiro plano mostra e qual é a sua aparência.

**Grade** (<kbd>F7</kbd>) oculta ou mostra a grade de fundo da planta, e **Linhas finas** (<kbd>TL</kbd>) desenha todas as linhas com um pixel de espessura, para ler um desenho denso. As duas valem só para a tela: pranchas, PDF e DXF mantêm as suas penas, e o snap na grade continua. A grade do chão da vista 3D segue o mesmo botão.

![A guia Vista](shots/ribbon-view.webp)

- **Configurações da vista.** A disciplina e o estilo da vista, a sua faixa (a altura e a profundidade do corte) e os seus gráficos.
- **Estilos de vista.** Arquitetônico, Estrutural (só os núcleos), MEP ou o seu próprio. Cada estilo escolhe as camadas desenhadas (todas, sem acabamentos ou o núcleo) e como os materiais se unem.
- **Símbolos 2D do IFC.** Elementos importados ou vinculados de um arquivo IFC que trazem o seu próprio desenho de planta (uma projeção, o giro de uma porta, o símbolo de uma peça, com as cores, os preenchimentos e os textos do arquivo) o desenham nas plantas em vez do seu corte. Desmarque a opção em Configurações da vista (ou em um estilo de vista) para desenhar o corte 3D deles em vez disso.
- **Visibilidade** (<kbd>VV</kbd>) e **Gráficos** (<kbd>VG</kbd>). Eles mostram ou ocultam categorias e definem as suas penas, tipos de linha e preenchimentos.
- **Dividir** e **Janela.** Eles mostram duas vistas lado a lado ou abrem uma vista em uma janela separada.
- **Qualidade de exibição.** Como a vista 3D é desenhada (**Desenho**, **Médio** ou **Realista**, guardado neste computador: ver *Realista, com o sol*) e como a vista 3D desenha as peças de muitos triângulos (uma planta importada, móveis, tubulações) quando estão longe. **Equilibrada** desenha uma peça distante mais grosseira só onde a diferença é menor que um pixel. **Rápida** a deixa se desviar até 3 pixels, para modelos grandes e computadores mais lentos. **Precisa** desenha cada peça como ela é. A configuração pertence ao projeto. O modelo, os seus desenhos, as exportações e os quantitativos nunca são simplificados: uma curva é feita de trechos retos que se desviam no máximo um milímetro dela.
- **Sol e sombras.** O sol do projeto num dia a uma hora, iluminando a vista 3D com as suas sombras (ver *Realista, com o sol*).

![Qualidade de exibição](shots/display-quality.webp)

### Visibilidade / Gráficos

**Visibilidade** (<kbd>VV</kbd>) ou **Gráficos** (<kbd>VG</kbd>) abre a visibilidade / gráficos da vista à frente: uma planta, um corte, uma elevação ou uma vista 3D. O que ela oculta fica oculto também nas pranchas da vista, no seu PDF e DXF, e numa exportação 3D.

- **Categorias de modelo.** Cada categoria é mostrada ou oculta, com cor e espessura de linha, preenchimento e hachura de corte. A seta ao lado de uma categoria abre suas subcategorias: o **Núcleo**, os **Acabamentos**, o **Isolamento**, as **Membranas** e as **Outras camadas** de uma parede, uma laje ou um telhado (pelo material de cada camada e sua categoria em Gerenciar → Materiais), as **Ombreiras** de uma porta ou janela. Desmarque **Acabamentos** em **Paredes** e o reboco some, o tijolo fica. As armaduras, os revestimentos, as fôrmas, os andaimes, as instalações, as ligações de aço, os elevadores e o sombreamento são categorias próprias.
- **Categorias de anotação.** Cotas, textos, etiquetas, etiquetas de ambiente, eixos, níveis, marcas de corte e de elevação, chamadas, planos de referência, limites da vista e legendas, cada uma mostrada ou oculta nesta vista.
- **Categorias importadas.** Cada DXF importado na vista, pelo seu arquivo: mostrado, em meio-tom, e suas camadas uma a uma (a seta ao lado). Um PDF ou uma imagem para decalcar é mostrado ou oculto aqui também.
- **Filtros.** Por regras, como no Revit: **Novo filtro**, depois suas regras (classe IFC, categoria, tipo, nome, material, pavimento, grupo, vínculo, fase de criação, tipo predefinido ou um parâmetro, como *Resistência ao fogo* ou *Pset_WallCommon.IsExternal*; é igual a, contém, começa com, maior, menor, tem valor…), todas ou qualquer uma. A vista usa os filtros da sua lista, em ordem; o primeiro que pega um elemento decide se ele é mostrado. **Gerenciar → Filtros de vista** abre esta aba.
- **Vínculos.** Cada arquivo vinculado, com **Visível** e **Meio-tom** só para esta vista. **Exibição** é **Pela vista anfitriã** (o vínculo usa as categorias desta vista) ou **Personalizado**: então **Categorias…** dá ao vínculo suas próprias categorias, subcategorias e classes IFC nesta vista. A planta do arquiteto oculta as armaduras do vínculo estrutural; a do engenheiro, os acabamentos do vínculo de arquitetura.
- **Classes IFC.** As classes dos elementos próprios do projeto, mostradas ou ocultas.
- **Substituições gráficas.** Uma categoria recebe também um **Padrão** para as suas linhas de corte e de projeção, **Meio-tom** e uma **Transparência** (em 3D, de 0 opaco a 95); uma categoria de anotação uma **Cor** e **Meio-tom**; um filtro uma **Cor**, um **Padrão**, **Meio-tom** e uma **Transparência** para o que pega, o primeiro filtro da lista dando cada campo. **Nível de detalhe** (baixo, médio, alto) define quanto desenham os símbolos de uma planta; senão, pela escala.
- **Nível de detalhe.** Por vista, no topo do diálogo (também de uma vista 3D): **Baixo** desenha paredes, lajes e coberturas como um só contorno hachurado como o seu núcleo, sem linhas entre as camadas nem detalhes de portas e janelas; **Médio** desenha as suas camadas; **Alto** acrescenta os detalhes de portas e janelas (ombreiras, peitoris, guarnições, soleiras, peitoris internos, ferragens, venezianas, grades), os revestimentos dos vãos e as armaduras de um modelo vinculado. Deixada à escala, uma vista é de detalhe alto a 1:50 e maiores, médio até 1:100 e baixo a partir de 1:200 (os símbolos do terreno mantêm as suas escalas); uma planta 1:50 já é alta, então passe-a para médio ou baixo para ver o que muda. As paredes e lajes de um IFC vinculado ou importado dividem-se pelos conjuntos de camadas que o arquivo lhes dá, de modo que os seus **Acabamentos**, **Isolamento** e **Núcleo** se ocultam como numa parede nativa e o desenho só de núcleos do estilo Estrutural chega a elas; o reboco de uma parede trazido como peça própria, ou um revestimento de um só acabamento, vai com **Acabamentos**. Uma vista 3D de arquitetura deixa de fora as **Armaduras** até que sejam mostradas; uma de estrutura mostra-as.

As configurações são da vista, ou de um conjunto com nome que várias vistas compartilham (**Salvar como conjunto…**). Um estilo de vista pode nomear um conjunto (Gerenciar → Estilos de vista): uma vista sem configurações próprias usa o conjunto do seu estilo, como os modelos de vista do Revit; **Torná-los próprios desta vista** começa os seus a partir dele.

![Visibilidade / Gráficos: os acabamentos de uma parede ocultos](shots/vg-categories.webp)

![Uma estrutura vinculada, personalizada numa vista](shots/vg-links.webp)

![Um filtro que oculta o que suas regras pegam](shots/vg-filters.webp)

![Categorias de anotação](shots/vg-annotations.webp)

![Uma importação DXF por camadas](shots/vg-imports.webp)

![A cor e o padrão de um filtro na planta](shots/vg-overrides.webp)

![A planta com o seu filtro](shots/vg-plan.webp)

![Uma planta 1:50 em detalhe alto: as camadas das paredes](shots/detail-fine.webp)

![A mesma planta em detalhe baixo: um só contorno hachurado como o núcleo](shots/detail-coarse.webp)

### Nuvens de pontos nas vistas

Uma nuvem de pontos vinculada (*Importar e exportar*, Nuvens de pontos) aparece em todas as vistas. Uma **planta** desenha a fatia que o seu corte toma, 10 cm em volta do plano de corte, como pontos sob as paredes; um **corte** a fatia logo atrás do seu plano; uma **elevação** todos os pontos para os quais olha, o mais próximo à frente. Os pontos têm as cores da varredura, escurecidas para se lerem no papel, e imprimem-se nas pranchas e no PDF como a tela os mostra. Os seus pontos são pontos de snap: paredes desenhadas sobre a fatia de uma varredura caem nas suas faces. Na **vista 3D** as ferramentas de colocação e Move fazem snap nos pontos da nuvem como nos cantos. Uma planta nova abre enquadrada no modelo, ou na fatia da nuvem quando não tem mais nada; só se desenha a parte da fatia que está no ecrã, pelo que o levantamento de um bairro inteiro continua rápido com zoom num edifício.

**Visibility / Graphics** (<kbd>VV</kbd>) tem uma aba **Point clouds**: por nuvem, só nesta vista, **Shown**, **Halftone**, as suas **Colours** (como o seu vínculo, RGB, intensidade, cota ou classificação), a **Slice** que uma planta ou um corte desenha (cm, até 100 m: as fachadas de um levantamento aéreo pedem uma fatia grossa) e **Classes…**: as classes ASPRS dos seus pontos (terreno, vegetação, edifício, ruído…), cada uma visível ou oculta. A aba de uma vista 3D decide o que o 3D desenha. **Export 3D…** leva uma amostra das nuvens visíveis (no máximo 5 milhões de pontos) como pontos em GLB e glTF quando **Point clouds** está marcado; nenhuma outra exportação as leva.

![A fatia de uma varredura na planta, uma parede desenhada por cima](shots/point-cloud-plan.webp)

![Visibility / Graphics: a vegetação de uma nuvem oculta numa vista](shots/point-cloud-vg.webp)
