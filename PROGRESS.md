# Progress

The list of work on the drawing stack: Part A, the 2D presenter (`GeometryHelper.Dxf.Viewer`, `.Wpf`, `Drawing.App`),
which aims to work like AutoCAD 2021 and replace it, and Part B, model drawings (`GeometryHelper.Drawing`).

- `[x]` done and merged into `main`; `[ ]` to do; `(running)` an agent is on it now.
- The maintainer adds, removes or reorders items here; the lead takes the next open item from the top of each section,
  ticks it when it is merged, and adds what a finished item leaves behind.
- A UI item is designed in the Design artifact first, then built from the design.

## Part A: the presenter

### Done

- [x] C1 Viewer: open a DXF, display, zoom, pan, select, Properties, layers, layouts and viewports
- [x] C2 Light editing: grips, MOVE, COPY, ERASE, ROTATE, SCALE, Properties edited, draw commands, undo, save
- [x] C3 Command plugins: load, unload, reload
- [x] C5 Drawings list: double click opens, right-click Open / Close / Close selected / Close other / Close all
- [x] C6 Object snap UI: Drafting Settings, status-bar and Shift+right-click menus, AutoCAD's markers
- [x] C7 UCS, UCSICON, PLAN, named UCS; grid, ortho, polar and snap follow the UCS
- [x] C8 RECOVER and AUDIT; fonts (SHX, TrueType, FONTALT with the built-in Hershey font, FONTMAP, Missing SHX dialog)
- [x] C9 Linetypes (39 built in) and HATCH (48 patterns built in, solid, gradient) with HATCHEDIT
- [x] C10 Text Style and Dimension Style dialogs, style boxes on Annotate
- [x] Built-in resources: 20 arrowhead blocks, metric NEW template, point styles (PTYPE), pattern-file dialog
- [x] C11 Modify: TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN, LENGTHEN, OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, ALIGN
- [x] C12 Clipboard (Ctrl+C/X/V, COPYBASE, PASTEBLOCK, PASTEORIG, PASTESPEC), TEXTEDIT, the MTEXT editor
- [x] C13 Blocks and attributes: BLOCK, INSERT, WBLOCK, ATTDEF, EATTEDIT, ATTEDIT, BASE
- [x] C14 Dimension editing: DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE, DIMBREAK
- [x] C15 PLOT, -PLOT, PREVIEW, PAGESETUP to vector PDF
- [x] C16 Snaps: Extension, Parallel, Apparent Intersection, Insertion, object snap tracking (F11)
- [x] C17 QSELECT, FIND, DRAWORDER, GROUP, PURGE, selection cycling (Ctrl+W)
- [x] C18b External references (DXF) and raster images (png, jpg, bmp)
- [x] XLINE and RAY: drawn, picked, snapped, previewed while moved
- [x] Picking takes the object drawn on top (draw order)
- [x] Ribbon shrinks to the window as AutoCAD's: Home, Annotate Dimensions panel, Text Editor tab
- [x] C19 Speed at scale: an edit patches the display instead of redrawing it (1-line move at 1 000 000 entities 10.6 s -> 0.6 s)
- [x] C18a Tables: TABLE, -TABLE, TABLESTYLE, TABLEDIT, editing cells, the Table Cell tab
- [x] Ribbon round 3: the Insert tab (Block, Block Definition, Reference, the block gallery); Annotate and View shrink to 1 280 px

### In progress


### To do

- [ ] GeometryHelper.Dxf.App (running): a DXF-only app for Part A (the presenter alone, no Drawing); the screenshot and dialog modes
      move into Dxf.Viewer.Wpf, shared with Drawing.App (after ribbon round 3)
- [ ] Images of the view: PNGOUT, JPGOUT, BMPOUT as AutoCAD's: the drawing area as shown, at the window's pixel size,
      all objects or those selected; asks the file name
- [ ] Images of an area: PNG and JPG devices in PLOT and -PLOT ("GeometryHelper PNG", "GeometryHelper JPG", as AutoCAD's
      PublishToWeb PNG/JPG), plot area Window, Extents, Display or Layout, size in pixels (1920 x 1080, 3840 x 2160 or
      typed) in place of a paper (design first)
