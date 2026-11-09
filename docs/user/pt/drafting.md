---
title: Desenho e a linha de comando
order: 80
summary: Digite comandos como no CAD, faça snap em pontos, mova e copie, desenhe linhas e anote com cotas, textos e identificadores.
---

## A linha de comando

Digite as letras de uma ferramenta em qualquer lugar da janela e pressione <kbd>Enter</kbd>. Por exemplo, digite <kbd>WA</kbd> para uma parede, <kbd>CO</kbd> para copiar, <kbd>M</kbd> para mover ou <kbd>L</kbd> para uma linha. A linha de comando, embaixo, mostra o que a ferramenta precisa em seguida. Você pode clicar num ponto ou digitar um comprimento, um ângulo ou coordenadas.

As coordenadas digitadas estão na unidade do projeto, assim como os comprimentos ao lado do cursor: num projeto em milímetros, `@4000,4000` depois do primeiro canto de um retângulo o faz com 4 m por 4 m, e `4000<90` são 4 m direto para cima.

![Copiando uma parede: depois do ponto base, a linha de comando e a entrada dinâmica mostram o comprimento e o ângulo até o mouse](shots/command-line.webp)

Cada ferramenta mostra as suas letras na dica; você pode mudá-las em **Vista → Atalhos de teclado…** (veja [A interface e os atalhos](interface.md)). Estas são as mais usadas:

| Modelo | | Desenho | |
|---|---|---|---|
| Parede | <kbd>WA</kbd> | Linha | <kbd>L</kbd> |
| Porta, janela | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilinha | <kbd>PL</kbd> |
| Piso | <kbd>SB</kbd> | Retângulo, círculo, arco | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Telhado | <kbd>RF</kbd> | Mover, copiar | <kbd>M</kbd>, <kbd>CO</kbd> |
| Escada, ambiente | <kbd>ST</kbd>, <kbd>RM</kbd> | Girar, espelhar, escalar | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Pilar, viga | <kbd>CL</kbd>, <kbd>BM</kbd> | Deslocar, aparar, estender | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Corte | <kbd>SE</kbd> | Concordância, chanfro | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Medir | <kbd>ME</kbd> | Hachura, texto | <kbd>H</kbd>, <kbd>T</kbd> |
| Enquadrar tudo | <kbd>ZE</kbd> | Apagar | <kbd>E</kbd> |

## Snaps e auxiliares

O Lapin faz snap nas pontas, nos pontos médios e nas interseções do que está desenhado, e na grade. <kbd>F3</kbd> liga ou desliga o snap, <kbd>F8</kbd> o modo orto e <kbd>F10</kbd> o rastreamento polar. O estado deles aparece no canto inferior direito da vista.

## Selecionando

Clique num elemento para selecioná-lo; <kbd>Shift</kbd>-clique adiciona à seleção. Para selecionar vários de uma vez, arraste com o botão esquerdo onde não há nada selecionado. Arrastada para a direita, a caixa é azul e pega o que está inteiramente dentro dela (janela); arrastada para a esquerda, ela é verde e tracejada e pega tudo o que toca (cruzamento), como no AutoCAD. Pressione <kbd>Space</kbd> enquanto arrasta e a caixa vira um laço que segue o mouse, mantendo o modo que a caixa tinha; <kbd>Space</kbd> de novo volta para a caixa. Na linha de comando, quando um comando pede para selecionar objetos, digite **WP** ou **CP** e clique nos cantos de um polígono, depois <kbd>Enter</kbd>: um polígono de janela ou um polígono de cruzamento.

![Um laço na planta: a caixa arrastada para a direita, depois Space, e o laço em volta das duas paredes que ele pega](shots/select-lasso.webp)

## Copiando e movendo

Para copiar, selecione o elemento e digite <kbd>CO</kbd>. Clique num ponto base e depois num segundo ponto. O Lapin continua copiando até você pressionar <kbd>Enter</kbd> ou <kbd>Esc</kbd>. **Mover** (<kbd>M</kbd>) funciona do mesmo jeito.

