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

## Segurança

Um script pode fazer tudo o que você pode fazer neste computador: ele roda no seu próprio Python, com as suas permissões. Execute scripts escritos por você ou em que você confia.

Um convidado de uma sessão compartilhada não pode executar scripts no seu computador, e um script só se conecta ao Lapin do próprio computador.

## Para desenvolvedores

O que `lapin` oferece (os comandos, as consultas, os lotes, o modelo, os eventos) está descrito no guia para desenvolvedores, *Lapin from Python* (`docs/python.md` no repositório do Lapin); o pacote está no PyPI como `lapinbim`.
