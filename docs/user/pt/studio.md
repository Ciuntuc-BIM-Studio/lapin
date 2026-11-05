---
title: Studio
order: 85
summary: Modele sólidos de forma livre, esboçados em qualquer face ou plano, e depois empurre, puxe, arredonde, esvazie, divida e repita esses sólidos em 3D.
---

## A guia Studio

A guia **Studio** cria sólidos que nenhuma outra guia constrói: um banco, uma marquise, um corrimão, uma fonte, um embasamento em uma fachada. Cada sólido começa de um esboço ou de uma primitiva. Depois você o altera em 3D com passos, e cada passo continua editável.

![A guia Studio](shots/ribbon-studio.webp)

Um sólido do Studio tem uma classe IFC, como qualquer elemento. Escolha-a nas suas propriedades: um membro, uma placa, um revestimento, um guarda-corpo, uma laje ou qualquer outra. Os seus quantitativos e a sua exportação seguem essa classe.

## Onde você esboça

Você esboça o contorno de um sólido com as ferramentas de **Desenho**, como polilinhas fechadas, retângulos e círculos. Linhas e arcos que se encontram ponta a ponta também formam um contorno fechado. Uma forma fechada dentro do contorno é um furo.

Você pode esboçar em quatro tipos de vista:

- **Uma planta** desenha no nível do seu pavimento.
- **Um corte** desenha na sua linha de corte, em pé.
- **Uma elevação** ou **um plano de referência**: veja *Planos de referência* abaixo.

## Criar um sólido

- **Extrusão** (EXTRUDE) empurra o contorno para fora em linha reta. Digite S para os dois sentidos a partir do esboço, T para uma conicidade que inclina os lados para dentro, e Cut para que ela corte os elementos que você selecionou antes. Para levá-la até a face de outro elemento, use **Até → uma face…** nas suas propriedades, ou **Extrudar até a face**. A profundidade, as alturas e as espessuras são digitadas na unidade do projeto, como diz a mensagem (`Depth (mm)`: `2000` é 2 m).
- **Varredura** (SWEEP) passa um perfil ao longo de um caminho. TWist gira a seção no caminho, SCale a aumenta e RIse a eleva. Um círculo que sobe vira uma hélice, dando TUrns voltas.
- **Loft** (LOFT) une perfis desenhados em vistas diferentes. Digite S para que ele passe suave por eles. Uma linha selecionada com o papel *trilho* o curva como guia.
- **Revolução** (REVOLVE) gira um perfil em torno de uma linha de eixo.
- **Primitivas** colocam uma caixa, um cilindro, uma esfera, um cone, um toro, uma cunha ou uma pirâmide com um clique. Altere os seus tamanhos e a sua posição nas suas propriedades.

![Um corrimão varrido ao longo de um círculo que sobe 3 m em duas voltas](shots/studio-helix.webp)

Cada sólido continua ligado ao seu esboço. Altere a forma e o sólido acompanha, com o mesmo GlobalId. Selecione um sólido para alterar a sua classe IFC, o seu tipo predefinido e o seu tipo no painel de propriedades. **Editar esboço** abre a vista em que o esboço está.

Um sólido recém-criado fica selecionado, então ele se ilumina com as alças do seu esboço. Em uma planta, um corte ou uma elevação, onde a vista corta um sólido, o seu corte é preenchido de branco: as linhas do esboço viram uma forma que se vê maciça. Um material com preenchimento ou hachura próprios desenha o corte com eles.

## Anel, Caixa e Faixa de parede

Estas três ferramentas dão a uma forma desenhada uma espessura no seu próprio plano e depois uma altura para fora desse plano:

- **Anel** (<kbd>RING</kbd>) pega um círculo, um arco ou um contorno fechado e faz um anel ou um tubo. A sua espessura fica **por dentro**, **por fora** ou **centrada** na forma.
- **Caixa** (<kbd>BOX</kbd>) pega um retângulo, uma polilinha fechada ou um círculo e faz as paredes em volta dele. Um **fundo** e um **topo** são opcionais, cada um com a sua própria espessura.
- **Faixa de parede** (<kbd>TWALL</kbd>) pega uma linha, uma polilinha ou um arco e faz uma faixa de parede. A sua espessura fica à **esquerda**, à **direita** ou **centrada**, vista caminhando ao longo da linha no sentido em que você a desenhou. Os seus cantos são em meia-esquadria.

