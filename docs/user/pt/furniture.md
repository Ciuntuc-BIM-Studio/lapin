---
title: Mobiliário
order: 47
summary: Sofás, sofás de canto, poltronas, cadeiras, camas, guarda-roupas, cômodas, estantes, aparadores, mesas, escrivaninhas e cozinhas em seis disposições, a partir de tipos paramétricos, encostados nas paredes com um clique.
---

## Posicionar mobiliário

A guia **Interiores** tem, depois dos acabamentos, uma ferramenta para cada tipo de móvel: **Sofá** (<kbd>SO</kbd>), **Sofá de canto**, **Poltrona** (<kbd>AC</kbd>), **Cadeira**, **Cama** (<kbd>BD</kbd>), **Criado-mudo**, **Guarda-roupa** (<kbd>WD</kbd>), **Cômoda**, **Estante de livros**, **Aparador**, **Mesa** (<kbd>TB</kbd>), **Mesa de centro** e **Escrivaninha**. Escolha uma e clique na planta.

Clique perto de uma parede e o móvel se encosta nela. A parte de trás fica na face da parede (no reboco, não no núcleo), a frente fica voltada para o ambiente, e ele desliza ao longo da parede até não entrar mais na parede do canto. Clique mais longe das paredes e o móvel fica onde você clicou.

Enquanto uma ferramenta está ativa, a barra da linha de comando mostra:

- **o tipo** que a ferramenta posiciona, escolhido entre os tipos desse gênero do projeto (se ainda não houver nenhum, o primeiro é criado quando você posiciona o móvel);
- **a rotação**, 0°, 90°, 180° ou 270°, para um móvel solto;
- **Junto às paredes**, que você pode desligar para posicionar um móvel exatamente onde clica, mesmo ao lado de uma parede.

O painel de propriedades mostra o mesmo tipo antes do clique, com o seu tamanho e **Editar tipos…**.

![Uma sala, uma cozinha e um quarto mobiliados, em planta](shots/furniture-plan.webp)

As plantas desenham o mobiliário pelo seu símbolo, conforme a convenção de desenho, e não cortado:

- um sofá com o encosto, os braços e as almofadas;
- uma cama com a cabeceira, os travesseiros e o edredom dobrado;
- um guarda-roupa com o cabideiro e as portas;
- uma cozinha com a pia e o cooktop, e os armários aéreos tracejados.

As elevações e os cortes desenham os corpos, com as hachuras dos seus materiais. Em 3D, os móveis são corpos low-poly feitos pelo Lapin.

![A casa mobiliada em 3D](shots/furniture-3d.webp)

## Cadeiras em volta da mesa, e a cozinha

**Cadeiras em volta da mesa:** clique numa mesa ou numa escrivaninha. As cadeiras vão ao longo dos lados maiores, cerca de uma a cada 600 mm, e uma em cada ponta quando ela é larga o bastante; uma mesa redonda as recebe em círculo. Cada cadeira fica de frente para a mesa. Uma escrivaninha recebe cadeiras só na frente. As cadeiras são do tipo escolhido para a ferramenta **Cadeira**.

**Cozinha** (<kbd>KT</kbd>) se desenha com dois pontos ao longo de uma parede:

- o comprimento da linha é o comprimento da cozinha, e a parte de trás fica na face da parede;
- os módulos inferiores são divididos igualmente, com cerca de 600 mm;
- ela tem bancada, pia e cooktop (a posição deles ao longo da cozinha se define no tipo), armários aéreos acima e uma torre para a geladeira numa das pontas.

O **Comprimento** de uma cozinha continua editável nas suas propriedades; os módulos são divididos de novo.

### Disposições da cozinha

