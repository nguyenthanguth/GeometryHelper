# Settings, scale and sheet

Everything one drawing may vary is in a [`DrawingSettings`](xref:GeometryHelper.Drawing.DrawingSettings): which views it has,
the scales it may be drawn at, its dimensions, marks and symbols, its sheet, how the views go round the front view and how the
DXF is laid out. This page lists them, says how [`DrawingBuilder.Build`](xref:GeometryHelper.Drawing.DrawingBuilder) chooses
the scale and the sheet from them, and says what ends up in the DXF.

## The presets

There is one kind of drawing. A single part, an assembly and a general arrangement are presets of settings, each a new object
each time it is read, so changing one changes nothing already made.

| | `DrawingSettings.Part` | `.Assembly`, the default | `.General` |
|---|---|---|---|
| for | one part | an assembly | many assemblies |
| views | front, top, right | front, top, right | front, top |
| sections | "A", looking as the right end view, at 0.5 | none | none |
| dimensioned | front, top, right, section | front, top, section | none |
| marks | the main part, no fittings | the fittings, not the main part | the main part of each assembly |
| hidden lines | drawn | drawn | not drawn |
| level marks, part list | no level marks, a part list | no level marks, a part list | level marks, no part list |
| sheet | A3 | A2 | A1 |

All three allow the scales 1, 2, 5, 10, 15, 20, 25, 50, 100, 200 and 500, third-angle projection and the layout and viewports
export.

## Every setting

A drawing copies its settings when it is made, so a settings object changed afterwards changes nothing made. A setter refuses
a value out of range with `ArgumentOutOfRangeException`, and the properties that hold a group of settings refuse null.

