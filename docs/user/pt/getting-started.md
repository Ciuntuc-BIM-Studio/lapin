---
title: Primeiros passos
order: 10
summary: A tela inicial, um projeto novo, as partes da janela, como se mover por um desenho e como escolher as unidades.
---

## A tela inicial

Quando o Lapin abre, ele mostra a tela inicial. **Novo projeto** começa um edifício vazio. **Abrir projeto…** abre um arquivo `.lapin`. Os projetos que você já abriu aparecem em **Recentes**.

**Começar de** dá um projeto novo preparado para o seu trabalho: **Casa térrea** (fundações e térreo), **Sobrado** (e um pavimento superior), **Edifício residencial** (térreo e quatro pavimentos), **Galpão ou estrutura** (um pavimento de 6 m de altura, no espaço de trabalho de estrutura), **Só desenho** (um nível, no espaço de trabalho de desenho 2D) e **Começar de um IFC** (um projeto novo e depois o arquivo IFC a importar). Cada um escolhe também o seu espaço de trabalho; os níveis podem depois ser renomeados, acrescentados ou apagados como em qualquer projeto.

![A tela inicial](shots/start-screen.webp)

Um projeto novo já tem três níveis: **Fundações** (−1.50), **Térreo** (±0.00) e **Primeiro andar** (+3.00). Ele abre na planta do térreo, pronto para a primeira parede.

## A janela

![As partes da janela do Lapin](shots/workspace.webp)

1. **As guias da faixa de opções.** Desenho, Arquitetura, Acabamentos, Estrutura, Implantação, Studio, Anotar, Gerenciar, Custos e Vista agrupam cada uma as suas ferramentas. **Arquivo** abre, salva, importa e exporta. **Desfazer** e **Refazer** ficam na ponta direita da mesma barra (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **A faixa de opções.** Ela traz as ferramentas da guia atual. Passe o mouse sobre uma ferramenta para ver o nome e as letras de teclado dela.
3. **O navegador de projeto.** Ele lista os níveis, plantas, cortes, elevações, vistas 3D, pranchas e tabelas. Dê um clique duplo em um item para abri-lo em uma guia. Um projeto abre na planta do térreo, com a vista 3D em uma guia ao lado.
4. **A vista.** É onde você desenha: uma planta, um corte, uma vista 3D ou uma prancha. Você pode abrir várias vistas lado a lado com os dois botões no canto superior direito da vista.
5. **A barra rápida.** Ela põe as ferramentas mais usadas nas teclas numéricas <kbd>1</kbd>–<kbd>9</kbd>.
6. **A linha de comando.** Digite aqui as letras de uma ferramenta e pressione <kbd>Enter</kbd>. Enquanto uma ferramenta está em execução, a linha diz o que ela precisa a seguir. Veja [Desenho e a linha de comando](drafting.md).
7. **O painel Propriedades.** Ele mostra o que estiver selecionado. Sem nada selecionado, mostra a vista em primeiro plano.
8. **Pergunte-me.** É o assistente de ajuda do Lapin. Veja [Ajuda e feedback](help.md).

Quer mais espaço para o desenho? **Vista → Interface…** escolhe uma janela mais compacta, e os painéis laterais se recolhem. Veja [A interface e os atalhos](interface.md).

## Mover-se pelo desenho

- **Zoom.** Gire a roda do mouse ou faça o movimento de pinça em um trackpad. O Lapin aplica o zoom onde está o cursor.
- **Deslocar.** Arraste com o botão do meio ou o botão direito do mouse, ou com dois dedos em um trackpad.
- **Orbitar em 3D.** Arraste com o botão esquerdo do mouse. Com elementos selecionados, a vista gira em volta deles; senão, em volta do ponto do modelo sob o cursor.
- **Mostrar tudo.** Pressione o botão **Ajustar**, digite <kbd>ZE</kbd> ou dê um clique duplo com o botão do meio.

## Unidades

O Lapin trabalha em milímetros, a menos que você escolha outra coisa. Para mudar as unidades, abra **Gerenciar → Padrões de desenho → Projeto → Unidades**, ou clique em **Unidades** na barra de status, no pé da janela. Você pode escolher milímetros, centímetros, metros, pés e polegadas, ou polegadas.

![Padrões de desenho, guia Projeto: as unidades do projeto](shots/standards-project.webp)

Todo comprimento segue essa configuração: os painéis, os valores que você digita enquanto desenha, a linha de comando e as cotas. O modelo em si é guardado em metros, então mudar as unidades nunca muda o seu edifício. Os níveis e os eixos da grade também a seguem.

Em milímetros, centímetros e metros, os comprimentos são guardados e mostrados com um décimo de milímetro: uma placa de 12,5 mm continua 12.5 mm (1.25 cm), e os valores inteiros são escritos sem decimais. Pés e polegadas vão ao 1/16" mais próximo. As cotas dos desenhos são arredondadas como diz o seu estilo de cota.

## Salvar

**Arquivo → Salvar** (<kbd>Ctrl</kbd> <kbd>S</kbd>) grava um arquivo `.lapin`. Veja [Arquivos, versões e recuperação](files.md).
