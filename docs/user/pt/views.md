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

Um corte feito através de uma viga ou de um pilar (**Corte aqui**, **Corte transversal** na guia Armadura 2D) também tem alças em planta: arraste qualquer uma das extremidades ao longo da linha para dar mais ou menos margem a esse lado, arraste o meio ao longo do elemento para mover a estaca em que ele corta. A janela do corte acompanha.

![Vista → Cortes… para a elevação sul](shots/sections-dialog.webp)

## Elevações

O **+** ao lado de **Elevações** cria as quatro elevações do edifício de uma vez.

![A elevação sul](shots/elevation-view.webp)

## Vistas 3D

<!-- video section-3d -->

Clique duas vezes em **3D** para abrir a vista 3D. Arraste com o botão esquerdo para orbitar em volta do modelo. A planta-chave no canto superior direito mostra de onde você está olhando. **Planta**, **Persp** e **Ajustar** mudam a câmera. Para guardar uma câmera, duplique a vista 3D.

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

## O que uma vista mostra

A guia **Vista** define o que a vista em primeiro plano mostra e qual é a sua aparência.

**Grade** (<kbd>F7</kbd>) oculta ou mostra a grade de fundo da planta, e **Linhas finas** (<kbd>TL</kbd>) desenha todas as linhas com um pixel de espessura, para ler um desenho denso. As duas valem só para a tela: pranchas, PDF e DXF mantêm as suas penas, e o snap na grade continua.

![A guia Vista](shots/ribbon-view.webp)

- **Configurações da vista.** A disciplina e o estilo da vista, a sua faixa (a altura e a profundidade do corte) e os seus gráficos.
- **Estilos de vista.** Arquitetônico, Estrutural (só os núcleos), MEP ou o seu próprio. Cada estilo escolhe as camadas desenhadas (todas, sem acabamentos ou o núcleo) e como os materiais se unem.
- **Símbolos 2D do IFC.** Elementos importados ou vinculados de um arquivo IFC que trazem o seu próprio desenho de planta (uma projeção, o giro de uma porta, o símbolo de uma peça, com as cores, os preenchimentos e os textos do arquivo) o desenham nas plantas em vez do seu corte. Desmarque a opção em Configurações da vista (ou em um estilo de vista) para desenhar o corte 3D deles em vez disso.
- **Visibilidade** (<kbd>VV</kbd>) e **Gráficos** (<kbd>VG</kbd>). Eles mostram ou ocultam categorias e definem as suas penas, tipos de linha e preenchimentos.
- **Dividir** e **Janela.** Eles mostram duas vistas lado a lado ou abrem uma vista em uma janela separada.
- **Qualidade de exibição.** Como a vista 3D desenha as peças de muitos triângulos (uma planta importada, móveis, tubulações) quando estão longe. **Equilibrada** desenha uma peça distante mais grosseira só onde a diferença é menor que um pixel. **Rápida** a deixa se desviar até 3 pixels, para modelos grandes e computadores mais lentos. **Precisa** desenha cada peça como ela é. A configuração pertence ao projeto. O modelo, os seus desenhos, as exportações e os quantitativos nunca são simplificados: uma curva é feita de trechos retos que se desviam no máximo um milímetro dela.

![Qualidade de exibição](shots/display-quality.webp)