Um tipo de cozinha tem uma **Layout** (disposição): **One wall** (a linha reta), **Galley** (duas linhas paralelas com um corredor entre elas), **L-shaped**, **U-shaped**, **Island** (uma linha na parede do fundo e uma ilha separada com o seu corredor) ou **Peninsula** (um L com uma linha que sai do braço, a bancada em balanço para bancos). Escolher uma faz o tipo partir das medidas da disposição, e cada uma continua editável ao lado da pré-visualização: o comprimento dos braços a partir da parede do fundo, o corredor entre as linhas, o comprimento da linha oposta, o comprimento, a profundidade e o lugar da ilha, o comprimento e o balanço da península e, num L ou numa península, o canto à esquerda ou à direita.

![As seis disposições em planta: numa parede, em paralelo, em L, em U, com ilha e com península](shots/kitchen-layouts.webp)

**Sink on** e **Hob on** põem-nos na linha principal ou na outra parte (o braço, a linha oposta, a ilha, a península), **Sink at** e **Hob at** dizem onde ao longo dela, e a **Tall unit (fridge)** fica numa ponta livre. Os móveis de canto são cegos; os móveis altos só existem nas linhas contra uma parede, não numa ilha nem numa península.

![Um tipo de cozinha: em L](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) desenha qualquer disposição com dois pontos ao longo da parede onde está a linha principal: a linha é o seu comprimento, as costas na face da parede, e um L, uma península, um U ou uma cozinha em paralelo desliza para o canto ou entre as paredes junto das quais se desenha. **Flip** dá a um L ou a uma península o canto do outro lado. A tabela de mobiliário nomeia a disposição e dá a profundidade da pegada. No IFC continua a ser um IfcFurniture de um tipo Kitchen definido pelo usuário.

### Módulos e eletrodomésticos da cozinha

Um tipo de cozinha pode listar os seus próprios **módulos** sob os seus parâmetros, em **Tipos de mobiliário**, em duas linhas:

- a **Linha de móveis baixos**: os módulos da linha principal a partir da esquerda, vistos de frente (depois de um canto cego);
- a **Linha de móveis altos**: os módulos pendurados por cima dela.

**Adicionar** põe um módulo do gênero escolhido no fim de uma linha. **Cozinha típica** começa a linha de móveis baixos com uma pia, uma máquina de lavar louça, um módulo de gavetas e uma placa sobre um forno. As setas movem um módulo para a esquerda ou para a direita, e o lixo retira-o.

Cada módulo tem uma **largura**. Um móvel aceita qualquer largura de 100 a 800 mm: escolha 150, 200, 300, 400, 450, 500, 600 ou 800, ou digite outra. Um eletrodoméstico aceita os tamanhos em que é fabricado. Os módulos da linha de móveis baixos são:

- **Móvel baixo**, cuja **Frente** é uma gaveta sobre uma porta, portas (uma ou duas, com o lado em que uma porta única tem as dobradiças, **Dobradiças do lado**), gavetas (1 a 4), gavetas para panelas (uma gaveta baixa sobre duas fundas) ou prateleiras abertas;
- **Móvel de lava-loiça**: uma pia simples, de 1½ cuba, dupla (a partir de 800) ou de canto, com torneira normal, torneira extraível ou sem torneira. Uma pia de canto entra no canto cego quando está ao lado dele.
- **Placa sobre forno**, de 600, 800 ou 900 de largura, de indução ou a gás, com ou sem o forno por baixo;
- **Fogão independente**, de 500, 600 ou 900 de largura, de indução ou a gás. A bancada para de cada lado dele.
- **Máquina de lavar loiça** integrada, de 450 ou 600 de largura, atrás de uma frente de altura total igual às outras;
- **Frigorífico**: sob a bancada, coluna integrada (uma porta de frigorífico sobre uma de congelador) ou independente;
- **Coluna de forno**, com gavetas sob o forno e uma porta por cima;
- **Coluna**, com portas ou prateleiras abertas;
- **Tapa-juntas** e **Vão**.