Inicie a ferramenta e clique na forma. A linha de comando oferece as opções como letras: <kbd>I</kbd>, <kbd>O</kbd> e <kbd>C</kbd> (ou <kbd>L</kbd>, <kbd>R</kbd> e <kbd>C</kbd>) para onde fica a espessura, e para uma caixa <kbd>B</kbd> e <kbd>T</kbd> para ativar ou desativar o fundo e o topo. Depois digite a espessura e a altura, em metros, ou pressione <kbd>Enter</kbd> para manter o valor mostrado.

**Em uma planta**, as ferramentas criam por padrão paredes nativas, do tipo de parede escolhido em **Arquitetura**. Uma forma fechada vira um anel de paredes unidas nos seus cantos, uma polilinha uma cadeia de paredes, e um arco ou um círculo paredes curvas. Onde fica a espessura decide onde as paredes ficam: a espessura inteira do tipo de parede vai para o lado que você escolheu. O fundo e o topo de uma caixa viram lajes. Digite <kbd>S</kbd> antes de escolher a forma para criar um sólido do Studio em vez disso.

**Em um corte, uma elevação ou um plano de referência**, as ferramentas criam um sólido do Studio da classe IFC IfcWall; o fundo e o topo de uma caixa são lajes separadas (IfcSlab). A altura vai ao longo da normal do plano, na sua direção; uma altura negativa se afasta de você, como com **Extrusão**. Para dar um tipo a uma parede assim, selecione-a e escolha ou crie um em **Tipo** no painel de propriedades.

![Um anel de paredes curvas nativas em volta de um círculo, uma caixa com fundo e topo e uma faixa de parede, na planta do térreo](shots/studio-thick-plan.webp)

Tudo continua paramétrico. Selecione o sólido e altere a sua **Espessura**, **Altura** e **A espessura fica** no painel de propriedades. Para uma caixa, marque **Inferior** ou **Superior** e defina as suas espessuras: o Lapin adiciona ou remove as lajes. Uma caixa com fundo ou topo é agrupada com eles em um conjunto (IfcElementAssembly). Arraste as alças da forma, ou edite-a na sua vista, e o sólido é reconstruído.

![As propriedades de uma caixa: espessura, altura, fundo e topo](shots/studio-box-properties.webp)

## Planos de referência

Um plano de referência é um plano em qualquer lugar do modelo com uma vista própria. Você desenha nele com Desenho, como em um corte, e o Studio cria sólidos nele. Crie um a partir da guia **Studio**:

- **Plano da vista** (<kbd>REFPLANE</kbd>) fica paralelo à planta, ao corte, à elevação ou ao plano à sua frente. Digite a que distância ele fica na sua direção (um valor negativo se afasta).
- **Plano na face**: na vista 3D, clique em uma face de qualquer elemento. O plano fica sobre a face, voltado para fora dela.
- **Plano por 3 pontos**: na vista 3D, clique em três pontos do modelo. O plano passa por eles.

O novo plano abre na hora. Ele aparece em **Planos de referência** no navegador do projeto. O seu desenho é um corte no plano, olhando contra o lado para o qual ele está voltado.

Na vista 3D, cada plano de referência é desenhado como um painel translúcido com o seu nome, do tamanho que o modelo tem sobre ele (ou a sua **Largura desenhada (3D)** e **Altura desenhada (3D)**, no mínimo 2 × 2 m), iluminado sob o cursor. Clique nele para selecionar o plano e clique duas vezes para abrir a sua vista. Com o plano selecionado, arraste a seta no meio dele para movê-lo ao longo da sua normal: o deslocamento aparece ao lado da seta, e o que está desenhado no plano vai junto. **Vista → Planos de referência** oculta e mostra os painéis; eles nunca vão para exportações, renderizações ou imagens. Em plantas, cortes e elevações, um plano que corta a vista é desenhado como uma linha tracejada com o seu nome, por toda a extensão da vista: clique nela para selecionar o plano e clique duas vezes para abri-lo. Um plano paralelo à vista não é desenhado nela (ele não corta nada dela). Essas linhas são só para a tela: as pranchas nunca as imprimem.

![Um plano de referência na fachada sul, com um anel desenhado nele](shots/studio-plane.webp)

Selecione o plano no navegador para movê-lo. **Origem x**, **y** e **z** o posicionam. **Faces** é a direção em planta para a qual ele está voltado, **Inclinação** o quanto ele se inclina para trás (90° fica deitado, voltado para cima) e **Giro** gira o seu desenho em torno da sua normal. **Mover ao longo da normal…** e **Girar em torno da vertical…** fazem o mesmo em um passo. O que está desenhado no plano, e cada sólido feito a partir dele, se move junto.

