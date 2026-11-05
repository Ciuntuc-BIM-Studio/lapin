---
title: Deteção de colisões
order: 58
summary: Encontre onde os elementos do modelo se atravessam ou se aproximam demais: conjuntos de pesquisa por regras, testes rígidos e de folga entre eles, colisões seguidas de uma execução para a outra, mostradas em 3D e enviadas como BCF.
---

**Gerenciar → Deteção de colisões** encontra onde os elementos do modelo se atravessam ou ficam mais perto do que deviam. Funciona da mesma forma com os elementos do Lapin e com ficheiros IFC e Fragments ligados, por isso também coordena os modelos que as outras especialidades lhe enviam.

![A deteção de colisões, ao lado da vista 3D](shots/clash-dialog.webp)

## Testes e conjuntos

Um **teste de colisão** compara dois **conjuntos de pesquisa**: o conjunto A com o B, ou um conjunto consigo mesmo. Um projeto começa com três conjuntos (Structure, Services, Architecture) e três testes rígidos: Structure × Services, Services × Services e Architecture × Services.

- Os testes **rígidos** encontram elementos que se atravessam. A sua **tolerância** (5 mm por omissão) ignora atravessamentos tão pequenos, para que um revestimento metido 3 mm numa parede não seja indicado.
- Os testes **de folga** encontram elementos mais próximos do que uma distância (50 mm por omissão), e também os atravessamentos.

Alguns pares nunca são testados, porque devem encontrar-se: uma parede e a porta ou janela da sua abertura, as partes de um mesmo conjunto, paredes unidas num canto e elementos fixados entre si (uma viga assente num pilar, uma parede ligada a uma cobertura).

![As definições de um teste](shots/clash-test.webp)

## Executar e seguir as colisões

**Executar teste** executa o teste à frente; **Executar todos** executa cada teste ligado. Nada corre sozinho: os resultados ficam no projeto até voltar a executar o teste, e a janela diz quando o modelo mudou desde então.

Cada colisão mantém o seu número de uma execução para a outra. Uma colisão encontrada pela primeira vez é **Nova**; encontrada de novo, é **Ativa**. Marque-a à mão como **Revista** ou **Aprovada**, atribua-a a alguém (**Atribuído a**) e escreva **Notas**. Uma colisão que uma execução já não encontra passa a **Resolvida**; se voltar, fica aberta de novo, com o mesmo número.

Clique numa colisão para a ver em 3D. O Lapin seleciona os dois elementos, põe uma caixa de corte à volta de onde se encontram e olha para eles de lado, para que veja o tubo a atravessar a parede e não a face da parede. **sozinhos em 3D** esconde o resto; as setas percorrem as colisões uma a uma. A janela fica à direita, por isso pode mover a câmara enquanto está aberta.

![Um tubo a atravessar uma parede, enquadrado pela colisão](shots/clash-3d.webp)

**BCF** faz de cada colisão aberta (ou das selecionadas) uma observação do tipo Clash com o seu ponto de vista e guarda-as como ficheiro BCF para outras ferramentas BIM; enviadas de novo, as mesmas observações são atualizadas. **CSV** guarda a lista para uma folha de cálculo.

## Conjuntos de pesquisa

**Conjuntos de pesquisa** contém as regras que escolhem os elementos de um conjunto. Uma regra lê um campo do elemento (a classe, o tipo predefinido, o tipo, o piso, o material, o nome, o ficheiro ligado ou qualquer parâmetro) com um operador: é, é um de, não é, não é nenhum de, contém, não contém, pelo menos, no máximo, entre. Os números leem-se como as pessoas os escrevem: «600 mm» é 600.

Um conjunto inclui os elementos que cumprem alguma regra, ou todas. Uma regra «não» retira sempre elementos, e um conjunto sem nenhuma regra que acrescente elementos inclui tudo. O número junto ao conjunto mostra quantos elementos as regras incluem enquanto escreve.

![As regras de um conjunto de pesquisa](shots/clash-sets.webp)

## Grupos, relatórios e nuvens

A lista agrupa as colisões **por piso**, **por elemento** (o elemento presente em mais colisões fica com elas, depois o seguinte), **por classes** ou **por estado**. A caixa de um grupo seleciona todas as suas colisões, para lhes dar um estado de uma vez.

![Colisões agrupadas por elemento](shots/clash-groups.webp)

- **XML** escreve as colisões como o Navisworks escreve os seus relatórios, nos seus grupos, para que outras ferramentas de coordenação as leiam.
- **PDF** imprime a lista em A4, agrupada da mesma forma.
- **HTML** cria uma página com uma imagem de cada colisão aberta, enquadrada à vez na vista 3D.
- **Marcar na vista** desenha as colisões abertas na planta, corte ou alçado à frente como nuvens vermelhas com a etiqueta teste.colisão (2.14: o segundo teste, colisão 14), só no ecrã. Um clique numa nuvem seleciona o elemento.

![Colisões como nuvens numa planta](shots/clash-marks.webp)

Um projeto começa também com um quarto teste, **Openings for MEP**: a verificação de Gerenciar → Verificação de conflitos… executada como teste, para que os seus problemas sejam numerados e seguidos como os outros. A sua profundidade é quanto falta a um traçado na sua abertura.

Um teste rígido encontra também um elemento inteiramente dentro de outro, sem faces que se atravessem: um tubo esquecido dentro de uma laje. A sua profundidade é a menor largura do elemento interior.

> Os traçados de instalações do Lapin podem abrir eles próprios as suas aberturas em paredes e lajes (Gerenciar → Ordem de corte). Então um tubo a atravessar uma parede não é uma colisão: a parede tem a sua abertura. **Gerenciar → Verificação de conflitos…** verifica essas aberturas.

A deteção de colisões segue o desenho, os números e os testes do [BimGo](https://github.com/aussieBIMguru/BimGo), de Aussie BIM Guru (MIT-0), reescritos no Lapin.
