# L5: the DXF

`DrawingDxfWriter` turns a `Drawing` into a `DxfDatabase` and saves it. Nothing else in the module writes DXF.

## Layout (D1)

The sheet at 1:1 in model space, in millimetres. Each view is drawn at its scale where L4 placed it. Dimensions use a
dimension style whose `DIMLFAC` is the view's scale (one style per scale used), so they read model lengths.

## What goes where

| Drawn | Entity | Layer (names a setting) |
|---|---|---|
| visible lines | LINE, ARC, CIRCLE | Visible |
| hidden lines | LINE, ARC, CIRCLE with the HIDDEN linetype | Hidden |
| centre lines | LINE with the CENTER linetype | Centre |
| section faces | HATCH | Hatch |
| dimensions | DIMENSION (rotated, aligned, radial, diametric, ordinate) | Dimension |
| marks | MLEADER | Mark |
| title block | INSERT of a block with attributes | Sheet |
| part list, revisions | TABLE | Sheet |
| frame, view titles, texts | LWPOLYLINE, MTEXT | Sheet |

The linetypes come from the standard definitions loaded into the drawing (`DxfDatabase`'s linetypes from a file, or
written in code). The text style and the dimension styles are made by the writer from the settings.

## Checks

- Opened in AutoCAD 2021 through accoreconsole: no error, AUDIT finds nothing to fix, the counts of entities per layer as
  expected.
- Read back by `DxfReader`: the lines of every view are the ones of the `Drawing`, to the point tolerance.
- The same drawing saved twice gives the same bytes (handles and order fixed).
- Version AC1027 (D8) unless G0 finds a reason for another.
