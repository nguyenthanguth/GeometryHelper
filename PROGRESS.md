# Progress

The list of work on the drawing stack. Part A is the 2D presenter (`GeometryHelper.Cad`, `.Cad.Wpf`, and its app
`GeometryHelper.Cad.App`), which works like AutoCAD 2021. Part C is scripting the presenter in AutoLISP and Python.
Model drawings live in the private repository TeklaHelper.DrawingManager.

- `[x]` done and merged into `main`; `[ ]` to do; `(running)` an agent is on it now.
- The maintainer adds, removes or reorders items here; the lead takes the next open item from the top, ticks it when it
  is merged, and adds what a finished item leaves behind.
- A UI item is designed in the Design artifact first, then built from the design.
- The full item-by-item history of Part A (about 1 100 lines) is in this file at commit `ddf1ff27`
  (`git show ddf1ff27:PROGRESS.md`).

## Part A: the presenter (finished, 2026-11-17)

The maintainer judged Part A done on 2026-11-17. What it covers, in short:

- Files: DXF R12 to R2018 and our own DWG reader and writer (R2000 to R2018), opened by AutoCAD 2021 with AUDIT 0;
  RECOVER, AUDIT, autosave, templates, xrefs, raster images, PDF underlays, OLE frames, WMF in and out.
- Drawing and editing: AutoCAD 2021's 2D draw and modify commands with their options and prompts, grips and grip modes,
  object snaps and tracking, UCS, selection, undo, clipboard, blocks and dynamic blocks, attributes, groups, arrays,
  constraints, MLINE, regions, hatches and gradients.
- Annotation: text and MTEXT with its editor, tables, fields, dimensions (associative, annotative) and multileaders.
- Layouts: viewports (double click in, snaps through them, resize and preview as AutoCAD), layers per viewport, PLOT,
  PUBLISH, page setups, plot styles, PDF and image export.
- The app: ribbon, command line with AutoComplete, Properties with the selection box, palettes, managers and dialogs,
  Global settings, shortcuts and acad.pgp aliases, plugins.

### Asked for since

- [x] A1. Command line history as AutoCAD's (maintainer 2026-11-21; design first): drag the top edge to show more or
      fewer lines (kept in the settings), scroll back with the wheel, select lines and copy them (Ctrl+C, right-click
      Copy and Copy History); F2's text window stays as it is