Uma coluna, um frigorífico ou uma coluna de forno tem a sua própria **Altura**. Na linha de móveis altos, um **Móvel alto** tem portas, portas de vidro ou prateleiras abertas, a sua própria **Altura** e a sua própria altura **Do chão**. Deixe qualquer uma vazia para tomar os valores da linha. Um **Exaustor** é um exaustor de chaminé (a 650 acima da bancada, com o seu tubo até ao topo dos móveis altos) ou um exaustor integrado num móvel alto. Um **Vão** deixa a parede livre. Os móveis altos contornam uma coluna.

Sem uma linha de móveis altos listada, cada móvel baixo recebe um móvel alto da sua largura, e uma placa ou um fogão recebe um exaustor de chaminé. Numa ilha ou numa península, **Exaustor sobre a placa aí** pendura um exaustor sobre a placa que está nela.

O que os módulos deixam da linha reparte-se por igual como antes, e menos de 100 mm torna-se um tapa-juntas. Os módulos que não cabem na linha ficam de fora, e a pré-visualização e o elemento dizem-no. **Ajustar aos módulos**, nas propriedades de uma cozinha, dá ao seu comprimento o que os seus módulos precisam: o deles, mais um canto cego e a coluna da ponta. Um tipo sem módulos próprios é construído como antes.

Em planta, cada módulo tem o seu próprio símbolo:

- a abertura das portas baixas, a tracejado e só no detalhe mais fino;
- as cubas da pia e a sua torneira;
- as zonas ou os bicos da placa;
- as colunas, cruzadas;
- **DW** na máquina de lavar loiça, **F** num frigorífico e **OV** numa coluna de forno;
- os móveis altos e os exaustores, por cima.

Os alçados e o 3D mostram cada frente, puxador e eletrodoméstico.

Os materiais têm mais duas funções: **Rodapé** e **Puxadores**, além das frentes, do corpo, do tampo (bancada), do metal e do vidro. Um tipo de cozinha feito antes toma para o rodapé o material do seu corpo, e para os puxadores o do seu metal.

![Os módulos de um tipo de cozinha: a linha de móveis baixos e a de móveis altos, com a pré-visualização do motor](shots/kitchen-units-types.webp)

![Uma cozinha de módulos e eletrodomésticos misturados, em 3D](shots/kitchen-units-3d.webp)

**Interiores → Móveis de cozinha** faz uma tabela dos módulos de cada cozinha, a partir da esquerda, a linha de móveis baixos e depois a de móveis altos, com a sua largura, altura e frente ou eletrodoméstico. Em IFC a cozinha continua a ser um único IfcFurniture do seu tipo, e os módulos viajam com o tipo, por isso o Lapin lê-os de volta.

## Marcenaria e mobiliário moderno

**Interiores → Marcenaria** posiciona mobiliário feito de chapas: guarda-roupas, estantes, aparadores, racks de TV, cômodas, gabinetes de banheiro com cuba, escrivaninhas, móveis de entrada, camas e criados-mudos. Cada um se configura em **Tipos de mobiliário**. Escolha ali **Marcenaria** e um **Modelo**, depois **Novo tipo**. O modelo define as medidas, a base, o tampo e os corpos, e todo valor continua editável. Se mais tarde escolher outro modelo, o tipo recomeça com os valores desse modelo e mantém seus materiais.

Uma peça de marcenaria é um ou mais **corpos** lado a lado, listados da esquerda, vistos de frente:

- **Corpo** adiciona um no fim, o botão de dividir o parte em duas metades, as setas o movem e a lixeira o remove.
- Um corpo com **Largura** a mantém. Os corpos sem largura dividem o que os outros deixam da largura da peça. **Ajustar a largura** deixa a peça tão larga quanto seus corpos quando todos têm largura. Os corpos que não cabem ficam de fora, e a pré-visualização avisa.
- Um corpo com **Altura** própria é mais baixo que a peça, como a parte baixa de um móvel de entrada.
- Um **Vão para as pernas** é um corpo sem caixa, como o espaço sob uma escrivaninha. O tampo passa por cima, e uma lateral sustenta o tampo na ponta da peça.

