---
title: Portas e janelas
order: 30
summary: Coloque portas e janelas nas paredes, mova-as e projete os seus tipos no configurador.
---

<!-- video doors-windows -->

## Colocando uma porta ou uma janela

Escolha **Arquitetura → Porta** (<kbd>DR</kbd>) ou **Janela** (<kbd>WN</kbd>) e clique numa parede onde você quer a abertura. O Lapin corta a abertura em todas as camadas da parede e adiciona a verga que o tipo de parede pede.

Enquanto Porta ou Janela está ativa, o painel de propriedades mostra o tipo que ela vai colocar, com o seu tamanho: escolha outro ali antes de clicar na parede.

![Uma planta com uma porta e janelas](shots/openings-plan.webp)

Para mover uma porta ou janela ao longo da sua parede, selecione-a e arraste-a. Para mudá-la, use o painel de propriedades: o tipo, a altura do peitoril e para que lado ela abre (**Lado**). Com várias portas e janelas selecionadas, **Mão**, **Lado**, o peitoril e o tipo mudam todas num só passo, tal como uma seta de inversão na planta.

Para uma distância exata, use **Mover** (<kbd>M</kbd>) ou **Copiar** (<kbd>CO</kbd>) na linha de comando, como para qualquer elemento: selecione a porta, a janela ou a abertura na parede, clique num ponto base e depois digite a distância (`500`) ou clique num segundo ponto. Só conta a parte do movimento ao longo da parede: a abertura desliza pela sua parede e mantém o peitoril. Movida para outra parede do mesmo pavimento, ela passa para essa parede e continua abrindo do mesmo jeito para o cômodo. Uma cópia é uma nova porta ou janela do mesmo tipo, com o número seguinte. Se não couber (além do fim da parede ou sobre outra abertura), nada se move. **Girar** e **Espelhar** a posicionam do mesmo modo, e o espelhamento troca a sua mão.

![As propriedades de uma porta](shots/door-properties.webp)

## Aberturas sem porta nem janela

**Arquitetura → Abertura na parede** (<kbd>WO</kbd>) corta só uma abertura numa parede onde você clica: uma passagem, um alçapão, um vão para uma porta que vem depois. Defina a largura, a altura e o peitoril no painel de propriedades antes do clique. Com ela selecionada, o tamanho, a posição ao longo da parede e o peitoril são mudados ali; como uma porta, ela precisa caber na parede e ficar livre das outras aberturas da parede. No IFC, ela é uma abertura que vaza a parede, sem nada preenchendo-a.

Selecionada, uma abertura tem alças como uma janela: arraste o meio para deslizá-la ao longo da parede, ou um batente para alargá-la ou estreitá-la enquanto o outro fica parado; o comprimento junto ao mouse pode ser digitado, como ao desenhar. Ela é escolhida com um clique no seu vão na planta, ou no seu furo na vista 3D, onde o contorno se acende. **Mover** (<kbd>M</kbd>) a desliza ao longo da parede por uma distância digitada, e a altura e o peitoril estão no painel de propriedades.

**Forma superior** dá a forma da parte de cima: **Retângulo**, **Arco abatido** (um arco abatido), **Arco elíptico** (meia elipse, com um quarto da largura de altura, a menos que você diga outra coisa), **Semicírculo** ou **Cantos arredondados** (um topo reto com os cantos superiores arredondados). **Flecha do arco** define quanto sobe um topo segmentado ou elíptico, **Raio dos cantos** quanto os cantos são arredondados (150 mm, a menos que você diga outra coisa, nunca mais que metade da largura); vazio, o do próprio tipo. A abertura da parede segue o contorno em 3D, nas elevações e nos cortes e no IFC; uma planta mostra os batentes como antes.

## O configurador

**Arquitetura → Portas e janelas** abre o configurador. Todos os tipos de porta e de janela do projeto estão listados à esquerda. Você pode mudar:

- o nome e a marca do tipo (a letra nas etiquetas);
- a largura e a altura;
- a profundidade e a largura do marco;
- as colunas e linhas de folhas, e como cada folha abre;
- o acabamento da ombreira, a moldura na fachada, a soleira e as cores.

Os desenhos à direita mostram o tipo como ele será desenhado em elevação, planta e corte, enquanto você o muda. Abaixo deles, uma **pré-visualização 3D** mostra o tipo num pedaço de parede do primeiro tipo de parede do projeto, com o requadro, os peitoris, o forro e os materiais, enquanto você o muda (também uma janela em sacada, em arco ou em caixa): arraste para girar, use a roda para ampliar e arraste com o botão direito para mover. Começa vista pelo lado externo.

**Esquerda e direita.** A abertura de uma porta é nomeada como os arquitetos a nomeiam: olhe da maçaneta para as dobradiças; uma folha que abre à esquerda dessa linha abre à esquerda, uma que abre à direita, à direita. As opções do configurador para uma folha dizem assim, com o lado das dobradiças na **Abertura** de um módulo, vista pelo lado de fora, o lado de onde se empurram as folhas: *Abertura à direita (dobradiças à esquerda)*. Um arquivo IFC nomeia a mão do seu jeito (as dobradiças vistas olhando para onde a folha abre), então a mesma porta é escrita lá como SINGLE_SWING_LEFT; o Lapin converte uma na outra ao escrever e ler IFC. A esquerda e a direita de uma janela são vistas pelo lado de dentro, o lado para onde abrem as folhas: *De abrir à direita (dobradiças à esquerda)* no configurador, cujos módulos são vistos pelo lado de fora. O IFC escreve a mesma janela como esquerda. A elevação é vista pelo lado de dentro, o lado para onde as folhas abrem, a menos que **Pelo lado externo** esteja marcado; como a planta, ela mostra a porta como está no modelo. Uma porta invertida para a outra mão (**Mão**, ou uma seta de inversão ao longo da parede) é a imagem espelhada do seu tipo; as tabelas a mostram num cartão próprio (veja [Tabelas](schedules.md)).

![O configurador de portas e janelas](shots/joinery-configurator.webp)

**Salvar tipo** guarda as suas mudanças. Todas as portas ou janelas desse tipo as acompanham. **Posicionar com ele** fecha o configurador e começa a colocar esse tipo.

### Módulos, montantes e perfis

Uma porta ou uma janela é uma grade de módulos. Cada módulo escolhe como abre e o que o preenche, então um só tipo pode ter duas folhas com laterais fixas e uma bandeira em cima, ou uma janela com um painel cego sob uma folha.

**Colunas (relativas)** e **Linhas (relativas)** dividem o vão rapidamente: `1 1` são dois módulos iguais, `1 1 1` três, `2 1` um largo ao lado de um estreito. As linhas contam de baixo para cima.

**Larguras das colunas** e **Alturas das linhas** dão as mesmas medidas em milímetros. Digite-as e a última fica com o resto. Na elevação você também pode arrastar um montante ou uma travessa; a distância aparece enquanto arrasta.

**Largura do montante** e **Largura da travessa** têm medidas próprias, à parte da **Largura do quadro**. Um quadro de 50 mm com montantes de 50 mm aparece com 50 mm em toda parte.

**Módulos** lista todos os módulos. Clique em um, na lista ou na elevação, para editá-lo:

- **Abertura**: fixo, uma folha (de abrir lateral, oscilobatente, basculante para dentro, maxim-ar, pivotante, de correr) ou, numa porta, uma folha de porta.
- **Enchimento**: **Vidro** ou **Painel cego**. As folhas e laterais de uma porta podem ser cegas, e também um módulo de janela (um painel sob uma folha, um vão opaco).
- **Dividir**: **Em colunas** ou **Em linhas**, a uma distância em milímetros da borda esquerda ou inferior do módulo (metade por padrão). Dividida na horizontal, uma folha de porta mantém a parte de baixo e a de cima vira uma bandeira envidraçada fixa.
- **Unir**: com o módulo ao lado, acima ou abaixo, quando têm uma borda inteira em comum. Os montantes e travessas de que nenhum módulo precisa mais somem.