- [x] A2. Plugins extend the window as in AutoCAD (maintainer 2026-11-21; design first): a plugin loaded at run
      time (PLUGINLOAD/APPLOAD, both runtimes, unloadable) adds its own ribbon tabs and panels (large/small buttons,
      icons, split buttons, tooltips, as AutoCAD's RibbonTab/RibbonPanel), rows of the PROPERTIES palette for its
      objects, buttons and items of the DRAWINGS list, and custom object snap modes (as AutoCAD's CustomObjectSnapMode
      with a glyph); described without WPF in GeometryHelper.Cad.Plugins, drawn by the window, gone when unloaded
- [ ] A2 parked: colour swatches in the Color rows of Properties (the presenter's and a plugin's; the A2Properties board
      to match), collapsing plugin panels, slide-outs, dialog launchers, macros, a pick-point button, and plugin snaps
      through a viewport, inside a block and for typed points

### Parked follow-ups

Small parity and speed follow-ups left by finished items. They stay parked under "features first" unless the
maintainer meets one as a real fault. Details for each are in the history at `ddf1ff27`.

- [ ] Checks only the AutoCAD GUI can give: LAYERP with U and the Layer manager; the guessed prompts of SPELL,
      ATTIPEDIT, MLEADERALIGN Spacing/Distribute; DIMBREAK Auto breaks; filter.nfl; Ctrl cycling of grip modes; the
      Properties kind list order and its reset to All
- [ ] Dialog polish (design3+design4): one footer button order, error brush, MTextMask header drag, SelectTemplate list,
      Plot styles/Publish layout details
- [ ] Ribbon at 125 % and 150 % display scaling; Home panels a few pixels wider than the design
- [ ] PLOT on real printers and Microsoft Print to PDF; -PLOT choosing a printer; smaller PDFs (glyph subsetting)
- [ ] Trans-spatial dimensions (DIMASSOC 2 through a viewport); typed snaps and OTRACK through a viewport; viewports
      with a 3D view direction entered
- [ ] MLINE edits at mitred corners and CJ pairs; annotative turned words on an extension line; MTEXT rare codes
      and column grips; table long texts and data links in old versions
- [ ] DWG and DXF version leftovers: R2013 AuxHeader numbers, spline normals off XY, R12 gradient meshes, version
      converter number parsing
- [ ] Xrefs, REFEDIT, managers and hatch dialogs: GUI-only checks and small wording differences
- [ ] Core: Try audit follow-ups 6 (NaN-shape members)

## Part C: scripting

Asked for by the maintainer on 2026-11-20 and not started: the maintainer starts it later. (The letter B went with model
drawings, which moved to TeklaHelper.DrawingManager.) Each stage starts with a look at open-source projects on GitHub
that do the same, and names the ideas it takes. The maintainer's own AutoLISP routines are the acceptance set for the AutoLISP
stages: they are read where they are and never copied into the repository, and tests build small routines of their own.

### AutoLISP

The goal is one routine for both programs: a `.lsp` (and its `.dcl`) written once runs in AutoCAD 2021 and in
`GeometryHelper.Cad.App` with the same result. A stage is done when the maintainer's routines that need only what it adds
run in both alike. Each test runs the same routine in AutoCAD (accoreconsole, hidden) and in the presenter, and compares
the two drawings written as DXF (entities, layers, coordinates within the tolerance), the values returned and the
command-line output. What AutoCAD alone can do (COM to other programs, ARX and .NET loading) is refused with a message
naming it, never ignored; `(getvar "PRODUCT")` tells a routine which program runs it. Input a routine asks for (points,
picks, keywords) is fed to both programs from the test's script; DCL dialogs, which accoreconsole cannot show, are
compared by their values and by eye.

- [x] L1. The interpreter: AutoLISP's reader, evaluator and data types (lists, symbols, reals, integers, strings, entity
      names, selection sets), `defun` and `c:` commands, error handling with `*error*`, APPLOAD, `(load)`, `acad.lsp`,
      `acaddoc.lsp` and `S::STARTUP`; LISP expressions typed at the command line
- [x] L1b. APPLOAD dialog (design first): the Load/Unload Applications dialog with the Startup Suite, designed in the
      Design artifact before it is built
- [ ] L1 parked: acad.lsp/acaddoc.lsp order in AutoCAD and Unicode strings typed at the console (the console cannot
      show them); the harness's Enter-repeat echo and U naming `L` for LINE
- [x] L2. Drawing functions: `command` and `vl-cmdf` driving the presenter's commands with their prompts; `ssget` with
      its modes and filters, `ssname`, `sslength`, `ssadd`; `entsel`, `nentsel`, `entget`, `entmod`, `entmake`,
      `entdel`, `entnext`, `tblsearch`; `getpoint`, `getcorner`, `getdist`, `getangle`, `getreal`, `getint`,
      `getstring`, `getkword`, `initget`; `getvar`, `setvar`, `trans`, `polar`, `inters`, `osnap`; `princ` and `prompt`
- [ ] L2 parked: entmake of POLYLINE with VERTEX/SEQEND, INSERT with attributes, block definitions and table records,
      and the ATTRIB/SEQEND/MTEXT/BLOCK groups entget leaves out; `ssget "_:L"` skipping locked layers; -HATCH
      boundaries as line edges, island style 75, seed 98; DIMBLK `None` against `_None`; prompt wording of
      -ATTDEF/-BLOCK/-INSERT, `New color [...] :`, AUDIT's last line, -LAYER `<select objects>` and Set/Rename's
      `Invalid layer name.`; -STYLE Annotative; ELLIPSE Parameter and group 42 past 2 pi; nentsel's block matrix
      measured; our linetype dashes against acad.lin
- [x] L3. vl- functions and files: `vl-string-*`, `vl-sort`, `vl-remove`, `vl-position`, `vl-file-*`, `open`, `close`,
      `read-line`, `write-line`, `findfile`, `getfiled`