Cada corpo tem **zonas** empilhadas de baixo para cima, com uma divisória entre cada duas. A lista as mostra como estão, a de cima primeiro. A seta adiciona uma zona sobre a de cima. Uma zona tem sua própria **Altura**, ou uma **Parte do resto**, e sua **Frente**:

- **Prateleiras abertas**;
- **Portas**: de 1 a 4, uma única porta com **Dobradiças do lado** esquerdo ou direito, **Envidraçados** ou não;
- **Gavetas**: de 1 a 6, cada uma com sua caixa atrás. **Alturas por proporção, de cima** define suas alturas: 1:1:2 deixa a gaveta de baixo duas vezes mais alta que as outras. Vazio, são todas iguais.
- **Basculante**, que **sobe** ou **desce**.

Atrás de frentes abertas, de portas ou de uma basculante, uma zona pode ter **Prateleiras** (reguláveis, a menos que desmarque **Prateleiras reguláveis**) e uma **Barra de cabides**. Atrás de portas ela pode ter também **Gavetas internas** embaixo. Gavetas sob portas são duas zonas no mesmo corpo: as gavetas embaixo, com sua altura, e as portas em cima.

As seções abaixo do modelo definem o resto:

- **Construção**: a espessura das chapas do corpo, do fundo, das prateleiras e das frentes; o topo e a base **entre as laterais** ou **sobre as laterais**; o fundo **embutido** (a 10 mm da borda traseira) ou **aplicado na traseira**.
- **Base**: um **Rodapé**, com sua altura e seu recuo; **Pés** (metal fino, madeira cônica ou trenó), com sua altura; **Suspenso na parede**, com a altura de sua face inferior; ou **Nenhum**. Sobre rodapé ou pés, a altura da peça vai até o seu tampo. Suspensa na parede, a altura é a da peça, e **Suspenso a** diz onde fica sua face inferior.
- **Tampo**: **Nenhum** (os topos dos corpos), **Em balanço** (além da frente e das pontas pelo seu balanço) ou uma **Bancada** (além da frente apenas), com sua espessura. **Recorte da cuba no tampo** corta o lugar da cuba de um gabinete de banheiro e a desenha com sua torneira.
- **Frentes**: **Lisa**, **Com moldura** (montantes e travessas em volta de um painel recuado) ou **Ranhurado** (ranhuras verticais cortadas na frente). Os puxadores são **Nenhum** (push to open), um **Puxador perfil J** fresado na borda das frentes, uma **Barra**, uma **Bola** ou um **Puxador de borda**. **Portas de correr em toda a largura** coloca esse número de portas de correr em dois trilhos na frente de todos os corpos, como num guarda-roupa.

Uma cama feita com o modelo **Cama** tem estrado, uma cabeceira tão alta quanto o tipo, uma base, o colchão e travesseiros. Tem o **Topo do colchão** e gavetas embaixo, dos dois lados, o esquerdo ou o direito.

Os botões de **Estilos** aplicam um visual moderno sobre os valores do tipo em um só passo:

- **Sem puxadores, push to open**;
- **Puxador perfil J**;
- **Pés de metal finos**;
- **Flutuante, preso na parede**;
- **Frentes ranhuradas**;
- **Duas cores, madeira e cor**: um corpo e um tampo de carvalho com frentes verde-sálvia;
- **Frentes com moldura, botões**.

Os materiais têm seus papéis: corpo, frentes, tampo, fundo, rodapé, pés, puxadores, vidro, metal (a barra, a torneira) e roupa de cama (o colchão de uma cama). Os materiais com que o Lapin começa são indicativos: coloque os seus.

