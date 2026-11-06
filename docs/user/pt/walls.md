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
- **Parede-cortina** (<kbd>CW</kbd>) desenha uma parede envidraçada com montantes e painéis.
- As paredes se unem sozinhas onde se encontram: um canto em meia-esquadria, uma parede que termina em outra em T, uma que cruza outra em X. Em um T ou em um X as camadas entram umas nas outras como na obra: o núcleo da parede que termina atravessa os acabamentos da outra até o núcleo dela, e os acabamentos dela param na face da outra, encontrando os seus iguais, assim a planta mostra uma única composição sem linha atravessando. **Unir** (<kbd>JW</kbd>), **Meia-esquadria** e **Topo** mudam como duas paredes se encontram quando não é o que você quer; **Novos cantos em meia-esquadria** torna a junção de topo o padrão neste computador. Em qualquer caso, cada camada vira o canto por conta própria: os acabamentos encontram os seus iguais e contornam sem interrupção, e os núcleos se encontram na diagonal (meia-esquadria) ou um núcleo segue até a face externa do outro (de topo), assim uma planta de paredes em camadas mostra cantos limpos. Cada camada entra na outra parede através das camadas dela de **Prioridade** menor e para na primeira de prioridade igual ou maior (veja **Tipos de parede** abaixo), de modo que, por padrão, núcleos encontram núcleos e acabamentos param nas faces; num canto, um núcleo de prioridade maior atravessa o da outra, em meia-esquadria ou de topo. A **Prioridade de corte** de uma parede nunca a faz entrar numa parede unida a ela: entre paredes unidas, decide apenas qual núcleo passa quando os dois núcleos têm a mesma prioridade (num canto de topo ou num cruzamento).
- **Inverter** muda qual das duas paredes passa direto.

**A linha em que você desenha.** Enquanto a ferramenta Parede está ativa, a lista na barra da linha de comando escolhe a linha em que você clica: **Eixo do núcleo** (o eixo, como antes), **Núcleo, face externa**, **Núcleo, face interna**, **Face externa** ou **Face interna**. O exterior é a direita da parede no sentido em que você desenha (desenhe em volta de um edifício no sentido anti-horário para tê-lo do lado de fora). O eixo da parede fica no núcleo, afastado da linha, e paredes desenhadas uma depois da outra na mesma face se encontram no canto dessa face.

## Alterar uma parede

Clique em uma parede para selecioná-la. As alças dela aparecem. Arraste as pontas para alongá-la ou encurtá-la, ou arraste a cruz no meio para movê-la.

![Uma parede selecionada com as suas alças](shots/wall-selected.webp)

O painel Propriedades mostra a parede selecionada. O alto do painel traz o tipo e as camadas. Abaixo ficam o comprimento, a altura, o deslocamento da base, o pavimento e a fase. Mude qualquer valor e o modelo acompanha.

![As propriedades de uma parede](shots/wall-properties.webp)

Mais abaixo, **Dependências** mostra o que é construído a partir da parede e a partir de que a parede é construída. **Usado por** lista os elementos que mudam quando a parede muda: as paredes a que se une, suas portas e janelas, uma parede ligada a ela. Clique em um nome para selecionar o elemento. **Construído a partir de** lista os elementos de que a parede depende, depois seu tipo e seus materiais. Quando você exclui um elemento de que outros dependem, uma mensagem nomeia os que foram reconstruídos; <kbd>Ctrl</kbd> <kbd>Z</kbd> traz tudo de volta.

**Inverter** vira as camadas da parede para o outro lado. Você também pode clicar nas pequenas setas mostradas ao lado de uma parede selecionada na planta.

Uma parede reconhecida de um IFC importado cuja extremidade o ficheiro corta em ângulo mantém esse corte: **Ângulo de corte inicial** e **Ângulo de corte final** nas suas propriedades, em graus a partir do esquadro. Uma junção nessa extremidade toma o seu lugar.

**Alinhamento da hachura** define como as camadas desta parede são hachuradas em planta. **material** segue a configuração de cada material. **axis** assenta a hachura de cada camada ao longo do eixo da parede, a partir do início do eixo, de modo que uma parede de tijolos inclinada mostra suas fiadas ao longo dela, contínuas de uma camada à outra. **view** começa cada hachura na origem da vista, sem rotação.

## Tipos de parede

Um tipo de parede é um conjunto de camadas, da face à direita do sentido em que a parede é desenhada (a externa, quando você desenha ao redor de um edifício no sentido anti-horário) à face à sua esquerda. Cada camada tem um material e uma espessura, e uma camada é o **núcleo**. A linha da parede corre ao longo do núcleo. Para editar os tipos, escolha **Arquitetura → Tipos de parede**. Uma camada que não é o núcleo pode ter uma altura própria: **De** e **Até** definem até onde ela sobe acima da base da parede (um barrado de azulejos até 1500, um revestimento de 300 para cima); vazios, ela ocupa a altura toda.

A coluna **Prioridade** (de 0 a 100) diz como a camada encontra as camadas de outra parede onde as paredes se unem: atravessa as camadas da outra de prioridade menor e para na primeira de prioridade igual ou maior. Vazia, a camada assume a prioridade do seu material em **Gerenciar → Ordem de corte**, senão 50 para o núcleo e 0 para as demais camadas (o número mostrado em cinza). Assim, um núcleo de concreto de 100 não é interrompido por um núcleo de montantes de 99 e atravessa a divisória, e um reboco duro de 60 segura o núcleo de uma parede que termina nele. A exportação IFC a grava como a Priority da camada, e a importação a lê de volta.

A coluna **Nota** diz como a camada é aplicada neste tipo (*em camada contínua, Baumit SupraFix*): pertence ao tipo, não ao material, e uma etiqueta de composição escreve-a depois da camada; o IFC leva-a como Description da camada. Quando o material da camada tem espessuras de catálogo (**Gerenciar → Materiais → Dados**), a pequena seta ao lado da espessura oferece-as e um aviso marca uma camada de outra espessura.

Uma camada que não é o núcleo também pode mudar de material na altura: o botão **camadas** no fim da sua linha adiciona uma peça (azulejos, 2100 por padrão), da base da camada para cima; adicione mais para uma faixa (pintura até 900, azulejos até 1500), e acima da última peça a camada continua no seu próprio material até o topo. Cada peça é desenhada, cortada em planta e em cortes e medida no seu próprio material.

Uma camada que não é o núcleo pode ter espessura 0, para um material fino demais para modelar mas que precisa ser medido: uma impermeabilização, uma barreira de vapor, a pintura. Ela não forma nenhum sólido; as plantas e os cortes a desenham como uma linha grossa tracejada onde ela fica entre duas camadas (numa face, a linha da face é a dela), o desenho de Composições dos tipos a nomeia como as outras, e ela é medida por área: a face da camada sobre a qual fica, descontadas as aberturas, nas quantidades da parede e no levantamento de quantidades por material.

![Uma camada de reboco com azulejos até 2100 e o reboco acima](shots/wall-layer-parts.webp)

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