| Member | Holds | Default |
|---|---|---|
| `Views` | a [`ViewSettings`](xref:GeometryHelper.Drawing.ViewSettings) | below |
| `Scales` | the denominators allowed, a list; 20 for 1:20 | 1, 2, 5, 10, 15, 20, 25, 50, 100, 200, 500 |
| `Dimensions` | a [`DimensionSettings`](xref:GeometryHelper.Drawing.Annotation.DimensionSettings): the rules, text height, tier spacing, rounding; its scale is set by the drawing to each view's | see [Dimensions](annotation.md#the-settings) |
| `Marks` | a [`MarkSettings`](xref:GeometryHelper.Drawing.MarkSettings): which parts, text height, offset, content | see [Marks](annotation.md#marks) |
| `Symbols` | a [`SymbolSettings`](xref:GeometryHelper.Drawing.Annotation.SymbolSettings): text height, centre line extension, the weld standard, level marks | 2.5, 3, ISO 2553, off |
| `Sheet` | a [`SheetSettings`](xref:GeometryHelper.Drawing.SheetSettings) | below |
| [`ProjectionMethod`](xref:GeometryHelper.Drawing.ProjectionMethod) | where the views go round the front view | `ThirdAngle` |
| `ExportMode` | how the DXF is laid out | `LayoutAndViewports` |

**`ViewSettings`**

| Member | Holds | Default |
|---|---|---|
| `Kinds` | the views along the axes and the isometric one, in the order they are made; a kind given twice is made once | front, top, right |
| `Sections`, `Details` | the [`SectionViewRequest`](xref:GeometryHelper.Drawing.SectionViewRequest)s and [`DetailViewRequest`](xref:GeometryHelper.Drawing.DetailViewRequest)s asked for | none |
| `Dimensioned` | the kinds of view dimensioned by the rules; a detail and the isometric view are not, whatever this holds | front, top, section |
| `DimensionedViews`, `MarkedViews` | ids of views dimensioned or marked whatever their kind, as `ReDimension` and `ReMark` add them; an id no view has is passed over | none |
| `CropMargin` | how far a crop stands off the box of its lines, in model millimetres | 10 |
| `SectionDepth` | how far behind its cut a section sees; infinity to see all | 50 |
| `SmoothAngleRad` | below this angle the edge between two faces is smooth and drawn only as a silhouette | 30 degrees |
| `HiddenLines` | whether hidden lines are drawn | true |

**`SheetSettings`**

| Member | Holds | Default |
|---|---|---|
| `Size` | a [`PaperSize`](xref:GeometryHelper.Packing.PaperSize), A0 to A4, or `Custom` | A2 |
| `Orientation` | which way round an A sheet lies | `Landscape` |
| `CustomWidth`, `CustomHeight` | the size of a sheet of its own, in millimetres; both above nought for one (`IsCustom`) | 0 |
| `Margin` | how far the frame stands in from the edge | 10 |
| `ViewSpacing` | the gap between two views and between a view and the frame | 10 |
| `PartList` | whether the sheet has a part list | true |
| `AllowLargerSheet` | whether a larger A sheet is taken where the views fit at no scale | false |
| `TitleBlock` | the texts of the [`TitleBlock`](xref:GeometryHelper.Drawing.TitleBlock): Project, DrawingNumber, Title, Revision, Company, DrawnBy, Date | all empty |

## How the scale and the sheet are chosen

`DrawingBuilder.Build(source, settings, tolerance)` makes the views once, at 1:1. Then the scales allowed are tried from the
largest drawing down, the smallest denominator first: at each, the views are dimensioned and marked and packed onto the sheet
by [`SheetPacker`](xref:GeometryHelper.Packing.SheetPacker), the views along the axes kept round the front view as the projection
method puts them, the sections, the details, the isometric view and the part list each a box of their own. The first scale at
which every box finds a place is taken. The strip of the title block along the bottom of the frame is kept free, since the
packer has no blocked areas. A scale at which the views and their dimensions alone find no place is passed over before the
marks, which take the longest, are placed.

The beam of the first look on an A3 comes out at 1:10: it fits at 1:10 and not at 1:5. The same drawing after the beam is made
3 500 long comes out at 1:15, and the scale is chosen anew at every update. A detail has a scale of its own, `AddDetail` giving
the detail 1:5 beside views at 1:15.

Where nothing fits:

| | |
|---|---|
| the beam on an A4 with only 1:10 allowed | drawn at 1:10, and `drawing.Warnings` says `The views do not fit the sheet at any scale allowed; they are drawn at 1:10 and 3 of them stand off the sheet.` |
| the same with `AllowLargerSheet` | an A3, at 1:10, no warning |
| `Scales` empty, or a scale that is not a finite number above nought | `ArgumentException`: `A drawing needs at least one scale allowed.` |
| `Size` `Custom` with no width or height | `ArgumentException` |
| `Symbols.WeldStandard = AwsA24` | `NotSupportedException`: not drawn yet |

Without `AllowLargerSheet` the smallest scale is taken, and the drawing says so in its warnings.

## The sheet

A [`DrawingSheet`](xref:GeometryHelper.Drawing.DrawingSheet) (`drawing.Sheet`) is the paper made: `SizeName` (`A3`, or `500x300` for
a sheet of its own), `Width` and `Height` (420 by 297 for an A3 on its side), the `Frame` (the sheet less its margin: 400 by 277
with a margin of 10), the `TitleBlock`, where it stands (`TitleBlockBox`, the lower right corner of the frame, 180 by 40) and
the part list.

The title block takes what the settings give, and what they leave empty is filled from the source: the drawing number from the
id of the main object (`B1`) and the title from its name and mark (`BEAM B1`).

The part list has one row, a [`PartListRow`](xref:GeometryHelper.Drawing.PartListRow), for each mark, profile, material,
length and name that parts have, with the quantity; only objects of the kind `Part` are listed, and the length is how far the body
reaches along its frame's X, rounded to a millimetre. The beam and plate of the first look give `B1 1 HEA300 S355 3000 BEAM` and
`P1 1 PL20*300 S355 300 END PLATE`. Its rows are 6 high and it is 160 wide, in the order of the marks.

## Laid out in the DXF

[`DrawingExportMode`](xref:GeometryHelper.Drawing.DrawingExportMode) says how the DXF holds the views.

| | `LayoutAndViewports`, the default | `SheetInModelSpace` |
|---|---|---|
| model space | the lines of each view at 1:1, each view in a place of its own: a beam 3 000 long is 3 000 long | the whole sheet at 1:1 in paper millimetres, each view drawn at its scale where the sheet puts it: the beam is 300 long |
| the sheet | a layout "Sheet" of paper space with one viewport per view at its scale | none; the layout is empty |
| dimensions | in model space, reading true lengths | read true lengths through a dimension style whose DIMLFAC is the scale |
| for | AutoCAD and any viewer that shows layouts | viewers that show model space only |

For the beam of the first look, `LayoutAndViewports` gives a model space of 57 entities and a layout "Sheet" of 39, among them 4
viewports, the sheet's own and three at the scale 0.1 and the widths 314, 312.3 and 32; `SheetInModelSpace` gives a model space
of 92 entities, none wider than 420, and an empty layout.

The drawing is written as DXF AC1027 (AutoCAD 2013) with the layers Visible (white), Hidden (grey, linetype HIDDEN), Centre
(red, linetype CENTER), Hatch, Dimension, Mark, Symbol, Sheet and Viewport. Hatching is ANSI31.

## Ids

An update recognises everything again by ids made from the source and the kinds of view. The drawing is the main object's id;
a view is `V-FRONT`, `V-TOP`, `V-RIGHT`, `V-SECTION-A`, `V-DETAIL-B`, `V-ISOMETRIC`; a dimension is its view's id, `/D-`, its kind, its
axis and its number among those of its kind and axis, `V-FRONT/D-Overall-X-1`; a mark its view's id, `/M-` and the object's id,
`V-FRONT/M-P1`; a section mark `V-FRONT/SM-B` and a detail mark `V-FRONT/DM-C`.

## See also

- [Sources](source.md): the objects the settings act on.
- [Making it again](update.md): `Update(source, settings)`, which makes a drawing again with other settings.
- [Packing boxes onto sheets](../packing.md): how the views are placed on a sheet.
- [The document in the DXF](document.md): how the settings are kept in the file.
