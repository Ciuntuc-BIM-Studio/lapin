---
title: Pisos e telhados
order: 40
summary: Desenhe pisos e telhados pelo contorno, escolha a forma e as bordas de um telhado e corte aberturas.
---

## Pisos

Escolha **Arquitetura → Piso** (<kbd>SB</kbd>) e clique nos cantos do contorno. Feche o contorno no primeiro ponto ou pressione <kbd>Enter</kbd>. Um piso também é feito de camadas, definidas em **Arquitetura → Tipos de piso**. Uma camada de um tipo de piso pode ter espessura 0 (uma impermeabilização, a pintura): ela não constrói nada, é desenhada como uma linha nos cortes e é medida pela área do piso.

Em **Tipos de piso**, cada camada tem uma **Prioridade** (de 0 a 100) e uma **Categoria**, como as camadas de um tipo de parede. Uma camada de parede que desce no piso (**Nos pisos** em **Tipos de parede**) atravessa as camadas do piso de prioridade menor e para na primeira de prioridade igual ou maior. Vazia, a prioridade é a do material em **Gerenciar → Ordem de corte**, senão 50 para o núcleo e 0 para as demais camadas. Uma camada marcada **Acabamento** sai quando uma vista omite os acabamentos dos pisos.

Para mudar o contorno, selecione o piso e arraste os cantos. Para adicionar um canto, mantenha <kbd>Ctrl/⌘</kbd> pressionado e clique no pequeno losango no meio de uma aresta, ou clique com o botão direito no piso selecionado perto de uma aresta e escolha **Adicionar canto aqui**: o novo canto fica onde você clicou. Para remover um, faça <kbd>Ctrl/⌘</kbd>-clique nele, ou clique com o botão direito e escolha **Remover canto** (um contorno mantém pelo menos três cantos). Cada ação é um único passo de desfazer, e o mesmo vale para aberturas no piso, telhados, ambientes e varandas. **Abertura no piso** (<kbd>SH</kbd>) corta um furo no piso, para uma escada ou um shaft.

Para aumentar ou reduzir todo o contorno de uma vez, cada aresta pela mesma distância (o *Offset all edges* do Archicad), mantenha <kbd>Shift</kbd> pressionado e arraste o losango no meio de qualquer aresta: o contorno acompanha o cursor ao vivo, em planta ou em 3D, e a distância pode ser digitada enquanto você arrasta (<kbd>Enter</kbd> a fixa). Ou clique com o botão direito no piso selecionado, escolha **Deslocar contorno…** e digite a distância: positiva vai para fora, negativa para dentro. As arestas retas continuam paralelas e voltam a se encontrar nos seus cantos; uma aresta curva continua sendo um arco em torno do seu próprio centro, e onde um arco encontra outra aresta em um canto, o aumento arredonda esse canto. Um deslocamento que faria uma aresta desaparecer ou o contorno se cruzar é recusado, com o motivo junto ao cursor. Funciona em pisos, aberturas no piso, forros, telhados (cada aresta mantém a sua regra de beiral ou empena), ambientes e varandas, e em vários selecionados de uma vez; cada um é um único passo de desfazer.

![O contorno de um piso deslocado arrastando com Shift o meio de uma aresta](shots/floor-offset-outline.webp)

### Arestas curvas, arredondamentos e pisos redondos

A barra da ferramenta **Piso**, na linha de comando, desenha o contorno como **Polígono** (clicando nos cantos), **Retângulo** (dois cantos opostos), **Círculo** (o centro e depois um ponto nele) ou **Dentro das paredes** (um clique dentro das paredes que o fecham; o piso vai até as faces delas). Num polígono, **Linhas (Espaço)** passa a **Arcos (Espaço)**: as arestas seguintes são arcos, cada um tangente à aresta anterior, até premir <kbd>Espaço</kbd> de novo. **Abertura no piso** e **Forro** têm a mesma barra, sem **Dentro das paredes**.

![A ferramenta Piso desenhando um círculo ao lado de um piso](shots/floor-shapes.webp)