Enquanto você move ou copia, um marcador fica no ponto base e uma linha tracejada vai dele até o mouse, com um contorno esmaecido do que você move deixado onde estava até você clicar ou pressionar <kbd>Esc</kbd>. A linha e o marcador mudam de cor com o snap: verde num ponto com snap, azul quando o orto ou o rastreamento polar mantém a direção, a cor de destaque numa linha de rastreamento, cinza quando livre. Ao lado do mouse, o Lapin mostra quanto você moveu: Δx, Δy, a distância e o ângulo, nas unidades do projeto. Você pode digitar a distância e o ângulo ali, como quando desenha. Na interface Compacta, esses valores ficam na linha de status. Arrastar uma alça mostra o mesmo, e arrastar o próprio elemento selecionado também, a partir do ponto onde você pressionou; pressione <kbd>Esc</kbd> antes de soltar e o elemento fica onde estava.

![Movendo uma parede: o marcador no ponto base, a linha tracejada até o mouse, o fantasma da parede onde ela estava e o deslocamento no mouse](shots/move-trace.webp)

Você também pode arrastar. Arraste a cruz no meio de um elemento selecionado para movê-lo. Segure <kbd>Ctrl</kbd> (<kbd>⌘</kbd> num Mac) enquanto arrasta para deixar uma cópia.

Uma porta, uma janela ou uma abertura na parede se move e se copia ao longo da sua parede: só conta a parte do movimento ao longo da parede, então digitar `500` depois do ponto base a desliza 500 ao longo da parede; movida para outra parede, ela passa para essa parede. Com a parede também selecionada, ela vai junto com a parede.

Um eixo da malha move, copia, gira e espelha sozinho ou junto com outros elementos. Uma cópia é um novo eixo da mesma malha, nomeado a partir do último eixo que corre no mesmo sentido: com os eixos 1, 2 e 3, uma cópia de qualquer um deles é o 4.

## Desenho

A guia **Desenho** desenha linhas, polilinhas, retângulos, círculos e arcos. Ela os edita com deslocar, aparar, estender, concordância, chanfro e unir, preenche áreas com hachuras e os organiza em **Layers**. **Importar DXF** traz um desenho CAD como linhas em que você pode fazer snap e que pode editar.

![A guia Desenho](shots/ribbon-draft.webp)

**Deslocamento** (<kbd>O</kbd>) copia uma linha, uma polilinha, um retângulo, um círculo ou um arco a uma distância. Digite a distância na unidade do projeto (num projeto em milímetros `500` é meio metro; `2m` ou `1'6"` dizem a própria unidade), clique na forma e depois no lado para onde ela vai; ele pede a forma seguinte até você apertar <kbd>Enter</kbd> ou <kbd>Esc</kbd>. Digite **T** (Through) em vez disso e cada cópia passa pelo ponto clicado. Os cantos de uma polilinha ficam vivos (em meia-esquadria), como no CAD. O deslocamento funciona em plantas, cortes, elevações, em planos de referência e na vista 3D.

**Decompor** (<kbd>X</kbd>, ou **EXPLODE**) é o caminho de volta de **Unir**: selecione polilinhas ou retângulos e cada um vira as suas linhas, uma por lado, na mesma camada com a mesma caneta e cor e no mesmo grupo, em um único passo de desfazer. Círculos, arcos, hachuras e cadeias de cotas ficam como estão, e uma seleção sem nada para dividir avisa. Funciona em plantas, cortes, elevações, em planos de referência e no plano de trabalho da vista 3D.

Todo comprimento que a linha de comando pede está na unidade do projeto, e a mensagem diz qual: `Depth (mm)`, `Height (ft-in)`. Ângulos, fatores e contagens são números simples: `90` depois de **Girar** é 90°, seja qual for a unidade.

### Decalcar um PDF ou uma imagem

**Arquivo → Importar PDF ou imagem…**, ou **Desenho → PDF ou imagem**, traz um desenho para decalcar: uma planta digitalizada, a foto de uma fachada, um PDF de outro escritório. Ele entra na planta, no corte ou na elevação à frente, sob o seu desenho.