- [ ] COPYVIEW (not in AutoCAD): the view as an image on the clipboard, also on the canvas right-click menu (optional)
- [ ] Tables: the cell right-click menu, column letters and row numbers, autofill and width grips, drag-select a range
- [ ] Tables: rows grow for wrapped text; Tab in the last cell adds a row; first/second/other row cell styles
- [ ] Tables: set the current table style in drawings made new (DICTIONARYVAR class in the template)

- [ ] Ribbon: check drop-downs and the Insert block gallery at 125 % and 150 % display scaling; pin and Esc by hand
- [ ] Ribbon: disabled buttons barely look greyed (GhFlatButton); Home panels a few pixels wider than their design widths
- [ ] XLINE and RAY: pick and snap test only the visible part (they cost 9 times a line today) (running)
- [ ] Speed: undo by deltas (undo reloads the whole drawing, 2.5 s at 1 000 000 entities) (running)
- [ ] Speed: draw the first display off the UI thread (10–13 s at 1 000 000 entities)
- [ ] Speed: frame at extents at 1 000 000 entities (0.8–1.1 s, inside Skia); level of detail would change pixels (needs a decision)
- [ ] Speed: highlight of 100 000 selected objects (1.2–1.6 s)
- [ ] Blocks: EATTEDIT stays open on Apply; INSERT drag preview; pick one ATTRIB in -ATTEDIT; ATTSYNC (running); BATTMAN (design first)
- [ ] Blocks: Block Definition dialog previews the selected objects (running)
- [ ] Dimensions: DIMORDINATE (and its part in DIMCONTINUE, DIMBASELINE, QDIM), DIMJOGLINE, the DIM command (running)
- [ ] Dimensions: draw DIMTAD and DIMJUST (words above or beside the line) (running)
- [ ] MTEXT: word wrap at the width, stacked fractions, columns; line spacing 5/3 as AutoCAD (check first)
- [ ] Clipboard: preview of the objects while PASTECLIP asks for the point
- [ ] PLOT: print to Windows printers; add named page setups; embed only the glyphs used (smaller PDFs)
- [ ] Xrefs: XOPEN, a balloon when a file changes, editable xref layers (`NAME|LAYER`), nested xrefs in the palette
- [ ] Images: IMAGEFRAME in a drawing with no image yet (needs RASTERVARIABLES made in core)
- [ ] PURGE dialog, Group Manager, Draw Order dialogs (design first)
- [ ] Selection: QSELECT's "select objects" button

### Decisions for the maintainer

- [ ] Esc while editing a TEXT in place: cancel (now) or keep what was typed (AutoCAD?)
- [ ] DIMBREAK breaks are drawn only, not stored as AutoCAD stores them: a DXF of AutoCAD 2021 with a broken dimension
      would let them be stored
- [ ] The oblique angle of a dimension: from the x axis (presenter) or from the dimension line (core's doc)
- [ ] Picking: the object on top wins among those within a tenth of the pickbox (now), or always within the whole pickbox

## Part B: model drawings

On hold: Part A is finished first, and the maintainer may change Part B before it goes on.

- [x] G1 Hidden-line projection written to DXF
- [x] G2 Views (viewports), the drawing document, owners
- [x] G3 Dimensions
- [x] G4 Marks and symbols
- [x] G5 The sheet
- [x] U1 Model drawing UI: drawing list, New drawing, Update, Export, Drawing settings
- [x] P1 Drawing plugins: the API, a sample, Run plugin
- [ ] G0 The maintainer's list of Tekla drawing faults to compare with
- [ ] G3 Dimension chain texts that fit
- [ ] G6 step 4: comparison with Tekla; Tekla welds are unreadable
- [ ] U1 checked against the maintainer's own UI design
- [ ] P1 picks on the canvas and a Run button in the Plugin manager

## Release

- [ ] Review by the maintainer of everything above
- [ ] Packages `GeometryHelper.Drawing` and `GeometryHelper.Dxf.Viewer` on NuGet (only when the work is complete)
