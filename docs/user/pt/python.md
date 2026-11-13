---
title: Scripts Python
order: 127
summary: Controle o Lapin com Python pelo painel Script, um extra para desenvolvedores: um console e um editor que executam o seu próprio Python, cada Execução um único passo de desfazer.
---

## Ligue os Extras para desenvolvedores

O Lapin pode ser controlado com Python: cada ferramenta é um comando, e um script envia os mesmos comandos. O painel **Script** é um *extra para desenvolvedores*, desligado até você ligá-lo; fora isso, nada dele aparece.

Abra **Arquivo → Preferências…** e, em **Geral**, marque **Extras para desenvolvedores**.

![Preferências: Extras para desenvolvedores](shots/python-preferences.webp)

- **Python**: o Python que o painel executa. Vazio, o Lapin procura um neste computador (`python3`, `py -3` no Windows, o do python.org e o do Homebrew). Precisa ser **Python 3.11 ou mais recente**; o Lapin não instala Python. Baixe-o de [python.org](https://www.python.org/downloads/) se não tiver, ou digite o caminho do que você quer (o de um ambiente virtual, por exemplo).
- **Editor externo**: o editor que **Abrir no editor externo** usa, como comando (`code` para o Visual Studio Code). Vazio, o editor de texto do sistema.

O Python roda apenas no aplicativo para desktop: no Lapin para o navegador, o painel diz isso.

## O painel Script

Abra **Vista → Script**. O painel abre ao longo da borda inferior da janela; arraste a borda de cima para deixá-lo mais alto. O cabeçalho diz qual Python ele encontrou, ou por que não encontrou nenhum.

![O painel Script depois de uma execução: quatro paredes, um único passo de desfazer](shots/python-panel.webp)

- **Meus**, à esquerda: os seus próprios scripts, arquivos `.py` comuns guardados na pasta do Lapin neste computador. Clique em um para abri-lo; **Novo script** começa por um exemplo.
- **O editor**, no meio: Python com suas cores e indentação. Digite `lapin.` e ele oferece o que o Lapin tem (os comandos de todos os módulos da sua edição e as suas próprias chamadas); `lapin.architecture.` oferece os comandos de arquitetura com seus argumentos. Dê um nome ao script e **salve-o** em Meus; **Abrir no editor externo** abre o arquivo no seu editor, e o que você salvar lá volta para o painel.
- **O console**, à direita: o que os scripts imprimem, seus erros com a linha em que pararam (marcada no editor) e uma linha para digitar Python. **Enter** a executa, **Shift+Enter** começa uma nova linha, **Seta para cima** e **para baixo** trazem de volta as linhas digitadas antes. Um nome definido no console fica para a próxima linha e para os scripts que você executar.

No painel, `lapin` é o pacote `lapinbim`, já carregado. O mesmo script roda de um terminal com `import lapinbim as lapin`, enquanto o Lapin estiver aberto.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Comprimentos são em metros e ângulos em graus, seja qual for a unidade que o projeto mostra.

## Executar, Parar, Reiniciar, Desfazer

- **Executar** (**Ctrl+Enter** no editor) executa o script. Tudo o que ele faz é **um único passo de desfazer**, com o nome do script: um único **Desfazer** retira tudo.

  ![Um único Desfazer retirou as quatro paredes da execução](shots/python-undo.webp)

- Enquanto um script roda, o modelo é do script: uma ferramenta usada na janela é recusada com *A script is editing the model*; vistas, seleção e tabelas continuam funcionando.
- Um script que falha não mantém **nada** do que fez: o modelo fica como estava antes de **Executar**, e o console mostra o erro e a sua linha.
- **Parar** para o script; nada do que ele fez é mantido. Um script que não para em dois segundos tem o seu Python encerrado.
- **Reiniciar** inicia o Python de novo: os nomes que o console guardava são esquecidos.

## Grave o que você faz como script

A aba **Gravador**, ao lado do console, escreve o que você faz na janela como Python, uma linha por passo.

1. Clique em **Gravar**. Um ponto vermelho na aba mostra que está gravando.
2. Trabalhe como de costume: desenhe paredes, mude uma propriedade, apague algo. Cada passo aparece na hora como uma linha, por exemplo `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Clique em **Parar** e depois em **Copiar** (para colar o script em outro lugar) ou em **Salvar como script** (ele vai para Meus e abre no editor).

![O gravador depois de desenhar duas paredes](shots/python-recorder.webp)

O script é escrito de modo que funcione também em outro projeto:
- o que um passo criou fica guardado numa variável (`wall1`), e os passos seguintes a usam (`wall1.id`);
- o pavimento em que você desenhou é achado pelo nome, senão pela cota (`lapin.model.storey("Ground floor", 0)`), e um tipo pelo nome (`lapin.model.find("IfcWallType", "Brick 250")`);
- outro elemento que já estava no projeto é indicado pelo seu id (`lapin.model["…"]`), então essa linha só funciona neste projeto;
- com **Seleção como lapin.selection()** marcada, um comando sobre o que você tinha selecionado é escrito `lapin.selection()`: o script passa a trabalhar sobre o que estiver selecionado quando você o executar.

Um **Desfazer** durante a gravação tira o último passo do script, e **Refazer** o devolve, de modo que o script contém o que você fez no fim. O que um script faz nunca é gravado, nem as consultas que não mudam nada (ler uma tabela, uma medição).

![O script gravado, executado em um projeto novo: as mesmas duas paredes](shots/python-recorder-new.webp)

## Os scripts do projeto

Um script pode ser guardado **no projeto**, para viajar com o arquivo: abra-o ou escreva-o no editor e clique em **Salvar no projeto**. Ele aparece em **Do projeto**, à esquerda. Salvar, renomear e apagar um script do projeto são, cada um, um único passo de desfazer, e as versões do projeto guardam seus scripts. No arquivo `.lapin`, cada um é também um arquivo de texto simples, `scripts/<name>.py`, e por isso pode ser lido sem o Lapin.

**Um script do projeto nunca é executado sozinho**, nem quando o projeto abre nem quando é salvo. Na primeira vez que você o executa, e de novo sempre que seu código mudou, o Lapin pergunta:
- **Executar uma vez**: ele é executado desta vez, e a próxima execução pergunta de novo;
- **Confiar**: ele é executado, e este código fica lembrado como confiável neste computador (uma mudança nele pergunta de novo);
- **Cancelar**: nada é executado.

Um script que você mesmo salva no projeto é confiável desde que o salva. Seus próprios scripts (Meus) nunca perguntam.

## O que está selecionado

`lapin.selection()` dá o que está selecionado na janela, como elementos:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Os campos de um elemento são lidos pelos seus nomes e mudados por atribuição: `wall.height = 3` muda a parede (um único passo de desfazer, ou parte da Execução). `with lapin.step("Name"):` faz de vários comandos um único passo de desfazer, e cada um continua respondendo na hora, de modo que um posterior pode usar o que um anterior criou.

## Um script como ferramenta

Um script pode ser uma **ferramenta**: um botão em **Minhas ferramentas** que o executa, para qualquer pessoa, com os Extras para desenvolvedores ligados ou não. Diga isso nas primeiras linhas do script, o seu *cabeçalho*:

```python
# lapin: name="Perimeter walls", version="1.0.0", tool="Perimeter walls", icon="square", run="perimeter"
# lapin: tooltip="Four walls round a rectangle, one undo step"

def perimeter():
    v = lapin.ui.ask("Perimeter walls", {
        "width": lapin.ui.number(8.0, unit="m", min=0.5),
        "depth": lapin.ui.number(6.0, unit="m", min=0.5),
        "storey": lapin.ui.storey(),
    })
    if v is None:          # Cancelar
        return
    w, d = v["width"], v["depth"]
    corners = [(0, 0), (w, 0), (w, d), (0, d)]
    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
        lapin.architecture.add_wall(storey_id=v["storey"].id, x1=x1, y1=y1, x2=x2, y2=y2)
```

- **tool**: o nome do botão; **run**: a função que ele chama (sem ela, o script inteiro é executado); **icon**: um dos ícones do Lapin (`wall`, `door`, `stair`…) ou um comum (`square`, `ruler`, `layers`, `grid`, `star`…), senão uma letra (`icon="P"`, ou a primeira do nome); **tooltip**: o que o botão diz quando você passa o mouse sobre ele.
- Salve o script em **Meus** (ou no projeto): a sua ferramenta aparece em **Minhas ferramentas**, em um grupo **Scripts**, sem que o script seja executado. Mova-a, deixe-a pequena ou tire-a como qualquer ferramenta; **Comandos…** (Ctrl+K) também a encontra.

![Minhas ferramentas com a ferramenta de um script](shots/python-tools-tab.webp)

Um clique executa o script como uma única **Execução**: tudo o que ele faz é **um único passo de desfazer**, com o nome da ferramenta. `lapin.ui.ask` mostra uma caixa de diálogo do próprio Lapin, feita dos campos que o script nomeia: um número com a sua unidade, um texto, uma escolha, uma caixa de seleção, um elemento (o que você seleciona no modelo enquanto isso: **Usar seleção**), um pavimento, um tipo. **OK** dá os valores ao script; **Cancelar** não lhe dá nada.

![A pergunta da ferramenta: as medidas e o pavimento](shots/python-tools-ask.webp)

![As quatro paredes da ferramenta; um único Desfazer as tira](shots/python-tools-walls.webp)

Um script também pode criar ferramentas enquanto é executado: `@lapin.ui.tool("Name", icon="ruler")` acima de uma função, executado uma vez pelo painel, coloca essa ferramenta em Minhas ferramentas.

De onde vêm as ferramentas:
- **Meus**: sempre;
- **os scripts do projeto**: só quando você confiou no código deles neste computador, assim um projeto de outra pessoa nunca coloca um botão na sua faixa de opções; **Esquecer a confiança** no painel os tira;
- **Lapin Extensions**: um script que você instala mostra a sua ferramenta; o primeiro clique pergunta **Executar uma vez / Confiar / Cancelar**, e **Executar script** em **Extensões → Instaladas** também o executa.

Um script excluído, desinstalado ou em que já não se confia tira as suas ferramentas de Minhas ferramentas.

## Formas criadas por um script

Um script, ou um programa do seu computador (um script Python no VS Code, por exemplo), pode criar elementos cuja forma ele próprio calcula: uma tesoura sobre um cômodo, um painel de fachada, uma forma desenhada por outra ferramenta. São elementos normais: aparecem em plantas, cortes e 3D, são medidos nas tabelas, exportados para IFC, e você os move, gira, copia, oculta ou apaga como qualquer outro. As propriedades indicam **Criado por um script:** e qual. A sua forma é do script: o que a alteraria aqui é recusado.

Um script pode seguir aquilo de que é feito. Executado fora do Lapin, atualiza ele próprio os seus elementos quando você muda o cômodo ou a parede que observa. Um script do projeto que observa elementos volta a ser executado pelo Lapin após a sua alteração, só se você confiar nele neste computador, como um passo de desfazer próprio, *«<script> regenerated»*. Um script sem confiança não é executado; o painel Script avisa.

## Segurança

Um script pode fazer tudo o que você pode fazer neste computador: ele roda no seu próprio Python, com os seus direitos. Execute scripts que você mesmo escreveu ou em que confia; um script do projeto e um instalado perguntam antes.

Um convidado de uma sessão compartilhada não pode executar scripts no seu computador, nem colocar um no seu projeto, nem excluir, renomear ou mover um script do projeto; um script só se conecta ao Lapin do próprio computador, e suas perguntas são feitas só na sua janela.

## Para desenvolvedores

O que `lapin` oferece (os comandos, as consultas, os lotes, o modelo, os eventos) está descrito no guia para desenvolvedores, *Lapin from Python* (`docs/python.md` no repositório do Lapin); o pacote está no PyPI como `lapinbim`.

Um programa no seu computador encontra o Lapin na porta **14912**; um segundo Lapin aberto ao mesmo tempo usa a 14913, e assim por diante. Com vários abertos, o programa pergunta qual (`lapin.connect(project="Casa")`); para forçar um, defina `LAPIN_PORT`.