![Uma porta de loja num só tipo: duas folhas, laterais e uma bandeira em toda a largura](shots/joinery-panes.webp)

**Sistema de perfis** começa o tipo de um perfil de **PVC**, **Alumínio**, **Madeira** ou **Aço (esbelto)**: define as medidas do marco, dos montantes, das travessas e das folhas e dá aos perfis as suas formas finas. O marco tem um **Rebaixo** onde a folha fecha, a folha se sobrepõe ao marco pela **Sobreposição da folha**, **baguetes** prendem o vidro, e uma janela tem uma pingadeira sob a travessa inferior. Cada medida continua editável. **Simples (perfis retos)** mantém peças retangulares simples, como antes.

![Uma janela de PVC: as folhas sobrepostas ao marco, as baguetes, um painel cego sob um módulo fixo](shots/joinery-profiles-3d.webp)

Em **Detalhes**, **Peitoril interno** e **Peitoril externo** ligam e desligam os peitoris, e **Revestimento das ombreiras** liga o acabamento do vão nas ombreiras e na verga. Onde peitoris e revestimentos não são usados, desmarque-os.

### Venezianas, grades, floreiras e marquises

<!-- video window-dressings -->

Em **Na fachada**, um tipo de janela pode ter:

- **Venezianas**: de palhetas, de almofada ou de tábuas, de uma ou duas folhas, mostradas abertas (dobradas contra a parede ao lado da janela) ou fechadas sobre ela;
- uma **Grade de segurança**: na ombreira do lado do ambiente (o padrão, onde as grades de segurança costumam ir), na ombreira do lado da rua, na fachada ou na face interna, com barras horizontais, verticais ou ambas, com o seu tamanho e espaçamento;
- uma **Floreira** sob a janela;
- uma **Marquise** sobre ela, com o seu balanço e inclinação.

Cada uma se posiciona pelas faces reais da parede onde a janela está, a partir das camadas da parede, então fica certa em qualquer tipo de parede. Quando a janela tem uma moldura, o que está na fachada se afasta da moldura. Você pode mover cada uma à mão: **Da face da parede** define quanto ela se afasta da parede (para uma grade na ombreira, **Recuada da face** define quanto ela fica para dentro), **Do eixo da janela** a move ao longo da parede, e **Para cima ou para baixo** a move na altura. Elas aparecem em 3D, nas plantas, nas elevações e nos cortes, e as tabelas as medem.

![Venezianas, uma grade, uma floreira e uma marquise no configurador](shots/joinery-dressings.webp)

![As janelas guarnecidas, em 3D](shots/window-dressings-3d.webp)

### Formas, estilos e ornamentos

**Forma** dá a uma porta ou janela um topo em arco (**Arco pleno**, **Arco abatido**, **Arco elíptico**, **Cantos arredondados**, **Arco ogival**, **Topo triangular**) ou a torna uma janela **Redonda ou oval** (redonda quando a largura e a altura são iguais). Um tipo em arco mantém os seus vidros sob uma travessa na linha de nascença, e acima dela uma bandeira fixa que segue o topo, com barras se você quiser: um **Em leque**, **Os montantes prolongados** ou uma **Cruz**. **Flecha do arco** define quanto sobe um topo segmentado, elíptico, ogival ou triangular, **Raio dos cantos** quanto os cantos de um topo arredondado são redondos. A abertura da parede, o marco, o vidro, a moldura e os desenhos seguem todos o contorno.

