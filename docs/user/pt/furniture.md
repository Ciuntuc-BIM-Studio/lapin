---
title: Mobiliário
order: 47
summary: Sofás, sofás de canto, poltronas, cadeiras, camas, guarda-roupas, cômodas, estantes, aparadores, mesas, escrivaninhas e cozinhas lineares, a partir de tipos paramétricos, encostados nas paredes com um clique.
---

## Posicionar mobiliário

A guia **Mobiliário** tem uma ferramenta para cada tipo de móvel: **Sofá** (<kbd>SO</kbd>), **Sofá de canto**, **Poltrona** (<kbd>AC</kbd>), **Cadeira**, **Cama** (<kbd>BD</kbd>), **Criado-mudo**, **Guarda-roupa** (<kbd>WD</kbd>), **Cômoda**, **Estante de livros**, **Aparador**, **Mesa** (<kbd>TB</kbd>), **Mesa de centro** e **Escrivaninha**. Escolha uma e clique na planta.

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

**Mobiliário → Tipos de mobiliário** lista os tipos do projeto por grupo (assentos, camas, armazenamento, mesas, cozinha), cada um com quantos móveis o usam. Escolha um gênero e **Novo tipo**, ou **Duplicar** um tipo para alterar uma cópia. Um tipo tem:

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
  - uma cozinha: largura dos módulos inferiores, rodapé, bancada, posição da pia e do cooktop, armários aéreos, torre;
- os seus **materiais** por função: corpo, estofamento, tampo, frentes, roupa de cama, metal, vidro. Digite qualquer nome de material; um que o projeto não tenha é criado, e **Gerenciar → Materiais** define a sua cor e hachuras.

A pré-visualização ao lado dos campos mostra o símbolo em planta e a frente do tipo enquanto você o altera. Cada móvel de um tipo acompanha qualquer alteração.

![O diálogo Tipos de mobiliário com um sofá e a sua chaise](shots/furniture-types.webp)

## Tabelas e IFC

**Tabela de mobiliário** lista cada tipo com o seu gênero, tamanho, quantos móveis o usam e os ambientes onde estão, com o total. **Mobiliário por ambiente** lista cada ambiente com o que há nele. As duas ficam no navegador do projeto, em **Tabelas**, e são impressas e exportadas como qualquer tabela. Um tipo de mobiliário com um item de custo é orçado por peça no orçamento.

Em IFC, um móvel é um **IfcFurniture**, com o tipo predefinido do seu gênero (SOFA, CHAIR, BED, TABLE, DESK, SHELF, ou USERDEFINED com Wardrobe, Kitchen…). O seu **IfcFurnitureType** é ligado por IfcRelDefinesByType, e os seus corpos levam os seus materiais. O Lapin lê de volta o seu próprio mobiliário como móveis nativos e editáveis. O mobiliário de outros programas entra como referências.
