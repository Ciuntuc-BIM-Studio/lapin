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

## Record what you do as a script

The **Recorder** tab, beside the console, writes what you do in the window as Python, one line per step.

1. Click **Record**. A red dot on the tab shows it is recording.
2. Work as usual: draw walls, change a property, delete something. Each step shows at once as a line, for example `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Click **Stop**, then **Copy** (to paste the script elsewhere) or **Save as script** (it goes into Mine and opens in the editor).

![The recorder after drawing two walls](shots/python-recorder.webp)

The script is written so it works on another project too:
- what a step made is kept in a variable (`wall1`), and later steps use it (`wall1.id`);
- the storey you drew on is found by its name, else by its elevation (`lapin.model.storey("Ground floor", 0)`), and a type by its name (`lapin.model.find("IfcWallType", "Brick 250")`);
- another element that was already in the project is named by its id (`lapin.model["…"]`), so that line works only in this project;
- with **Selection as lapin.selection()** ticked, a command on what you had selected is written `lapin.selection()`: the script then works on what is selected when you run it.

An **Undo** while recording takes the last step out of the script, and **Redo** puts it back, so the script holds what you did in the end. What a script does is never recorded, nor are questions that change nothing (a schedule read, a measurement).

![The recorded script run in a new project: the same two walls](shots/python-recorder-new.webp)

## The project's scripts

A script can be kept **in the project**, so it travels with the file: open or write it in the editor and click **Save in the project**. It shows under **Project's**, on the left. Saving, renaming and deleting a project's script are each one undo step, and the project's versions keep its scripts. In the `.lapin` file each one is also a plain text file, `scripts/<name>.py`, so it can be read without Lapin.

**A project's script never runs by itself**, not when the project opens nor when it is saved. The first time you run it, and again whenever its code has changed, Lapin asks:
- **Run once**: it runs this time, and the next run asks again;
- **Trust**: it runs, and this code is remembered as trusted on this computer (a change to it asks again);
- **Cancel**: nothing runs.

A script you save in the project yourself is trusted as you save it. Your own scripts (Mine) never ask.

## What is selected

`lapin.selection()` gives what is selected in the window, as elements:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

An element's fields read by their names and change by assignment: `wall.height = 3` changes the wall (one undo step, or part of the Run). `with lapin.step("Name"):` makes several commands one undo step while each still answers at once, so a later one can use what an earlier one made.

## Safety

A script can do anything you can do on this computer: it runs in your own Python, with your rights. Run scripts you wrote or trust; a project's script asks first.

A guest of a shared session cannot run scripts on your computer nor put one in your project, and a script connects only to the Lapin on its own computer.

## For developers

What `lapin` offers (the commands, queries, batches, the model, events) is described in the developer guide, *Lapin from Python* (`docs/python.md` in Lapin's repository); the package is on PyPI as `lapinbim`.