**Travessas de vidro** dividem os vidros: **Quadriculado (colonial)** com os seus quadros na largura e na altura, **Prairie** com barras perto das bordas, ou uma **Losangos**.

Na fachada, além da moldura, uma janela pode ter uma **Fecho de arco**, um **Coroamento sobre a abertura** (uma **Cornija**, um **Frontão**, um **Frontão curvo** ou uma **Pingadeira** que segue um topo em arco) e um **Painel sob o peitoril** com os seus consoles.

**Aplicar um estilo…** define a forma, as barras de vidro e os ornamentos de uma vez: Moderno, Clássico, Georgiano, Vitoriano, Românico, Barroco, Gótico, Tudor, Prairie, Tradicional ou uma janela redonda. Tudo o que ele define continua editável. A lista mostra depois o estilo que o tipo tem; assim que muda algo que o estilo definiu, indica *Georgiano (modificado)*, e escolher o estilo de novo aplica-o de novo.

![Uma janela barroca: um arco pleno com leque de raios, uma pedra de fecho e um frontão segmentado](shots/joinery-styles.webp)

![Janelas em arco em 3D](shots/window-styles-3d.webp)

### Janelas de sacada, em arco e em caixa

Um tipo de janela pode sair da parede. No configurador, **Projects** torna-o um **Bay** (um centro e dois lados em ângulo), um **Bow** (painéis iguais num arco) ou um **Box** (lados em esquadria com a parede), e **New bay window** começa um tipo novo como sacada a 45°. A janela continua a preencher uma só abertura na parede, tão larga e alta como o tipo: coloca-se, move-se, copia-se, vira-se, numera-se e entra nos mapas como qualquer janela. **Architecture → Bay window** (<kbd>BW</kbd>) põe uma na parede clicada: o tipo escolhido no configurador quando sai da parede, senão a primeira sacada do projeto, senão uma sacada a 45°.

As colunas da grelha são as suas faces. Numa sacada e numa caixa a primeira e a última coluna são os lados e as do meio o centro; uma janela em arco tem uma face por coluna, as larguras repartindo o arco. Cada módulo abre como diz a sua **Abertura**, por isso os lados podem abrir e o centro ficar fixo. Depois:

- **Projection**, da face exterior da parede à face exterior dos caixilhos, e numa sacada o ângulo **Sides to the wall** (15° a 90°);
- **Sides**: os lados da caixa envidraçados ou cheios, com o seu material;
- **Seat inside**: o assento no peitoril, quanto entra na divisão, o seu material;
- **Sill outside** sob as faces;
- **Base**: nenhuma (suspensa na parede), **paredes até ao chão**, **uma laje em consola** ou **uma laje sobre consolas**, com espessura, avanço e material;
- **Head**: um telhado **de quatro águas**, **de uma água** ou **plano**, ou uma **laje**, a espessura no beirado, o avanço, a inclinação e o material, e um **rufo contra a parede**.

Caixilhos, folhas, vidros e puxadores são os do tipo, por isso os seus materiais e puxadores valem para todas as faces. **Bay style** define tudo de uma vez: sacada a 45° ou pouco funda a 30°, sacada assente no chão, janela em arco de cinco ou quatro painéis, caixa com lados envidraçados ou cheios, sacada sobre consolas. Como em **Aplicar um estilo…**, a lista mostra o estilo de sacada que a janela tem, com *(modificado)* assim que muda o que ele definiu. **Save as bay style…** guarda a configurada como estilo do projeto, junto aos do Lapin; renomeia-se ou elimina-se na lista sob os campos.

Em planta as faces são cortadas, o assento, o peitoril e a base desenhados sob o corte e o telhado a tracejado por cima; alçados, cortes e 3D mostram o corpo todo. O mapa de janelas tem uma ficha para o tipo, as faces desdobradas, com uma linha **Projecting** (tipo, saliência, faces); a sua área é a das faces envidraçadas e os detalhes medem o telhado, o assento e a base. Em IFC é um único IfcWindow de partição definida pelo utilizador (*Bay 45°, projection 600, 3 faces, 4 panels*) cujo corpo é toda a sacada, os painéis face a face; o Lapin lê a sacada de volta.

