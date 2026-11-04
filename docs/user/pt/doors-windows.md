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

![As propriedades de uma porta](shots/door-properties.webp)

## Aberturas sem porta nem janela

**Arquitetura → Abertura na parede** (<kbd>WO</kbd>) corta só uma abertura numa parede onde você clica: uma passagem, um alçapão, um vão para uma porta que vem depois. Defina a largura, a altura e o peitoril no painel de propriedades antes do clique. Com ela selecionada, o tamanho, a posição ao longo da parede e o peitoril são mudados ali; como uma porta, ela precisa caber na parede e ficar livre das outras aberturas da parede. No IFC, ela é uma abertura que vaza a parede, sem nada preenchendo-a.

**Forma superior** dá a forma da parte de cima: **Retângulo**, **Arco abatido** (um arco abatido), **Arco elíptico** (meia elipse, com um quarto da largura de altura, a menos que você diga outra coisa), **Semicírculo** ou **Cantos arredondados** (um topo reto com os cantos superiores arredondados). **Flecha do arco** define quanto sobe um topo segmentado ou elíptico, **Raio dos cantos** quanto os cantos são arredondados (150 mm, a menos que você diga outra coisa, nunca mais que metade da largura); vazio, o do próprio tipo. A abertura da parede segue o contorno em 3D, nas elevações e nos cortes e no IFC; uma planta mostra os batentes como antes.

## O configurador

**Arquitetura → Portas e janelas** abre o configurador. Todos os tipos de porta e de janela do projeto estão listados à esquerda. Você pode mudar:

- o nome e a marca do tipo (a letra nas etiquetas);
- a largura e a altura;
- a profundidade e a largura do marco;
- as colunas e linhas de folhas, e como cada folha abre;
- o acabamento da ombreira, a moldura na fachada, a soleira e as cores.

Os desenhos à direita mostram o tipo como ele será desenhado em elevação, planta e corte, enquanto você o muda.

![O configurador de portas e janelas](shots/joinery-configurator.webp)

**Salvar tipo** guarda as suas mudanças. Todas as portas ou janelas desse tipo as acompanham. **Posicionar com ele** fecha o configurador e começa a colocar esse tipo.

### Janelas com mais vidros

**Colunas de folhas** recebe as larguras relativas dos vidros, um número para cada: `1 1` são dois vidros iguais, `1 1 1` três, `1 2 1` um largo entre dois estreitos. **Linhas de folhas** divide a janela na altura do mesmo jeito, de baixo para cima: `1 1` dá duas linhas. Seis vidros são `1 1 1` em Colunas de vidros e `1 1` em Linhas de vidros. Depois, em **Painéis**, escolha como cada vidro abre: fixo, de abrir lateral, oscilobatente, basculante para dentro, maxim-ar, pivotante ou de correr.

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

**Aplicar um estilo…** define a forma, as barras de vidro e os ornamentos de uma vez: Moderno, Clássico, Georgiano, Vitoriano, Românico, Barroco, Gótico, Tudor, Prairie, Tradicional ou uma janela redonda. Tudo o que ele define continua editável.

![Uma janela barroca: um arco pleno com leque de raios, uma pedra de fecho e um frontão segmentado](shots/joinery-styles.webp)

![Janelas em arco em 3D](shots/window-styles-3d.webp)

### Puxadores

**Maçaneta** escolhe o puxador do tipo: uma **Alavanca**, uma **Bola**, um **Puxador** ou uma **Puxador de janela**, ou **Nenhum**. Por padrão, uma porta tem uma maçaneta de alavanca e uma janela uma manopla de janela. Uma porta tem o puxador nas duas faces, na **Altura da maçaneta** que você define; **Cor da maçaneta** define a sua cor. Os puxadores aparecem em 3D, nos cortes e nas elevações, e nos desenhos do configurador quando **Ferragens** está marcado.

### Parâmetros

**Parâmetros · nas tabelas**, embaixo no configurador, guarda os parâmetros próprios do tipo: **+ Resistência ao fogo** (guardada no Pset_DoorCommon ou no Pset_WindowCommon do IFC como FireRating), **+ Fabricante**, **+ Código do produto**, **+ Site**, ou qualquer parâmetro que você nomear. As portas e janelas do tipo os herdam, e cada um vira uma linha das tabelas de esquadrias: veja [Tabelas e gráficos](schedules.md).

## Etiquetas na planta

**Anotar → Identificadores de portas e janelas** coloca um marcador em cada porta e janela de uma planta: uma linha atravessando a parede, a marca do lado de fora e o tamanho e o peitoril do lado de dentro. Para mudar a aparência dos identificadores, vá em **Gerenciar → Padrões de desenho → Identificadores**.