- **Um PDF**: escolha a **Página** e a escala em que o seu desenho foi feito (**Desenhado a 1:**). Um milímetro de papel mede então aqui tantos milímetros, assim uma planta em 1:100 entra no seu tamanho real. As linhas da página entram como desenho a que você pode se prender, que pode editar e decalcar, e as suas imagens (uma digitalização dentro do PDF) como imagens por baixo delas. Qualquer das duas pode ficar de fora. A página entra como um só grupo com o nome do arquivo, assim **Mover**, **Girar** e **Escala** a tomam inteira; clique duas vezes nela para trabalhar nas suas partes.
- **Uma imagem** (PNG ou JPEG): dê a sua **Largura**; a altura segue os seus pixels. **Desenho → Escalar imagem** a define depois exatamente: selecione a imagem, clique em dois pontos dela a uma distância conhecida e digite essa distância.

Uma imagem é transparente (a sua **Opacidade**, 0,5 por padrão, nas suas propriedades) e nunca faz parte do modelo: só sai impressa nas pranchas se você a deixar na vista.

![Uma planta impressa em PDF, trazida para outro nível para decalcar](shots/trace-pdf.webp)

### Desenhar na vista 3D

**Linha**, **Polilinha**, **Retângulo**, **Círculo**, **Arco** e as edições de Desenho funcionam também na vista 3D, com a linha de comando embaixo. Enquanto um comando roda, a etiqueta no alto diz onde você desenha: no nível do pavimento ativo ou no plano de referência selecionado. **Escolher uma face** na etiqueta, e depois um clique numa face do modelo, cria um plano de referência nessa face e desenha nele. Os pontos se prendem como numa planta: às linhas cortadas da planta (cantos e faces das paredes) e ao que está desenhado no mesmo plano, com orto e as linhas de rastreamento. O que você desenha é o mesmo elemento de desenho que um feito na planta, e aparece lá também.

![Desenhar na vista 3D: a etiqueta diz o plano, a linha segue o mouse no nível do pavimento](shots/draft-3d.webp)

### Os desenhos na vista 3D

O que você desenha numa planta (linhas, polilinhas, retângulos, círculos, arcos) aparece também na vista 3D, como linhas no nível do seu pavimento (o próprio nível, não a altura de corte); o que você desenha num plano de referência, num corte ou numa elevação fica nesse plano. Clique num deles em 3D para selecioná-lo e depois mova, copie, desloque ou apague. **Vista → Desenhos em 3D**, também em **Vista → Configurações da vista** de uma vista 3D, mostra ou oculta todos; fica ligado até você desligar. Cotas, textos, etiquetas, hachuras e linhas de construção (o papel draft) ficam nas suas vistas. Um DXF trazido para uma vista fica também em 3D, no plano dessa vista, com um interruptor próprio (veja [Importar e exportar](import-export.md)).

### Ordem de desenho

Onde elementos desenhados se sobrepõem, o desenhado por último fica por cima. Para mudar isso, selecione-os (linhas, polilinhas, retângulos, círculos, arcos, hachuras e preenchimentos, textos, cotas, identificadores, as formas de um DXF), clique com o botão direito e escolha **Trazer para a frente**, **Avançar**, **Recuar** ou **Enviar para trás**; **Desenho → Ordem** e **Anotar → Ordem** têm o mesmo, com <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> e <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ no Mac). Avançar e recuar passam o próximo elemento que a seleção sobrepõe. O desenho 2D fica sobre o desenho do modelo; **Enviar para trás do modelo** o coloca sob o corte das paredes e as linhas vistas (uma hachura sob uma parede), e um clique ali pega então a parede; **Trazer para a frente** ou **Enviar para trás** o trazem de novo sobre o modelo. Na linha de comando, **DRAWORDER** (**DRO**) pede os objetos e depois **Above objects** ou **Under objects** (clique nos elementos acima ou abaixo dos quais eles vão, depois <kbd>Enter</kbd>), **Front**, **Back** ou atrás do modelo, **Model**. A ordem é da vista, desfaz-se em um passo, e as pranchas, o PDF e o DXF a imprimem como você a vê; o que você desenhar depois fica por cima.

![Clique direito em um preenchimento selecionado: a ordem de desenho](shots/draw-order-menu.webp)

