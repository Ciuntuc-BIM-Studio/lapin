# fem-check: Lapin's structural analysis against other programs

A development check, run by hand on a developer's machine. It is not part of the build, the tests or any product, and
nothing it downloads goes into this repository.

It takes a set of models, exports each with IFC's structural analysis view (docs/plan-fem.md §8.7), and compares
Lapin's own analysis of them (§9) with two other programs:

- **ifc_structural_mechanics** (Bruno Postle, https://github.com/brunopostle/ifc_structural_mechanics), which reads the
  exported `.ifc` and analyses it with Gmsh and CalculiX. This tests the export as much as the solver: a model read
  differently (axes, releases, loads, units, materials) shows up as different numbers. Its `check` command compares
  the analysis members with the building elements.
- **PyNite** (MIT, https://github.com/JWock82/Pynite), which solves the same bar problem Lapin's solver took (nodes,
  bars, releases, arms, loads), read from the dump. This tests the solver alone.

## Licences

ifc_structural_mechanics is **GPL-3.0**. It is run only as a separate program, on files Lapin wrote, on the
developer's machine. None of its code is copied, adapted, vendored or imported here; nothing of it goes into Lapin's
code, builds or products. `fem_check.py` only starts its `ifc-analysis` command and reads the `results.json` it
writes. CalculiX is GPL too, and run the same way (by ifc_structural_mechanics). PyNite is MIT; `pynite_run.py`
imports it, in a separate virtual environment. The scripts here are Lapin's own, MIT.

## The models

`SavDumpTests` (engine/tests/Lapin.Engine.Tests/SavDumpTests.cs) builds them through Lapin's commands, as a user
would, and writes `<model>.ifc` and `<model>.lapin.json` (analysis.run, the solver's problem, analysis.results for
every case, each bar's axis displaced at 21 points, and the problem solved again as Euler-Bernoulli bars) when
`LAPIN_SAV_OUT` names a folder; without it the test does nothing.

| Model | What | Kind |
|---|---|---|
| `cantilever` | a concrete column 20 × 40 on a pad footing, its weight, 10 kN along x and 5 kN along y at its head | rect |
| `simple-beam` | a 6 m beam 30 × 50 pinned at both ends on two columns, 10 kN/m and 20 kN in its middle | rect |
| `portal` | a fixed-base portal 6 m wide, 10 kN sideways and 10 kN/m on its beam | rect |
| `space-frame` | 5 × 4 m on three concrete columns and a UPN 200, a diagonal pinned at both ends, a beam pinned about its strong axis at one end, loads along x, y and z | open |
| `space-frame-rect` | the same with four concrete columns | rect |
| `guide-frame` | the user guide's floor: 6 × 4 m slab on four beams and columns, 5 kN/m² | shell |
| `steel-hall-upn` | the default steel hall, its purlins, rails and eaves beams UPN 160 (about 35 minutes at a 0.5 m mesh; beyond the tolerances as of 12.11.2026: its Ø20 X-bracing rods sag 0.6 m as beams under their own weight, and ifc_structural_mechanics joins them where they cross, Lapin does not) | open |
| `steel-hall` | the default steel hall (Z purlins, lipped C rails) | open |

The kind sets the tolerances against ifc_structural_mechanics:

- **rect**: solid rectangles only; the same model on both sides. What differs: its quadratic mesh, its 9.81 m/s²
  against Lapin's 9.80665, its torsion of rectangles corrected through the shear across the width, its displacements
  read on the faces of the solids CalculiX expands bars into. 0.2 % on the reactions' sum, 2 % on each support,
  3 % on displacements and forces.
- **open**: other sections, which it analyses as rectangles of the same area, second moments and shear areas, far too
  stiff in torsion for open sections (its README). Torsion is shown and not judged.
- **shell**: a floor both analyse as a shell (Lapin from S2b, docs/plan-fem.md §10.13), each beam on its own axis
  under it (the export moves its profile by the beam's offset). It ties a beam to the slab in translation along the
  line (a hinge, its W-JNT-001), Lapin's slab is monolithic with the beam: the sums, supports (10 %), nodes and
  deflected axes (5 %) are judged; the beams' shear, moments and torsion are shown, not judged.

PyNite takes bars only: a model with shells is compared with ifc_structural_mechanics alone. Against PyNite every model is held to 1e-4 of the largest value: the same bars, Lapin's arms rigid and PyNite's very
stiff bars.

## Installing the other programs (once, outside the repository)

Put them in a folder of your own, for example `~/fem-tools`.

**CalculiX** (`ccx`, 2.23 or later: 2.21 misreports reactions at supports that hold a rotation).
- Linux: `apt install calculix-ccx` gives 2.21 on Ubuntu 24.04; build 2.23 as below for exact reactions.
- macOS: Homebrew has no formula. Build it (about five minutes):
  ```sh
  brew install arpack                     # brings gcc (gfortran) and openblas
  mkdir -p ~/fem-tools/ccx && cd ~/fem-tools/ccx
  curl -LO http://www.dhondt.de/ccx_2.23.src.tar.bz2 && tar xjf ccx_2.23.src.tar.bz2
  mkdir spooles && cd spooles && curl -L https://www.netlib.org/linalg/spooles/spooles.2.2.tgz | tar xz
  # Make.inc: CC = /usr/bin/cc, RANLIB = ranlib, CFLAGS = -O2 -std=gnu89 -w (clang's errors for old C turned off:
  # -Wno-implicit-function-declaration -Wno-implicit-int -Wno-int-conversion -Wno-incompatible-pointer-types)
  sed -i '' 's/drawTree.c/draw.c/' Tree/src/makeGlobalLib && make lib
  cd ../CalculiX/ccx_2.23/src
  make -k CFLAGS='-O2 -w -I ../../../spooles -DARCH="Linux" -DSPOOLES -DARPACK -DMATRIXSTORAGE -DNETWORKOUT -Wno-implicit-function-declaration -Wno-int-conversion -Wno-incompatible-pointer-types -Wno-return-mismatch' \
       FFLAGS='-O2 -w -cpp -fallow-argument-mismatch' DIR=../../../spooles LIBS=''
  cc -O2 -w -I ../../../spooles -DARCH="Linux" -DSPOOLES -DARPACK -DMATRIXSTORAGE -DNETWORKOUT -c ccx_2.23.c
  gfortran -O2 -o ccx ccx_2.23.o ccx_2.23.a ../../../spooles/spooles.a -L$(brew --prefix arpack)/lib -larpack \
       -L$(brew --prefix openblas)/lib -lopenblas -lpthread -lm -fopenmp
  ./ccx -v                                # This is Version 2.23
  ```
  (The final link of the Makefile fails on the libraries' paths, hence the last two lines.)

**ifc_structural_mechanics** (with Gmsh and IfcOpenShell from pip):
```sh
cd ~/fem-tools
git clone https://github.com/brunopostle/ifc_structural_mechanics
python3.12 -m venv ism-venv && ism-venv/bin/pip install -e ./ifc_structural_mechanics
```

**PyNite**:
```sh
python3 -m venv ~/fem-tools/pynite-venv && ~/fem-tools/pynite-venv/bin/pip install PyNiteFEA
```

## Running

From the repository:
```sh
python3 scripts/fem-check/fem_check.py \
  --ism ~/fem-tools/ism-venv/bin/ifc-analysis --ccx-dir ~/fem-tools/ccx/CalculiX/ccx_2.23/src \
  --pynite ~/fem-tools/pynite-venv/bin/python
```
or with `FEM_ISM`, `FEM_CCX_DIR`, `FEM_PYNITE_PYTHON` (and `FEM_CHECK_OUT`, `FEM_MESH_SIZE`) set. Options:
`--models a,b` (default all), `--out` (default `build/fem-check`), `--no-dump` (reuse the files in `<out>/sav`),
`--mesh-size` (ifc_structural_mechanics', default 0.25 m; 0.5 m for the halls keeps them to minutes). Leave
`--ism` or `--pynite` out to skip that program. The script exits with 1 when anything is beyond its tolerance.

For each model it prints `ifc-analysis check`'s findings and the warnings of its analysis, then a row a load case:

- **ΣR**: the reactions' sum, Lapin's and theirs (kN), and the difference over the load.
- **supports**: the largest difference of a support's force or moment (supports matched by place), over the largest.
- **nodes**: the largest difference of a node's displacement, over the largest displacement in the model.
- **along**: each bar's axis displaced along it (Lapin's 21 points against their deflected shape), the largest of
  each side (mm) and the largest difference over Lapin's largest.
- **N, V, T, M**: each bar's largest axial force, shear, torsion and moment, the worst difference over the model's
  largest of that kind. Their axis 1 is Lapin's local 2 (IFC's `Axis`), their 2 Lapin's local 3.

and PyNite's: the nodes' translations, the reactions and the bars' end forces against Lapin's Euler-Bernoulli solve.

The files are kept in the output folder: `sav/` (the dumps), `ism/<model>/` (their results.json, analyze.log,
check.json), `pynite/<model>.json`.

## Plates (stage S2)

`pynite_plate.py` solves the plates of `ShellElementTests` (a 4 × 4 m square, t = 0.04 m, 10 kPa, simply supported or
clamped, 8 × 8 and 16 × 16) with PyNite's quadrilaterals, which are DKMQ (Katili 1993) where Lapin's are MITC4, and
prints both as shares of plate theory (docs/plan-fem.md §10.9):
```sh
~/fem-tools/pynite-venv/bin/python -I scripts/fem-check/pynite_plate.py
```
Lapin's numbers in it are those its tests printed on 12.11.2026; rerun the tests with `--logger "console;verbosity=detailed"`
to see them again.
