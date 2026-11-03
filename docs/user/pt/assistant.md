---
title: O assistente do modelo
order: 127
summary: Pergunte a um modelo de IA local sobre o seu projeto, receba relatórios curtos e deixe-o propor valores de propriedades que você aprova. Só no aplicativo de desktop.
---

O **Assistente** responde a perguntas sobre o projeto aberto com as suas próprias palavras: quantas paredes cada pavimento tem e qual o comprimento delas, quais portas têm mais de 1 m de largura, quais ainda não têm resistência ao fogo. Ele faz relatórios curtos como tabelas que você pode exportar, e pode **propor** valores de propriedades (uma resistência ao fogo, um status, os seus próprios parâmetros) que você revisa e aplica.

Ele executa um modelo de linguagem **no seu computador**, por meio do [Ollama](https://ollama.com). Nada sobre o seu projeto sai da máquina, e nada é enviado para nós nem para ninguém.

> O assistente existe só no **aplicativo de desktop**. A versão do Lapin para navegador não tem assistente.

## O que ele pode e não pode fazer

Ele **pode**:

- ler o projeto: pavimentos, elementos, os tipos deles, materiais, conjuntos de propriedades e quantitativos, e as tabelas;
- contar, somar e tirar médias (comprimentos, áreas, volumes, qualquer quantitativo), agrupando por pavimento, tipo ou qualquer propriedade. O Lapin calcula os números; o modelo só os informa;
- propor valores de propriedades, nos conjuntos padrão do IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) ou nos seus.

Ele **não pode**:

- desenhar, mover, remodelar, criar ou excluir nada. Ele não muda a geometria;
- executar comandos ou escrever código;
- aplicar nada sozinho. Cada valor que ele propõe espera por você.

Um modelo local pode entender mal uma pergunta. Leia a resposta e confira os valores propostos antes de aplicá-los.

## Configurar

1. Instale o Ollama a partir de [ollama.com](https://ollama.com) e inicie-o (o aplicativo Ollama, ou `ollama serve` em um terminal).
2. Instale um modelo. O que funcionou melhor nos nossos testes é o **qwen3-coder:30b** (rápido e confiável com as ferramentas do Lapin, precisa de cerca de 20 GB de memória):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   Em um computador com menos memória, o **gemma4:12b** também funciona, mais devagar.
3. No Lapin, abra o **Assistente**: a guia **Assistente** ao lado de **Propriedades** à direita, **Vista → Assistente**, ou <kbd>Ctrl</kbd>+<kbd>K</kbd> e digite *Assistente*.

O Lapin encontra o Ollama em `http://127.0.0.1:11434` e escolhe o melhor modelo que você tem. O botão de engrenagem no alto do painel mostra o **Endereço do Ollama**, o **modelo** a usar e se o Ollama está em execução. Só é aceito um Ollama neste computador.

## Perguntar

Digite uma pergunta e pressione <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd> para uma nova linha). Pergunte em inglês, romeno ou outro idioma: a resposta vem no mesmo.

![O Assistente: um relatório por pavimento e uma resistência ao fogo proposta para uma porta, aguardando aprovação](shots/assistant.webp)

Enquanto ele trabalha, o painel mostra cada passo em uma linha, por exemplo *Listados 12 de 12 · IfcDoor* ou *Calculado(s) 2 grupo(s) sobre 40 elemento(s)*. Passe o mouse sobre um passo para ver exatamente o que ele pediu. **Parar** encerra a resposta; **Nova conversa** recomeça.

Listas e totais também aparecem como **tabelas** sob a resposta. O botão de download de uma tabela a salva como arquivo CSV, para uma planilha.

Algumas perguntas para começar:

- *Quantas paredes há por pavimento, e qual o comprimento total delas?*
- *Liste as janelas do primeiro andar com largura e altura.*
- *Quais portas não têm resistência ao fogo?*

## Aprovar os valores que ele propõe

Peça, por exemplo, *Portas com mais de 1 m de largura: dê a elas FireRating EI30.* O assistente encontra as portas e propõe os valores. Eles aparecem em **Alterações propostas**: cada elemento, o conjunto de propriedades e o nome, o valor atual e o novo, com uma caixa de seleção.

- Desmarque o que você não quer; a caixa no cabeçalho marca ou desmarca todas.
- Clique no nome de um elemento para selecioná-lo nas vistas.
- **Aplicar** grava os valores marcados, todos juntos, como **um único** passo: <kbd>Ctrl</kbd>+<kbd>Z</kbd> desfaz todos eles.
- **Rejeitar** os descarta.

Os valores vão para os parâmetros dos elementos, mostrados no painel **Propriedades** com o seu conjunto, e para a exportação IFC: uma propriedade padrão entra no seu conjunto (`Pset_DoorCommon`), as suas vão para um conjunto próprio.

As regras que o assistente segue (e que o Lapin verifica): um conjunto IFC padrão (que começa com `Pset_`) aceita só os nomes de propriedades dele, nos elementos a que se destina; um conjunto seu nunca começa com `Pset_`.
