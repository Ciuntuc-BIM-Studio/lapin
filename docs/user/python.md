---
title: Python scripts
order: 127
summary: Drive Lapin with Python from the Script panel, a developer extra: a console and an editor that run your own Python, each Run one undo step.
---

## Turn on Developer extras

Lapin can be driven with Python: every tool is a command, and a script sends the same commands. The **Script** panel is a *developer extra*, off until you turn it on, so nothing of it shows otherwise.

Open **File → Preferences…** and, on **General**, tick **Developer extras**.

![Preferences: Developer extras](shots/python-preferences.webp)

- **Python**: the Python the panel runs. Empty, Lapin looks for one on this computer (`python3`, `py -3` on Windows, python.org's and Homebrew's). It must be **Python 3.11 or newer**; Lapin does not install Python. Get it from [python.org](https://www.python.org/downloads/) if you have none, or type the path of the one you want (a virtual environment's, for example).
- **External editor**: the editor **Open in external editor** uses, as a command (`code` for Visual Studio Code). Empty, the system's text editor.

Python runs in the desktop app only: in Lapin for the browser the panel says so.

## The Script panel

Open **View → Script**. The panel opens along the bottom of the window; drag its top edge to make it taller. Its header says which Python it found, or why it found none.

![The Script panel after a run: four walls, one undo step](shots/python-panel.webp)

- **Mine**, on the left: your own scripts, plain `.py` files kept in Lapin's folder on this computer. Click one to open it; **New script** starts from an example.
- **The editor**, in the middle: Python with its colours and indentation. Type `lapin.` and it offers what Lapin has (the commands of every module of your edition, and its own calls); `lapin.architecture.` offers the architecture commands with their arguments. Give the script a name and **Save** it in Mine; **Open in external editor** opens its file in your editor, and what you save there comes back into the panel.
- **The console**, on the right: what scripts print, their errors with the line they stopped at (marked in the editor), and a line to type Python in. **Enter** runs it, **Shift+Enter** starts a new line, **Up** and **Down** bring back the lines typed before. A name you set in the console stays for the next line and for the scripts you run.

In the panel, `lapin` is the `lapinbim` package already there. The same script runs from a terminal with `import lapinbim as lapin`, while Lapin is open.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Lengths are in metres and angles in degrees, whatever unit the project shows.

## Run, Stop, Restart, Undo

- **Run** (**Ctrl+Enter** in the editor) runs the script. Everything it does is **one undo step**, named after the script: one **Undo** takes all of it away.

  ![One Undo took the run's four walls away](shots/python-undo.webp)

- While a script runs, the model is the script's: a tool you use in the window is refused with *A script is editing the model*; views, selection and schedules go on.
- A script that fails keeps **nothing** of what it did: the model is as it was before **Run**, and the console shows the error and its line.
- **Stop** stops the script; nothing it did is kept. A script that does not stop within two seconds has its Python ended.
- **Restart** starts Python again: the names the console kept are forgotten.

## Safety

A script can do anything you can do on this computer: it runs in your own Python, with your rights. Run scripts you wrote or trust.

A guest of a shared session cannot run scripts on your computer, and a script connects only to the Lapin on its own computer.

## For developers

What `lapin` offers (the commands, queries, batches, the model, events) is described in the developer guide, *Lapin from Python* (`docs/python.md` in Lapin's repository); the package is on PyPI as `lapinbim`.