Cada chapa é um sólido do seu material. Em planta a peça mostra seu contorno, a linha de suas frentes, as juntas entre seus corpos e, no nível de detalhe mais fino, o giro de suas portas tracejado. Uma peça suspensa acima de 1,2 m é desenhada como elemento superior. Elevações e 3D mostram as chapas, as frentes e os puxadores.

A pré-visualização lista o **Plano de corte** do tipo: cada parte (laterais, base, topo, fundo, divisórias, prateleiras, rodapé, caixas das gavetas, depois as frentes à parte) com seu corpo, seu comprimento ao longo do veio × largura × espessura e quantas são. **Interiores → Plano de corte** faz o plano de corte de todos os tipos de marcenaria do projeto, multiplicado por suas peças. Lista só chapas: corrediças, dobradiças, pés e puxadores são ferragens.

![O configurador de marcenaria: um modelo, os estilos, a construção, os corpos e suas zonas, ao lado da pré-visualização e do plano de corte do motor](shots/casework-types.webp)

![Marcenaria em 3D: um aparador sobre pés cônicos, um rack de TV ranhurado preso na parede, um guarda-roupa e uma escrivaninha](shots/casework-3d.webp)

Em IFC uma peça de marcenaria é um IfcFurniture do seu tipo, com o tipo predefinido do seu modelo (SHELF para uma estante, DESK, BED, ou USERDEFINED com Wardrobe, Sideboard, TV unit, Bathroom vanity…). Seus corpos viajam com o tipo, então o Lapin os lê de volta. Os tipos guarda-roupa, aparador e cômoda feitos antes ficam como estavam.

## Mobiliário a partir de modelos

**Interiores → A partir de um modelo** coloca mobiliário macio e de formas feito a partir de modelos prontos: sofás, um sofá com chaise longue, módulos de sofá de canto, um puf, poltronas, uma poltrona reclinável, cadeiras, uma cadeira de escritório, bancos de bar, bancos, mesas de jantar, redondas e de centro, uma mesa de apoio, candeeiros de pé, de mesa, suspensos e de parede, plantas em vaso e tapetes. Os modelos são malhas de poucos polígonos com licença CC0 (Furniture Kit de Kenney), fornecidas com o Lapin com a origem e a licença registadas.

Um modelo é **esticado por zonas**: cada modelo tem em cada eixo uma faixa que estica, enquanto o que fica antes e depois mantém o tamanho. Alargue um sofá e os braços e os pés ficam como estão enquanto o assento cresce: um de dois lugares passa a ser de três. Suba uma mesa e só as pernas crescem, o tampo mantém a espessura. As mesas redondas, os abajures e as plantas escalam inteiros. Cada modelo tem um mínimo e um máximo; um tamanho fora deles é mantido no limite.

Em **Tipos de mobiliário**, escolha **A partir de um modelo** e um modelo, depois **Novo tipo**. Um tipo de um modelo tem:

- a **galeria** de modelos por grupo (sofás, poltronas, cadeiras, mesas, candeeiros, plantas, tapetes): clique noutro para mudar o modelo do tipo, que passa a ter o seu tamanho e os seus materiais;
- a sua **largura**, **profundidade** e **altura**, cada uma dentro do intervalo do modelo (passe o cursor por um campo para o ver). A pré-visualização ao lado é a do motor, por isso mostra o esticamento enquanto escreve: a planta vista de cima e a frente com as cores do tipo;
- **Suspenso a**, para um candeeiro suspenso, de parede ou de mesa: a altura da sua face inferior;
- a **Aparência** (Nórdico, Escuro, Quente, Suave), que definem de uma vez os materiais de todas as partes, e uma fila de cores para cada parte: estofamento, destaque (a borda de um tapete, um segundo tecido), corpo, superior, pés, metal, vidro, abajur, folhagem e vaso. Os materiais são indicativos: escreva os seus nos materiais abaixo.