![Trazido para a frente, o preenchimento cinza cobre o texto](shots/draw-order-front.webp)

## Anotando

A guia **Anotar** tem as cotas, os textos, os eixos da malha, os níveis e os identificadores. Uma cota lê o modelo, então ela se atualiza quando uma parede se move. A aparência de textos, cotas, eixos e identificadores é definida em **Gerenciar → Padrões de desenho**.

Uma cota alinhada ou linear entra como uma cadeia: depois dos dois primeiros pontos e da sua linha, cada clique adiciona o próximo ponto na mesma linha, e **Enter** (ou **Esc**) faz da cadeia inteira uma só cota (**U** desfaz o último ponto; digite **S** antes para uma cota simples). Uma cadeia se comporta como uma polilinha: um clique a seleciona inteira, e ela se move, é copiada e é excluída como uma só. Uma cadeia que mede paredes, bordas ou eixos da malha fica presa neles: arrastada ou movida sozinha, só a sua linha de cota desliza, perpendicular à cadeia, para que um deslize do mouse nunca a deixe medindo o vazio; movida junto com o que mede, ela vai junto. As suas alças ficam em cada ponto medido (arraste para movê-lo, Ctrl/⌘-clique para retirá-lo), no meio de cada segmento (arraste para adicionar um ponto, Ctrl/⌘-clique para adicionar um na metade) e nas pontas da linha de cota (arraste para mover a linha). Para pôr mais pontos numa cadeia, como no ArchiCAD, selecione-a e clique em **Anotar → Adicionar pontos** (ou digite **DIMADD**, ou **Adicionar pontos** nas propriedades): cada ponto que você clica entra na cadeia onde cai ao longo da sua linha, entre os pontos vizinhos ou além de uma ponta como a nova ponta, e fica preso na parede, na borda ou no eixo em que cai; **Enter** ou **Esc** termina. Para retirar pontos, Ctrl/⌘-clique nas suas alças, ou clique em **Anotar → Remover pontos** (**DIMDEL**, ou **Remover pontos** nas propriedades) e clique perto de cada ponto; os segmentos de cada lado viram um só. O valor de cada segmento também tem uma alça: arraste-a para tirar o valor do seu lugar onde os segmentos são curtos e os valores se sobreporiam; quando ele fica fora do seu segmento, uma linha de chamada fina o liga de volta. Ctrl/⌘-clique na alça, ou **Textos de volta à linha** nas propriedades, o põe de volta. Um valor movido fica com o seu segmento quando os pontos se movem e gira com a cadeia. Dê um clique duplo num segmento para digitar o seu valor: o elemento que ele mede se move, e tudo o que vem depois dele na cadeia também. As propriedades mostram o valor e o texto de cada segmento, dividem uma cadeia em cotas simples e unem cotas simples que continuam uma à outra numa só linha.

As anotações de uma planta (cotas, textos, linhas desenhadas, etiquetas) podem passar para outra planta no mesmo lugar, pois as plantas partilham as coordenadas: selecione-as e escolha a planta nas propriedades, em **Vista** para as mover ou em **Copiar para** para as copiar. Assim as cotas feitas numa planta de situação vão para a planta do nível. Os pontos de uma cota prendem-se então ao que a nova planta corta nesse lugar: paredes, arestas, eixos.

**Alturas de aberturas.** Um segmento de uma cadeia de cotas que vai de uma ombreira à outra de uma porta, janela ou vão escreve a largura acima da linha e a altura da abertura abaixo dela, como nos desenhos romenos e europeus. É lida do modelo a cada vez, então acompanha a abertura quando ela muda de tamanho, é movida ou recebe outro tipo; um segmento sobre o qual você escreveu um texto não a escreve. **Gerenciar → Padrões de desenho → Dimensões → Altura da abertura** a define para cada estilo de cota: desligada, `150`, `h=150`, `h=150` com o peitoril `p=90` abaixo, ou `150/90`, na unidade do estilo. Funciona em plantas, cortes e elevações, e as pranchas, o PDF e o DXF a imprimem. Projetos novos a têm ligada (só a altura); um projeto feito antes mantém suas cotas como estavam até você ligá-la ali.