A mesma barra serve todas as ferramentas de contorno (laje, abertura de laje, teto, furo, ambiente e varanda por contorno, plataforma, lote), e três das suas escolhas tomam o contorno inteiro com um clique: **Inside walls** (as paredes que se fecham à volta do ponto), **From drawn shape** (a menor forma desenhada fechada à volta do ponto: uma polilinha fechada, um retângulo ou um círculo, que fica redondo) e **From room** (o ambiente à volta do ponto, com as suas arestas curvas).

![A barra das ferramentas de contorno, com From drawn shape escolhido](shots/outline-sources.webp)

Para laje, abertura de laje, teto e ambiente por contorno a barra oferece também **Ellipse**: o centro, a ponta de um semieixo e depois o outro. As arestas do contorno seguem a elipse verdadeira (quatro quartos), medidas exatamente; um canto tirado da elipse torna retas as suas duas arestas, e um canto junto a uma aresta elíptica não é arredondado. O IFC leva-a como IfcEllipse. **From drawn shape** toma do mesmo modo uma elipse desenhada. Um furo próprio de uma laje ou de um teto (**Hole in the outline**) também aceita uma elipse. Deslocar o contorno inteiro (Shift-arrastar, **Offset outline…**) faz de uma aresta elíptica a curva verdadeira a essa distância, em arestas retas curtas (a curva a uma distância de uma elipse não é uma elipse); para dentro pára antes de a curva se dobrar na curvatura mais apertada da elipse.

![Uma laje elíptica a ser desenhada ao lado de uma elipse desenhada](shots/ellipse-floor.webp)

Qualquer aresta de um piso, de uma abertura no piso ou de um forro pode ser curva. Clique com o botão direito no piso selecionado perto de uma aresta e escolha **Tornar arco**: a aresta curva-se para fora um oitavo do seu comprimento e aparece uma alça redonda no meio do arco. Arraste-a para curvar o arco pelo ponto desejado (o losango, agora no meio da corda, continua a mover a aresta). **Tornar reto** volta a fazê-la uma linha. Um canto entre duas arestas retas pode ser arredondado ou chanfrado: clique nele com o botão direito e escolha **Arredondar canto…** (digite o raio) ou **Chanfrar canto…** (digite quanto recua ao longo de cada aresta). O canto torna-se dois, onde o arco ou o chanfro encontra as arestas; um raio grande demais para as arestas é recusado. Cada um é um passo de desfazer.

![Um piso com uma aresta curva e um canto arredondado](shots/floor-curved-edge.webp)