![As propriedades de um plano de referência](shots/studio-plane-properties.webp)

Cortes e elevações também são planos. Um sólido esboçado em um corte fica sobre a sua linha de corte e acompanha a linha quando você a move. Em um corte desenvolvido, ele fica sobre o trecho em que foi desenhado. Uma elevação ganha um plano próprio na primeira vez que você cria um sólido nela: na frente do modelo, com o seu x como está desenhado agora. Altere a **Profundidade do plano de esboço** nas propriedades da elevação para mover o plano, e os seus sólidos, ao longo da vista.

![A casa com os seus sólidos do Studio em 3D](shots/studio-3d.webp)

No IFC, um anel, as paredes de uma caixa e uma faixa de parede são elementos IfcWall, o fundo e o topo de uma caixa são elementos IfcSlab, e uma caixa com eles é um IfcElementAssembly com IfcRelAggregates; os seus tipos são gravados como IfcWallType e IfcSlabType. Cada exportação 3D (IFC, Fragments, glTF, USD, COLLADA) grava um sólido do Studio onde ele está no modelo, qualquer que seja o plano em que foi esboçado, com os seus furos; um conjunto é um nó de grupo sobre as suas partes. Abrir de novo o IFC do próprio Lapin traz os sólidos de volta como referências, no lugar, com a sua classe, tipo e conjunto, mas não mais editáveis (os seus esboços não estão no arquivo). O histórico de cada sólido fica guardado no conjunto de propriedades do próprio Lapin, Lapin_Intent. Os sólidos do Studio aceitam só tipos simples, sem camadas: essa é a modelagem livre para a qual eles servem; paredes com camadas são as paredes nativas do Lapin.
## Alterar um sólido em 3D

Estas ferramentas trabalham nas faces e nas arestas do corpo como ele está construído agora. A face sob o mouse se ilumina.

**Empurrar/Puxar**, **Dividir**, **Fatiar** e **Mover 3D** são usados no próprio modelo. Clique em uma face, e uma seta aparece sobre ela: o mouse move a face ao longo da seta, e o que o passo vai fazer é desenhado enquanto você move. Empurrar/Puxar desenha em verde o que adiciona e em vermelho o que tira. Uma divisão ou um fatiamento desenha o seu plano em amarelo. A distância aparece ao lado da seta. Digite um valor para defini-la com exatidão, como 500 ou -250. Clique ou pressione <kbd>Enter</kbd> para aplicá-la, ou pressione <kbd>Esc</kbd> para desistir; a ferramenta continua ativa para a próxima face. <kbd>Enter</kbd> sem nada movido ou digitado repete a última distância. Um arraste anda de centímetro em centímetro, ou de 10 em 10 cm com <kbd>Shift</kbd>. Os botões direito e do meio continuam girando e movendo a vista. As outras ferramentas pedem o seu valor em uma pequena caixa ao lado do cursor.

- **Empurrar/Puxar** (PU) move uma face para fora, ou para dentro quando a distância é negativa. Em uma extrusão, a sua extremidade altera a profundidade e a sua lateral move a aresta do esboço abaixo dela. Em uma parede ou em qualquer outro elemento, puxar adiciona um sólido no material da face, e empurrar corta o elemento.
- **Arredondar aresta** (FE) arredonda a aresta mais próxima do seu clique, e **Chanfrar aresta** (CE) a chanfra. Escolha *em toda a face* para fazer todas as arestas da face de uma vez.
- **Casca** esvazia os sólidos selecionados, com paredes da espessura que você digitar. Ela pode ficar fechada, ou ficar aberta na sua extremidade, no seu esboço ou nos dois.
- **Dividir** corta um sólido em dois por um plano paralelo à face que você escolher, até a profundidade na face que você arrastar ou digitar. As duas partes mantêm o esboço. **Fatiar** mantém só um lado: escolha **Manter atrás** ou **Manter na frente** na barra embaixo da vista.
- **Matriz** repete o sólido em uma fileira, e **Matriz polar** o repete em volta de um eixo vertical.
- **Girar 3D** gira o sólido em torno de uma aresta que você escolher. **Espelhar 3D** o espelha no plano de uma face. **Mover 3D** coloca setas ao longo de x, y e z nos sólidos selecionados: arraste uma, ou digite uma distância para o eixo mostrado ao lado do valor (<kbd>Tab</kbd> vai para o próximo eixo).