Em planta, uma peça de um modelo é o seu corpo visto de cima, desenhado pelo motor sem as linhas ocultas; um sofá mostra também os assentos a 1:50 e mais fino. Um candeeiro suspenso acima de 1,2 m é desenhado como elemento superior. As mesas, as cadeiras, os tapetes e os candeeiros de pé ficam onde clica; os sofás, as poltronas e as mesas de apoio encostam-se à parede mais próxima como o restante mobiliário.

![A caixa Tipos de mobiliário com um sofá a partir de um modelo: a galeria, o tamanho, as aparências e as cores, ao lado da pré-visualização do motor](shots/furniture-models-types.webp)

![Uma sala mobilada com modelos: um sofá esticado a 2,6 m, poltronas, uma mesa de centro sobre um tapete, um candeeiro de pé e plantas](shots/furniture-models-3d.webp)

Em IFC, uma peça de um modelo é um IfcFurniture do seu tipo com o tipo predefinido do modelo (SOFA, CHAIR, TABLE ou USERDEFINED com Floor lamp, Plant, Rug…), e o seu corpo é a malha esticada, como B-rep facetado ou conjunto de faces conforme a exportação pedir. O modelo e o tamanho viajam com o tipo, por isso o Lapin lê-os de volta. A tabela de mobiliário lista-os por grupo e modelo.

## As propriedades de um móvel

Selecione um móvel para ver:

- **Tipo**, o seu gênero e tamanho;
- **Rotação**;
- **Acima do piso**, para um móvel elevado sobre um tablado;
- **Comprimento**, para uma cozinha.

**Inverter** dá ao móvel a sua outra mão, no lugar: a chaise do outro lado, a geladeira na outra ponta, o canto do sofá de canto do outro lado. A frente continua onde estava. **Modificar → Espelhar** faz o mesmo.

Arraste a alça no meio de um móvel para movê-lo; arraste a alça à frente dele para virar a frente para o cursor.

![As propriedades de um sofá](shots/furniture-properties.webp)

## Tipos de mobiliário

**Interiores → Tipos de mobiliário** lista os tipos do projeto por grupo (assentos, camas, armazenamento, mesas, cozinha), cada um com quantos móveis o usam. Escolha um gênero e **Novo tipo**, ou **Duplicar** um tipo para alterar uma cópia. Um tipo tem:

- o seu **nome**, **largura**, **profundidade** e **altura** (num sofá, a do encosto; numa cama, a da cabeceira; numa cozinha, a da bancada);
- os parâmetros próprios do seu gênero:
  - sofás e poltronas: lugares, braços, largura do braço, altura do assento, uma chaise à esquerda ou à direita e o seu comprimento, pés;
  - um sofá de canto: o lado do canto e o comprimento desse lado;
  - uma cama: o topo do colchão, a cabeceira, a peseira, os travesseiros, um edredom dobrado;
  - um guarda-roupa: portas, de abrir ou de correr, gavetas embaixo, rodapé;
  - uma cômoda: fileiras e colunas de gavetas;
  - uma estante: prateleiras e portas embaixo até uma altura;
  - um aparador: portas e gavetas;
  - uma mesa: retangular, redonda ou oval, sobre quatro pernas ou um pé central;
  - uma escrivaninha: um gaveteiro à esquerda ou à direita;
  - uma cozinha: largura dos módulos inferiores, rodapé, bancada, posição da pia e do cooktop, armários aéreos, torre, e os seus próprios módulos (acima);
- os seus **materiais** por função: corpo, estofamento, tampo, frentes, roupa de cama, metal, vidro, e numa cozinha o rodapé e os puxadores. Digite qualquer nome de material; um que o projeto não tenha é criado, e **Gerenciar → Materiais** define a sua cor e hachuras.

A pré-visualização ao lado dos campos mostra o símbolo em planta e a frente do tipo enquanto você o altera. Cada móvel de um tipo acompanha qualquer alteração.

![O diálogo Tipos de mobiliário com um sofá e a sua chaise](shots/furniture-types.webp)

