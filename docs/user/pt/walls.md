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
- As paredes se unem sozinhas onde se encontram: um canto em meia-esquadria, uma parede que termina em outra em T, uma que cruza outra em X. Em um T ou em um X as camadas entram umas nas outras como na obra: o núcleo da parede que termina atravessa os acabamentos da outra até o núcleo dela, e os acabamentos dela param na face da outra, encontrando os seus iguais, assim a planta mostra uma única composição sem linha atravessando. **Unir** (<kbd>JW</kbd>), **Meia-esquadria** e **Topo** mudam como duas paredes se encontram quando não é o que você quer; **Novos cantos em meia-esquadria** torna a junção de topo o padrão neste computador. Em qualquer caso, cada camada vira o canto por conta própria: os acabamentos encontram os seus iguais e contornam sem interrupção, e os núcleos se encontram na diagonal (meia-esquadria) ou um núcleo segue até a face externa do outro (de topo), assim uma planta de paredes em camadas mostra cantos limpos.
- **Inverter** muda qual das duas paredes passa direto.

**A linha em que você desenha.** Enquanto a ferramenta Parede está ativa, a lista na barra da linha de comando escolhe a linha em que você clica: **Eixo do núcleo** (o eixo, como antes), **Núcleo, face externa**, **Núcleo, face interna**, **Face externa** ou **Face interna**. O exterior é a direita da parede no sentido em que você desenha (desenhe em volta de um edifício no sentido anti-horário para tê-lo do lado de fora). O eixo da parede fica no núcleo, afastado da linha, e paredes desenhadas uma depois da outra na mesma face se encontram no canto dessa face.

## Alterar uma parede

Clique em uma parede para selecioná-la. As alças dela aparecem. Arraste as pontas para alongá-la ou encurtá-la, ou arraste a cruz no meio para movê-la.

![Uma parede selecionada com as suas alças](shots/wall-selected.webp)

O painel Propriedades mostra a parede selecionada. O alto do painel traz o tipo e as camadas. Abaixo ficam o comprimento, a altura, o deslocamento da base, o pavimento e a fase. Mude qualquer valor e o modelo acompanha.

![As propriedades de uma parede](shots/wall-properties.webp)

**Inverter** vira as camadas da parede para o outro lado. Você também pode clicar nas pequenas setas mostradas ao lado de uma parede selecionada na planta.

## Tipos de parede

Um tipo de parede é um conjunto de camadas, da face externa à face interna. Cada camada tem um material e uma espessura, e uma camada é o **núcleo**. A linha da parede corre ao longo do núcleo. Para editar os tipos, escolha **Arquitetura → Tipos de parede**. Uma camada que não é o núcleo pode ter uma altura própria: **De** e **Até** definem até onde ela sobe acima da base da parede (um barrado de azulejos até 1500, um revestimento de 300 para cima); vazios, ela ocupa a altura toda.

Uma camada que não é o núcleo também pode mudar de material na altura: o botão **camadas** no fim da sua linha adiciona uma peça (azulejos, 2100 por padrão), da base da camada para cima; adicione mais para uma faixa (pintura até 900, azulejos até 1500), e acima da última peça a camada continua no seu próprio material até o topo. Cada peça é desenhada, cortada em planta e em cortes e medida no seu próprio material.

![Uma camada de reboco com azulejos até 2100 e o reboco acima](shots/wall-layer-parts.webp)

![Tipos de parede: as camadas de uma parede de tijolos](shots/wall-types.webp)

- **+ Camada** adiciona uma camada. As setas movem uma camada para cima ou para baixo.
- **Zonas na altura** divide o núcleo de baixo para cima, por exemplo uma primeira fiada, a alvenaria e uma cinta.
- **Vergas** escolhe as vergas colocadas sobre portas e janelas: pré-moldadas ou moldadas no local, com o apoio e a altura.
- **Desenhar com ele** fecha a caixa de diálogo e começa a desenhar paredes deste tipo.

Uma mudança em um tipo muda todas as paredes desse tipo.

**Importar…**, **Exportar CSV** e **Exportar JSON**, sob a lista de tipos, levam os tipos de parede e de piso de um projeto para outro, ou para uma planilha e de volta: o CSV tem uma linha por camada (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), as linhas de um tipo uma depois da outra, e os pontos e vírgulas e as vírgulas decimais do Excel também são lidos. Ao entrar, um tipo de mesmo nome passa a ter as camadas do arquivo e qualquer outro é criado, em um único passo para desfazer; um material que o projeto não tem é criado pelo nome. A tabela **Composições dos tipos** (Tabelas → + → Composições de tipos) lista cada tipo de parede e de piso com a marca, a espessura total, quantos elementos o usam e as camadas em ordem.

## Paredes sob um telhado

As paredes no mesmo nível de um telhado param sob ele sozinhas e sobem até as empenas. Para uma parede em outro nível, selecione-a e escolha **Arquitetura → Anexar ao telhado** (<kbd>AR</kbd>), depois clique no telhado — ou em qualquer outro corpo até o qual a parede deve subir: um sólido do Studio, um elemento importado. Veja [Pisos e telhados](floors-roofs.md).