![As alturas da porta e da janela sob seus segmentos](shots/dimension-opening-height.webp)

![A altura da abertura nos estilos de cota](shots/dimension-opening-height-style.webp)

**Cotas sobre uma linha.** Um ponto de cota clicado num eixo da malha ou no eixo de uma parede prende-se à linha, não a um ponto dela. Arraste a extremidade do eixo ao longo dele próprio para que a sua bolha caiba na vista, ou alongue ou encurte a parede: a cota continua a medir onde você a pôs e a sua linha não roda. Se o eixo ou a parede se desloca na transversal, o ponto vai com ele, no mesmo sítio ao longo. Uma cota na face de uma parede também fica onde media quando a parede é esticada no seu comprimento. As cotas feitas antes prendem-se a um eixo da malha da mesma maneira; uma no eixo de uma parede prende-se de novo clicando outra vez no seu ponto.

### Identificadores

**Anotar → Identificador** (<kbd>TG</kbd>) coloca numa vista uma caixa com o que um elemento diz de si mesmo, e uma linha de chamada até ele. Enquanto a ferramenta está ativa, a barra sob a vista escolhe o que o identificador mostra: a marca, o nome, a classe ou o tipo, **Resistência ao fogo**, **Isolamento acústico**, **Transmitância térmica** ou **Outro parâmetro…** pelo nome; ou um tipo de etiqueta dos padrões de desenho; e **CHAMADA** liga ou desliga a linha de chamada. Clique no elemento e depois onde vai o identificador (digite antes **E** para dobrar a linha num cotovelo). **Anotar → Etiquetar um parâmetro** pede o nome do parâmetro e inicia a ferramenta com ele.

![A barra da ferramenta Identificador sob a vista](shots/tag-options.webp)

Um identificador lê qualquer parâmetro: o do elemento, o do seu tipo (então todos os elementos do tipo o mostram), uma propriedade padrão como `Pset_WallCommon.FireRating`, ou uma com que veio um elemento importado. Um nome é encontrado também sem espaços, então **Fire rating** e `FireRating` são o mesmo. Para preenchê-lo, use **Parâmetros** nas propriedades do elemento ou do seu tipo: digite um nome (são sugeridos Fire rating, Sound rating, Moisture resistance e Thermal resistance) ou escolha **Adicionar uma propriedade padrão…** para as propriedades IFC da sua classe. O identificador acompanha assim que há um valor, e os comprimentos são escritos nas unidades do projeto.

Um identificador selecionado tem pegas. Arraste a caixa para mover o identificador: a linha de chamada continua a apontar para onde apontava. Um identificador acompanha o seu elemento: quando o elemento é movido, girado, espelhado ou remodelado pelas suas pegas, o identificador o segue mantendo o seu lugar ao lado dele, e a linha de chamada continua nele; um identificador que você moveu mantém o afastamento que lhe deu. Arraste o meio da linha para dobrá-la num cotovelo (Ctrl/⌘-clique no cotovelo endireita-a) e a sua ponta para apontar para outro ponto (Ctrl/⌘-clique: de volta ao meio do elemento). As propriedades mudam o que o identificador **Mostra**, dão-lhe um **Texto** próprio (`{mark}|{FireRating}` escreve duas linhas), um **Tipo de etiqueta**, ou desligam a **Linha de chamada**. O motor desenha os identificadores, por isso folhas e PDFs imprimem-nos como os vê. Os tipos em **Gerenciar → Padrões de desenho → Identificadores** leem os mesmos campos nos seus modelos; um projeto novo traz entre eles Resistência ao fogo, Isolamento acústico e Transmitância térmica.

