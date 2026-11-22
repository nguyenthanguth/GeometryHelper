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
- [ ] L2 parked (most done 2026-11-22: entmake of complex objects and table records, entget slots, `ssget ":L"`,
      -HATCH edges, ELLIPSE Parameter, DIMBLK, -STYLE Annotative, the block, layer and AUDIT prompts, nentsel 4 by 3;
      12 of 17 L2 routines now identical; round 2 the same day: BLOCK group 1, an empty block's ENDBLK, the look of
      ATTRIB/SEQEND, MTEXT 42/43, hatch loop order, LAYER 290/347/348): what is left is an entmade MTEXT's group 50
      with no 11 read as degrees, -HATCH's second Select objects adding to the first, the SELECT command, the MATERIAL
      object of a layer's 347, U's message, entget of MTEXT about 0.05 ms slower (no measure cache); our linetype
      dashes stay ISO mm against acad.lin's inches (decided)
- [x] L3. vl- functions and files: `vl-string-*`, `vl-sort`, `vl-remove`, `vl-position`, `vl-file-*`, `open`, `close`,
      `read-line`, `write-line`, `findfile`, `getfiled`
- [x] L4. vla- and vlax- objects: the members the maintainer's routines use (about 40), on the presenter's own object
      model; COM to other programs (Excel) refused with a message
- [ ] L4 parked: ObjectID as AutoCAD's small numbers, typed forms after a `c:` routine joining its undo step
      (unmeasured), a fence's objects in the order along the fence, a crossing wholly
      outside the view finding a long line (probe p4, unexplained), ZOOM's 5 % margin against AutoCAD's about 1 % and
      ZOOM n alone keeping the centre
- [x] L5. DCL dialogs: the DCL parser and its tiles drawn as WPF, with action_tile, get_tile and set_tile (designed in
      the Design artifact first). Accepted 2026-11-21: E1 and FDVAL run in the full AutoCAD and the presenter with the
      same dialog values and E1 transcript
- [ ] L5 parked: a dialog of ok_cancel alone (162 x 93, a minimum body height), AutoCAD's narrower dialog text (FDVAL
      219 against Segoe's 242), list double click, slider drag, Ctrl-click and getpoint in an action unmeasured
- [x] L6. Trusted folders and SECURELOAD, as AutoCAD 2021, shared with the Python stages. Done 2026-11-22:
      SECURELOAD, TRUSTEDPATHS (`\...` for subfolders) and LEGACYCODESEARCH as measured in AutoCAD 2021, the warning
      "Security - Unsigned Executable File" (Always Load by file content), a Security page in Global settings; the four
      harness feeds and the L2/L5 routines give the same transcripts as before
- [ ] L6 parked: the plugin manager's trusted folders kept apart from TRUSTEDPATHS and the app defaulting to SECURELOAD 1
      (the maintainer to confirm); the Security page's Add folder and writable-folder warning checked by screenshot and
      tests only, not by hand
- [x] L7. Finishing AutoLISP in the app (maintainer 2026-11-22, "focus on finishing LISP first"). Done 2026-11-22: a
      `Lisps` folder beside the app loaded into every drawing and on the search path, relative names (`Sub/x`, `./x.lsp`)
      looked up along it as AutoCAD 2021 does; APPLOAD's Look in, typed folders and wildcards, list keys, Size column and
      drops on Loaded Applications as measured in AutoCAD 2021; files dropped on the window (`(LOAD "...")`, `_SCRIPT`, the
      Drag and Drop box, a drawing on the canvas INSERTed); 90 more functions (`command-s` and the error modes, `getenv`/`setenv`, `autoload`, `regapp`,
      xdata, dictionaries, `vlax-ldata-*`, `entmakex`, `ssnamex`, `grdraw`/`grvecs`), 161 of AutoCAD's 489 non-vla
      built-ins missing before, 72 after. The 19 L2 routines and L3's five against the stored AutoCAD side at e7c354e1:
      13 of 19 and 4 of 5 identical (hh newly), the rest unchanged known differences