![Uma janela em arco no configurador](shots/bay-configurator.webp)

![Sacada, janela em arco, caixa e sacada sobre consolas em planta](shots/bay-plan.webp)

![Sacada, janela em arco, caixa e sacada sobre consolas em 3D](shots/bay-3d.webp)

### Puxadores

**Maçaneta** escolhe o puxador do tipo: uma **Alavanca**, uma **Bola**, um **Puxador** ou uma **Puxador de janela**, ou **Nenhum**. Por padrão, uma porta tem uma maçaneta de alavanca e uma janela uma manopla de janela. Uma porta tem o puxador nas duas faces, na **Altura da maçaneta** que você define; **Cor da maçaneta** define a sua cor. Os puxadores aparecem em 3D, nos cortes e nas elevações, e nos desenhos do configurador quando **Ferragens** está marcado.

### Parâmetros

**Parâmetros · nas tabelas**, embaixo no configurador, guarda os parâmetros próprios do tipo: **+ Resistência ao fogo** (guardada no Pset_DoorCommon ou no Pset_WindowCommon do IFC como FireRating), **+ Fabricante**, **+ Código do produto**, **+ Site**, ou qualquer parâmetro que você nomear. As portas e janelas do tipo os herdam, e cada um vira uma linha das tabelas de esquadrias: veja [Tabelas e gráficos](schedules.md).

## Palas de sombreamento

**Architecture → Sunshade** (<kbd>SN</kbd>) põe uma pala na porta ou janela clicada: do tamanho da sua abertura, para além das ombreiras, acima da padieira ou à frente, e segue-a quando se move. **Sunshade along a facade** toma dois pontos: numa parede corre ao longo da sua face exterior, noutro sítio ao longo da linha, para a direita. A barra das duas ferramentas escolhe o tipo, ou um estilo de que se faz um tipo novo.

Os tipos:

- **Canopy** (pala): laje, de lâminas ou de vidro, com saliência, espessura, inclinação e testeira, sobre consolas, tirantes ou postes;
- **Vertical fins**: profundidade, espessura, espaçamento e ângulo, travessas em cima e em baixo;
- **Louvres (brise-soleil)**: lâminas horizontais, planas, aerodinâmicas ou em caixa, profundidade, espaçamento e ângulo, afastadas da parede, entre placas de topo;
- **Egg-crate**: placas horizontais e verticais juntas;
- **Perforated screen**: uma chapa afastada da parede, com furos redondos ou quadrados ou rasgos, o seu tamanho e passo;
- **Awning** (toldo): lona inclinada, sanefa, caixa e braços articulados;
- **Pergola**: varas, uma viga, ripas por cima e postes até ao chão;
- **External venetian blind**: lâminas, quanto está descido, caixa e calhas guia.

**Sunshade types** (em Types) edita um tipo: o seu tipo e cada parâmetro, os materiais, ao lado do seu desenho de fora, em corte e em planta. Cada pala do tipo segue uma alteração. Um tipo novo parte de um estilo: pala de betão, de madeira sobre consolas, de vidro com tirantes, de lâminas, alpendre de telha, brise-soleil, aletas verticais, grelha de betão, painel perfurado em Corten, toldo de lona, pérgola de madeira, estore veneziano exterior; **Save as style…** guarda um tipo como estilo do projeto.

![As propriedades de uma pala após o cálculo do meio-dia](shots/sunshade-properties.webp)