**Linhas de chamada como as etiquetas do ArchiCAD.** Por padrão, a linha de chamada de um identificador é *com cotovelo*: uma linha oblíqua do elemento até um cotovelo, depois um ombro que passa por baixo do texto e sublinha a sua primeira linha, sem caixa em volta do texto. Depois do clique no elemento, o clique seguinte é o cotovelo, e o texto fica ao lado. Arraste a pega do cotovelo: a linha oblíqua mantém 30°, 45°, 60° ou 90° a partir da ponta (segure <kbd>Shift</kbd> para qualquer ângulo), e arrastada além da ponta o texto passa para esse lado, alinhado com o cotovelo. Arraste o texto para movê-lo com o seu cotovelo. Em **Gerenciar → Padrões de desenho → Identificadores**, **Linha de chamada…** num tipo de identificador (e o editor de composição) define a **Forma da linha de chamada** (com cotovelo, ou reta a partir de uma caixa), o que o ombro sublinha (**Sublinhado**: a primeira linha, cada linha, a última linha ou nada, quando para ao lado do texto) e **Extensão mm**, quanto o ombro se estende além do texto, ao lado da pré-visualização do motor. Com uma linha de chamada por camada, cada uma termina sob a sua própria linha, com os cotovelos numa coluna; arrastar o cotovelo move a coluna. **Forma da linha de chamada** nas propriedades de um identificador prevalece sobre a do tipo; um identificador cuja linha de chamada foi dobrada num cotovelo livre mantém a linha reta. As etiquetas de projetos feitos antes mantêm a linha de chamada reta numa caixa.

![A linha de chamada de um identificador dobrada, com as suas pegas](shots/tag-leader.webp)

![As propriedades de um identificador](shots/tag-properties.webp)

O marcador de uma porta ou janela é editado parte por parte, cada texto movido ou girado e cada ponta da sua linha alongada: veja [Portas e janelas](doors-windows.md).

**Rodar um identificador.** Nas propriedades de um identificador, **Rotação** roda o seu texto e o seu ombro ou caixa num ângulo, em graus a partir da horizontal, e **Alinhar ao elemento** faz com que siga a direção do elemento que identifica (o eixo de uma parede ou de uma viga, a aresta mais longa de qualquer outro) e rode com ele; a rotação soma-se então a essa direção. Um identificador é sempre desenhado legível, nunca de cabeça para baixo. Um identificador selecionado tem sobre o texto uma pega de rotação: arraste-a de 15° em 15° (incluindo 45° e 90°; mantenha <kbd>Shift</kbd> para qualquer ângulo), e Ctrl/⌘-clique volta a pôr o texto na horizontal. A linha de chamada continua a alcançar o elemento. Os identificadores simples e os de composição rodam da mesma forma, em plantas, cortes e alçados, e as pranchas, o PDF e o DXF imprimem-nos como você os vê.

**O que um identificador identifica.** Enquanto a ferramenta **Identificador** espera um elemento, o que está sob o cursor é realçado com a cor de destaque; passe o cursor sobre um identificador colocado, ou selecione-o, e o elemento que identifica é realçado.

![Um identificador alinhado à sua parede, com a pega de rotação, e a parede identificada realçada](shots/tag-rotate.webp)

#### Identificador de composição

Um identificador de composição escreve as camadas de um elemento como uma nota de arquitetura: um cabeçalho (*EXTERIOR*), uma linha por camada, um rodapé (*INTERIOR*). Um projeto novo tem **Build-up (exterior → interior)**: cada linha é a descrição do material (o seu nome, se não tiver), o seu λ, se o tiver, e a espessura da camada em mm. Escolha-o na barra da ferramenta **Identificador** e clique numa parede, numa laje, numa cobertura ou no acabamento de um compartimento; os outros elementos recebem o campo escolhido. O texto é lido do modelo sempre que é desenhado: mude uma camada, a descrição de um material ou o seu λ, e o identificador acompanha.

![Dois identificadores de composição numa parede de fachada: uma linha de chamada, e uma por camada](shots/buildup-tag.webp)

A sua forma define-se em **Gerenciar → Padrões de desenho → Identificadores**: ponha a **Forma** de um tipo de identificador em *composição*, ou clique em **Composição…** num deles, para o editar ao lado de uma pré-visualização num dos tipos de parede ou de laje do projeto.