## A minha biblioteca de mobiliário

Um tipo que configurou bem (uma cozinha, um armário, uma secretária de painéis, um sofá) pode ser guardado para todos os projetos em **O meu mobiliário**, a sua própria biblioteca neste computador.

**Guardar na minha biblioteca…** em **Tipos de mobiliário** guarda o tipo selecionado. Indique:

- um **nome**;
- uma **categoria**: cozinha, sala, quarto, casa de banho, escritório, arrumação ou outra;
- **identificadores**, separados por vírgulas (carvalho, moderno…);
- um **código de artigo** e um **preço indicativo**, se quiser. Ambos são seus: o Lapin não inclui preços.

A miniatura é desenhada a partir da pré-visualização do tipo. O tipo leva consigo tudo o que precisa: a sua configuração e os seus materiais, com as suas cores, tramas e imagens.

![Guardar na minha biblioteca: o nome, a categoria, os identificadores, o código de artigo e o preço junto à miniatura](shots/furniture-save-library.webp)

**Interiores → O meu mobiliário…** (ou **O meu mobiliário…** em Tipos de mobiliário) mostra a biblioteca como miniaturas. Pesquise-a por nome, identificador, artigo ou tipo, e filtre-a por categoria e por identificador. Para cada item:

- **Inserir** coloca o tipo no projeto aberto e escolhe-o para a sua ferramenta. Um material que o projeto já tenha com esse nome é usado tal como está, e os outros são criados. Se o projeto já tem um tipo idêntico, esse é usado e não se faz cópia. Cada inserção é um passo de anular.
- **Colocar** insere-o e inicia a sua ferramenta, de modo que o clique seguinte põe uma peça. Uma cozinha desenha-se ao longo de uma parede.
- **Renomear**, **Identificadores**, **Duplicar** e **Excluir** alteram apenas a biblioteca. Os projetos que já têm o tipo mantêm-no.
- **Exportar** escreve o item como um ficheiro `.lapinlib`. **Exportar tudo…** escreve toda a biblioteca, e **Importar…** lê um de volta. A importação ignora um item que já está na biblioteca sem alterações. Um item com o mesmo id mas conteúdo alterado é adicionado ao lado do que tem, de modo que nada é substituído.
- **Adicionar a um modelo…** coloca o tipo num dos seus modelos de projeto, de modo que todo o projeto iniciado a partir dele tenha o tipo.

![O meu mobiliário: as miniaturas da biblioteca, pesquisadas e filtradas, cada uma pronta a inserir ou a colocar](shots/furniture-library.webp)

No computador a biblioteca fica na sua pasta Lapin, um ficheiro por item. No navegador é guardada pelo navegador. Para a passar de um para o outro, ou para outro computador, exporte-a e importe o ficheiro. Um modelo de projeto guarda os tipos de mobiliário do projeto a partir do qual é guardado. **Guardar como modelo…** também pode adicionar todos os tipos de O meu mobiliário (**Adicionar todos os tipos de O meu mobiliário**).

## Tabelas e IFC

**Tabela de mobiliário** lista cada tipo com o seu gênero, tamanho, quantos móveis o usam e os ambientes onde estão, com o total. **Mobiliário por ambiente** lista cada ambiente com o que há nele. As duas ficam no navegador do projeto, em **Tabelas**, e são impressas e exportadas como qualquer tabela. Um tipo de mobiliário com um item de custo é orçado por peça no orçamento.

Em IFC, um móvel é um **IfcFurniture**, com o tipo predefinido do seu gênero (SOFA, CHAIR, BED, TABLE, DESK, SHELF, ou USERDEFINED com Wardrobe, Kitchen…). O seu **IfcFurnitureType** é ligado por IfcRelDefinesByType, e os seus corpos levam os seus materiais. O Lapin lê de volta o seu próprio mobiliário como móveis nativos e editáveis. O mobiliário de outros programas entra como referências.