Selecionada, uma pala mostra o seu tipo, onde está e a sua própria **Projection** (vazia: a do tipo). **Full shade at noon, 21 June** calcula, para uma pala, um toldo ou uma pérgola sobre uma janela, quanto tem de sair para que toda a janela fique à sombra quando o sol está mais alto no dia mais longo, no local do projeto (**Manage → Location**; Bucareste até se definir um), e define-o. Numa fachada sul a 45° N o sol está então a 68,4°: uma janela de 1,5 m sob uma pala 150 mm acima da padieira precisa de 650 mm. As suas pegas arrastam as pontas ao longo da parede ou linha e a saliência.

![Os tipos de pala: lâminas, de fora, em corte e em planta](shots/sunshade-types.webp)

As plantas desenham a tracejado uma pala acima do corte, cortada uma que o atravessa (aletas, um painel) e vista uma abaixo; alçados, cortes e 3D mostram o corpo com a trama do material, e projeta a sua sombra com o resto (**View → Sun and shadows**). **Sunshade schedule** lista-as por tipo com onde estão, comprimento e saliência, quantas e a área sombreada; as suas quantidades (comprimento, saliência, área, volume) tomam os artigos de custo do tipo. Em IFC são IfcShadingDevice de um IfcShadingDeviceType: um toldo AWNING, um estore JALOUSIE, as lâminas LOUVER em IFC 4.3 (definidas pelo utilizador em IFC4), as outras definidas pelo utilizador (Canopy, Fins, Egg-crate, Screen, Pergola); o Lapin lê-as de volta como eram.

![Palas em planta](shots/sunshades-plan.webp)

![Uma pala, lâminas, aletas, um toldo, uma pérgola e um estore em 3D](shots/sunshades-3d.webp)

## Etiquetas na planta

**Anotar → Identificadores de portas e janelas** coloca um marcador em cada porta e janela de uma planta: uma linha atravessando a parede, a marca do lado de fora e o tamanho e o peitoril do lado de dentro. Para mudar a aparência dos identificadores, vá em **Gerenciar → Padrões de desenho → Identificadores**.

### Editar um marcador na tela

Cada marcador pode ser arrumado à mão, parte por parte. Clique na sua linha ou num dos seus textos para selecioná-lo: cada texto tem então um grip quadrado (arraste para mover o texto) e um redondo depois do seu fim (arraste para girar o texto), e cada ponta da linha tem um grip (arraste para alongar ou encurtar a linha; o texto daquela ponta vai junto). Ctrl/⌘-clique num grip devolve aquela parte ao que era. Cada arraste é um passo de desfazer. Com vários marcadores da mesma planta selecionados, uma parte arrastada num muda a mesma parte em todos.

![O marcador de uma porta selecionado: um grip por texto, seu grip de giro e um em cada ponta da linha](shots/marker-grips.webp)

![A marca movida e girada, a ponta externa da linha alongada](shots/marker-edited.webp)

As alterações pertencem ao marcador, e portanto àquela planta: a mesma porta pode ser arrumada de outro jeito em outra planta. Elas são guardadas ao longo e através da parede, então seguem a porta quando ela se move, gira ou passa para o outro lado. Nas propriedades do marcador, **Marcador** lista suas partes: digite o giro de um texto em graus ou quanto uma ponta avança em milímetros no papel, devolva uma parte com ↺, ou todas com **Recolocar o rótulo no lugar**. O motor desenha os marcadores como editados, então as folhas, o PDF e o DXF os imprimem igual.

![As partes de um marcador nas suas propriedades](shots/marker-properties.webp)

Se os textos ficam através da linha do marcador (ao longo da parede, o padrão) ou ao longo dela é escolhido para cada tipo de etiqueta, em **Gerenciar → Padrões de desenho → Identificadores**, coluna **Direção do texto**: **perpendicular à linha da etiqueta** ou **ao longo da linha da etiqueta**. Nos dois casos são desenhados legíveis, nunca de cabeça para baixo; um texto girado à mão também fica legível.

![Os textos do marcador da porta ao longo da linha da etiqueta](shots/marker-along.webp)