- **Cabeçalho** e **Rodapé**: escritos acima e abaixo das camadas (vazio: nenhum; `|` começa uma nova linha).
- **Cada camada**: a linha escrita uma vez por camada. Lê os campos da camada, `{description}`, `{material}`, `{thickness}` (na unidade escolhida em **Espessura em**), `{note}` (a nota da camada no seu tipo), `{function}` (núcleo, acabamento, isolamento…) e `{index}`; qualquer parâmetro do material pelo nome ou símbolo, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (veja os [Dados](materials.md#dados-descricao-parametros-como-se-mede) do material); e os do elemento, `{name}`, `{type}`, `{mark}`. **Inserir um campo…** põe um onde está o cursor e lista os parâmetros que os materiais do projeto têm.
- **Os colchetes** tornam uma parte opcional: `[, λ = {λ} W/mK]` só se escreve onde o material tem λ; uma parte cai inteira quando um campo nela está vazio. **[ ]** faz da seleção uma parte assim. Para escrever o próprio colchete, digite `\[` ou `\]`.
- **Ordem**: do exterior para o interior de uma parede (o lado da sua primeira camada é o seu exterior, por isso uma parede espelhada continua a começar aí), de cima para baixo numa laje ou numa cobertura; ou ao contrário. Escolha a ordem em que as camadas estão ao lado da nota, para que as linhas de chamada por camada nunca se cruzem.
- **As camadas sem espessura** (membranas, películas, tinta) são escritas, a menos que desmarque **visíveis**. Uma camada que muda de material em altura escreve uma linha por material.
- **Linhas de chamada**: uma, ao elemento, ou **uma por camada**, cada uma da sua linha até um ponto dentro da sua camada, onde a vista distingue as camadas (uma parede em plantas e cortes, uma laje em cortes; noutros casos o identificador mantém uma). Arraste a ponta da linha de chamada ao longo do elemento para o atravessar noutro sítio.
- **As linhas começam com** um marcador, um travessão, o seu número ou nada; quebram em **Largura mm** no papel, recuadas após o marcador, e um valor fica junto ao seu símbolo e à sua unidade (`λ = 0.035 W/mK`).

![O editor de um tipo de identificador de composição ao lado da sua pré-visualização](shots/buildup-tag-type.webp)

Como qualquer identificador, o identificador de composição acompanha o seu elemento, e as folhas e os PDF imprimem-no como o vê, com λ, μ e ρ. Um **Texto** próprio nas suas propriedades substitui a linha por camada só para esse identificador.

### Eixos da grade

**Anotar → Eixo de grade** (<kbd>GR</kbd>) desenha um eixo pelas suas duas pontas; recebe o rótulo seguinte da sua direção. **Anotar → Grelha de eixos…** coloca uma grade inteira de uma vez: os espaçamentos **Na transversal** (os eixos numerados, da esquerda para a direita) e **Na longitudinal** (os eixos com letras, de baixo para cima) nas unidades do projeto, como `6000 6000 4500` ou `3*6000`; a **Origem** onde se cruzam os primeiros eixos, um **Ângulo**, quanto os eixos se prolongam **Além da grelha**, os **Primeiros rótulos** e o **Tipo de eixo**. Com **substituir os eixos da grelha** marcado, os eixos desenhados até então desaparecem.

![O diálogo Grelha de eixos](shots/grid-dialog.webp)

Clique num eixo para selecioná-lo. As suas propriedades mostram o **Início** e o **Fim**, o **Comprimento**, o **Ângulo** e o espaçamento desde o eixo paralelo anterior (↔). Digite um novo espaçamento e o eixo move-se; os eixos seguintes também se movem, a não ser que desmarque **os eixos seguintes também se movem**. **Bolhas** escolhe que pontas têm bolha. **Estilos de eixo…** abre **Padrões de desenho → Eixos da grade**, onde se ajustam a bolha, o seu tamanho, o tipo de linha e o primeiro rótulo. Selecione a grade inteira para ver os seus eixos e espaçamentos.

![As propriedades de um eixo](shots/axis-properties.webp)

Todos estes comprimentos seguem as unidades do projeto. Em pés e polegadas, pontas e espaçamentos leem-se 19'-8 1/4", e os níveis também são escritos em pés e polegadas.

![O mesmo eixo em pés e polegadas](shots/axis-properties-imperial.webp)

![A guia Anotar](shots/ribbon-annotate.webp)