O piso é construído ao longo dos seus arcos, nenhuma corda a mais de 1 mm deles, em 3D, plantas e cortes; a sua área e o seu perímetro são medidos nos arcos verdadeiros. Uma camada que passa uma aresta curva (veja [Extensões de camada](walls.md#layer-extents)) segue o arco. Uma abertura também pode ter arestas curvas, e um piso curvo pode ter aberturas. Em IFC o piso leva o seu contorno com os arcos verdadeiros como pegada (uma IfcIndexedPolyCurve com segmentos de arco), ao lado do seu corpo. As coberturas mantêm arestas retas, porque as suas regras de aresta (beiral, empena) são definidas por aresta reta.

Uma laje ou um teto falso pode ter **furos próprios**: ilhas recortadas do seu contorno, parte do elemento, não aberturas à parte. Escolha **Hole in the outline** na ferramenta **Opening** (em *In a floor*) e desenhe-o dentro da laje (ou da laje ou teto selecionado) com a mesma barra: um polígono cujas arestas podem ser arcos, um retângulo ou um círculo. Selecionada, a laje mostra os cantos e as arestas de cada furo como pegas, com o mesmo clique direito (**Make arc**, **Fillet corner…**, **Chamfer corner…**); a cruz no meio de um furo move-o, e o seu clique direito oferece **Remove hole**. A laje é construída em volta dos furos e a sua área deixa-os de fora, medidos nos arcos verdadeiros; um furo tem de ficar dentro do contorno e afastado dos outros, e um contorno alterado de modo a já não o conter é recusado. Em **Edit sketch** os furos também são laços do esboço: desenhe outro laço fechado lá dentro e **Finish sketch** torna-o um furo (o maior laço é o contorno). No IFC os furos são mais curvas da pegada da laje, lidos de volta como furos. Uma plataforma (**Site → Hole in platform**) aceita furos da mesma forma: o terreno dentro do furo mantém as suas cotas.

![Uma laje com um furo retangular e um redondo no seu próprio contorno, selecionada](shots/floor-holes.webp)

#### Editar esboço

Para mais do que um canto ou um arco, clique com o botão direito no piso selecionado (ou abertura no piso, ou forro) e escolha **Editar esboço** (também **Studio → Editar esboço**). O seu contorno abre como esboço numa planta própria, uma linha ou um arco por aresta, os cantos mantidos juntos e o próprio piso oculto ali; uma barra sobre a planta diz de quem é o esboço. Desenhe e altere com o separador **Desenho** (Linha, Arco, Concordância, Aparar, Estender, Deslocar…) e restrinja e cote com **Studio → Restrições** (veja [Restrições de esboço](sketch-constraints.md)): mude uma cota e o contorno acompanha. **Terminar esboço** escreve-o de volta no piso num só passo de desfazer, e o piso mantém a sua identidade (o seu GlobalId); **Cancelar** deixa o piso como estava. O esboço tem de ser um só contorno fechado (arestas ponta com ponta, ou um círculo sozinho); caso contrário, Terminar diz o que está aberto.

![O contorno de um piso editado como esboço: uma concordância e uma cota](shots/floor-sketch-edit.webp)

O esboço, com as suas restrições e cotas, fica no piso (vai para o IFC com a intenção de projeto do piso, em Lapin_Intent), por isso **Editar esboço** abre-o de novo como o deixou. Se entretanto o contorno foi alterado pelas alças, o esboço recomeça a partir do contorno, sem as suas restrições. Um piso reconhecido de um ficheiro IFC só recebe um esboço quando o edita; um mantido como referência não é editado assim.

![O piso depois de Terminar esboço](shots/floor-sketch-done.webp)

Um piso desenhado fora das paredes, diante de uma porta, é uma varanda. Para os degraus até ela, veja [Escadas](stairs.md).

Uma camada de uma laje também pode ir além do seu contorno, ou parar antes, à volta toda ou numa aresta, escrita nas suas propriedades ou arrastada em planta ou em corte com **Editar camadas**, e um tipo de laje pode dar às suas lajes as suas extensões: veja [Extensões de camadas](walls.md#extensoes-de-camadas). Os telhados ainda não têm composição de camadas (a cobertura, a laje e a estrutura são partes próprias), por isso não têm extensões de camadas.

**Modificar → Dividir elemento** numa laje ou num teto pede dois pontos de uma linha de corte: a linha continua para lá deles e tem de cruzar o contorno exatamente duas vezes. A laje passa a ser duas lajes; a parte clicada mantém o id (GlobalId em IFC), os arcos nos bordos continuam arcos (um arco cortado continua um arco) e um vão da laje vai com a parte em que está. Um vão que a linha atravesse é recusado. **Modificar → Aparar/estender elementos** aceita um limite reto (uma parede, uma viga ou uma linha desenhada) e corta aí a laje ou o teto, mantendo o lado clicado.

![Uma laje com um bordo curvo dividida por uma linha: a parte da direita selecionada](shots/floor-split.webp)

Os furos próprios de uma laje vão para a parte em que caem; uma camada que ultrapassa um bordo curvo continua a fazê-lo nas duas partes; a malha de um teto falso fica onde estava, cada luminária ou difusor na parte em que cai a sua célula. Uma laje com bordo elíptico não é cortada por uma linha.

Um **telhado** é dividido e aparado do mesmo modo. Cada parte mantém as regras dos seus bordos (beiral ou empena, inclinação, beiral saliente), e o corte é uma empena sem beiral; o corte só é permitido onde deixa o telhado como estava, com as águas de cada parte onde estavam as do telhado, como através da cumeeira de um telhado de duas águas ou ao longo dela. Noutros casos é recusado (*would change its slopes*): as águas de uma parte subiriam a novas cumeeiras. Janelas de telhado, trapeiras e tubos de queda vão para a sua parte. Um telhado em abóbada de berço não é cortado.

![Um telhado de duas águas dividido através da cumeeira: dois telhados, as mesmas águas](shots/roof-split.webp)

## Telhados

Escolha **Arquitetura → Telhado** (<kbd>RF</kbd>) e clique no contorno das paredes sob ele. O Lapin constrói o telhado sobre esse contorno, no topo do seu nível.

![Uma casa com telhado de duas águas na vista 3D](shots/house-3d.webp)

Selecione o telhado para mudá-lo no painel de propriedades:

- **Forma.** Quatro águas, duas águas, uma água, mansarda, abobadado ou plano. Um telhado abobadado é uma abóbada curva entre duas empenas; **Abóbada: flecha** define quanto ele sobe no topo, no máximo metade do vão.
- **Inclinação e beiral** para o telhado inteiro.
- **Direção da cumeeira (duas águas, uma água)**: automática, x ou y. A cumeeira de um telhado de uma água é o seu lado alto: em x ele cai para −y, em y para −x; **Uma água: inverter a inclinação** faz ele cair para o outro lado.
- **Cada borda** com o seu tipo (beiral ou empena), inclinação e beiral. Uma mansarda com bordas de empena é um telhado holandês.
- **Estilo.** Os estilos de telhado dão um visual completo numa só escolha: a cobertura, os beirais, as calhas e os condutores.
- **Material da cobertura.** A cobertura é de um material do projeto, por omissão o da cobertura escolhida (Clay tile, Metal sheet, Slate…), criado na cor da cobertura da primeira vez que um telhado precisa dele. Escolha outro nas propriedades ou em **Cobertura e drenagem** do configurador; mudar a cobertura muda-o também, a menos que tenha escolhido um. Alçados e cortes tracejam a cobertura por esse material (telhas, telhas de madeira e ardósia em fiadas horizontais, chapa em juntas verticais, a menos que lhe dê um tracejado **Elevation** próprio), um corte desenha a cobertura cortada com o tracejado de corte do material, e o IFC, as medições e o orçamento listam a cobertura sob o seu material.
- **Espessura da cobertura.** Vazia, a cobertura é tão espessa como a do catálogo (uma membrana 2 cm, chapa metálica 3 cm, telha 5 cm…); escreva uma para lhe dar a sua. Uma cobertura importada de outro programa mantém a do ficheiro.
- **Laje.** Uma cobertura plana, uma abóbada de berço e uma cobertura com **Structure** none têm uma laje sob o revestimento, uma parte própria: **Material da laje** (por omissão Reinforced concrete, criado da primeira vez que uma cobertura precisa dele) e **Espessura da laje** (por omissão 16 cm), nas propriedades ou em **Estrutura** no configurador. O revestimento assenta nela, por isso a cobertura fica tão espessa como as duas juntas. Um corte desenha duas faixas, cada uma tracejada pelo seu material; as janelas de cobertura atravessam ambas; uma parede ligada a uma cobertura plana sobe até à face inferior da laje; o IFC escreve a laje e o revestimento como duas lajes de cobertura dos seus materiais, e a medição e o orçamento medem a laje (área e volume) e o revestimento (área), cada um sob o seu material. As propriedades mostram estes dois campos só numa cobertura sem estrutura de madeira, e os campos das varas (secções, afastamento, classe, madres, pontas) só numa com estrutura; um campo próprio de uma só forma (o da mansarda, a flecha da abóbada, a inversão da cobertura de uma água) aparece só nessa forma.

![As propriedades de um telhado](shots/roof-properties.webp)

![Uma cobertura plana em corte: a laje de betão e o revestimento por cima, cada um tracejado pelo seu material](shots/roof-deck-section.webp)

**Configurador de telhado** (o botão ao lado do estilo, ou **Arquitetura → Configurador de telhado** com um telhado selecionado) reúne tudo num só lugar: os estilos à esquerda, depois a forma e as bordas, a cobertura e a drenagem, a estrutura e as aberturas, sobre uma planta do telhado (clique numa borda para torná-la automática, beiral ou empena) e, para a estrutura, um corte através da cumeeira com a tesoura ou o par de caibros no meio. Escolha um estilo para começar e mude qualquer campo à mão; **Aplicar** faz disso uma só etapa de desfazer. **Salvar como estilo…** guarda o telhado como está configurado como um estilo do projeto, listado em **Projeto** acima dos do catálogo (e na lista do painel de propriedades) para outros telhados e para os novos; **Atualizar o estilo** salva as mudanças nele de novo, e todos os telhados do estilo o acompanham. Um estilo do projeto pode ser renomeado ou excluído; seus telhados mantêm a aparência.

![O configurador de telhado](shots/roof-configurator.webp)

**Calhas e condutores.** Em **Cobertura e drenagem** do configurador, a calha recebe o tamanho, quanto ela fica para fora do beiral e quanto abaixo da cobertura, o caimento até as saídas (mm/m) e as tampas das pontas; a tabela das bordas liga ou desliga a calha de cada beiral. Os condutores começam como do próprio telhado, um a cada 10 m; **Torná-los editáveis** os transforma em elementos, e **Arquitetura → Condutor** adiciona um com um clique numa calha. A alça de um condutor só o desliza ao longo da sua calha; suas propriedades definem o diâmetro, a curva no pé e o trajeto, uma cadeia de segmentos, cada um com seu comprimento e seu ângulo em relação à vertical (na direção da parede), o último descendo até o chão. No IFC, uma calha é um IfcPipeSegment GUTTER e um condutor é um segmento rígido chamado Downpipe.

As paredes no nível do telhado param sob ele e sobem até as empenas sozinhas, também sob um telhado curvo. Uma parede em outro nível acompanha o telhado depois de **Arquitetura → Anexar ao telhado** (<kbd>AR</kbd>): selecione as paredes e clique no telhado. A mesma ferramenta aceita qualquer outro corpo sobre as paredes: um sólido do Studio, uma laje, uma viga, um elemento importado de IFC. As paredes sobem até a face inferior dele e são cortadas ao longo dela, seja qual for a forma. **Desanexar do telhado** desfaz isso.

**Janela de telhado** e **Água-furtada** adicionam aberturas a um telhado: clique numa água. Elas pertencem ao telhado. Selecione o telhado, e cada uma mostra suas alças na planta. Arraste a alça do meio para movê-la no telhado. Arraste as alças das bordas ou dos cantos de uma janela de telhado para redimensioná-la na água (a borda oposta fica onde está; você pode digitar o comprimento enquanto arrasta). A largura corre ao longo do beiral e o comprimento sobe pela água, medido sobre ela. Uma água-furtada tem uma alça de cada lado para a sua largura. Uma mudança que não caberia no telhado é recusada. Para removê-la, clique com o botão direito numa alça dela e escolha **Remover janela de telhado** (ou **Remover água-furtada**), pressione **Delete** com o cursor sobre a alça, ou **Ctrl/⌘-clique** na alça do meio. As propriedades do telhado também as listam: digite ali a largura e o comprimento de uma janela, ou clique em **×** para removê-la. O telhado de uma água-furtada é coberto com o material do revestimento e medido com ele: as quantidades da cobertura dão à parte o **revestimento das águas-furtadas** (a área das suas águas, o número de águas-furtadas), por isso a medição por material, o orçamento e uma tarefa de planeamento sobre o revestimento contam as águas da cobertura (sem os recortes das águas-furtadas) e os telhados das águas-furtadas, cada um uma vez.

![A alça de uma janela de telhado no telhado selecionado](shots/roof-window-grip.webp)

### Estrutura de madeira

**Estrutura** define como o telhado é estruturado:

- **caibros**: caibros sobre terças, com uma viga de cumeeira.
- **tirante**: pares de caibros com um tirante horizontal em **Tirantes: fração da altura** (dois terços por padrão), sem terças.
- **tesouras**: tesouras pré-fabricadas ao longo da cumeeira principal, no **Espaçamento dos caibros**: banzos superiores nas águas, um banzo inferior sobre os frechais, diagonais em W (uma tesoura de uma água num telhado de uma água). As pontas de um telhado de quatro águas mantêm seus caibros.
- **nenhuma**: um painel, sem madeira.

**Caibros e banzos** aceita as seções antigas em cm ou as nórdicas em mm (45×95 a 45×245); **Diagonais e montantes da tesoura** e **Classe de resistência da madeira** (C14 a C30, madeira laminada colada) vêm junto. Os caibros se apoiam no frechal por um entalhe (**Apoio dos caibros no frechal**) e terminam no beiral em prumo ou em esquadro (**Pontas dos caibros**). Os estilos **Telhado com tesouras suecas** e **Telhado nórdico com tirantes** trazem sua estrutura com eles.

Cada peça é uma parte própria: a tabela **Madeira do telhado** as lista como uma lista de corte (peça, seção, classe, comprimento, quantidade, volume), e o IFC recebe cada uma como um IfcMember do seu tipo, cada tesoura como um IfcElementAssembly.

## Forros

**Interiores → Forro** (<kbd>CG</kbd>) desenha um forro sobre um contorno que você clica; **Forro em um ambiente** preenche o ambiente que você clica, seguindo o seu contorno. Um forro fica pendurado na sua **Altura** acima do pavimento (2700 mm, a menos que você mude nas propriedades).

O tipo, em **Interiores → Tipos de forro**, define o **Sistema**: **Placas sobre perfis aparentes** (a largura, o comprimento e a espessura da placa, a largura da face da grelha, a altura e a rotação, pendurais até a estrutura no seu espaçamento) ou **Gesso acartonado, contínuo**. A grelha fica centrada no forro, para que as placas cortadas nas bordas saiam iguais.

**Painel luminoso** e **Difusor de ar** colocam um na célula que você clica; clique de novo na célula com a outra ferramenta para trocá-lo. No painel de propriedades, **Distribuir** coloca painéis de luz ou difusores de ar a cada tantas células, e **Limpar** os remove. As tabelas contam as placas inteiras, os painéis de luz e os difusores, e o forro vai para o IFC como um IfcCovering do tipo CEILING.

![As propriedades de um forro: tipo, altura e acessórios](shots/ceiling-properties.webp)

![Forros em grelha com painéis de luz e difusores de ar, em 3D](shots/ceilings-3d.webp)

## Rodapés

**Interiores → Rodapé no ambiente** (<kbd>SK</kbd>) coloca um rodapé em volta do ambiente que você clica, ao longo das faces das paredes, interrompido em cada porta (qualquer abertura que desce até o piso); clicado dentro de paredes sem ambiente, ele as segue do mesmo jeito. O rodapé de um ambiente acompanha o ambiente: mova uma parede ou uma porta e o rodapé vai junto. **Rodapé** desenha um ao longo de uma linha que você clica na face de uma parede (Enter termina); a peça fica do lado oposto à parede, e **Trocar de lado** nas propriedades a vira.

O tipo, em **Interiores → Tipos de rodapé**, define o **Perfil** — **Liso (reto)**, **Chanfrado (MDF)**, **Moldurado (madeira)**, **Com meia-cana (metal)**, **Borda boleada** ou **Borda arredondada** — desenhado ao lado em escala, a **Altura** (40 a 300 mm), a **Espessura** (5 a 40 mm), o **Material** e a sua cor. **Da biblioteca** traz tipos prontos para o projeto: MDF 80 chanfrado, Carvalho 70 moldurado, Cerâmica 80 reto, Alumínio 60 arredondado, Boleado 50 e Meia-cana 70. Enquanto uma ferramenta de rodapé está ativa, o painel de propriedades mostra o tipo que ela coloca, para escolher antes do clique.

As propriedades de um rodapé têm o tipo, o **Deslocamento da base** acima do piso (em Dimensões) e **Interrompido nas portas** (desligado, ele continua através delas). **Tabela de rodapés** (ou **Rodapés** na lista de novas tabelas do navegador do projeto) os lista por tipo com as peças e o comprimento, sem as folgas das portas. A peça é cortada em meia-esquadria nos cantos; uma planta desenha a sua borda frontal, e um rodapé vai para o IFC como um IfcCovering do tipo SKIRTINGBOARD, de um IfcCoveringType do mesmo tipo.

![Os tipos de rodapé: o perfil desenhado em escala, a biblioteca](shots/skirting-types.webp)

![As propriedades de um rodapé: tipo, deslocamento da base e folgas nas portas](shots/skirting-properties.webp)