- [ ] L7 parked: GRREAD; the 54 `vlr-` reactors; the rare functions (menucmd, help, trace, vmon, tablet, vports, setview,
      `vl-exit-with-*`, `vlax-queueexpr` and the like); ldata kept as XRECORDs, which AutoCAD does not read back as ldata
      (nor the presenter AutoCAD's VLO-VL); entmod of XRECORD, DICTIONARY and table records; ssnamex's fence crossing
      points and the method of a pick-first set; GRCLEAR checked in the GUI; entdel keeping an extension dictionary;
      APPLOAD's Create New Folder, View menu, visited chains in Look in and a wildcard with a folder part; dcd's MTEXT
      42/43 still 0.0
- [x] L8. Round L8 (spec .claude/scratch/l8/spec.md, 2026-11-22): GRREAD with AutoCAD's window codes (measured by
      SendInput), the 46 `vlr-` functions with command, lisp, sysvar, dwg and object events, menucmd, menugroup, vmon,
      help, setfunhelp, getcname, fnsplitl, acdimenableupdate, `*error*`, the vl-exit functions, entmod of table records,
      XRECORD and VERTEX, SELECT, 3DPOLY, UNDO No/No, REVCLOUD lengths kept, AutoCAD linetype lengths, the command
      parity fixes, an open line continued as AutoCAD's and a waiting prompt written again. Acceptance: L2 19/19
      transcripts (ll with its linetype-description and regen allowances), L3 5/5, L8 feeds rx, gk, fx the same.
- [ ] L8 parked: ldata as VLO-VL; entmod of DICTIONARY and BLOCK_RECORD; ssnamex fence points; mouse and docmanager
      reactors and persistent reactors; MLEADER's Options and landing-first orders; 3DPOLY's typed Z; FILLETRAD of a
      header with none (10, AutoCAD 0: accepted); trace, tablet, vports, setview and the `_vl-` internals

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
      hook; one `<summary>` per partial class so hovers stay short (CadDocument); in the stubs, 331 settable reference
      properties and 139 optional parameters that default to null still refuse None, getters are never `| None`
- [x] P3. Python inside `GeometryHelper.Cad.App`: a PYTHON console palette and the commands PYLOAD and PYRUN, with the
      active document, the selection, the command line, prompts (get point, get selection) and undo of a script as one
      step. Done 2026-11-21: GeometryHelper.Cad.Python embeds CPython 3.11-3.15 on the window thread, Esc stops Python
      code in about 20 ms, one undo step per run, PYLOAD with `@cad_command`, a script connecting to its own app refused
- [ ] P3 parked: the command and function names (PYTHON, PYRUN, PYLOAD, get_point...) await the maintainer's word; the
      CLAUDE.md verify line for the Cad.Python suite; a Wpf test of CadAppRemoteHost.IsOwnerThread; EntityCount cached inside an open group and a failed ApplyEdit taking back a run's whole group
      silently (the maintainer to decide); output flushed only when the window is next idle during a long run
- [x] P4. Driving a running app from outside: a Python client over `GeometryHelper.Cad.Remote`'s named pipe (open,
      draw, select, run commands, read the drawing). Done 2026-11-21: the .NET CadApplication through pythonnet in a
      `with` block, no protocol change; a headless test server and 63 pytest tests on both runtimes; checked on the
      real app (100 lines committed in 207 ms, one U takes the commit back)
- [ ] P4 parked: a CadApplication used after Dispose reconnects silently (documented); the app's announced title empty
      just after it starts
- [x] P5. Examples and the guide: a script per common task, and the API reference for Python from the XML docs.
      Done 2026-11-22: 13 examples in src/python/examples (library, in the app, remote), each run by a test on both
      runtimes; a Python API section of the docs site, one page per type (806), generated from the stubs with links to
      the .NET pages
- [ ] P5 parked: the place of the examples and the docs build needing Python (the maintainer to confirm); a
      `src/python` row in CLAUDE.md's layout table; 548 member groups with no .NET page to link (enum `__int__` and
      `__init__`, flags operators, delegates' `__call__`)
- [x] P6. Scripts in the trusted folders of L6 only, and a script's errors shown without closing the app. Done
      2026-11-22: PYRUN, PYLOAD and the palette go through the same gate; every kind of script error leaves the app
      running with one line on the command line (a .NET error too), the streams put back before each run
- [ ] P6 parked: a list of Python startup scripts; recursion through .NET callbacks past about 650 levels overflows the
      stack

## Release

- [ ] Review by the maintainer of everything above
- [ ] Package `GeometryHelper.Cad` on NuGet (only when the work is complete)