- [x] L4. vla- and vlax- objects: the members the maintainer's routines use (about 40), on the presenter's own object
      model; COM to other programs (Excel) refused with a message
- [ ] L4 parked: ObjectID as AutoCAD's small numbers, typed forms after a `c:` routine joining its undo step
      (unmeasured), NOMUTT 1 muting `command` prompts, a fence's objects in the order along the fence, a crossing wholly
      outside the view finding a long line (probe p4, unexplained), ZOOM's 5 % margin against AutoCAD's about 1 % and
      ZOOM n alone keeping the centre
- [x] L5. DCL dialogs: the DCL parser and its tiles drawn as WPF, with action_tile, get_tile and set_tile (designed in
      the Design artifact first). Accepted 2026-11-21: E1 and FDVAL run in the full AutoCAD and the presenter with the
      same dialog values and E1 transcript
- [ ] L5 parked: a dialog of ok_cancel alone (162 x 93, a minimum body height), AutoCAD's narrower dialog text (FDVAL
      219 against Segoe's 242), list double click, slider drag, Ctrl-click and getpoint in an action unmeasured
- [ ] L6. Trusted folders and SECURELOAD, as AutoCAD 2021, shared with the Python stages

### Python

Python works with GeometryHelper.Cad in three places: a script outside the app on the libraries alone, a console and
scripts inside the app, and a script outside that drives a running app.

- [x] P1. Choose how: CPython through pythonnet against IronPython, on net48 and .NET 10, with numpy usable; how a
      script finds the libraries; a short decision in the plan. Decided 2026-11-21: CPython 3.11+ through pythonnet 3.2
      (IronPython 3.4 has no numpy and no `match`/`:=`); the netstandard2.0 libraries load unchanged on net48, .NET 8 and
      .NET 10; a 10 000-line DXF opens in 30 ms from Python; scripts read coordinates in blocks (numpy), not one by one
- [x] P2. A Python package `geometryhelper` (pip) loading the core and `GeometryHelper.Cad` without a window: geometry,
      DXF and DWG read and write, a document edited and saved headless; type stubs (`.pyi`) so editors complete names.
      Done 2026-11-21: a local wheel `py3-none-win_amd64` (10.2 MB), 152 pytest tests on .NET 8 and .NET Framework 4.8,
      stubs generated by tools/GeometryHelper.PyStubs (806 types), guide docs/python.md
- [ ] P2 parked: the PyPI name (`geometryhelper` is taken; `geometryhelper-dotnet` proposed) and the upload, both the
      maintainer's word; the CLAUDE.md verify line; a Pylance look in VS Code; Linux, macOS and ARM64; a PyInstaller
      hook; delegate properties typed `| None` and enums made from an int in the stubs; one `<summary>` per partial
      class so hovers stay short (CadDocument)
- [x] P3. Python inside `GeometryHelper.Cad.App`: a PYTHON console palette and the commands PYLOAD and PYRUN, with the
      active document, the selection, the command line, prompts (get point, get selection) and undo of a script as one
      step. Done 2026-11-21: GeometryHelper.Cad.Python embeds CPython 3.11-3.15 on the window thread, Esc stops Python
      code in about 20 ms, one undo step per run, PYLOAD with `@cad_command`, a script connecting to its own app refused
- [ ] P3 parked: the command and function names (PYTHON, PYRUN, PYLOAD, get_point...) await the maintainer's word; the
      CLAUDE.md verify line for the Cad.Python suite; a Wpf test of CadAppRemoteHost.IsOwnerThread; the ribbon layer box
      blank after U; EntityCount cached inside an open group and a failed ApplyEdit taking back a run's whole group
      silently (the maintainer to decide); output flushed only when the window is next idle during a long run
- [ ] P4. Driving a running app from outside: a Python client over `GeometryHelper.Cad.Remote`'s named pipe (open,
      draw, select, run commands, read the drawing)
- [ ] P5. Examples and the guide: a script per common task, and the API reference for Python from the XML docs
- [ ] P6. Scripts in the trusted folders of L6 only, and a script's errors shown without closing the app

## Release

- [ ] Review by the maintainer of everything above
- [ ] Package `GeometryHelper.Cad` on NuGet (only when the work is complete)
