# L4: the sheet

## The sheet

- the size: A0 to A4 or a custom size, from `GeometryHelper.Packing`'s `PaperSize`, either way round;
- the frame and its margins;
- the title block in a corner, its texts filled from the source (project, drawing number, revision) and the settings
  (company, drawn by, date);
- the part list (bill of materials): mark, quantity, profile, material, length, weight, one row per mark, written as a DXF
  table;
- the revision table.

The title block is either drawn in code or taken from a drawing the maintainer supplies (Q2). A supplied one is read at
run time from the user's file and never copied into the repository.

## Placing the views

- each view's size is its lines plus its annotation, at its scale;
- the front view goes first, the others where the projection method (D9) puts them relative to it, sections and details
  where there is room;
- placed through `SheetPacker.Pack`. `SheetPacker` has no blocked areas (the maintainer declined them for packing), so the
  views are packed into the sheet less the strip the title block and tables take; whether the packer should learn
  blocked areas after all is for G5 to show, and the maintainer to decide;
- if the views do not fit, the scale steps down (L2) and the placing is tried again; if they still do not, the sheet size
  steps up when the settings allow it, and otherwise the drawing reports it.

Nothing may overlap and nothing may run off the sheet: a test checks both on every test body.