![Empurrar/puxar: a face de cima puxada para cima, o que ela adiciona desenhado em verde e a distância ao lado da seta](shots/studio-push-pull.webp)

![A aresta arredondada: o arredondamento acompanha o bloco quando a sua profundidade muda](shots/studio-fillet.webp)

## Desenhar sobre uma face

Na vista 3D você pode desenhar diretamente sobre uma face e empurrar ou puxar o que desenhou. O grupo **Na face** da guia **Studio** tem **Retângulo na face** (FRE), **Círculo na face** (FCI) e **Polígono na face** (FPL).

1. Clique em uma face de qualquer elemento: um sólido do Studio, uma parede, uma laje ou um pilar. O clique é o primeiro ponto: o canto de um retângulo, o centro de um círculo ou o primeiro vértice de um polígono. A face fica contornada enquanto você desenha.
2. Mova o mouse. A forma o acompanha sobre a face, com o seu tamanho ao lado. Os pontos se prendem aos cantos da face, aos pontos médios das suas arestas e ao seu centro; fora isso, andam de centímetro em centímetro, ou de 10 em 10 cm com <kbd>Shift</kbd>. Clique no canto oposto ou em um ponto do círculo. Você também pode digitar o tamanho: a largura e a altura de um retângulo como `1200, 600`, ou o raio de um círculo. Um polígono recebe um clique em cada vértice, ou um comprimento digitado na direção do mouse. Clique no seu primeiro vértice ou pressione <kbd>Enter</kbd> para fechá-lo; <kbd>Backspace</kbd> desfaz o último vértice.
3. Assim que a forma se fecha, uma seta aparece sobre ela, como em **Empurrar/Puxar**. Puxe-a para fora e ela vira um sólido apoiado na face, no material da face. Empurre-a para dentro e ela vira um corte no elemento: um nicho, um rebaixo, ou um furo quando o atravessa. Clique ou pressione <kbd>Enter</kbd> para aplicar. <kbd>Esc</kbd>, ou uma distância de 0, deixa a forma na face como esboço, para extrudar depois.

A forma é um esboço sobre um plano de referência apoiado na face. O plano leva o nome do elemento e da face, como *Extrude 1, north face*, e as formas desenhadas na mesma face o compartilham. A vista 3D não desenha esses planos, porque a face já mostra onde eles estão. Abra um pelo navegador do projeto para alterar o esboço em 2D; o sólido ou o corte o acompanha. **Empurrar/Puxar** na extremidade do sólido altera a sua profundidade. **Empurrar/Puxar** no fundo de um rebaixo deixa o corte mais fundo ou mais raso, em vez de acrescentar ao elemento.

![Um retângulo desenhado na lateral de um bloco, com o seu tamanho ao lado do cursor](shots/studio-on-face.webp)

## Os passos

As propriedades de um sólido do Studio listam os seus passos em ordem. Cada passo pode ser desativado, movido para antes ou para depois, alterado ou removido. Altere o esboço, e cada passo é refeito sobre o novo corpo. Um passo que não consegue mais encontrar a sua face ou aresta avisa em **Problemas**.

![Um cilindro esvaziado em um tubo e repetido três vezes: os seus dois passos nas suas propriedades](shots/studio-steps.webp)

As propriedades de uma polilinha listam os seus lados. Digite o comprimento de um lado, e a sua extremidade se move ao longo dele. Os lados seguintes acompanham, até o próximo lado que segue na mesma direção, para que um retângulo continue um retângulo. Um sólido desenhado sobre ela também acompanha.

## Malhas de outros programas

**Arquivo → Importar malha (OBJ, STL, glTF)…** traz uma malha como referências que você pode ver e nas quais pode usar o snap. Um arquivo OBJ ou STL não diz a sua unidade nem qual lado é para cima, então o Lapin pergunta. Selecione uma referência e clique em **Tornar sólido Studio** para torná-la editável. Ela passa então a aceitar empurrar/puxar, arredondamentos, operações booleanas e os outros passos. A mesma ferramenta funciona em paredes, lajes e elementos de arquivos IFC.

## Operações booleanas

**Subtrair**, **União** e **Interseção** combinam quaisquer elementos, com o primeiro selecionado como alvo. As ferramentas continuam editáveis, e o resultado as acompanha. **Mostrar operandos** mostra as ferramentas ocultas de uma união para edição. **Remover a operação** separa os elementos de novo.
